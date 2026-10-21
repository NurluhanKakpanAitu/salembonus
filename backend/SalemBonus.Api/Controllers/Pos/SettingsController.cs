using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalemBonus.Application.Pos.Sales;
using SalemBonus.Application.Pos.Settings;

namespace SalemBonus.Api.Controllers.Pos;

/// <summary>
/// «Настройка»: дүкен профилі, дүкендер, кассалар, төлем түрлері мен чек үлгісі, хабарламалар, тариф.
/// Құқық — <c>settings.manage</c>; дүкендер мен бизнес деректері — тек иесіне.
/// </summary>
[ApiController]
[Route("api/pos/v1/settings")]
[Authorize(Policy = StaffAuth.Policy)]
public class SettingsController(StoreSettingsService settings, CashierSettingsService cashier) : ControllerBase
{
    private string? DeviceToken => Request.Cookies[StaffAuth.RegisterCookie];

    [HttpGet("store")]
    public async Task<ActionResult<StoreProfileDto>> Store(CancellationToken ct) => Ok(await settings.GetProfileAsync(ct));

    [HttpPut("store")]
    public async Task<ActionResult<StoreProfileDto>> UpdateStore([FromBody] UpdateStoreProfileRequest r, CancellationToken ct) => Ok(await settings.UpdateProfileAsync(r, ct));

    [HttpGet("stores")]
    public async Task<ActionResult<IReadOnlyList<StoreRowDto>>> Stores(CancellationToken ct) => Ok(await settings.ListStoresAsync(ct));

    [HttpPost("stores")]
    public async Task<ActionResult<StoreRowDto>> CreateStore([FromBody] CreateStoreRequest r, CancellationToken ct) => Ok(await settings.CreateStoreAsync(r, ct));

    [HttpPost("stores/{id:guid}/active")]
    public async Task<ActionResult<StoreRowDto>> SetStoreActive(Guid id, [FromBody] StoreActiveRequest r, CancellationToken ct) =>
        Ok(await settings.SetStoreActiveAsync(id, r.Active, ct));

    [HttpGet("registers")]
    public async Task<ActionResult<IReadOnlyList<RegisterRowDto>>> Registers(CancellationToken ct) => Ok(await settings.ListRegistersAsync(DeviceToken, ct));

    [HttpPost("registers")]
    public async Task<ActionResult<RegisterRowDto>> CreateRegister([FromBody] SaveRegisterRequest r, CancellationToken ct) =>
        Ok(await settings.CreateRegisterAsync(r, DeviceToken, ct));

    [HttpPut("registers/{id:guid}")]
    public async Task<ActionResult<RegisterRowDto>> UpdateRegister(Guid id, [FromBody] SaveRegisterRequest r, CancellationToken ct) =>
        Ok(await settings.UpdateRegisterAsync(id, r, DeviceToken, ct));

    [HttpPost("registers/{id:guid}/active")]
    public async Task<ActionResult<RegisterRowDto>> SetRegisterActive(Guid id, [FromBody] RegisterActiveRequest r, CancellationToken ct) =>
        Ok(await settings.SetRegisterActiveAsync(id, r.Active, DeviceToken, ct));

    [HttpPost("registers/{id:guid}/unbind")]
    public async Task<ActionResult<RegisterRowDto>> UnbindRegister(Guid id, CancellationToken ct) => Ok(await settings.UnbindRegisterAsync(id, DeviceToken, ct));

    [HttpGet("cashier")]
    public async Task<ActionResult<StoreCashierSettingsDto>> Cashier(CancellationToken ct) => Ok(await settings.GetCashierSettingsAsync(ct));

    [HttpPut("cashier")]
    public async Task<ActionResult<StoreCashierSettingsDto>> UpdateCashier([FromBody] UpdateStoreCashierSettingsRequest r, CancellationToken ct) =>
        Ok(await settings.UpdateCashierSettingsAsync(r, ct));

    [HttpPost("recipients")]
    public async Task<ActionResult<TransferRecipientDto>> CreateRecipient([FromBody] SaveRecipientRequest r, CancellationToken ct) =>
        Ok(await cashier.SaveRecipientAsync(null, r, ct));

    [HttpPut("recipients/{id:guid}")]
    public async Task<ActionResult<TransferRecipientDto>> UpdateRecipient(Guid id, [FromBody] SaveRecipientRequest r, CancellationToken ct) =>
        Ok(await cashier.SaveRecipientAsync(id, r, ct));

    [HttpDelete("recipients/{id:guid}")]
    public async Task<IActionResult> RemoveRecipient(Guid id, CancellationToken ct)
    {
        await cashier.RemoveRecipientAsync(id, ct);
        return NoContent();
    }

    [HttpGet("plan")]
    public async Task<ActionResult<PlanInfoDto>> Plan(CancellationToken ct) => Ok(await settings.GetPlanAsync(ct));
}
