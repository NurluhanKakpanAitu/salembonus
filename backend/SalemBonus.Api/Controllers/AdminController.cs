using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalemBonus.Application.Platform;

namespace SalemBonus.Api.Controllers;

/// <summary>Әкімші токенінің атаулары: бөлек audience, «typ» = admin — бизнес пен клиент токендері бұл API-ға жарамайды.</summary>
public static class AdminAuth
{
    public const string Scheme = "Admin";
    public const string Policy = "Admin";
}

/// <summary>
/// SalemTech әкімшілігі (admin.salemtech.kz): кіру, шолу, бизнестер (қосу, модульдер, тоқтату, дүкен, аккаунттар), абонент төлемдері,
/// тарифтер, әкімшілер. Барлық өзгеріс platform.audit_log-қа жазылады.
/// </summary>
[ApiController]
[Route("api/admin/v1")]
[Authorize(Policy = AdminAuth.Policy)]
public class AdminController(PlatformAuthService auth, PlatformBusinessService businesses, PlatformBillingService billing, PlatformLeadsService leads) : ControllerBase
{
    // ---------- Кіру ----------

    [HttpPost("auth/login")]
    [AllowAnonymous]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(StaffAuth.LoginRateLimit)]
    public async Task<ActionResult<AdminSessionDto>> Login([FromBody] AdminLoginRequest r, CancellationToken ct) => Ok(await auth.LoginAsync(r, ct));

    [HttpGet("auth/me")]
    public async Task<ActionResult<AdminDto>> Me(CancellationToken ct) => Ok(await auth.MeAsync(ct));

    [HttpPost("auth/password")]
    public async Task<IActionResult> ChangePassword([FromBody] AdminChangePasswordRequest r, CancellationToken ct) { await auth.ChangePasswordAsync(r, ct); return NoContent(); }

    // ---------- Шолу ----------

    [HttpGet("overview")]
    public async Task<ActionResult<PlatformOverviewDto>> Overview(CancellationToken ct) => Ok(await billing.OverviewAsync(ct));

    // ---------- Бизнестер ----------

    [HttpGet("businesses")]
    public async Task<ActionResult<BusinessPageDto>> Businesses([FromQuery] string? search, [FromQuery] string? status, [FromQuery] Guid? planId,
        [FromQuery] string? module, [FromQuery] string? sort, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        Ok(await businesses.ListAsync(search, status, planId, module, sort, page, pageSize, ct));

    [HttpPost("businesses")]
    public async Task<ActionResult<BusinessCreatedDto>> CreateBusiness([FromBody] CreateBusinessRequest r, CancellationToken ct) => Ok(await businesses.CreateAsync(r, ct));

    [HttpGet("businesses/{id:guid}")]
    public async Task<ActionResult<BusinessDetailDto>> Business(Guid id, CancellationToken ct) => Ok(await businesses.DetailAsync(id, ct));

    [HttpPut("businesses/{id:guid}")]
    public async Task<ActionResult<BusinessDetailDto>> UpdateBusiness(Guid id, [FromBody] UpdateBusinessRequest r, CancellationToken ct) =>
        Ok(await businesses.UpdateAsync(id, r, ct));

    [HttpPut("businesses/{id:guid}/modules")]
    public async Task<ActionResult<BusinessDetailDto>> Modules(Guid id, [FromBody] SetModulesRequest r, CancellationToken ct) => Ok(await businesses.SetModulesAsync(id, r, ct));

    [HttpPost("businesses/{id:guid}/suspend")]
    public async Task<ActionResult<BusinessDetailDto>> Suspend(Guid id, [FromBody] ReasonRequest r, CancellationToken ct) => Ok(await businesses.SuspendAsync(id, r, ct));

    [HttpPost("businesses/{id:guid}/activate")]
    public async Task<ActionResult<BusinessDetailDto>> Activate(Guid id, CancellationToken ct) => Ok(await businesses.ActivateAsync(id, ct));

    [HttpPost("businesses/{id:guid}/stores")]
    public async Task<ActionResult<BusinessDetailDto>> AddStore(Guid id, [FromBody] CreateStoreRequest r, CancellationToken ct) => Ok(await businesses.AddStoreAsync(id, r, ct));

    [HttpPost("businesses/{id:guid}/stores/{storeId:guid}/active")]
    public async Task<ActionResult<BusinessDetailDto>> StoreActive(Guid id, Guid storeId, [FromBody] SetActiveRequest r, CancellationToken ct) =>
        Ok(await businesses.SetStoreActiveAsync(id, storeId, r.Active, ct));

    [HttpPost("businesses/{id:guid}/accounts")]
    public async Task<ActionResult<StaffCredentialsDto>> AddAccount(Guid id, [FromBody] CreateAccountRequest r, CancellationToken ct) => Ok(await businesses.AddAccountAsync(id, r, ct));

    [HttpPost("businesses/{id:guid}/accounts/{staffId:guid}/reset-password")]
    public async Task<ActionResult<StaffCredentialsDto>> ResetStaffPassword(Guid id, Guid staffId, CancellationToken ct) =>
        Ok(await businesses.ResetStaffPasswordAsync(id, staffId, ct));

    [HttpPost("businesses/{id:guid}/accounts/{staffId:guid}/active")]
    public async Task<ActionResult<StaffAccountDto>> StaffActive(Guid id, Guid staffId, [FromBody] SetActiveRequest r, CancellationToken ct) =>
        Ok(await businesses.SetStaffActiveAsync(id, staffId, r.Active, ct));

    // ---------- Төлемдер ----------

    [HttpGet("payments")]
    public async Task<ActionResult<PaymentsPageDto>> Payments([FromQuery] Guid? organizationId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] string? method, [FromQuery] bool includeVoided = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        Ok(await billing.PaymentsAsync(organizationId, from, to, method, includeVoided, page, pageSize, ct));

    [HttpGet("payments/suggest")]
    public async Task<ActionResult<PaymentSuggestionDto>> Suggest([FromQuery] Guid organizationId, [FromQuery] int months = 1, CancellationToken ct = default) =>
        Ok(await billing.SuggestAsync(organizationId, months, ct));

    [HttpPost("payments")]
    public async Task<ActionResult<PaymentDto>> CreatePayment([FromBody] CreatePaymentRequest r, CancellationToken ct) => Ok(await billing.CreatePaymentAsync(r, ct));

    [HttpPost("payments/{id:guid}/void")]
    public async Task<ActionResult<PaymentDto>> VoidPayment(Guid id, [FromBody] ReasonRequest r, CancellationToken ct) => Ok(await billing.VoidPaymentAsync(id, r, ct));

    // ---------- Лендинг өтінімдері ----------

    [HttpGet("leads")]
    public async Task<ActionResult<LeadsPageDto>> Leads([FromQuery] string? status, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        Ok(await leads.ListAsync(status, search, page, pageSize, ct));

    [HttpPut("leads/{id:guid}")]
    public async Task<ActionResult<LeadDto>> UpdateLead(Guid id, [FromBody] UpdateLeadRequest r, CancellationToken ct) => Ok(await leads.UpdateAsync(id, r, ct));

    // ---------- Тарифтер ----------

    [HttpGet("plans")]
    public async Task<ActionResult<IReadOnlyList<PlanDto>>> Plans(CancellationToken ct) => Ok(await billing.PlansAsync(ct));

    [HttpPost("plans")]
    public async Task<ActionResult<PlanDto>> CreatePlan([FromBody] SavePlanRequest r, CancellationToken ct) => Ok(await billing.CreatePlanAsync(r, ct));

    [HttpPut("plans/{id:guid}")]
    public async Task<ActionResult<PlanDto>> UpdatePlan(Guid id, [FromBody] SavePlanRequest r, CancellationToken ct) => Ok(await billing.UpdatePlanAsync(id, r, ct));

    // ---------- Әкімшілер ----------

    [HttpGet("admins")]
    public async Task<ActionResult<IReadOnlyList<AdminDto>>> Admins(CancellationToken ct) => Ok(await auth.AdminsAsync(ct));

    [HttpPost("admins")]
    public async Task<ActionResult<AdminCreatedDto>> CreateAdmin([FromBody] CreateAdminRequest r, CancellationToken ct) => Ok(await auth.CreateAdminAsync(r, ct));

    [HttpPost("admins/{id:guid}/active")]
    public async Task<ActionResult<AdminDto>> AdminActive(Guid id, [FromBody] SetActiveRequest r, CancellationToken ct) => Ok(await auth.SetAdminActiveAsync(id, r.Active, ct));

    [HttpPost("admins/{id:guid}/reset-password")]
    public async Task<ActionResult<AdminCreatedDto>> ResetAdminPassword(Guid id, CancellationToken ct) => Ok(await auth.ResetAdminPasswordAsync(id, ct));
}
