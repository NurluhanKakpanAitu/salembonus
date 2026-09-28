using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalemBonus.Application.Pos.Sales;

namespace SalemBonus.Api.Controllers.Pos;

/// <summary>
/// Касса экраны (ТЗ «Касса» §2–5): контекст, каталог баға мен қалдықпен, клиент іздеу мен тіркеу.
/// Касса құрылғысы httpOnly cookie арқылы анықталады.
/// </summary>
[ApiController]
[Route("api/pos/v1/cashier")]
[Authorize(Policy = StaffAuth.Policy)]
public class CashierController(CashierService cashier) : ControllerBase
{
    private string? DeviceToken => Request.Cookies[StaffAuth.RegisterCookie];

    [HttpGet("context")]
    public async Task<ActionResult<CashierContextDto>> Context(CancellationToken ct) =>
        Ok(await cashier.GetContextAsync(DeviceToken, ct));

    [HttpGet("catalog")]
    public async Task<ActionResult<CashierCatalogDto>> Catalog(
        [FromQuery] string? search, [FromQuery] Guid? nodeId, [FromQuery] string? sort,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 40, CancellationToken ct = default) =>
        Ok(await cashier.CatalogAsync(search, nodeId, sort, page, pageSize, ct));

    [HttpGet("catalog/by-barcode/{barcode}")]
    public async Task<ActionResult<CashierProductDto>> ByBarcode(string barcode, CancellationToken ct) =>
        Ok(await cashier.ByBarcodeAsync(barcode, ct));

    [HttpGet("customers")]
    public async Task<ActionResult<IReadOnlyList<CashierCustomerDto>>> SearchCustomers([FromQuery] string? q, CancellationToken ct) =>
        Ok(await cashier.SearchCustomersAsync(q, ct));

    [HttpGet("customers/{id:guid}")]
    public async Task<ActionResult<CashierCustomerDto>> Customer(Guid id, CancellationToken ct) =>
        Ok(await cashier.GetCustomerAsync(id, ct));

    [HttpPost("customers")]
    public async Task<ActionResult<CashierCustomerDto>> RegisterCustomer([FromBody] RegisterCustomerRequest request, CancellationToken ct) =>
        Ok(await cashier.RegisterCustomerAsync(request, ct));
}

/// <summary>Сатылым: бір транзакцияда чек, төлем, бонус, қалдық пен аудит (ТЗ «Касса» §20).</summary>
[ApiController]
[Route("api/pos/v1/sales")]
[Authorize(Policy = StaffAuth.Policy)]
public class SalesController(SaleService sales) : ControllerBase
{
    private string? DeviceToken => Request.Cookies[StaffAuth.RegisterCookie];

    [HttpPost]
    public async Task<ActionResult<ReceiptDto>> Create([FromBody] CreateSaleRequest request, CancellationToken ct) =>
        Ok(await sales.CreateAsync(request, DeviceToken, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReceiptDto>> Get(Guid id, CancellationToken ct) => Ok(await sales.GetAsync(id, ct));
}
