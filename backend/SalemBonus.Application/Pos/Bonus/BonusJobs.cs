using Microsoft.Extensions.Logging;
using SalemBonus.Application.BonusCards;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Enums;
using SalemBonus.Domain.Pos.Bonus;

namespace SalemBonus.Application.Pos.Bonus;

/// <summary>
/// «Бонусы» фондық тапсырмалары (ТЗ «Бонусы» §4.3, §5, §6.5, §3.2): туған күн сыйлықтары, жану алдындағы ескертулер, жоспарланған
/// таратулар, мәртебе мерзімі біткенде қайта бағалау. Бәрі қайталауға қауіпсіз: туған күн — жылына бір жол, ескерту — партия×күн бірегей,
/// тарату — жол бұғатталады. Бір дүкеннің қатесі басқасын тоқтатпайды, журналға жазылады.
/// </summary>
public class BonusJobs(
    IBonusRepository repo,
    IStoreRepository stores,
    BonusBirthdays birthdays,
    BonusProgram program,
    BonusLedger ledger,
    BonusCampaignService campaigns,
    IUnitOfWork unitOfWork,
    ILogger<BonusJobs> logger)
{
    public record RunResult(int Birthdays, int Warnings, int Campaigns, int Statuses);

    public async Task<RunResult> RunAsync(CancellationToken ct)
    {
        int b = 0, w = 0, k = 0, s = 0;
        foreach (var settings in await repo.AllSettingsAsync(ct))
        {
            try { if (BonusBirthdays.Enabled(settings)) b += await BirthdaysAsync(settings.StoreId, ct); }
            catch (Exception ex) { logger.LogError(ex, "Туған күн сыйлықтары: дүкен {Store}", settings.StoreId); }
            try { if (settings.WarnEnabled && settings.ExpiryEnabled) w += await WarningsAsync(settings.StoreId, ct); }
            catch (Exception ex) { logger.LogError(ex, "Жану ескертулері: дүкен {Store}", settings.StoreId); }
        }
        try { k = await campaigns.ProcessDueAsync(ct); }
        catch (Exception ex) { logger.LogError(ex, "Жоспарланған таратулар"); }
        try { s = await StatusReviewAsync(ct); }
        catch (Exception ex) { logger.LogError(ex, "Мәртебелерді қайта бағалау"); }
        return new RunResult(b, w, k, s);
    }

    /// <summary>
    /// Дүкеннің туған күн сыйлықтары: жоспарланған күні бүгін (уақыты келсе) не соңғы екі күн (сервер ұйықтап қалса) болған
    /// туған күндер. Әр клиент-жылға бір жол — қайта іске қосу екінші сыйлық жасамайды.
    /// </summary>
    public async Task<int> BirthdaysAsync(Guid storeId, CancellationToken ct)
    {
        var store = await stores.GetByIdAsync(storeId, ct);
        if (store is null) return 0;
        program.Forget(storeId);
        var settings = await repo.SettingsAsync(storeId, ct);
        if (settings is null || !BonusBirthdays.Enabled(settings)) return 0;
        var zone = Sales.StoreTime.Zone(store);
        var now = DateTime.UtcNow;
        var local = TimeZoneInfo.ConvertTimeFromUtc(now, zone);
        var today = DateOnly.FromDateTime(local);
        var lastPlanned = TimeOnly.FromDateTime(local) >= settings.BirthdayTime ? today : today.AddDays(-1);
        var offset = BonusBirthdays.Offset(settings);
        var customers = (await repo.AllCustomersAsync(storeId, ct)).Where(r => r.BirthDate is not null).ToList();
        var due = new List<(BonusCustomerRow Row, DateOnly Birthday)>();
        for (var planned = lastPlanned.AddDays(-2); planned <= lastPlanned; planned = planned.AddDays(1))
        {
            var birthday = planned.AddDays(-offset);
            foreach (var r in customers)
                if (BonusBirthdays.Occurrence(r.BirthDate!.Value, birthday.Year) == birthday) due.Add((r, birthday));
        }
        if (due.Count == 0) return 0;
        var existing = (await repo.GrantsAsync(storeId, due.Select(d => d.Birthday.Year).Distinct().ToList(), ct)).Select(g => (g.CustomerId, g.Year)).ToHashSet();
        var done = 0;
        foreach (var (row, birthday) in due)
        {
            if (existing.Contains((row.Card.CustomerId, birthday.Year))) continue;
            await unitOfWork.InTransactionAsync(async () =>
            {
                await birthdays.ProcessAsync(store, settings, row.Card.CustomerId, row.FirstName, BonusAccess.FullName(row.FirstName, row.LastName),
                    row.BirthDate!.Value, birthday.Year, now, false, null, null, ct);
                await unitOfWork.SaveChangesAsync(ct);
                return 0;
            }, ct);
            existing.Add((row.Card.CustomerId, birthday.Year));
            done++;
        }
        return done;
    }

    /// <summary>Жану алдында ескерту (§4.1): әр партияға әр күн бойынша бір рет, SalemBonus қосымшасына.</summary>
    public async Task<int> WarningsAsync(Guid storeId, CancellationToken ct)
    {
        var store = await stores.GetByIdAsync(storeId, ct);
        var settings = await repo.SettingsAsync(storeId, ct);
        if (store is null || settings is null || settings.WarnDays.Count == 0) return 0;
        var now = DateTime.UtcNow;
        var zone = Sales.StoreTime.Zone(store);
        var lots = await repo.WarnableLotsAsync(storeId, now, now.AddDays(settings.WarnDays.Max()), ct);
        if (lots.Count == 0) return 0;
        var sent = await repo.WarningsAsync(lots.Select(l => l.Lot.Id).ToList(), ct);
        var count = 0;
        foreach (var l in lots)
        {
            var left = (l.Lot.ExpiresAt!.Value - now).TotalDays;
            // Ең жақын шекті ғана: 7 және 1 күн болса, 0,5 күн қалғанда «1 күн» ескертуі.
            var day = settings.WarnDays.Where(d => left <= d).OrderBy(d => d).FirstOrDefault();
            if (day == 0 || sent.Contains((l.Lot.Id, day))) continue;
            var expires = TimeZoneInfo.ConvertTimeFromUtc(l.Lot.ExpiresAt.Value, zone);
            var n = ledger.Notify(l.CustomerId, store, NotificationType.System, "Бонусы скоро сгорят",
                $"{store.Name} — {l.Lot.Remaining:N0} бонусов сгорят {expires:dd.MM.yyyy HH:mm}. Успейте потратить!", null, now, null, l.Lot.Remaining);
            repo.AddWarning(new ExpiryWarning { Id = Guid.NewGuid(), LotId = l.Lot.Id, DaysBefore = day, NotificationId = n.Id, SentAt = now });
            foreach (var d in settings.WarnDays.Where(d => d > day)) if (!sent.Contains((l.Lot.Id, d))) repo.AddWarning(new ExpiryWarning { Id = Guid.NewGuid(), LotId = l.Lot.Id, DaysBefore = d, SentAt = now });
            count++;
        }
        await unitOfWork.SaveChangesAsync(ct);
        return count;
    }

    /// <summary>Мәртебе мерзімі біткен карталар (§3.2): автотөмендету қосулы болса — шарттар қайта тексеріледі, әйтпесе мерзім ұзарады.</summary>
    public async Task<int> StatusReviewAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var cards = await repo.CardsDueForReviewAsync(now, 200, ct);
        foreach (var card in cards)
        {
            if (card.Store is null) continue;
            var list = await program.StatusesAsync(card.StoreId, ct);
            var status = BonusProgram.Current(card, list);
            if (status is { AutoDowngrade: true }) await program.EvaluateAsync(card, card.Store, now, true, 0, 0, ct);
            else card.StatusExpiresAt = status?.ValidityDays is { } d && d > 0 ? now.AddDays(d) : null;
        }
        if (cards.Count > 0) await unitOfWork.SaveChangesAsync(ct);
        return cards.Count;
    }
}
