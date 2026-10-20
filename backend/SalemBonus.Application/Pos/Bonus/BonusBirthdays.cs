using SalemBonus.Application.BonusCards;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Sales;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Enums;
using SalemBonus.Domain.Pos.Bonus;

namespace SalemBonus.Application.Pos.Bonus;

/// <summary>Хабарлама үлгісінің айнымалылары (§5.1, §6.4).</summary>
public static class BonusTemplates
{
    public static readonly string[] Variables = ["{{Имя}}", "{{ФИО}}", "{{Бонусы}}", "{{ДатаСгорания}}", "{{ДатаРождения}}", "{{НазваниеМагазина}}"];

    public static string Render(string template, string firstName, string fullName, int bonuses, DateTime? expiresLocal, DateOnly? birthDate, string storeName,
        AppLanguage lang) =>
        template
            .Replace("{{Имя}}", string.IsNullOrWhiteSpace(firstName) ? (lang == AppLanguage.Ru ? "Клиент" : "Клиент") : firstName.Trim())
            .Replace("{{ФИО}}", fullName)
            .Replace("{{Бонусы}}", bonuses.ToString("N0", new System.Globalization.CultureInfo("ru-RU")).Replace(' ', ' '))
            .Replace("{{ДатаСгорания}}", expiresLocal is { } e ? e.ToString("dd.MM.yyyy") : lang == AppLanguage.Ru ? "без срока" : "мерзімсіз")
            .Replace("{{ДатаРождения}}", birthDate is { } b ? b.ToString("dd.MM") : "—")
            .Replace("{{НазваниеМагазина}}", storeName);
}

/// <summary>
/// Туған күн сыйлығы (ТЗ «Бонусы» §5): жоспарланған күн, шарттар (мәртебе, сатып алу, ерекшеліктер, туған күні бар-жоғы), есептеу.
/// Бір клиентке жылына бір өңдеу — <see cref="BirthdayGrant"/> бірегей жолы (қайта өңдеу екінші сыйлық жасамайды).
/// </summary>
public class BonusBirthdays(IBonusRepository repo, BonusLedger ledger, ICurrentLanguage language)
{
    private AppLanguage Lang => language.Value;

    /// <summary>29 ақпан — кібісе емес жылы 28 ақпанда.</summary>
    public static DateOnly Occurrence(DateOnly birth, int year) =>
        new(year, birth.Month, birth.Month == 2 && birth.Day == 29 && !DateTime.IsLeapYear(year) ? 28 : birth.Day);

    public static int Offset(BonusSettings s) => s.BirthdayTiming switch
    {
        BirthdayTiming.DaysBefore => -s.BirthdayTimingDays,
        BirthdayTiming.DaysAfter => s.BirthdayTimingDays,
        _ => 0,
    };

    public static DateOnly PlannedDate(BonusSettings s, DateOnly birthday) => birthday.AddDays(Offset(s));

