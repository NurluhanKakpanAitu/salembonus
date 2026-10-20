using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalemBonus.Application.Pos.Finance;

namespace SalemBonus.Api.Controllers.Pos;

/// <summary>
/// «Финансы» (ТЗ «Финансы» §16): шоттар, санаттар, операциялар, аударымдар, есептер, қарыздар, экспорт.
/// Сатылым, қайтарым, кассалық қарыз төлемі мен склад төлемдері өз модулінде жасалып, қаржыға автоматты түседі.
/// </summary>
[ApiController]
[Route("api/pos/v1/finance")]
[Authorize(Policy = StaffAuth.Policy)]
public class FinanceController(FinanceOperationsService ops, FinanceReportsService reports, FinanceDebtsService debts, FinanceExport export) : ControllerBase
{
    [HttpGet("meta")]
    public async Task<ActionResult<FinanceMetaDto>> Meta([FromQuery] string? storeIds, CancellationToken ct) => Ok(await ops.MetaAsync(storeIds, ct));

    // ---------- Шоттар ----------

    [HttpGet("accounts")]
    public async Task<ActionResult<AccountsSummaryDto>> Accounts([FromQuery] string? storeIds, [FromQuery] bool all = false, CancellationToken ct = default) =>
        Ok(await ops.AccountsAsync(storeIds, all, ct));

    [HttpPost("accounts")]
    public async Task<ActionResult<FinanceAccountDto>> CreateAccount([FromBody] SaveAccountRequest r, CancellationToken ct) => Ok(await ops.CreateAccountAsync(r, ct));

    [HttpPut("accounts/{id:guid}")]
    public async Task<ActionResult<FinanceAccountDto>> UpdateAccount(Guid id, [FromBody] SaveAccountRequest r, CancellationToken ct) => Ok(await ops.UpdateAccountAsync(id, r, ct));

