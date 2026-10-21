using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalemBonus.Application.Pos.Clients;
using SalemBonus.Application.Pos.Finance;
using SalemBonus.Domain.Core;

namespace SalemBonus.Api.Controllers.Pos;

/// <summary>
/// «Клиенты» (ТЗ «Клиенты» v1.0): тізім мен KPI, шолу, карточка мен бөлімдері, CRM (жазба, тарих, хабар), сегменттер, мұрағат,
/// біріктіру, импорт пен экспорт, баптаулар. Ұйым — ағымдағы дүкендікі (X-Store-Id); құқық әр шақыруда серверде тексеріледі.
/// </summary>
[ApiController]
[Route("api/pos/v1/clients")]
[Authorize(Policy = StaffAuth.Policy)]
public class ClientsController(
    ClientService clients,
    ClientEditService edit,
    ClientCrmService crm,
    ClientSegmentsService segments,
    ClientImportService import,
    ClientExport export) : ControllerBase
{
    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet("meta")]
    public async Task<ActionResult<ClientMetaDto>> Meta(CancellationToken ct) => Ok(await clients.MetaAsync(ct));

    // ---------- Тізім, KPI, шолу ----------

    [HttpPost("search")]
    public async Task<ActionResult<ClientPageDto>> Search([FromBody] ClientSearchRequest r, CancellationToken ct) => Ok(await clients.SearchAsync(r, ct));

    [HttpPost("kpis")]
    public async Task<ActionResult<ClientKpisDto>> Kpis([FromBody] ClientSearchRequest r, CancellationToken ct) => Ok(await clients.KpisAsync(r, ct));

    [HttpGet("overview")]
    public async Task<ActionResult<ClientOverviewDto>> Overview([FromQuery] string? storeIds, CancellationToken ct) => Ok(await clients.OverviewAsync(storeIds, ct));

    [HttpGet("birthdays")]
    public async Task<ActionResult<IReadOnlyList<ClientBirthdayDto>>> Birthdays([FromQuery] string? storeIds, [FromQuery] int days = 7, CancellationToken ct = default) =>
        Ok(await clients.BirthdaysAsync(storeIds, days, ct));

    [HttpGet("debts")]
    public async Task<ActionResult<CustomerDebtsDto>> Debts([FromQuery] string? storeIds, [FromQuery] string? segment, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        Ok(await clients.DebtRegistryAsync(storeIds, segment, search, page, pageSize, ct));

    [HttpGet("lookup")]
    public async Task<ActionResult<ClientLookupDto>> Lookup([FromQuery] string? phone, CancellationToken ct) => Ok(await edit.LookupAsync(phone, ct));

    // ---------- Карточка ----------

    [HttpPost]
    public async Task<ActionResult<ClientCardDto>> Create([FromBody] ClientSaveRequest r, CancellationToken ct) => Ok(await edit.CreateAsync(r, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClientCardDto>> Card(Guid id, [FromQuery] string? storeIds, CancellationToken ct) => Ok(await clients.CardAsync(id, storeIds, ct));

    [HttpGet("by-customer/{customerId:guid}")]
    public async Task<ActionResult<ClientCardDto>> ByCustomer(Guid customerId, [FromQuery] string? storeIds, CancellationToken ct) =>
        Ok(await clients.ByCustomerAsync(customerId, storeIds, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ClientCardDto>> Update(Guid id, [FromBody] ClientSaveRequest r, CancellationToken ct) => Ok(await edit.UpdateAsync(id, r, ct));

    [HttpPost("{id:guid}/archive")]
    public async Task<ActionResult<ClientCardDto>> Archive(Guid id, [FromBody] ClientArchiveRequest r, CancellationToken ct) => Ok(await edit.ArchiveAsync(id, r, ct));

    [HttpPost("{id:guid}/restore")]
    public async Task<ActionResult<ClientCardDto>> Restore(Guid id, CancellationToken ct) => Ok(await edit.RestoreAsync(id, ct));

    [HttpPost("merge")]
    public async Task<ActionResult<ClientCardDto>> Merge([FromBody] ClientMergeRequest r, CancellationToken ct) => Ok(await edit.MergeAsync(r, ct));

    [HttpGet("{id:guid}/duplicates")]
    public async Task<ActionResult<IReadOnlyList<ClientDuplicateDto>>> Duplicates(Guid id, CancellationToken ct) => Ok(await edit.DuplicatesAsync(id, ct));

    [HttpGet("{id:guid}/sales")]
    public async Task<ActionResult<ClientSalesPageDto>> Sales(Guid id, [FromQuery] string? storeIds, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await clients.SalesAsync(id, storeIds, page, pageSize, ct));

    [HttpGet("{id:guid}/receipts")]
    public async Task<ActionResult<IReadOnlyList<ClientReceiptDto>>> Receipts(Guid id, [FromQuery] string? storeIds, CancellationToken ct) =>
        Ok(await clients.ReceiptsAsync(id, storeIds, ct));

    [HttpGet("{id:guid}/debts")]
    public async Task<ActionResult<CustomerDebtCardDto>> ClientDebts(Guid id, [FromQuery] string? storeIds, CancellationToken ct) =>
        Ok(await clients.DebtsAsync(id, storeIds, ct));

    [HttpGet("{id:guid}/documents")]
    public async Task<ActionResult<IReadOnlyList<ClientDocumentDto>>> Documents(Guid id, [FromQuery] string? storeIds, CancellationToken ct) =>
        Ok(await clients.DocumentsAsync(id, storeIds, ct));

    [HttpGet("{id:guid}/activity")]
    public async Task<ActionResult<ClientActivityDto>> Activity(Guid id, [FromQuery] string? storeIds, CancellationToken ct) =>
        Ok(await clients.ActivityAsync(id, storeIds, ct));

    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<IReadOnlyList<ClientHistoryDto>>> History(Guid id, CancellationToken ct) => Ok(await crm.HistoryAsync(id, ct));

    [HttpPost("{id:guid}/message")]
    public async Task<ActionResult<ClientCommunicationDto>> Message(Guid id, [FromBody] ClientMessageRequest r, CancellationToken ct) => Ok(await crm.MessageAsync(id, r, ct));

    // ---------- Жазбалар ----------

    [HttpGet("{id:guid}/notes")]
    public async Task<ActionResult<IReadOnlyList<ClientNoteDto>>> Notes(Guid id, CancellationToken ct) => Ok(await crm.NotesAsync(id, ct));

    [HttpPost("{id:guid}/notes")]
    public async Task<ActionResult<IReadOnlyList<ClientNoteDto>>> AddNote(Guid id, [FromBody] SaveClientNoteRequest r, CancellationToken ct) => Ok(await crm.AddNoteAsync(id, r, ct));

    [HttpPut("notes/{noteId:guid}")]
    public async Task<ActionResult<IReadOnlyList<ClientNoteDto>>> UpdateNote(Guid noteId, [FromBody] SaveClientNoteRequest r, CancellationToken ct) =>
        Ok(await crm.UpdateNoteAsync(noteId, r, ct));

    [HttpDelete("notes/{noteId:guid}")]
    public async Task<IActionResult> DeleteNote(Guid noteId, CancellationToken ct) { await crm.DeleteNoteAsync(noteId, ct); return NoContent(); }

    // ---------- Сегменттер мен топтық әрекеттер ----------

    [HttpGet("segments")]
    public async Task<ActionResult<IReadOnlyList<ClientSegmentDto>>> Segments([FromQuery] string? storeIds, CancellationToken ct) => Ok(await segments.ListAsync(storeIds, ct));

    [HttpPost("segments")]
    public async Task<ActionResult<ClientSegmentDto>> CreateSegment([FromBody] SaveClientSegmentRequest r, CancellationToken ct) => Ok(await segments.CreateAsync(r, ct));

    [HttpPut("segments/{id:guid}")]
    public async Task<ActionResult<ClientSegmentDto>> UpdateSegment(Guid id, [FromBody] SaveClientSegmentRequest r, CancellationToken ct) => Ok(await segments.UpdateAsync(id, r, ct));

    [HttpDelete("segments/{id:guid}")]
    public async Task<IActionResult> DeleteSegment(Guid id, CancellationToken ct) { await segments.DeleteAsync(id, ct); return NoContent(); }

    [HttpPost("bulk/segment")]
    public async Task<ActionResult<ClientBulkResultDto>> BulkSegment([FromBody] ClientBulkSegmentRequest r, CancellationToken ct) => Ok(await edit.AddToSegmentAsync(r, ct));

    [HttpPost("bulk/status")]
    public async Task<ActionResult<ClientBulkResultDto>> BulkStatus([FromBody] ClientBulkStatusRequest r, CancellationToken ct) => Ok(await edit.AssignStatusAsync(r, ct));

    // ---------- Импорт пен экспорт ----------

    [HttpPost("import/preview")]
    [RequestSizeLimit(ClientImportService.MaxFileBytes + 64 * 1024)]
    public async Task<ActionResult<ClientImportPreviewDto>> ImportPreview(IFormFile? file, [FromForm] string? mapping, [FromForm] string? strategy, CancellationToken ct)
    {
        await using var stream = file?.OpenReadStream() ?? Stream.Null;
        return Ok(await import.PreviewAsync(stream, file?.FileName ?? "", file?.Length ?? 0, mapping, strategy, ct));
    }

    [HttpPost("import/commit")]
    [RequestSizeLimit(ClientImportService.MaxFileBytes + 64 * 1024)]
    public async Task<ActionResult<ClientImportResultDto>> ImportCommit(IFormFile? file, [FromForm] string? mapping, [FromForm] string? strategy, [FromForm] Guid key,
        CancellationToken ct)
    {
        await using var stream = file?.OpenReadStream() ?? Stream.Null;
        return Ok(await import.CommitAsync(stream, file?.FileName ?? "", file?.Length ?? 0, mapping, strategy, key, ct));
    }

    [HttpGet("import/template")]
    public async Task<IActionResult> ImportTemplate(CancellationToken ct)
    {
        await clients.MetaAsync(ct);
        return File(import.Template(), XlsxType, "clients-template.xlsx");
    }

    [HttpPost("export")]
    public async Task<IActionResult> Export([FromBody] ClientExportRequest r, CancellationToken ct)
    {
        var (content, fileName, contentType) = await export.ExportAsync(r, ct);
        return File(content, contentType, fileName);
    }

    // ---------- Баптаулар ----------

    [HttpGet("settings")]
    public async Task<ActionResult<ClientSettingsDto>> Settings(CancellationToken ct) => Ok(await crm.SettingsAsync(ct));

    [HttpPut("settings")]
    public async Task<ActionResult<ClientSettingsDto>> SaveSettings([FromBody] SaveClientSettingsRequest r, CancellationToken ct) => Ok(await crm.SaveSettingsAsync(r, ct));
}