    public static DateTime AccrualUtc(BonusSettings s, DateOnly planned, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeToUtc(planned.ToDateTime(s.BirthdayTime, DateTimeKind.Unspecified), zone);

    /// <summary>Сыйлық партиясының мерзімі (§5.2).</summary>
    public static DateTime? ExpiresAt(BonusSettings s, DateOnly birthday, DateTime accrualUtc, TimeZoneInfo zone) => s.BirthdayValidity switch
    {
        BirthdayValidity.DaysAfterAccrual => accrualUtc.AddDays(s.BirthdayValidityDays ?? 3),
        BirthdayValidity.UntilBirthday => StoreTime.StartOfDayUtc(birthday.AddDays(1), zone),
        BirthdayValidity.DaysAfterBirthday => StoreTime.StartOfDayUtc(birthday.AddDays((s.BirthdayValidityDays ?? 3) + 1), zone),
        BirthdayValidity.FixedDate when s.BirthdayValidityDate is { } d => StoreTime.StartOfDayUtc(d.AddDays(1), zone),
        _ => null,
    };

    public static bool Enabled(BonusSettings s) => s.BirthdayEnabled && s.BirthdayAmount > 0;

    /// <summary>Шарттарды тексеру: бос — сәйкес, әйтпесе себеп кілті (excluded, status, purchases).</summary>
    public static string? Check(BonusSettings s, BonusCard card, PurchaseStats stats)
    {
        if (s.BirthdayExcludedCustomerIds.Contains(card.CustomerId)) return "excluded";
        if (s.BirthdayStatusIds.Count > 0 && (card.StatusId is not { } sid || !s.BirthdayStatusIds.Contains(sid))) return "status";
        if (s.BirthdayMinPurchaseAmount is { } min && stats.Amount(true) < min) return "purchases";
        if (s.BirthdayMinPurchaseCount is { } cnt && stats.Purchases(true) < cnt) return "purchases";
        return null;
    }

    public DateTime? PurchasesFrom(BonusSettings s, DateTime now) => s.BirthdayPurchasePeriodDays > 0 ? now.AddDays(-s.BirthdayPurchasePeriodDays) : null;

    /// <summary>
    /// Сыйлықты өңдеу: карта бұғатталады, жылдың жолы тексеріледі (бар болса — қайталанбайды), шарттар орындалса — партия, хабарлама;
    /// орындалмаса — «өткізілді» жолы себебімен (§5.3, тест 8). <paramref name="manual"/> — шарттар тексерілмейді, себеп жазылады.
    /// </summary>
    public async Task<BirthdayGrant> ProcessAsync(Store store, BonusSettings s, Guid customerId, string firstName, string fullName, DateOnly birthDate, int year,
        DateTime now, bool manual, string? reason, Guid? staffId, CancellationToken ct)
    {
        var zone = StoreTime.Zone(store);
        var existing = await repo.GrantForUpdateAsync(store.Id, customerId, year, ct);
        if (existing is { Status: BirthdayGrantStatus.Granted })
        {
            if (manual) throw new ValidationException(Messages.BonusBirthdayAlreadyGranted(Lang, year));
            return existing;
        }
        if (existing is not null && !manual) return existing;

        var card = await repo.CardForUpdateAsync(store.Id, customerId, ct) ?? throw new NotFoundException(Messages.BonusCustomerNotFound(Lang));
        var birthday = Occurrence(birthDate, year);
        string? skip = null;
        if (!manual)
        {
            var stats = await repo.PurchaseStatsAsync(store.Id, customerId, PurchasesFrom(s, now), ct);
            skip = Check(s, card, stats);
        }
        var grant = existing ?? new BirthdayGrant { Id = Guid.NewGuid(), StoreId = store.Id, CustomerId = customerId, Year = year, CreatedAt = now };
        grant.BirthdayDate = birthday;
        grant.Manual = manual;
        grant.StaffUserId = staffId;
        if (skip is not null)
        {
            grant.Status = BirthdayGrantStatus.Skipped;
            grant.Reason = skip;
        }
        else
        {
            var expires = ExpiresAt(s, birthday, now, zone);
            var lot = ledger.Credit(card, BonusTransactionType.Birthday, s.BirthdayAmount, expires, "birthday",
                manual ? (Lang == AppLanguage.Ru ? $"Подарок ко дню рождения вручную: {reason}" : $"Туған күн сыйлығы қолмен: {reason}") : (Lang == AppLanguage.Ru ? $"День рождения {birthday:dd.MM.yyyy}" : $"Туған күн {birthday:dd.MM.yyyy}"),
                now);
            var message = BonusTemplates.Render(string.IsNullOrWhiteSpace(s.BirthdayMessage) ? BonusProgram.DefaultBirthdayMessage(Lang) : s.BirthdayMessage,
                firstName, fullName, s.BirthdayAmount, expires is { } e ? TimeZoneInfo.ConvertTimeFromUtc(e, zone) : null, birthDate, store.Name, Lang);
            ledger.Notify(customerId, store, NotificationType.Birthday, Lang == AppLanguage.Ru ? "С днём рождения!" : "Туған күніңізбен!", message,
                expires is { } x ? (Lang == AppLanguage.Ru ? $"Бонусы действуют до {TimeZoneInfo.ConvertTimeFromUtc(x, zone):dd.MM.yyyy HH:mm}" : $"Бонус {TimeZoneInfo.ConvertTimeFromUtc(x, zone):dd.MM.yyyy HH:mm} дейін жарамды") : null,
                now, null, s.BirthdayAmount);
            grant.Status = BirthdayGrantStatus.Granted;
            grant.Reason = manual ? "manual" : null;
            grant.TransactionId = lot.Id;
        }
        if (existing is null) repo.AddGrant(grant);
        return grant;
    }
}
