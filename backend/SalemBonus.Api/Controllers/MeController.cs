using Microsoft.AspNetCore.Mvc;
using SalemBonus.Application.Customers;
using SalemBonus.Application.Customers.Dtos;

namespace SalemBonus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MeController(ICustomerService service) : ControllerBase
{
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

    /// <summary>Кассада көрсетілетін QR коды.</summary>
    [HttpGet("qr")]
    public async Task<ActionResult<QrCodeDto>> GetQr(CancellationToken ct) =>
        Ok(await service.GetMyQrAsync(ct));
}
