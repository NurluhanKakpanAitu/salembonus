using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Pos.Catalog;
using SalemBonus.Domain.Pos.Inventory;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Application.Pos.Inventory;

/// <summary>
/// Приёмка (ТЗ «Склад» §7, §9, §13) және списание (§11): черновик → проведение → сторно.
/// Черновик қалдыққа тимейді. Проведение бір транзакцияда, бір-ақ рет (қайта басу — сол құжат).
/// Проведённый құжат өшірілмейді: қателік сторно құжатымен түзетіледі, тарих қалады (§1, §20).
/// Приёмка закупочная бағаны дүкенге жазады; розничная — тек жолда анық көрсетілсе және құқық болса (§9).
/// </summary>
public class StockDocumentService(
    IInventoryRepository repo,
    IStoreRepository stores,
    ICatalogRepository catalog,
    StockPosting posting,
    WarehouseService warehouses,
    Finance.FinanceLedger finance,
    IFinanceRepository financeAccounts,
    CatalogAccess access,
    IUnitOfWork unitOfWork)
{
    private const int MaxPageSize = 100;
    private const int MaxLines = 500;
    private AppLanguage Lang => access.Lang;

    private static string PermissionFor(StockDocumentType type) =>
        type == StockDocumentType.Receipt ? StaffPermissions.InventoryReceive : StaffPermissions.InventoryWriteOff;

    private static string PostPermissionFor(StockDocumentType type) =>
        type == StockDocumentType.Receipt ? StaffPermissions.InventoryPost : StaffPermissions.InventoryWriteOff;

    private static (string Kind, string Prefix) NumberingFor(StockDocumentType type) =>
        type == StockDocumentType.Receipt ? ("receipt", "ПР") : ("writeoff", "СП");

    // ---------- Оқу ----------

    public async Task<StockDocumentPageDto> ListAsync(StockDocumentType type, Guid? warehouseId, Guid? supplierId, string? status, string? search,
        DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(PermissionFor(type), ct);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        StockDocumentStatus? st = Enum.TryParse<StockDocumentStatus>(status, true, out var s) ? s : null;
        var (fromUtc, toUtc) = await InventoryPeriod.ResolveAsync(stores, m.StoreId, from, to, ct);
        var filter = new DocumentFilter(type, m.StoreId, warehouseId, supplierId, st, search?.Trim(), fromUtc, toUtc, (page - 1) * pageSize, pageSize);
        var (items, total) = await repo.SearchDocumentsAsync(orgId, filter, ct);
        var (all, drafts, posted, awaiting) = await repo.DocumentStatsAsync(orgId, filter, ct);
        var lookup = await LookupAsync(orgId, items, ct);
        return new StockDocumentPageDto(items.Select(d => ToListItem(d, lookup)).ToList(), total, page, pageSize,
            new DocumentStatsDto(all, drafts, posted, awaiting));
    }

    public async Task<StockDocumentDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAnyAsync(ct, StaffPermissions.InventoryReceive, StaffPermissions.InventoryWriteOff);
        var doc = await repo.GetDocumentAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.DocumentNotFound(Lang));
        if (!m.Has(PermissionFor(doc.Type))) throw new ForbiddenException(Messages.StaffPermissionDenied(Lang));
        return await ToDtoAsync(orgId, m.StoreId, doc, ct);
    }

    // ---------- Черновик ----------

    public async Task<StockDocumentDto> CreateReceiptAsync(SaveReceiptRequest r, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.InventoryReceive, ct);
        var (kind, prefix) = NumberingFor(StockDocumentType.Receipt);
        var doc = new StockDocument
        {
            Id = Guid.NewGuid(), OrganizationId = orgId, StoreId = m.StoreId, Type = StockDocumentType.Receipt,
            CreatedBy = access.StaffUserId, IsQuick = r.IsQuick,
        };
        await ApplyReceiptAsync(orgId, m, doc, r, ct);
        var seq = await repo.NextDocumentNumberAsync(orgId, kind, ct);
        doc.Seq = seq;
        doc.Number = StockPosting.FormatNumber(prefix, seq);
        repo.AddDocument(doc);
        access.Audit(orgId, m.StoreId, "inventory.receipt.create", "stock_document", doc.Id, null, Snapshot(doc));
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(doc.Id, ct);
    }

    public async Task<StockDocumentDto> UpdateReceiptAsync(Guid id, SaveReceiptRequest r, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.InventoryReceive, ct);
        var doc = await RequireDraftAsync(orgId, id, StockDocumentType.Receipt, ct);
        var before = Snapshot(doc);
        doc.IsQuick = r.IsQuick;
        await ApplyReceiptAsync(orgId, m, doc, r, ct);
        doc.UpdatedAt = DateTime.UtcNow;
        access.Audit(orgId, m.StoreId, "inventory.receipt.update", "stock_document", doc.Id, before, Snapshot(doc));
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(doc.Id, ct);
    }

    public async Task<StockDocumentDto> CreateWriteOffAsync(SaveWriteOffRequest r, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.InventoryWriteOff, ct);
        var (kind, prefix) = NumberingFor(StockDocumentType.WriteOff);
        var doc = new StockDocument
        {
            Id = Guid.NewGuid(), OrganizationId = orgId, StoreId = m.StoreId, Type = StockDocumentType.WriteOff, CreatedBy = access.StaffUserId,
        };
        await ApplyWriteOffAsync(orgId, m, doc, r, ct);
        var seq = await repo.NextDocumentNumberAsync(orgId, kind, ct);
        doc.Seq = seq;
        doc.Number = StockPosting.FormatNumber(prefix, seq);
        repo.AddDocument(doc);
        access.Audit(orgId, m.StoreId, "inventory.writeoff.create", "stock_document", doc.Id, null, Snapshot(doc));
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(doc.Id, ct);
    }

    public async Task<StockDocumentDto> UpdateWriteOffAsync(Guid id, SaveWriteOffRequest r, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.InventoryWriteOff, ct);
        var doc = await RequireDraftAsync(orgId, id, StockDocumentType.WriteOff, ct);
        var before = Snapshot(doc);
        await ApplyWriteOffAsync(orgId, m, doc, r, ct);
        doc.UpdatedAt = DateTime.UtcNow;
        access.Audit(orgId, m.StoreId, "inventory.writeoff.update", "stock_document", doc.Id, before, Snapshot(doc));
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(doc.Id, ct);
    }

    private async Task ApplyReceiptAsync(Guid orgId, StoreMembership m, StockDocument doc, SaveReceiptRequest r, CancellationToken ct)
    {
        doc.WarehouseId = await ResolveWarehouseAsync(orgId, m.StoreId, r.WarehouseId, ct);
        doc.Date = r.Date ?? doc.Date;
        if (r.SupplierId is { } sid)
        {
            var supplier = await repo.GetSupplierAsync(orgId, sid, ct) ?? throw new ValidationException(Messages.SupplierNotFound(Lang), "supplierId");
            if (supplier.Status == CatalogStatus.Archived && doc.SupplierId != sid)
                throw new ValidationException(Messages.SupplierArchived(Lang), "supplierId");
        }
        doc.SupplierId = r.SupplierId;
        doc.SupplierInvoiceNumber = CatalogAccess.Optional(r.SupplierInvoiceNumber, 64);
        doc.Basis = CatalogAccess.Optional(r.Basis, 120);
        doc.PaymentDueDate = r.PaymentDueDate;
        doc.AttachmentUrl = CatalogAccess.Optional(r.AttachmentUrl, 500);
        doc.Comment = CatalogAccess.Optional(r.Comment, 1000);
        await ApplyLinesAsync(orgId, doc, r.Lines, requirePrice: true, ct);
    }

    private async Task ApplyWriteOffAsync(Guid orgId, StoreMembership m, StockDocument doc, SaveWriteOffRequest r, CancellationToken ct)
    {
        doc.WarehouseId = await ResolveWarehouseAsync(orgId, m.StoreId, r.WarehouseId, ct);
        doc.Date = r.Date ?? doc.Date;
        doc.Reason = r.Reason is null ? null
            : Enum.TryParse<WriteOffReason>(r.Reason, true, out var reason) ? reason
            : throw new ValidationException(Messages.WriteOffReasonInvalid(Lang), "reason");
        doc.Comment = CatalogAccess.Optional(r.Comment, 1000);
        doc.AttachmentUrl = CatalogAccess.Optional(r.AttachmentUrl, 500);
        await ApplyLinesAsync(orgId, doc, r.Lines, requirePrice: false, ct);
    }

    private async Task<Guid> ResolveWarehouseAsync(Guid orgId, Guid storeId, Guid? warehouseId, CancellationToken ct)
    {
        if (warehouseId is { } wid)
            return (await repo.GetWarehouseAsync(storeId, wid, ct) ?? throw new ValidationException(Messages.WarehouseNotFound(Lang), "warehouseId")).Id;
        var w = await warehouses.EnsureDefaultAsync(orgId, storeId, ct);
        return w.Id;
    }

    /// <summary>Жолдар айырма бойынша: барын жаңартамыз, жоғын алып тастаймыз, жаңасын қосамыз. Бағасы мен атауы сол сәттегі.</summary>
    private async Task ApplyLinesAsync(Guid orgId, StockDocument doc, IReadOnlyList<SaveDocumentLineRequest>? lines, bool requirePrice, CancellationToken ct)
    {
        var requested = (lines ?? []).Where(l => l.ProductId != Guid.Empty).ToList();
        if (requested.Count > MaxLines) throw new ValidationException(Messages.DocumentTooManyLines(Lang, MaxLines), "lines");
        // Бір тауар екі жолда болса — қосылады.
        var merged = requested.GroupBy(l => l.ProductId)
            .Select(g => new SaveDocumentLineRequest(g.Key, g.Sum(x => x.Quantity), g.Last().PurchasePrice, g.Last().RetailPrice)).ToList();
        var catalogProducts = (await repo.ProductsAsync(orgId, merged.Select(l => l.ProductId).ToList(), ct)).ToDictionary(p => p.Id);

        foreach (var existing in doc.Lines.ToList())
            if (merged.All(l => l.ProductId != existing.ProductId)) { doc.Lines.Remove(existing); repo.RemoveDocumentLine(existing); }

        var order = 0;
        foreach (var l in merged)
        {
            var p = catalogProducts.GetValueOrDefault(l.ProductId) ?? throw new ValidationException(Messages.ProductNotFound(Lang), "lines");
            if (l.Quantity <= 0 || l.Quantity > 1_000_000_000) throw new ValidationException(Messages.DocumentLineQuantityInvalid(Lang, p.Name), "lines");
            var price = l.PurchasePrice ?? 0;
            if (price < 0 || (requirePrice && l.PurchasePrice is null)) throw new ValidationException(Messages.DocumentLinePriceInvalid(Lang, p.Name), "lines");
            if (l.RetailPrice is < 0) throw new ValidationException(Messages.DocumentLinePriceInvalid(Lang, p.Name), "lines");

            var line = doc.Lines.FirstOrDefault(x => x.ProductId == l.ProductId);
            if (line is null)
            {
                line = new StockDocumentLine { Id = Guid.NewGuid(), DocumentId = doc.Id, ProductId = l.ProductId };
                doc.Lines.Add(line);
            }
            line.Name = p.Name;
            line.Article = p.Article;
            line.Quantity = Math.Round(l.Quantity, 3);
            line.PurchasePrice = Math.Round(price, 2);
            line.RetailPrice = l.RetailPrice is { } rp ? Math.Round(rp, 2) : null;
            line.LineTotal = Math.Round(line.Quantity * line.PurchasePrice, 2);
            line.SortOrder = ++order;
        }
        doc.Total = doc.Lines.Sum(x => x.LineTotal);
    }

    // ---------- Проведение, жою, сторно ----------

    public async Task<StockDocumentDto> PostAsync(Guid id, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAnyAsync(ct, StaffPermissions.InventoryPost, StaffPermissions.InventoryWriteOff);
        var doc = await repo.GetDocumentForUpdateAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.DocumentNotFound(Lang));
        if (!m.Has(PostPermissionFor(doc.Type))) throw new ForbiddenException(Messages.StaffPermissionDenied(Lang));
        if (doc.StoreId != m.StoreId) throw new ForbiddenException(Messages.StaffStoreForbidden(Lang));
        // Идемпотенттілік (§18): қайта басылса — сол құжат, қалдық екінші рет өзгермейді.
        if (doc.Status == StockDocumentStatus.Posted) return await GetAsync(doc.Id, ct);
        if (doc.Status != StockDocumentStatus.Draft) throw new ValidationException(Messages.DocumentNotDraft(Lang));
        if (doc.Lines.Count == 0) throw new ValidationException(Messages.DocumentLinesRequired(Lang), "lines");
        if (doc.Type == StockDocumentType.WriteOff)
        {
            if (doc.Reason is null) throw new ValidationException(Messages.WriteOffReasonRequired(Lang), "reason");
            if (doc.Reason == WriteOffReason.Other && string.IsNullOrWhiteSpace(doc.Comment))
                throw new ValidationException(Messages.WriteOffCommentRequired(Lang), "comment");
        }
        var warehouse = await repo.GetWarehouseAsync(doc.StoreId, doc.WarehouseId, ct) ?? throw new ValidationException(Messages.WarehouseArchived(Lang), "warehouseId");
        var catalogProducts = (await repo.ProductsAsync(orgId, doc.Lines.Select(l => l.ProductId).ToList(), ct)).ToDictionary(p => p.Id);
        foreach (var l in doc.Lines)
            if (catalogProducts.GetValueOrDefault(l.ProductId) is not { Status: CatalogStatus.Active } p)
                throw new ValidationException(Messages.DocumentProductArchived(Lang, l.Name), "lines");

        var canSetRetail = m.Has(StaffPermissions.ProductsEdit);
        await unitOfWork.InTransactionAsync(async () =>
        {
            var now = DateTime.UtcNow;
            var sign = doc.Type == StockDocumentType.Receipt ? 1 : -1;
            var deltas = doc.Lines.Select(l => new StockDelta(l.ProductId, sign * l.Quantity, l.PurchasePrice)).ToList();
            var type = doc.Type == StockDocumentType.Receipt ? StockMovementType.Receipt : StockMovementType.WriteOff;
            var comment = doc.Type == StockDocumentType.WriteOff ? $"{doc.Reason}{(doc.Comment is null ? "" : ": " + doc.Comment)}" : doc.Comment;
            await posting.ApplyAsync(orgId, doc.StoreId, warehouse.Id, deltas, catalogProducts, type, doc.Id, doc.Number, comment,
                access.StaffUserId, allowNegative: false, now, ct);

            if (doc.Type == StockDocumentType.Receipt)
            {
                // Закупочная баға дүкенге (пайда есебі үшін); розничная — тек анық көрсетілсе және құқық болса (§9).
                var prices = await repo.PricesAsync(doc.StoreId, doc.Lines.Select(l => l.ProductId).ToList(), ct);
                foreach (var l in doc.Lines)
                {
                    if (!prices.TryGetValue(l.ProductId, out var price))
                    {
                        price = new ProductPrice { ProductId = l.ProductId, StoreId = doc.StoreId, SalePrice = 0 };
                        repo.AddPrice(price);
                        prices[l.ProductId] = price;
                    }
                    price.PurchasePrice = l.PurchasePrice;
                    if (l.RetailPrice is { } retail && canSetRetail && retail != price.SalePrice)
                    {
                        access.Audit(orgId, doc.StoreId, "catalog.product.price", "product", l.ProductId,
                            new { SalePrice = price.SalePrice }, new { SalePrice = retail, Source = doc.Number });
                        price.SalePrice = retail;
                    }
                    price.UpdatedAt = now;
                }
            }

            doc.Status = StockDocumentStatus.Posted;
            doc.PostedAt = now;
            doc.PostedBy = access.StaffUserId;
            doc.UpdatedAt = now;
            access.Audit(orgId, doc.StoreId, doc.Type == StockDocumentType.Receipt ? "inventory.receipt.post" : "inventory.writeoff.post",
                "stock_document", doc.Id, null, Snapshot(doc));
            await unitOfWork.SaveChangesAsync(ct);
            return doc;
        }, ct);
        return await GetAsync(doc.Id, ct);
    }

    /// <summary>Черновикті жою: физикалық өшірілмейді, «Отменён» күйі (тарих үшін).</summary>
    public async Task CancelAsync(Guid id, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAnyAsync(ct, StaffPermissions.InventoryReceive, StaffPermissions.InventoryWriteOff);
        var doc = await repo.GetDocumentForUpdateAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.DocumentNotFound(Lang));
        if (!m.Has(PermissionFor(doc.Type))) throw new ForbiddenException(Messages.StaffPermissionDenied(Lang));
        if (doc.Status == StockDocumentStatus.Cancelled) return;
        if (doc.Status != StockDocumentStatus.Draft) throw new ValidationException(Messages.DocumentNotDraft(Lang));
        doc.Status = StockDocumentStatus.Cancelled;
        doc.UpdatedAt = DateTime.UtcNow;
        access.Audit(orgId, m.StoreId, "inventory.document.cancel", "stock_document", doc.Id, null, new { doc.Number });
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>Сторно (§11, §20): проведённый құжаттың қозғалыстары кері жазылады, құжат «Сторнирован».</summary>
    public async Task<StockDocumentDto> ReverseAsync(Guid id, ReverseRequest request, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAnyAsync(ct, StaffPermissions.InventoryPost, StaffPermissions.InventoryWriteOff);
        var doc = await repo.GetDocumentForUpdateAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.DocumentNotFound(Lang));
        if (!m.Has(PostPermissionFor(doc.Type))) throw new ForbiddenException(Messages.StaffPermissionDenied(Lang));
        if (doc.StoreId != m.StoreId) throw new ForbiddenException(Messages.StaffStoreForbidden(Lang));
        if (doc.Status == StockDocumentStatus.Reversed) return await GetAsync(doc.Id, ct);
        if (doc.Status != StockDocumentStatus.Posted) throw new ValidationException(Messages.DocumentNotPosted(Lang));
        var reason = CatalogAccess.Optional(request.Reason, 500) ?? throw new ValidationException(Messages.ReversalReasonRequired(Lang), "reason");
        if (doc.Paid > 0) throw new ValidationException(Messages.ReceiptHasPayments(Lang));

        var catalogProducts = (await repo.ProductsAsync(orgId, doc.Lines.Select(l => l.ProductId).ToList(), ct)).ToDictionary(p => p.Id);
        await unitOfWork.InTransactionAsync(async () =>
        {
            var now = DateTime.UtcNow;
            var sign = doc.Type == StockDocumentType.Receipt ? -1 : 1;
            var deltas = doc.Lines.Select(l => new StockDelta(l.ProductId, sign * l.Quantity, l.PurchasePrice)).ToList();
            // Приёмканы сторнолағанда тауар сатылып кеткен болуы мүмкін — минусқа кетуге рұқсат жоқ: алдымен түзету.
            await posting.ApplyAsync(orgId, doc.StoreId, doc.WarehouseId, deltas, catalogProducts, StockMovementType.Reversal, doc.Id, doc.Number,
                $"Сторно {doc.Number}: {reason}", access.StaffUserId, allowNegative: false, now, ct);
            doc.Status = StockDocumentStatus.Reversed;
            doc.ReversedAt = now;
            doc.ReversedBy = access.StaffUserId;
            doc.ReversalReason = reason;
            doc.UpdatedAt = now;
            access.Audit(orgId, doc.StoreId, "inventory.document.reverse", "stock_document", doc.Id, null, new { doc.Number, Reason = reason });
            await unitOfWork.SaveChangesAsync(ct);
            return doc;
        }, ct);
        return await GetAsync(doc.Id, ct);
    }

    // ---------- Жеткізушіге төлем (§13) ----------

    public async Task<StockDocumentDto> RegisterPaymentAsync(Guid documentId, RegisterPaymentRequest r, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.InventoryPost, ct);
        if (r.ClientRequestId is { } key && await repo.FindPaymentByClientRequestAsync(orgId, key, ct) is not null)
            return await GetAsync(documentId, ct);
        var doc = await repo.GetDocumentForUpdateAsync(orgId, documentId, ct) ?? throw new NotFoundException(Messages.DocumentNotFound(Lang));
        if (doc.Type != StockDocumentType.Receipt || doc.Status != StockDocumentStatus.Posted) throw new ValidationException(Messages.DocumentNotPosted(Lang));
        if (doc.SupplierId is not { } supplierId) throw new ValidationException(Messages.PaymentNeedsSupplier(Lang), "supplierId");
        var amount = Math.Round(r.Amount, 2);
        if (amount <= 0) throw new ValidationException(Messages.SupplierPaymentAmountInvalid(Lang), "amount");
        if (amount > doc.Remaining) throw new ValidationException(Messages.PaymentExceedsDebt(Lang, doc.Remaining), "amount");
        var method = Enum.TryParse<PaymentMethod>(r.Method, true, out var pm) && pm != PaymentMethod.Debt ? pm
            : throw new ValidationException(Messages.SupplierPaymentMethodInvalid(Lang), "method");

        var payment = new SupplierPayment
        {
            Id = Guid.NewGuid(), OrganizationId = orgId, StoreId = doc.StoreId, SupplierId = supplierId, DocumentId = doc.Id,
            Amount = amount, Method = method, Reference = CatalogAccess.Optional(r.Reference, 64),
            Date = r.Date ?? DateOnly.FromDateTime(DateTime.UtcNow), Comment = CatalogAccess.Optional(r.Comment, 500),
            ClientRequestId = r.ClientRequestId, CreatedBy = access.StaffUserId,
        };
        var supplierName = (await repo.GetSupplierAsync(orgId, supplierId, ct))?.Name;
        await unitOfWork.InTransactionAsync(async () =>
        {
            repo.AddPayment(payment);
            doc.Paid += amount;
            doc.UpdatedAt = DateTime.UtcNow;
            // Финанс (ТЗ «Финансы» §12): ақша таңдалған шоттан (не әдістің әдепкі шотынан) кетеді; қалдық жетпесе — тыйым (§17).
            var account = r.AccountId is { } aid
                ? await financeAccounts.GetAccountAsync(orgId, aid, ct) ?? throw new ValidationException(Messages.FinanceAccountNotFound(Lang), "accountId")
                : await finance.AccountForMethodAsync(orgId, m.StoreId, method, ct);
            await finance.PostAsync(orgId, doc.StoreId, new Finance.FinancePosting(Domain.Pos.Finance.FinanceOperationType.SupplierPayment, DateTime.UtcNow,
                [new Finance.FinanceLine(account.Id, -amount)], $"Оплата поставщику · {doc.Number}", CounterpartyType: Domain.Pos.Finance.CounterpartyType.Supplier,
                CounterpartyId: supplierId, CounterpartyName: supplierName, Method: method, DocumentNumber: payment.Reference, Comment: payment.Comment,
                SourceModule: "inventory", SourceDocumentId: doc.Id, SourceOperationId: payment.Id), ct);
            access.Audit(orgId, doc.StoreId, "inventory.supplier.payment", "supplier_payment", payment.Id, null,
                new { doc.Number, payment.Amount, Method = method.ToString(), payment.Reference, Account = account.Name, Remaining = doc.Remaining });
            await unitOfWork.SaveChangesAsync(ct);
            return payment;
        }, ct);
        return await GetAsync(doc.Id, ct);
    }

    public async Task ReversePaymentAsync(Guid paymentId, ReverseRequest request, CancellationToken ct = default)
    {
        var (orgId, m) = await access.RequireAsync(StaffPermissions.InventoryPost, ct);
        var payment = await repo.GetPaymentForUpdateAsync(orgId, paymentId, ct) ?? throw new NotFoundException(Messages.PaymentNotFound(Lang));
        if (payment.Status == SupplierPaymentStatus.Reversed) return;
        var reason = CatalogAccess.Optional(request.Reason, 500) ?? throw new ValidationException(Messages.ReversalReasonRequired(Lang), "reason");
        if (payment.DocumentId is { } did && await repo.GetDocumentForUpdateAsync(orgId, did, ct) is { } doc)
        {
            doc.Paid = Math.Max(0, doc.Paid - payment.Amount);
            doc.UpdatedAt = DateTime.UtcNow;
        }
        payment.Status = SupplierPaymentStatus.Reversed;
        payment.ReversedAt = DateTime.UtcNow;
        payment.ReversedBy = access.StaffUserId;
        payment.ReversalReason = reason;
        await unitOfWork.InTransactionAsync(async () =>
        {
            // Финанс: кері жазба — ақша шотқа қайтады, түпнұсқа операция Reversed.
            await finance.ReverseBySourceAsync(orgId, payment.Id, reason, ct);
            access.Audit(orgId, m.StoreId, "inventory.supplier.payment.reverse", "supplier_payment", payment.Id, null, new { payment.Amount, Reason = reason });
            await unitOfWork.SaveChangesAsync(ct);
            return payment;
        }, ct);
    }

    // ---------- Көмекшілер ----------

    private async Task<StockDocument> RequireDraftAsync(Guid orgId, Guid id, StockDocumentType type, CancellationToken ct)
    {
        var doc = await repo.GetDocumentForUpdateAsync(orgId, id, ct) ?? throw new NotFoundException(Messages.DocumentNotFound(Lang));
        if (doc.Type != type) throw new NotFoundException(Messages.DocumentNotFound(Lang));
        if (doc.Status != StockDocumentStatus.Draft) throw new ValidationException(Messages.DocumentNotDraft(Lang));
        return doc;
    }

    private sealed record Lookup(Dictionary<Guid, Warehouse> Warehouses, Dictionary<Guid, Supplier> Suppliers, Dictionary<Guid, string> Staff);

    private async Task<Lookup> LookupAsync(Guid orgId, IEnumerable<StockDocument> docs, CancellationToken ct)
    {
        var list = docs.ToList();
        var ws = (await repo.ListOrgWarehousesAsync(orgId, ct)).ToDictionary(w => w.Id);
        var suppliers = (await repo.ListSuppliersAsync(orgId, ct)).ToDictionary(s => s.Id);
        var staffIds = list.SelectMany(d => new[] { d.CreatedBy, d.PostedBy ?? Guid.Empty, d.ReversedBy ?? Guid.Empty }).Where(x => x != Guid.Empty).Distinct().ToList();
        return new Lookup(ws, suppliers, await repo.StaffNamesAsync(staffIds, ct));
    }

    private static StockDocumentListItemDto ToListItem(StockDocument d, Lookup l) => new(
        d.Id, d.Number, d.Date, d.Status.ToString(), d.WarehouseId, l.Warehouses.GetValueOrDefault(d.WarehouseId)?.Name ?? "?",
        d.SupplierId, d.SupplierId is { } sid ? l.Suppliers.GetValueOrDefault(sid)?.Name : null, d.Reason?.ToString(), d.IsQuick,
        d.Lines.Count, d.Lines.Sum(x => x.Quantity), d.Total, d.Paid, d.Remaining, l.Staff.GetValueOrDefault(d.CreatedBy) ?? "", d.CreatedAt);

    private async Task<StockDocumentDto> ToDtoAsync(Guid orgId, Guid storeId, StockDocument d, CancellationToken ct)
    {
        var l = await LookupAsync(orgId, [d], ct);
        var ids = d.Lines.Select(x => x.ProductId).ToList();
        var catalogProducts = (await repo.ProductsAsync(orgId, ids, ct)).ToDictionary(p => p.Id);
        var units = (await catalog.ListUnitsAsync(orgId, ct)).ToDictionary(u => u.Id, u => u.ShortName);
        var stock = await repo.BalancesAsync(d.WarehouseId, ids, ct);
        var prices = await repo.PricesAsync(storeId, ids, ct);
        var payments = d.Type == StockDocumentType.Receipt ? await repo.ListPaymentsAsync(orgId, null, d.Id, ct) : [];
        var payStaff = await repo.StaffNamesAsync(payments.Select(p => p.CreatedBy).Distinct().ToList(), ct);

        var lines = d.Lines.OrderBy(x => x.SortOrder).Select(x =>
        {
            var p = catalogProducts.GetValueOrDefault(x.ProductId);
            return new DocumentLineDto(x.Id, x.ProductId, x.Name, x.Article,
                p?.Barcodes.FirstOrDefault(b => b.IsPrimary)?.Barcode ?? p?.Barcodes.FirstOrDefault()?.Barcode,
                p?.Images.FirstOrDefault(i => i.IsPrimary)?.Url ?? p?.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Url,
                p is null ? null : units.GetValueOrDefault(p.UnitId), x.Quantity, x.PurchasePrice, x.RetailPrice, x.LineTotal,
                stock.GetValueOrDefault(x.ProductId), prices.GetValueOrDefault(x.ProductId)?.SalePrice);
        }).ToList();

        return new StockDocumentDto(d.Id, d.Type.ToString(), d.Number, d.Date, d.Status.ToString(), d.StoreId, d.WarehouseId,
            l.Warehouses.GetValueOrDefault(d.WarehouseId)?.Name ?? "?", d.SupplierId,
            d.SupplierId is { } sid ? l.Suppliers.GetValueOrDefault(sid)?.Name : null, d.SupplierInvoiceNumber, d.Basis, d.AttachmentUrl,
            d.Comment, d.Reason?.ToString(), d.IsQuick, d.Total, d.Paid, d.Remaining, d.Lines.Count, d.Lines.Sum(x => x.Quantity),
            l.Staff.GetValueOrDefault(d.CreatedBy) ?? "", d.CreatedAt, d.PostedAt, d.PostedBy is { } pb ? l.Staff.GetValueOrDefault(pb) : null,
            d.ReversedAt, d.ReversalReason, lines,
            payments.Select(p => new SupplierPaymentDto(p.Id, p.SupplierId, l.Suppliers.GetValueOrDefault(p.SupplierId)?.Name ?? "", p.DocumentId,
                d.Number, p.Amount, p.Method.ToString(), p.Reference, p.Date, p.Comment, p.Status.ToString(),
                payStaff.GetValueOrDefault(p.CreatedBy) ?? "", p.CreatedAt, p.ReversalReason)).ToList(), d.PaymentDueDate);
    }

    private static object Snapshot(StockDocument d) => new
    {
        d.Number, Type = d.Type.ToString(), Status = d.Status.ToString(), d.WarehouseId, d.SupplierId, d.SupplierInvoiceNumber, d.Basis,
        Reason = d.Reason?.ToString(), d.Comment, d.Total, d.Paid, d.IsQuick,
        Lines = d.Lines.OrderBy(x => x.SortOrder).Select(x => new { x.ProductId, x.Name, x.Quantity, x.PurchasePrice, x.RetailPrice }),
    };

    internal StockDocumentListItemDto ToListItemFor(StockDocument d, Dictionary<Guid, Warehouse> ws, Dictionary<Guid, Supplier> sup, Dictionary<Guid, string> staff) =>
        ToListItem(d, new Lookup(ws, sup, staff));
}
