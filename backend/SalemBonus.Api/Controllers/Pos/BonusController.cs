using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalemBonus.Application.BonusCards;
using SalemBonus.Application.Pos.Bonus;
using SalemBonus.Domain.Core;

namespace SalemBonus.Api.Controllers.Pos;

/// <summary>
/// «Бонусы» (ТЗ «Бонусы» §14): шолу, мәртебелер, жану, туған күн, таратулар, статистика, операциялар журналы, бағдарлама ережелері.
/// Бағдарлама — ағымдағы дүкендікі (X-Store-Id); құқық әр шақыруда серверде тексеріледі.
/// </summary>
[ApiController]
[Route("api/pos/v1/bonus")]
[Authorize(Policy = StaffAuth.Policy)]
public class BonusController(
    BonusStatusService statuses,
    BonusSettingsService settings,
    BonusOperationsService operations,
    BonusCampaignService campaigns,
    BonusReportsService reports,
    BonusTermsService terms,
    BonusExport export,
    BonusAccess access,
    BonusJobs jobs,
    IBonusExpiryService expiry) : ControllerBase
{
    [HttpGet("meta")]
    public async Task<ActionResult<BonusMetaDto>> Meta(CancellationToken ct) => Ok(await statuses.MetaAsync(ct));

    // ---------- Шолу мен статистика ----------

    [HttpGet("overview")]
    public async Task<ActionResult<BonusOverviewDto>> Overview([FromQuery] string? storeIds, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        Ok(await reports.OverviewAsync(storeIds, from, to, ct));

    [HttpGet("statistics")]
    public async Task<ActionResult<BonusStatisticsDto>> Statistics([FromQuery] string? storeIds, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] Guid? statusId, [FromQuery] string? type, CancellationToken ct) =>
        Ok(await reports.StatisticsAsync(storeIds, from, to, statusId, type, ct));

    // ---------- Мәртебелер ----------

    [HttpGet("statuses")]
    public async Task<ActionResult<IReadOnlyList<BonusStatusDto>>> Statuses(CancellationToken ct) => Ok(await statuses.ListAsync(ct));

    [HttpPost("statuses")]
    public async Task<ActionResult<BonusStatusDto>> CreateStatus([FromBody] SaveStatusRequest r, CancellationToken ct) => Ok(await statuses.CreateAsync(r, ct));

    [HttpPut("statuses/{id:guid}")]
    public async Task<ActionResult<BonusStatusDto>> UpdateStatus(Guid id, [FromBody] SaveStatusRequest r, CancellationToken ct) => Ok(await statuses.UpdateAsync(id, r, ct));

    [HttpPost("statuses/{id:guid}/duplicate")]
    public async Task<ActionResult<BonusStatusDto>> DuplicateStatus(Guid id, CancellationToken ct) => Ok(await statuses.DuplicateAsync(id, ct));

    [HttpPost("statuses/{id:guid}/activate")]
    public async Task<ActionResult<BonusStatusDto>> ActivateStatus(Guid id, CancellationToken ct) => Ok(await statuses.SetActiveAsync(id, true, ct));

    [HttpPost("statuses/{id:guid}/deactivate")]
    public async Task<ActionResult<BonusStatusDto>> DeactivateStatus(Guid id, CancellationToken ct) => Ok(await statuses.SetActiveAsync(id, false, ct));

    [HttpDelete("statuses/{id:guid}")]
    public async Task<IActionResult> DeleteStatus(Guid id, CancellationToken ct) { await statuses.DeleteAsync(id, ct); return NoContent(); }

    // ---------- Клиенттер ----------

    [HttpGet("customers")]
    public async Task<ActionResult<BonusCustomersPageDto>> Customers([FromQuery] string? search, [FromQuery] Guid? statusId, [FromQuery] bool positive = false,
        [FromQuery] string? sort = null, [FromQuery] string? dir = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        Ok(await statuses.CustomersAsync(search, statusId, positive, sort, dir, page, pageSize, ct));

    [HttpGet("customers/{customerId:guid}")]
    public async Task<ActionResult<BonusCustomerCardDto>> Customer(Guid customerId, CancellationToken ct) => Ok(await statuses.CustomerAsync(customerId, ct));

    [HttpPost("customers/{customerId:guid}/status")]
    public async Task<ActionResult<BonusCustomerCardDto>> ChangeStatus(Guid customerId, [FromBody] ChangeStatusRequest r, CancellationToken ct) =>
        Ok(await statuses.ChangeStatusAsync(customerId, r, ct));

    [HttpPost("customers/{customerId:guid}/opt-out")]
    public async Task<ActionResult<BonusCustomerCardDto>> OptOut(Guid customerId, [FromBody] OptOutRequest r, CancellationToken ct) =>
        Ok(await statuses.SetOptOutAsync(customerId, r, ct));

    // ---------- Жану ----------

    [HttpGet("expiry/settings")]
    public async Task<ActionResult<ExpirySettingsDto>> Expiry(CancellationToken ct) => Ok(await settings.ExpiryAsync(ct));

    [HttpPut("expiry/settings")]
    public async Task<ActionResult<ExpirySettingsDto>> SaveExpiry([FromBody] SaveExpirySettingsRequest r, CancellationToken ct) => Ok(await settings.SaveExpiryAsync(r, ct));

    [HttpGet("expiry/upcoming")]
    public async Task<ActionResult<ExpiringPageDto>> Expiring([FromQuery] int days = 30, [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null,
        [FromQuery] string? search = null, [FromQuery] Guid? statusId = null, [FromQuery] string? storeIds = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        Ok(await settings.ExpiringAsync(days, from, to, search, statusId, storeIds, page, pageSize, ct));

    [HttpPut("lots/{lotId:guid}/expiry")]
    public async Task<IActionResult> SetLotExpiry(Guid lotId, [FromBody] SetLotExpiryRequest r, CancellationToken ct) { await settings.SetLotExpiryAsync(lotId, r, ct); return NoContent(); }

    // ---------- Туған күн ----------

    [HttpGet("birthday/settings")]
    public async Task<ActionResult<BirthdaySettingsDto>> Birthday(CancellationToken ct) => Ok(await settings.BirthdayAsync(ct));

    [HttpPut("birthday/settings")]
    public async Task<ActionResult<BirthdaySettingsDto>> SaveBirthday([FromBody] SaveBirthdaySettingsRequest r, CancellationToken ct) => Ok(await settings.SaveBirthdayAsync(r, ct));

    [HttpGet("birthday/upcoming")]
    public async Task<ActionResult<BirthdaysPageDto>> Birthdays([FromQuery] int days = 30, [FromQuery] Guid? statusId = null, [FromQuery] string? state = null,
        [FromQuery] string? search = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        Ok(await settings.UpcomingBirthdaysAsync(days, statusId, state, search, page, pageSize, ct));

    [HttpGet("birthday/preview")]
    public async Task<ActionResult<BirthdayPreviewDto>> BirthdayPreview([FromQuery] Guid? customerId, CancellationToken ct) => Ok(await settings.PreviewAsync(customerId, ct));

    [HttpPost("birthday/grants")]
    public async Task<ActionResult<UpcomingBirthdayDto?>> GrantBirthday([FromBody] ManualBirthdayRequest r, CancellationToken ct) => Ok(await settings.GrantManualAsync(r, ct));

    // ---------- Таратулар ----------

    [HttpGet("campaigns")]
    public async Task<ActionResult<CampaignsPageDto>> Campaigns([FromQuery] string? status, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        Ok(await campaigns.ListAsync(status, search, page, pageSize, ct));

    [HttpPost("campaigns")]
    public async Task<ActionResult<CampaignDto>> CreateCampaign([FromBody] SaveCampaignRequest r, CancellationToken ct) => Ok(await campaigns.CreateAsync(r, ct));

    [HttpPost("campaigns/audience")]
    public async Task<ActionResult<AudiencePreviewDto>> Audience([FromBody] SaveCampaignRequest r, CancellationToken ct) => Ok(await campaigns.PreviewAsync(r, ct));

    [HttpGet("campaigns/{id:guid}")]
    public async Task<ActionResult<CampaignDto>> Campaign(Guid id, CancellationToken ct) => Ok(await campaigns.GetAsync(id, ct));

    [HttpPut("campaigns/{id:guid}")]
    public async Task<ActionResult<CampaignDto>> UpdateCampaign(Guid id, [FromBody] SaveCampaignRequest r, CancellationToken ct) => Ok(await campaigns.UpdateAsync(id, r, ct));

    [HttpPost("campaigns/{id:guid}/duplicate")]
    public async Task<ActionResult<CampaignDto>> DuplicateCampaign(Guid id, CancellationToken ct) => Ok(await campaigns.DuplicateAsync(id, ct));

    [HttpPost("campaigns/{id:guid}/test")]
    public async Task<ActionResult<TestSendResultDto>> TestCampaign(Guid id, [FromBody] TestSendRequest r, CancellationToken ct) => Ok(await campaigns.TestAsync(id, r, ct));

    [HttpPost("campaigns/{id:guid}/schedule")]
    public async Task<ActionResult<CampaignDto>> ScheduleCampaign(Guid id, [FromBody] ScheduleCampaignRequest r, CancellationToken ct) => Ok(await campaigns.ScheduleAsync(id, r, ct));

    [HttpPost("campaigns/{id:guid}/send")]
    public async Task<ActionResult<CampaignDto>> SendCampaign(Guid id, CancellationToken ct) => Ok(await campaigns.SendAsync(id, ct));

    [HttpPost("campaigns/{id:guid}/cancel")]
    public async Task<ActionResult<CampaignDto>> CancelCampaign(Guid id, CancellationToken ct) => Ok(await campaigns.CancelAsync(id, ct));

    [HttpPost("campaigns/{id:guid}/retry")]
    public async Task<ActionResult<CampaignDto>> RetryCampaign(Guid id, CancellationToken ct) => Ok(await campaigns.RetryAsync(id, ct));

    [HttpGet("campaigns/{id:guid}/recipients")]
    public async Task<ActionResult<CampaignRecipientsPageDto>> Recipients(Guid id, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        Ok(await campaigns.RecipientsAsync(id, status, page, pageSize, ct));

    // ---------- Операциялар ----------

    [HttpGet("operations")]
    public async Task<ActionResult<BonusOperationsPageDto>> Operations([FromQuery] BonusListQuery q, CancellationToken ct) => Ok(await operations.ListAsync(q, ct));

    [HttpGet("operations/{id:guid}")]
    public async Task<ActionResult<BonusOperationDetailDto>> Operation(Guid id, CancellationToken ct) => Ok(await operations.GetAsync(id, ct));

    [HttpPost("operations/accrue")]
    public async Task<ActionResult<BonusOperationDto>> Accrue([FromBody] ManualAccrualRequest r, CancellationToken ct) => Ok(await operations.AccrueAsync(r, ct));

    [HttpPost("operations/write-off")]
    public async Task<ActionResult<BonusOperationDto>> WriteOff([FromBody] ManualWriteOffRequest r, CancellationToken ct) => Ok(await operations.WriteOffAsync(r, ct));

    [HttpPost("operations/{id:guid}/return")]
    public async Task<ActionResult<BonusOperationDto>> Return(Guid id, [FromBody] ReturnBonusRequest r, CancellationToken ct) => Ok(await operations.ReturnAsync(id, r, ct));

    [HttpPost("operations/{id:guid}/reverse")]
    public async Task<ActionResult<BonusOperationDto>> Reverse(Guid id, [FromBody] ReverseBonusRequest r, CancellationToken ct) => Ok(await operations.ReverseAsync(id, r, ct));

    // ---------- Ережелер ----------

    [HttpGet("terms")]
    public async Task<ActionResult<TermsStateDto>> Terms(CancellationToken ct) => Ok(await terms.GetAsync(ct));

    [HttpPut("terms/draft")]
    public async Task<ActionResult<BonusTermsDto>> SaveTerms([FromBody] SaveTermsDraftRequest r, CancellationToken ct) => Ok(await terms.SaveDraftAsync(r, ct));

    [HttpPost("terms/publish")]
    public async Task<ActionResult<BonusTermsDto>> PublishTerms([FromBody] PublishTermsRequest r, CancellationToken ct) => Ok(await terms.PublishAsync(r, ct));

    [HttpPost("terms/{id:guid}/restore")]
    public async Task<ActionResult<BonusTermsDto>> RestoreTerms(Guid id, CancellationToken ct) => Ok(await terms.RestoreAsync(id, ct));

    // ---------- Экспорт, фондық тапсырмалар ----------

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string report, [FromQuery] string? format, [FromQuery] BonusListQuery q, [FromQuery] int days = 30,
        [FromQuery] Guid? id = null, [FromQuery] Guid? statusId = null, CancellationToken ct = default)
    {
        var (content, fileName, contentType) = await export.ExportAsync(report, format, q, days, id, statusId, ct);
        return File(content, contentType, fileName);
    }

    /// <summary>
    /// «Обработать сейчас»: жану, туған күн, жану ескертулері, жоспарланған таратулар, мәртебе мерзімдері — фондық тапсырманы күтпей.
    /// Барлығы қайталауға қауіпсіз (екінші іске қосу қайталанған операция жасамайды).
    /// </summary>
    [HttpPost("jobs/run")]
    public async Task<ActionResult<object>> RunJobs(CancellationToken ct)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusManage, ct);
        int burned, total = 0;
        do { burned = await expiry.BurnExpiredAsync(ct); total += burned; } while (burned > 0);
        var birthdays = await jobs.BirthdaysAsync(c.Store.Id, ct);
        var warnings = await jobs.WarningsAsync(c.Store.Id, ct);
        var r = await jobs.RunAsync(ct);
        return Ok(new { Expired = total, Birthdays = birthdays + r.Birthdays, Warnings = warnings + r.Warnings, r.Campaigns, r.Statuses });
    }
}
