using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Pos.Staff;

namespace SalemBonus.Api.Controllers.Pos;

/// <summary>
/// «Сотрудники»: қызметкерлер, рөлі мен дүкендері, рұқсаттары, PIN/құпиясөз, сеанстары, әрекет журналы.
/// Құқық — <c>staff.manage</c> (журнал — <c>settings.manage</c>-пен де).
/// </summary>
[ApiController]
[Route("api/pos/v1/staff")]
[Authorize(Policy = StaffAuth.Policy)]
public class StaffController(StaffManagementService staff) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<StaffListDto>> List(CancellationToken ct) => Ok(await staff.ListAsync(ct));

    /// <summary>Бизнестің әрекет журналы («Настройка → Журнал действий», қызметкер карточкасындағы «Активность»).</summary>
    [HttpGet("audit")]
    public async Task<ActionResult<AuditPageDto>> Audit(
        [FromQuery] Guid? staffId, [FromQuery] Guid? storeId, [FromQuery] string? action, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var take = Math.Clamp(pageSize, 1, 200);
        return Ok(await staff.AuditAsync(new AuditQuery(staffId, storeId, action, from?.ToUniversalTime(), to?.ToUniversalTime(), (Math.Max(page, 1) - 1) * take, take), ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StaffDetailDto>> Get(Guid id, CancellationToken ct) => Ok(await staff.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<StaffSavedDto>> Create([FromBody] SaveStaffRequest r, CancellationToken ct) => Ok(await staff.CreateAsync(r, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StaffSavedDto>> Update(Guid id, [FromBody] SaveStaffRequest r, CancellationToken ct) => Ok(await staff.UpdateAsync(id, r, ct));

    [HttpPost("{id:guid}/active")]
    public async Task<ActionResult<StaffDetailDto>> SetActive(Guid id, [FromBody] StaffActiveRequest r, CancellationToken ct) =>
        Ok(await staff.SetActiveAsync(id, r.Active, ct));

    [HttpPost("{id:guid}/reset-password")]
    public async Task<ActionResult<StaffSavedDto>> ResetPassword(Guid id, CancellationToken ct) => Ok(await staff.ResetPasswordAsync(id, ct));

    [HttpPost("{id:guid}/pin")]
    public async Task<ActionResult<StaffDetailDto>> SetPin(Guid id, [FromBody] SetStaffPinRequest r, CancellationToken ct) =>
        Ok(await staff.SetPinAsync(id, r.Pin, ct));

    [HttpPost("{id:guid}/sessions/revoke")]
    public async Task<ActionResult<StaffDetailDto>> RevokeSessions(Guid id, CancellationToken ct) => Ok(await staff.RevokeSessionsAsync(id, ct));
}
