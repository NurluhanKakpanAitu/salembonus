using System.Net;

namespace SalemBonus.Api.Tests;

/// <summary>
/// Тауар карточкасы қолданатын көмекші маршруттар: қоймалар тізімі («Начальный остаток») және суретке
/// жүктеу сілтемесі. 20.10.2026 «Склад» модулінде бұлар кездейсоқ өшіп, сервер 401 қайтарды — енді тестпен қорғалған.
/// </summary>
[Collection(ApiCollection.Name)]
public class CatalogRoutesTests(ApiFixture api)
{
    [Fact]
    public async Task Product_card_can_list_warehouses_of_the_current_store()
    {
        var pos = await api.NewDeviceAsync(activateRegister: false);

        var res = await pos.Get(pos.Owner, "/api/pos/v1/warehouses");

        var list = res.Ok.AsArray();
        Assert.NotEmpty(list);
        Assert.Contains(list, w => w!["isDefault"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Upload_route_exists_and_checks_kind_and_permissions()
    {
        var pos = await api.NewDeviceAsync(activateRegister: false);
        var body = new { kind = "product", contentType = "image/webp", size = 1024 };

        var owner = await pos.Post(pos.Owner, "/api/pos/v1/uploads", body);
        var badKind = await pos.Post(pos.Owner, "/api/pos/v1/uploads", new { kind = "avatar", contentType = "image/webp", size = 1024 });
        var cashier = await pos.Post(pos.Cashier, "/api/pos/v1/uploads", body);

        // Тестте R2 бапталмаған: маршрут бар, бірақ «хранилище не настроено» — 400 (401/404 емес).
        Assert.True(owner.Status is HttpStatusCode.OK or HttpStatusCode.BadRequest, $"status {(int)owner.Status}");
        Assert.Equal(HttpStatusCode.BadRequest, badKind.Status);
        Assert.Equal(HttpStatusCode.Forbidden, cashier.Status);
    }
}
