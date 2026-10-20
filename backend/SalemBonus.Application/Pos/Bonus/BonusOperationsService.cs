using SalemBonus.Application.BonusCards;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Enums;

namespace SalemBonus.Application.Pos.Bonus;

/// <summary>
/// «История операций» (ТЗ «Бонусы» §8): журнал, сүзгілер, іздеу (аты-жөні, телефон, карта, операция id, чек нөмірі), толық ақпарат,
/// әкімшілік әрекеттер — қолмен есептеу (себеп, партия мерзімі), қолмен шегеру (бөлек құқық, балансты тексеру), шегерілген бонусты қайтару
/// (бастапқыдан артық емес), сторно (түпнұсқа журналда қалады). Баланс тек осы операциялар арқылы өзгереді — тікелей өңделмейді (§1).
/// </summary>
public class BonusOperationsService(
    IBonusRepository repo,
    ISalesRepository sales,
    BonusAccess access,
    BonusProgram program,
    BonusLedger ledger,
    BonusMapper mapper,
    IUnitOfWork unitOfWork)
{
    private const int MaxPageSize = 100;
    public const int MaxManualAmount = 10_000_000;
    private AppLanguage Lang => access.Lang;
    private bool Ru => Lang == AppLanguage.Ru;

    public static readonly Dictionary<string, BonusTransactionType[]> TypeGroups = new()
    {
        ["accrual"] = [BonusTransactionType.Accrual, BonusTransactionType.Birthday, BonusTransactionType.Promo],
        ["redemption"] = [BonusTransactionType.Redemption],
        ["return"] = [BonusTransactionType.ReturnRestore, BonusTransactionType.ReturnReversal],
        ["expiration"] = [BonusTransactionType.Expiration],
        ["adjustment"] = [BonusTransactionType.Adjustment],
        ["reversal"] = [BonusTransactionType.Reversal],
    };

    public static IReadOnlyCollection<BonusTransactionType>? ParseTypes(string? type, string? types)
    {
        var keys = (types ?? type ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var list = new HashSet<BonusTransactionType>();
        foreach (var k in keys)
        {
            if (TypeGroups.TryGetValue(k.ToLowerInvariant(), out var g)) list.UnionWith(g);
            else if (Enum.TryParse<BonusTransactionType>(k, true, out var t)) list.Add(t);
        }
        return list.Count == 0 ? null : list;
    }

    public async Task<(BonusCtx Ctx, BonusOperationFilter Filter)> FilterAsync(BonusListQuery q, int skip, int take, CancellationToken ct)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusView, ct, q.StoreIds, q.From, q.To);
        var noPeriod = q.From is null && q.To is null && (q.CustomerId is not null || q.CampaignId is not null || !string.IsNullOrWhiteSpace(q.Search));
        return (c, new BonusOperationFilter(c.StoreIds, noPeriod ? null : c.FromUtc, noPeriod ? null : c.ToUtc, ParseTypes(q.Type, q.Types), q.Status, q.StaffUserId,
            q.Source, q.MinAmount, q.MaxAmount, q.Search, q.CustomerId, q.CampaignId, skip, take));
    }

    public async Task<BonusOperationsPageDto> ListAsync(BonusListQuery q, CancellationToken ct = default)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, MaxPageSize);
        var (c, f) = await FilterAsync(q, (page - 1) * size, size, ct);
        var (rows, total) = await repo.OperationsAsync(f, ct);
        return new BonusOperationsPageDto(await mapper.OperationsAsync(rows, c.Can.Contacts, ct), total, page, size);
    }

    public async Task<BonusOperationDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusView, ct);
        var row = await repo.OperationAsync(id, ct) ?? throw new NotFoundException(Messages.BonusOperationNotFound(Lang));
        // Бөтен дүкеннің операциясы — сол дүкенде bonus.view болмаса 403.
        if (row.StoreId != c.Store.Id) await access.ContextAsync(StaffPermissions.BonusView, ct, row.StoreId.ToString());
        var related = new List<BonusOperationRow>();
        foreach (var t in await repo.RelatedAsync(id, ct))
            if (await repo.OperationAsync(t.Id, ct) is { } r) related.Add(r);
        if (row.Tx.SourceTransactionId is { } src && await repo.OperationAsync(src, ct) is { } origin) related.Insert(0, origin);
        var returnable = 0;
        if (row.Tx.Type == BonusTransactionType.Redemption) returnable = Math.Max(0, -row.Tx.Amount - await repo.RestoredForRedemptionAsync(row.Tx, ct));
        var mapped = await mapper.OperationsAsync([row, .. related], c.Can.Contacts, ct);
        return new BonusOperationDetailDto(mapped[0], mapped.Skip(1).ToList(), returnable);
    }

    /// <summary>
    /// Қолмен есептеу (§8.3): клиент, сома, себеп (міндетті), партия мерзімі (күн, «мерзімсіз» не бағдарлама ережесі), коммент.
    /// Қайталанған сұраныс (сол кілт) — сол операцияны қайтарады.
    /// </summary>
    public async Task<BonusOperationDto> AccrueAsync(ManualAccrualRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusAccrue, ct);
        await access.ProgramAsync(c, ct);
        if (r.ClientRequestId is { } key && await repo.ByIdempotencyKeyAsync(key, ct) is { } done) return await OneAsync(c, done.Id, ct);
        if (r.Amount is < 1 or > MaxManualAmount) throw new ValidationException(Messages.BonusAmountInvalid(Lang), "amount");
        if (r.ValidityDays is { } vd && vd is < 1 or > 3650) throw new ValidationException(Messages.BonusSettingsValueInvalid(Lang), "validityDays");
        var reason = access.Reason(r.Reason);
        var tx = await unitOfWork.InTransactionAsync(async () =>
        {
            var card = await repo.CardForUpdateAsync(c.Store.Id, r.CustomerId, ct) ?? throw new NotFoundException(Messages.BonusCustomerNotFound(Lang));
            var now = DateTime.UtcNow;
            var expires = r.Unlimited ? null : r.ValidityDays is { } d ? now.AddDays(d) : await program.LotExpiresAtAsync(c.Store, card, BonusTransactionType.Adjustment, now, ct);
            var t = ledger.Credit(card, BonusTransactionType.Adjustment, r.Amount, expires, "manual", Join(reason, r.Comment), now, key: r.ClientRequestId);
            ledger.Notify(card.CustomerId, c.Store, NotificationType.BonusAccrued, "Бонус есептелді!", $"{c.Store.Name} — Сізге {r.Amount:N0} Б бонус есептелді.",
                reason, now, Notifications.NotificationTemplates.BonusAccrued, r.Amount);
            access.Audit(c, "bonus.manual.accrue", "bonus_transaction", t.Id, null, new { r.CustomerId, r.Amount, Reason = reason, ExpiresAt = expires, Balance = card.Balance });
            await unitOfWork.SaveChangesAsync(ct);
            return t;
        }, ct);
        return await OneAsync(c, tx.Id, ct);
    }

    /// <summary>Қолмен шегеру (§8.3): бөлек құқық, қолжетімді балансты тексеру (артығы — тыйым, баланс өзгермейді — тест 11), себеп.</summary>
    public async Task<BonusOperationDto> WriteOffAsync(ManualWriteOffRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusWriteOff, ct);
        await access.ProgramAsync(c, ct);
        if (r.ClientRequestId is { } key && await repo.ByIdempotencyKeyAsync(key, ct) is { } done) return await OneAsync(c, done.Id, ct);
        if (r.Amount is < 1 or > MaxManualAmount) throw new ValidationException(Messages.BonusAmountInvalid(Lang), "amount");
        var reason = access.Reason(r.Reason);
        var tx = await unitOfWork.InTransactionAsync(async () =>
        {
            var card = await repo.CardForUpdateAsync(c.Store.Id, r.CustomerId, ct) ?? throw new NotFoundException(Messages.BonusCustomerNotFound(Lang));
            var now = DateTime.UtcNow;
            var t = await ledger.DebitAsync(card, BonusTransactionType.Adjustment, r.Amount, "manual", Join(reason, r.Comment), now, ct, key: r.ClientRequestId);
            ledger.Notify(card.CustomerId, c.Store, NotificationType.System, Ru ? "Бонусы списаны" : "Бонус шегерілді",
                Ru ? $"{c.Store.Name} — списано {r.Amount:N0} бонусов: {reason}" : $"{c.Store.Name} — {r.Amount:N0} Б шегерілді: {reason}", null, now);
            access.Audit(c, "bonus.manual.writeoff", "bonus_transaction", t.Id, null, new { r.CustomerId, r.Amount, Reason = reason, Balance = card.Balance });
            await unitOfWork.SaveChangesAsync(ct);
            return t;
        }, ct);
        return await OneAsync(c, tx.Id, ct);
    }

    /// <summary>
    /// Шегерілген бонусты қайтару (§8.3, тест 12): бастапқы шегерудің сомасынан бұрын қайтарылғаны шегеріледі (тауар қайтарымы да),
    /// сатылым чегіне жататын болса — чектегі қайтарылған бонус та өседі (кейінгі тауар қайтарымы екінші рет қайтармайды).
    /// </summary>
    public async Task<BonusOperationDto> ReturnAsync(Guid redemptionId, ReturnBonusRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusAccrue, ct);
        await access.ProgramAsync(c, ct);
        if (r.ClientRequestId is { } key && await repo.ByIdempotencyKeyAsync(key, ct) is { } done) return await OneAsync(c, done.Id, ct);
        var reason = access.Reason(r.Reason);
        var tx = await unitOfWork.InTransactionAsync(async () =>
        {
            var original = await repo.OperationForUpdateAsync(redemptionId, ct) ?? throw new NotFoundException(Messages.BonusOperationNotFound(Lang));
            var card = await repo.CardByIdForUpdateAsync(original.BonusCardId, ct);
            if (card is null || card.StoreId != c.Store.Id) throw new NotFoundException(Messages.BonusOperationNotFound(Lang));
            if (original.Type != BonusTransactionType.Redemption) throw new ValidationException(Messages.BonusNotRedemption(Lang));
            var max = Math.Max(0, -original.Amount - await repo.RestoredForRedemptionAsync(original, ct));
            if (r.Amount < 1) throw new ValidationException(Messages.BonusAmountInvalid(Lang), "amount");
            if (r.Amount > max) throw new ValidationException(Messages.BonusReturnTooMuch(Lang, max), "amount");
            var now = DateTime.UtcNow;
            var expires = await program.LotExpiresAtAsync(c.Store, card, BonusTransactionType.ReturnRestore, now, ct);
            var t = ledger.Credit(card, BonusTransactionType.ReturnRestore, r.Amount, expires, "manual", reason, now, sourceTxId: original.Id, key: r.ClientRequestId,
                receiptId: original.ReceiptId);
            if (original.ReceiptId is { } saleId && await sales.GetSaleForUpdateAsync(c.Store.Id, saleId, ct) is { } sale) sale.BonusRestored += r.Amount;
            access.Audit(c, "bonus.manual.return", "bonus_transaction", t.Id, null, new { Original = original.Id, r.Amount, Reason = reason, Balance = card.Balance });
            await unitOfWork.SaveChangesAsync(ct);
            return t;
        }, ct);
        return await OneAsync(c, tx.Id, ct);
    }

    /// <summary>
    /// Сторно (§8.3, тест 13): түпнұсқа журналда «сторнирована» болып қалады, байланысты кері операция жасалады. Оң операция — тек
    /// жұмсалмаған партиядан (теріс баланс болмайды, §11.3); теріс операцияның сторносы бонусты жаңа партиямен қайтарады.
    /// </summary>
    public async Task<BonusOperationDto> ReverseAsync(Guid id, ReverseBonusRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusReverse, ct);
        await access.ProgramAsync(c, ct);
        var reason = access.Reason(r.Reason);
        var tx = await unitOfWork.InTransactionAsync(async () =>
        {
            var original = await repo.OperationForUpdateAsync(id, ct) ?? throw new NotFoundException(Messages.BonusOperationNotFound(Lang));
            var card = await repo.CardByIdForUpdateAsync(original.BonusCardId, ct);
            if (card is null || card.StoreId != c.Store.Id) throw new NotFoundException(Messages.BonusOperationNotFound(Lang));
            if (original.ReversedAt is not null) throw new ValidationException(Messages.BonusAlreadyReversed(Lang));
            if (!BonusMapper.Reversible(original)) throw new ValidationException(Messages.BonusNotReversible(Lang));
            var now = DateTime.UtcNow;
            var comment = (Ru ? "Сторно: " : "Сторно: ") + reason;
            BonusTransaction t;
            if (original.Amount > 0)
                t = ledger.DebitLot(card, original, BonusTransactionType.Reversal, original.Amount, "reversal", comment, now);
            else
            {
                var expires = await program.LotExpiresAtAsync(c.Store, card, BonusTransactionType.Reversal, now, ct);
                t = ledger.Credit(card, BonusTransactionType.Reversal, -original.Amount, expires, "reversal", comment, now, sourceTxId: original.Id);
            }
            t.SourceTransactionId = original.Id;
            original.ReversedAt = now;
            access.Audit(c, "bonus.reverse", "bonus_transaction", original.Id, new { original.Type, original.Amount }, new { Reversal = t.Id, Reason = reason, Balance = card.Balance });
            await unitOfWork.SaveChangesAsync(ct);
            return t;
        }, ct);
        return await OneAsync(c, tx.Id, ct);
    }

    private async Task<BonusOperationDto> OneAsync(BonusCtx c, Guid id, CancellationToken ct)
    {
        var row = await repo.OperationAsync(id, ct) ?? throw new NotFoundException(Messages.BonusOperationNotFound(Lang));
        return (await mapper.OperationsAsync([row], c.Can.Contacts, ct))[0];
    }

    private static string Join(string reason, string? comment) =>
        CatalogAccess.Optional(comment, 300) is { } cm ? $"{reason} · {cm}" : reason;
}
