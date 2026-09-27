using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Application.Pos.Inventory;
using SalemBonus.Application.Pos.Media;

namespace SalemBonus.Api.Controllers.Pos;

/// <summary>Ағымдағы дүкеннің қоймалары (толық «Склад» модулі кейін).</summary>
[ApiController]
[Route("api/pos/v1/warehouses")]
[Authorize(Policy = StaffAuth.Policy)]
public class WarehousesController(WarehouseService warehouses) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WarehouseDto>>> List(CancellationToken ct) => Ok(await warehouses.ListAsync(ct));
}

/// <summary>Суретті R2-ге тікелей жүктеуге қысқа мерзімді сілтеме.</summary>
[ApiController]
[Route("api/pos/v1/uploads")]
[Authorize(Policy = StaffAuth.Policy)]
public class UploadsController(MediaService media) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<UploadDto>> Create([FromBody] UploadRequest request, CancellationToken ct) =>
        Ok(await media.CreateUploadAsync(request, ct));
}
