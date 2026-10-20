using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Enums;
using SalemBonus.Domain.Pos.Bonus;

namespace SalemBonus.Application.Pos.Bonus;

/// <summary>Журнал жолдары мен клиенттерді DTO-ға: негіз (чек, тарату, туған күн…), автор/көз, мәртебе, құқыққа қарай телефон.</summary>
public class BonusMapper(IBonusRepository repo, BonusAccess access)
{
    private bool Ru => access.Lang == Common.Interfaces.AppLanguage.Ru;

    /// <summary>Бонус модулінен сторноланатын операциялар: қолмен, туған күн, тарату, жану. Сатылым бонусы — қайтарым арқылы (§8.3).</summary>
    public static bool Reversible(BonusTransaction t) =>
        t.ReversedAt is null && t.Type != BonusTransactionType.Reversal
        && t.Source is "manual" or "birthday" or "campaign" or "expiry"
        && t.Type is BonusTransactionType.Adjustment or BonusTransactionType.Birthday or BonusTransactionType.Promo or BonusTransactionType.Expiration or BonusTransactionType.ReturnRestore;

    public BonusCustomerDto Customer(BonusCustomerRow r, IReadOnlyList<LoyaltyStatus> statuses, bool canContacts)
    {
        var (name, color) = BonusAccess.StatusOf(r.Card.StatusId, r.Card.Level, statuses, access.Lang);
        return new BonusCustomerDto(r.Card.CustomerId, BonusAccess.FullName(r.FirstName, r.LastName), BonusAccess.Phone(r.Phone, canContacts), r.QrCode,
            r.Card.StatusId, name, color, r.Card.StatusManual, r.Card.Balance, r.Card.TotalSpent, canContacts ? r.BirthDate : null, r.RegisteredAt,
            r.LastOperationAt, r.Card.NotificationsOptOut);
    }

    public async Task<List<BonusOperationDto>> OperationsAsync(IReadOnlyList<BonusOperationRow> rows, bool canContacts, CancellationToken ct)
    {
        var staff = await repo.StaffNamesAsync(rows.Where(r => r.Tx.StaffUserId != null).Select(r => r.Tx.StaffUserId!.Value).Distinct().ToList(), ct);
        var sales = await repo.SaleNumbersAsync(rows.Where(r => r.Tx.ReceiptId != null).Select(r => r.Tx.ReceiptId!.Value).Distinct().ToList(), ct);
        var campaigns = await repo.CampaignNamesAsync(rows.Where(r => r.Tx.SourceId != null).Select(r => r.Tx.SourceId!.Value).Distinct().ToList(), ct);
        var stores = await repo.StoreNamesAsync(rows.Select(r => r.StoreId).Distinct().ToList(), ct);
        return rows.Select(r => Operation(r, staff, sales, campaigns, stores, canContacts)).ToList();
    }

    private BonusOperationDto Operation(BonusOperationRow r, Dictionary<Guid, string> staff, Dictionary<Guid, long> sales, Dictionary<Guid, string> campaigns,
        Dictionary<Guid, string> stores, bool canContacts)
    {
        var t = r.Tx;
        long? number = t.ReceiptId is { } rid && sales.TryGetValue(rid, out var n) ? n : null;
        var campaign = t.SourceId is { } sid ? campaigns.GetValueOrDefault(sid) : null;
        var (basis, link) = (t.Type, t.Source) switch
        {
            (BonusTransactionType.Accrual or BonusTransactionType.Redemption, _) when number is not null => (Ru ? $"Чек №{number}" : $"Чек №{number}", $"/sales?sale={t.ReceiptId}"),
            (BonusTransactionType.Accrual or BonusTransactionType.Redemption, "pos") => (Ru ? "Покупка (терминал)" : "Сатып алу (терминал)", (string?)null),
            (BonusTransactionType.Accrual or BonusTransactionType.Redemption, _) => (t.Comment ?? (Ru ? "Покупка" : "Сатып алу"), null),
            (BonusTransactionType.ReturnRestore or BonusTransactionType.ReturnReversal, "return") or (BonusTransactionType.ReturnReversal, _) =>
                (number is not null ? (Ru ? $"Возврат по чеку №{number}" : $"№{number} чек қайтарымы") : (Ru ? "Возврат покупки" : "Сатып алу қайтарымы"),
                 t.ReceiptId is not null && number is not null ? $"/sales?sale={t.ReceiptId}" : null),
            (BonusTransactionType.ReturnRestore, _) => (Ru ? "Возврат списанных бонусов" : "Шегерілген бонусты қайтару", null),
            (BonusTransactionType.Birthday, _) => (Ru ? "День рождения" : "Туған күн", "/bonus/birthday"),
            (BonusTransactionType.Promo, _) => (campaign is not null ? (Ru ? $"Рассылка «{campaign}»" : $"«{campaign}» таратуы") : (Ru ? "Акция" : "Акция"),
                t.SourceId is not null ? $"/bonus/campaigns?campaign={t.SourceId}" : null),
            (BonusTransactionType.Expiration, _) => (Ru ? "Истёк срок действия" : "Мерзімі өтті", "/bonus/expiry"),
            (BonusTransactionType.Adjustment, _) => (Ru ? "Ручная корректировка" : "Қолмен түзету", null),
            (BonusTransactionType.Reversal, _) => (Ru ? "Сторно операции" : "Операция сторносы", null),
            _ => (t.Type.ToString(), null),
        };
        var author = t.StaffUserId is { } s && staff.TryGetValue(s, out var nm) ? nm : t.Source switch
        {
            "pos" => Ru ? "Касса (терминал)" : "Касса (терминал)",
            "expiry" => Ru ? "Фоновая задача" : "Фондық тапсырма",
            "birthday" => Ru ? "Система (день рождения)" : "Жүйе (туған күн)",
            "campaign" => campaign is not null ? (Ru ? $"Рассылка «{campaign}»" : $"«{campaign}» таратуы") : (Ru ? "Рассылка" : "Тарату"),
            _ => Ru ? "Система" : "Жүйе",
        };
        return new BonusOperationDto(t.Id, t.CreatedAt, r.StoreId, stores.GetValueOrDefault(r.StoreId, ""), r.CustomerId,
            BonusAccess.FullName(r.FirstName, r.LastName), BonusAccess.Phone(r.Phone, canContacts), t.Type.ToString(), t.Amount,
            t.BalanceAfter is { } after ? after - t.Amount : null, t.BalanceAfter, basis, link, t.Source ?? "legacy", author,
            t.ReversedAt is null ? "posted" : "reversed", t.Comment, t.Percent, t.ReceiptId, number, t.SourceTransactionId, t.SourceId, t.ExpiresAt,
            t.Remaining, Reversible(t), t.Type == BonusTransactionType.Redemption && t.ReversedAt is null);
    }
}
