using System.Net;

namespace SalemBonus.Api.Tests;

/// <summary>КАТО анықтамалығы: SalemBonus клиенті де, SalemPos қызметкері де (дүкен баптауы, клиент карточкасы) оқиды, токенсіз — жоқ.</summary>
[Collection(ApiCollection.Name)]
public class KatoTests(ApiFixture api)
{
    [Fact]
    public async Task Staff_and_customer_tokens_both_read_the_kato_directory()
    {
        var pos = await api.NewDeviceAsync(activateRegister: false);
        var regions = (await pos.Get(pos.Owner, "/api/kato/children")).Ok.AsArray();
        Assert.NotEmpty(regions);
        var region = regions[0]!["code"]!.GetValue<string>();
        Assert.NotEmpty((await pos.Get(pos.Cashier, $"/api/kato/children?parent={region}")).Ok.AsArray());
        (await pos.Get(pos.Owner, "/api/kato/search?q=Алматы")).EnsureOk();

        var app = api.NewAppClient();
        var customer = (await ApiFixture.AppLoginAsync(app, "+7707" + Random.Shared.Next(1_000_000, 9_999_999)))["accessToken"]!.GetValue<string>();
        Assert.NotEmpty((await new Pos(app) { Owner = customer, Cashier = customer }.Get(customer, "/api/kato/children")).Ok.AsArray());

        Assert.Equal(HttpStatusCode.Unauthorized, (await app.GetAsync("/api/kato/children")).StatusCode);
    }
}
