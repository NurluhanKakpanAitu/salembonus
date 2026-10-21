using Microsoft.AspNetCore.Mvc;
using SalemBonus.Application.Customers;
using SalemBonus.Application.Customers.Dtos;
using SalemBonus.Application.Notifications;
using SalemBonus.Application.Notifications.Dtos;

namespace SalemBonus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MeController(ICustomerService service, IPushDeviceService devices) : ControllerBase
{
    /// <summary>
    /// Push алатын құрылғыны тіркеу (App Store / Google Play қосымшасы, кірген соң және токен жаңарғанда).
    /// Сол токен басқа аккаунтта тұрса, осы аккаунтқа көшеді.
    /// </summary>
    [HttpPost("devices")]
    public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceRequest request, CancellationToken ct)
    {
        await devices.RegisterAsync(request, ct);
        return NoContent();
    }

    /// <summary>Шыққанда: бұл құрылғыға енді push келмейді. Токен табылмаса да 204.</summary>
    [HttpDelete("devices")]
    public async Task<IActionResult> UnregisterDevice([FromBody] UnregisterDeviceRequest request, CancellationToken ct)
    {
        await devices.UnregisterAsync(request.Token, ct);
        return NoContent();
    }

    [HttpGet]
    public async Task<ActionResult<CustomerDto>> Get(CancellationToken ct)
    {
        var me = await service.GetMeAsync(ct);
        return me is null ? NotFound() : Ok(me);
    }

    /// <summary>Профильді толтыру/өзгерту.</summary>
    [HttpPut]
    public async Task<ActionResult<CustomerDto>> Update([FromBody] UpdateProfileRequest request, CancellationToken ct) =>
        Ok(await service.UpdateMeAsync(request, ct));

    /// <summary>Профиль фотосын жүктеу немесе өшіру.</summary>
    [HttpPut("avatar")]
    public async Task<ActionResult<CustomerDto>> UpdateAvatar([FromBody] UpdateAvatarRequest request, CancellationToken ct) =>
        Ok(await service.UpdateAvatarAsync(request, ct));

    /// <summary>
    /// Аккаунтты біржола өшіру. Барлық жеке дерек, карталар мен операциялар жойылады,
    /// қайтару мүмкін емес.
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> Delete(CancellationToken ct)
    {
        await service.DeleteMeAsync(ct);
        return NoContent();
    }

    /// <summary>Кассада көрсетілетін QR коды.</summary>
    [HttpGet("qr")]
    public async Task<ActionResult<QrCodeDto>> GetQr(CancellationToken ct) =>
        Ok(await service.GetMyQrAsync(ct));
}
