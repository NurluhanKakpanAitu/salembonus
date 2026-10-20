using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalemBonus.Application.Pos.Inventory;
using SalemBonus.Domain.Pos.Inventory;

namespace SalemBonus.Api.Controllers.Pos;

/// <summary>
/// «Склад» (ТЗ «Склад» §18): қоймалар, қалдықтар, журнал, приёмка, списание, перемещение, жеткізушілер.
/// Дүкен — X-Store-Id; әр сұраныста құқық серверде тексеріледі.
/// </summary>
[ApiController]
[Route("api/pos/v1/inventory")]
[Authorize(Policy = StaffAuth.Policy)]
public class InventoryController(InventoryService inventory, StockDocumentService documents, SupplierService suppliers, TransferService transfers,
    InvoiceRecognitionService recognition)
    : ControllerBase
{
    // ---------- Қоймалар ----------

    [HttpGet("warehouses")]
    public async Task<ActionResult<IReadOnlyList<WarehouseListDto>>> Warehouses([FromQuery] bool all = false, CancellationToken ct = default) =>
        Ok(await inventory.ListWarehousesAsync(all, ct));

    [HttpPost("warehouses")]
    public async Task<ActionResult<WarehouseListDto>> CreateWarehouse([FromBody] SaveWarehouseRequest request, CancellationToken ct) =>
        Ok(await inventory.CreateWarehouseAsync(request, ct));

    [HttpPut("warehouses/{id:guid}")]
    public async Task<ActionResult<WarehouseListDto>> UpdateWarehouse(Guid id, [FromBody] SaveWarehouseRequest request, CancellationToken ct) =>
        Ok(await inventory.UpdateWarehouseAsync(id, request, ct));

    [HttpPost("warehouses/{id:guid}/make-default")]
    public async Task<IActionResult> MakeDefault(Guid id, CancellationToken ct) { await inventory.MakeDefaultAsync(id, ct); return NoContent(); }

    [HttpPost("warehouses/{id:guid}/archive")]
    public async Task<IActionResult> ArchiveWarehouse(Guid id, CancellationToken ct) { await inventory.ArchiveWarehouseAsync(id, ct); return NoContent(); }

    [HttpPost("warehouses/{id:guid}/restore")]
    public async Task<IActionResult> RestoreWarehouse(Guid id, CancellationToken ct) { await inventory.RestoreWarehouseAsync(id, ct); return NoContent(); }

    [HttpGet("transfer-targets")]
    public async Task<ActionResult<IReadOnlyList<TransferTargetDto>>> TransferTargets(CancellationToken ct) => Ok(await inventory.TransferTargetsAsync(ct));

    // ---------- Қалдықтар мен журнал ----------

    [HttpGet("kpi")]
    public async Task<ActionResult<StockKpiDto>> Kpi([FromQuery] Guid? warehouseId, CancellationToken ct) => Ok(await inventory.KpiAsync(warehouseId, ct));

    [HttpGet("stock")]
    public async Task<ActionResult<StockPageDto>> Stock([FromQuery] Guid? warehouseId, [FromQuery] string? search, [FromQuery] Guid? nodeId,
        [FromQuery] Guid? brandId, [FromQuery] string? availability, [FromQuery] string? status, [FromQuery] string? sort,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await inventory.StockAsync(warehouseId, search, nodeId, brandId, availability, status, sort, page, pageSize, ct));

    [HttpGet("movements")]
    public async Task<ActionResult<MovementPageDto>> Movements([FromQuery] Guid? warehouseId, [FromQuery] Guid? productId, [FromQuery] string? type,
        [FromQuery] Guid? staffUserId, [FromQuery] string? document, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        Ok(await inventory.MovementsAsync(warehouseId, productId, type, staffUserId, document, from, to, page, pageSize, ct));

    // ---------- Приёмка ----------

    [HttpGet("receipts")]
    public async Task<ActionResult<StockDocumentPageDto>> Receipts([FromQuery] Guid? warehouseId, [FromQuery] Guid? supplierId, [FromQuery] string? status,
        [FromQuery] string? search, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await documents.ListAsync(StockDocumentType.Receipt, warehouseId, supplierId, status, search, from, to, page, pageSize, ct));

    [HttpPost("receipts")]
    public async Task<ActionResult<StockDocumentDto>> CreateReceipt([FromBody] SaveReceiptRequest request, CancellationToken ct) =>
        Ok(await documents.CreateReceiptAsync(request, ct));

    /// <summary>
    /// Накладнаяны тану (ТЗ «Склад» §8): фото не PDF → черновик ұсынысы (жеткізуші, реквизиттер, каталогпен сәйкестендірілген жолдар).
    /// Ештеңе сақталмайды және өткізілмейді — нәтижені пайдаланушы формада тексеріп, өзі қолданады.
    /// </summary>
    [HttpPost("receipts/recognize")]
    [RequestSizeLimit(InvoiceRecognitionService.MaxBytes + 64 * 1024)]
    public async Task<ActionResult<InvoiceRecognitionDto>> RecognizeInvoice(IFormFile? file, [FromForm] Guid? warehouseId, CancellationToken ct)
    {
        await using var stream = file?.OpenReadStream() ?? Stream.Null;
        return Ok(await recognition.RecognizeAsync(stream, file?.ContentType, file?.Length ?? 0, warehouseId, ct));
    }

    [HttpPut("receipts/{id:guid}")]
    public async Task<ActionResult<StockDocumentDto>> UpdateReceipt(Guid id, [FromBody] SaveReceiptRequest request, CancellationToken ct) =>
        Ok(await documents.UpdateReceiptAsync(id, request, ct));

    [HttpPost("receipts/{id:guid}/payments")]
    public async Task<ActionResult<StockDocumentDto>> RegisterPayment(Guid id, [FromBody] RegisterPaymentRequest request, CancellationToken ct) =>
        Ok(await documents.RegisterPaymentAsync(id, request, ct));

    [HttpPost("payments/{id:guid}/reverse")]
    public async Task<IActionResult> ReversePayment(Guid id, [FromBody] ReverseRequest request, CancellationToken ct)
    {
        await documents.ReversePaymentAsync(id, request, ct);
        return NoContent();
    }

    // ---------- Списание ----------

    [HttpGet("write-offs")]
    public async Task<ActionResult<StockDocumentPageDto>> WriteOffs([FromQuery] Guid? warehouseId, [FromQuery] string? status, [FromQuery] string? search,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await documents.ListAsync(StockDocumentType.WriteOff, warehouseId, null, status, search, from, to, page, pageSize, ct));

    [HttpPost("write-offs")]
    public async Task<ActionResult<StockDocumentDto>> CreateWriteOff([FromBody] SaveWriteOffRequest request, CancellationToken ct) =>
        Ok(await documents.CreateWriteOffAsync(request, ct));

    [HttpPut("write-offs/{id:guid}")]
    public async Task<ActionResult<StockDocumentDto>> UpdateWriteOff(Guid id, [FromBody] SaveWriteOffRequest request, CancellationToken ct) =>
        Ok(await documents.UpdateWriteOffAsync(id, request, ct));

    // ---------- Екі құжат түріне ортақ ----------

    [HttpGet("documents/{id:guid}")]
    public async Task<ActionResult<StockDocumentDto>> Document(Guid id, CancellationToken ct) => Ok(await documents.GetAsync(id, ct));

    /// <summary>Проведение: қалдық бір-ақ рет өзгереді, қайта басу сол құжатты қайтарады.</summary>
    [HttpPost("documents/{id:guid}/post")]
    public async Task<ActionResult<StockDocumentDto>> Post(Guid id, CancellationToken ct) => Ok(await documents.PostAsync(id, ct));

    [HttpPost("documents/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct) { await documents.CancelAsync(id, ct); return NoContent(); }

    [HttpPost("documents/{id:guid}/reverse")]
    public async Task<ActionResult<StockDocumentDto>> Reverse(Guid id, [FromBody] ReverseRequest request, CancellationToken ct) =>
        Ok(await documents.ReverseAsync(id, request, ct));

    // ---------- Перемещения ----------

    [HttpGet("transfers")]
    public async Task<ActionResult<TransferPageDto>> Transfers([FromQuery] string? status, [FromQuery] bool incoming = false, [FromQuery] string? search = null,
        [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await transfers.ListAsync(status, incoming, search, from, to, page, pageSize, ct));

    [HttpGet("transfers/{id:guid}")]
    public async Task<ActionResult<TransferDto>> Transfer(Guid id, CancellationToken ct) => Ok(await transfers.GetAsync(id, ct));

    [HttpPost("transfers")]
    public async Task<ActionResult<TransferDto>> CreateTransfer([FromBody] SaveTransferRequest request, CancellationToken ct) =>
        Ok(await transfers.CreateAsync(request, ct));

    [HttpPut("transfers/{id:guid}")]
    public async Task<ActionResult<TransferDto>> UpdateTransfer(Guid id, [FromBody] SaveTransferRequest request, CancellationToken ct) =>
        Ok(await transfers.UpdateAsync(id, request, ct));

    [HttpPost("transfers/{id:guid}/dispatch")]
    public async Task<ActionResult<TransferDto>> Dispatch(Guid id, CancellationToken ct) => Ok(await transfers.DispatchAsync(id, ct));

    [HttpPost("transfers/{id:guid}/receive")]
    public async Task<ActionResult<TransferDto>> Receive(Guid id, [FromBody] ReceiveTransferRequest request, CancellationToken ct) =>
        Ok(await transfers.ReceiveAsync(id, request, ct));

    [HttpPost("transfers/{id:guid}/cancel")]
    public async Task<ActionResult<TransferDto>> CancelTransfer(Guid id, [FromBody] ReverseRequest request, CancellationToken ct) =>
        Ok(await transfers.CancelAsync(id, request, ct));

    // ---------- Жеткізушілер ----------

    [HttpGet("suppliers")]
    public async Task<ActionResult<IReadOnlyList<SupplierDto>>> Suppliers(CancellationToken ct) => Ok(await suppliers.ListAsync(ct));

    [HttpGet("suppliers/{id:guid}")]
    public async Task<ActionResult<SupplierCardDto>> Supplier(Guid id, CancellationToken ct) => Ok(await suppliers.GetAsync(id, ct));

    [HttpPost("suppliers")]
    public async Task<ActionResult<SupplierDto>> CreateSupplier([FromBody] SaveSupplierRequest request, CancellationToken ct) =>
        Ok(await suppliers.CreateAsync(request, ct));

    [HttpPut("suppliers/{id:guid}")]
    public async Task<ActionResult<SupplierDto>> UpdateSupplier(Guid id, [FromBody] SaveSupplierRequest request, CancellationToken ct) =>
        Ok(await suppliers.UpdateAsync(id, request, ct));

    [HttpPost("suppliers/{id:guid}/archive")]
    public async Task<IActionResult> ArchiveSupplier(Guid id, CancellationToken ct) { await suppliers.SetStatusAsync(id, true, ct); return NoContent(); }

    [HttpPost("suppliers/{id:guid}/restore")]
    public async Task<IActionResult> RestoreSupplier(Guid id, CancellationToken ct) { await suppliers.SetStatusAsync(id, false, ct); return NoContent(); }
}