    [HttpPost("accounts/{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateAccount(Guid id, CancellationToken ct) { await ops.SetAccountActiveAsync(id, false, ct); return NoContent(); }

    [HttpPost("accounts/{id:guid}/activate")]
    public async Task<IActionResult> ActivateAccount(Guid id, CancellationToken ct) { await ops.SetAccountActiveAsync(id, true, ct); return NoContent(); }

    [HttpGet("accounts/{id:guid}/statement")]
    public async Task<ActionResult<AccountStatementDto>> Statement(Guid id, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        Ok(await ops.StatementAsync(id, from, to, ct));

    // ---------- Санаттар ----------

    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<FinanceCategoryDto>>> Categories(CancellationToken ct) => Ok(await ops.CategoriesAsync(ct));

    [HttpPost("categories")]
    public async Task<ActionResult<FinanceCategoryDto>> CreateCategory([FromBody] SaveCategoryRequest r, CancellationToken ct) => Ok(await ops.CreateCategoryAsync(r, ct));

    [HttpPut("categories/{id:guid}")]
    public async Task<ActionResult<FinanceCategoryDto>> UpdateCategory(Guid id, [FromBody] SaveCategoryRequest r, CancellationToken ct) => Ok(await ops.UpdateCategoryAsync(id, r, ct));

    [HttpPost("categories/{id:guid}/archive")]
    public async Task<IActionResult> ArchiveCategory(Guid id, CancellationToken ct) { await ops.SetCategoryActiveAsync(id, false, ct); return NoContent(); }

    [HttpPost("categories/{id:guid}/restore")]
    public async Task<IActionResult> RestoreCategory(Guid id, CancellationToken ct) { await ops.SetCategoryActiveAsync(id, true, ct); return NoContent(); }

    // ---------- Операциялар ----------

    [HttpGet("operations")]
    public async Task<ActionResult<FinanceOperationsPageDto>> Operations([FromQuery] FinanceListQuery q, CancellationToken ct) => Ok(await ops.ListAsync(q, ct));

    [HttpGet("operations/{id:guid}")]
    public async Task<ActionResult<FinanceOperationDto>> Operation(Guid id, CancellationToken ct) => Ok(await ops.GetAsync(id, ct));

    [HttpPost("operations")]
    public async Task<ActionResult<FinanceOperationDto>> CreateOperation([FromBody] SaveOperationRequest r, CancellationToken ct) => Ok(await ops.CreateAsync(r, ct));

    [HttpPut("operations/{id:guid}")]
    public async Task<ActionResult<FinanceOperationDto>> UpdateDraft(Guid id, [FromBody] SaveOperationRequest r, CancellationToken ct) => Ok(await ops.UpdateDraftAsync(id, r, ct));

    [HttpPost("operations/{id:guid}/post")]
    public async Task<ActionResult<FinanceOperationDto>> PostDraft(Guid id, CancellationToken ct) => Ok(await ops.PostDraftAsync(id, ct));

    [HttpDelete("operations/{id:guid}")]
    public async Task<IActionResult> DeleteDraft(Guid id, CancellationToken ct) { await ops.DeleteDraftAsync(id, ct); return NoContent(); }

    [HttpPost("operations/{id:guid}/reverse")]
    public async Task<ActionResult<FinanceOperationDto>> Reverse(Guid id, [FromBody] ReverseOperationRequest r, CancellationToken ct) => Ok(await ops.ReverseAsync(id, r, ct));

    [HttpPost("transfers")]
    public async Task<ActionResult<FinanceOperationDto>> Transfer([FromBody] TransferRequest r, CancellationToken ct) => Ok(await ops.TransferAsync(r, ct));

    [HttpPost("adjustments")]
    public async Task<ActionResult<FinanceOperationDto>> Adjust([FromBody] AdjustmentRequest r, CancellationToken ct) => Ok(await ops.AdjustAsync(r, ct));

    // ---------- Есептер ----------

    [HttpGet("overview")]
    public async Task<ActionResult<FinanceOverviewDto>> Overview([FromQuery] string? storeIds, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        Ok(await reports.OverviewAsync(storeIds, from, to, ct));

    [HttpGet("reports/pnl")]
    public async Task<ActionResult<PnlDto>> Pnl([FromQuery] string? storeIds, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] bool compare = false, CancellationToken ct = default) =>
        Ok(await reports.PnlAsync(storeIds, from, to, compare, ct));

    [HttpGet("cash-flow")]
    public async Task<ActionResult<CashFlowDto>> CashFlow([FromQuery] string? storeIds, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Guid? accountId, CancellationToken ct) =>
        Ok(await reports.CashFlowAsync(storeIds, from, to, accountId, ct));

    [HttpGet("reports/product-profit")]
    public async Task<ActionResult<ProductProfitDto>> ProductProfit([FromQuery] string? storeIds, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? search,
        [FromQuery] Guid? nodeId, [FromQuery] Guid? brandId, [FromQuery] string? sort, [FromQuery] string? dir, [FromQuery] int page = 1, [FromQuery] int pageSize = 15,
        CancellationToken ct = default) =>
        Ok(await reports.ProductProfitAsync(storeIds, from, to, search, nodeId, brandId, sort, dir, page, pageSize, ct));

    // ---------- Қарыздар ----------

    [HttpGet("customer-debts")]
    public async Task<ActionResult<CustomerDebtsDto>> CustomerDebts([FromQuery] string? storeIds, [FromQuery] string? segment, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 15, CancellationToken ct = default) =>
        Ok(await debts.CustomerDebtsAsync(storeIds, segment, search, page, pageSize, ct));

    [HttpGet("customer-debts/{customerId:guid}")]
    public async Task<ActionResult<CustomerDebtCardDto>> CustomerDebt(Guid customerId, [FromQuery] string? storeIds, CancellationToken ct) =>
        Ok(await debts.CustomerCardAsync(customerId, storeIds, ct));

    [HttpPost("customer-debts/payments")]
    public async Task<ActionResult<CustomerDebtCardDto>> CustomerPayment([FromBody] CustomerPaymentRequest r, CancellationToken ct) => Ok(await debts.CustomerPaymentAsync(r, ct));

    [HttpGet("supplier-debts")]
    public async Task<ActionResult<SupplierDebtsDto>> SupplierDebts([FromQuery] string? storeIds, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? segment,
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 15, CancellationToken ct = default) =>
        Ok(await debts.SupplierDebtsAsync(storeIds, from, to, segment, search, page, pageSize, ct));

    [HttpGet("supplier-debts/{supplierId:guid}")]
    public async Task<ActionResult<SupplierDebtCardDto>> SupplierDebt(Guid supplierId, [FromQuery] string? storeIds, CancellationToken ct) =>
        Ok(await debts.SupplierCardAsync(supplierId, storeIds, ct));

    [HttpPost("supplier-debts/payments")]
    public async Task<ActionResult<SupplierDebtCardDto>> SupplierPayment([FromBody] SupplierPaymentRequest r, CancellationToken ct) => Ok(await debts.SupplierPaymentAsync(r, ct));

    [HttpPut("supplier-debts/documents/{documentId:guid}/due-date")]
    public async Task<IActionResult> SetDueDate(Guid documentId, [FromBody] SetDueDateRequest r, CancellationToken ct) { await debts.SetDueDateAsync(documentId, r, ct); return NoContent(); }

    [HttpGet("supplier-debts/{supplierId:guid}/reconciliation")]
    public async Task<ActionResult<ReconciliationDto>> Reconciliation(Guid supplierId, [FromQuery] string? storeIds, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        Ok(await debts.SupplierReconciliationAsync(supplierId, storeIds, from, to, ct));

    // ---------- Экспорт, тарихты жүктеу ----------

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string report, [FromQuery] string? format, [FromQuery] FinanceListQuery q, [FromQuery] Guid? id, [FromQuery] bool compare = false,
        CancellationToken ct = default)
    {
        var (content, fileName, contentType) = await export.ExportAsync(report, format, q, id, compare, ct);
        return File(content, contentType, fileName);
    }

    /// <summary>Модуль қосылғанға дейінгі сатылым, қайтарым, қарыз төлемдері мен жеткізушіге төлемдерді шоттарға жазу (бір рет, қайталанбайды).</summary>
    [HttpPost("backfill")]
    public async Task<ActionResult<BackfillResultDto>> Backfill(CancellationToken ct) => Ok(await ops.BackfillAsync(ct));
}
