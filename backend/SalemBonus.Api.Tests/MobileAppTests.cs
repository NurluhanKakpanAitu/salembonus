using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using static SalemBonus.Api.Tests.Pos;

namespace SalemBonus.Api.Tests;

/// <summary>
/// App Store мен Google Play-дегі SalemBonus (STORE.md): нативті қабықтың CORS-ы, дүкен тексерушісінің кіруі
/// және аккаунтты қосымшаның ішінен өшіру — клиенттің SalemPos-тағы чектері мен қарыздары болса да.
/// </summary>
[Collection(ApiCollection.Name)]
public class MobileAppTests(ApiFixture api)
{
    private static object Debt(decimal amount) =>
        new { method = "Debt", amount, dueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7)) };

    private static string NewPhone() => "+7707" + Random.Shared.Next(1_000_000, 9_999_999);

    private static async Task<HttpResponseMessage> SendAsync(HttpClient http, HttpMethod method, string path, string token)
    {
        using var req = new HttpRequestMessage(method, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("ru"));
        return await http.SendAsync(req);
    }

    /// <summary>Кассада тіркелген клиент (SalemPos) — сол нөмірмен қосымшаға кіреді.</summary>
    private static async Task<Guid> PosCustomerAsync(Pos pos, string phone) =>
        (await pos.Post(pos.Owner, "/api/pos/v1/cashier/customers",
            new { phone, firstName = "Мобиль", lastName = "Клиент", birthDate = "1992-03-08" })).Ok["id"]!.GetValue<Guid>();

    [Theory]
    [InlineData("capacitor://localhost")] // iOS
    [InlineData("https://localhost")]     // Android
    public async Task Native_app_origins_pass_cors_preflight(string origin)
    {
        var http = api.NewAppClient();
        using var req = new HttpRequestMessage(HttpMethod.Options, "/api/me");
        req.Headers.Add("Origin", origin);
        req.Headers.Add("Access-Control-Request-Method", "DELETE");
        req.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        using var res = await http.SendAsync(req);

        Assert.True(res.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowed), $"{(int)res.StatusCode}");
        Assert.Equal(origin, Assert.Single(allowed));
    }

    [Fact]
    public async Task Unknown_origin_gets_no_cors_headers()
    {
        var http = api.NewAppClient();
        using var req = new HttpRequestMessage(HttpMethod.Options, "/api/me");
        req.Headers.Add("Origin", "https://evil.example");
        req.Headers.Add("Access-Control-Request-Method", "GET");

        using var res = await http.SendAsync(req);

        Assert.False(res.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Store_reviewer_signs_in_with_the_fixed_code_which_is_never_returned()
    {
        var http = api.NewAppClient();

        var requested = await http.PostAsJsonAsync("/api/auth/request-code", new { phone = ApiFixture.ReviewPhone });
        var body = (await requested.Content.ReadFromJsonAsync<JsonObject>())!;
        var verified = await http.PostAsJsonAsync("/api/auth/verify", new { phone = ApiFixture.ReviewPhone, code = ApiFixture.ReviewCode, device = "review" });
        var tokens = (await verified.Content.ReadFromJsonAsync<JsonObject>())!;

        requested.EnsureSuccessStatusCode();
        verified.EnsureSuccessStatusCode();
        Assert.Null(body["devCode"]); // тестте басқа нөмірлерге код жауапта келеді, тексерушінікі — ешқашан
        Assert.False(string.IsNullOrEmpty(tokens["accessToken"]!.GetValue<string>()));
    }

    [Fact]
    public async Task Deleting_the_account_keeps_the_receipt_for_the_store_but_without_the_customer()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 1500, stock: 5);
        var phone = NewPhone();
        var customerId = await PosCustomerAsync(pos, phone);
        var saleId = (await pos.SellAsync(pos.Owner, [Line(product, 1)], [Card(1500)], customerId: customerId)).Ok["id"]!.GetValue<Guid>();
        var http = api.NewAppClient();
        var login = await ApiFixture.AppLoginAsync(http, phone);
        var token = login["accessToken"]!.GetValue<string>();

        using var deleted = await SendAsync(http, HttpMethod.Delete, "/api/me", token);
        using var me = await SendAsync(http, HttpMethod.Get, "/api/me", token);
        var again = await ApiFixture.AppLoginAsync(api.NewAppClient(), phone);

        Assert.False(login["isNewCustomer"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, me.StatusCode);
        Assert.True(again["isNewCustomer"]!.GetValue<bool>()); // сол нөмірмен — жаңа, бос аккаунт
        Assert.Equal(1, await api.ScalarAsync<int>(
            $"select count(*)::int as \"Value\" from pos.sales where \"Id\" = '{saleId}' and \"CustomerId\" is null"));
        Assert.Equal(0, await api.ScalarAsync<int>(
            $"select count(*)::int as \"Value\" from \"Customers\" where \"Id\" = '{customerId}'"));
    }

    [Fact]
    public async Task Open_debt_blocks_deletion_and_after_repayment_the_customer_is_anonymised()
    {
        var pos = await api.NewDeviceAsync();
        var product = await pos.CreateProductAsync(price: 4000, stock: 5);
        var phone = NewPhone();
        var customerId = await PosCustomerAsync(pos, phone);
        var sale = await pos.SellAsync(pos.Owner, [Line(product, 1)], [Card(1000), Debt(3000)], customerId: customerId);
        var debtId = sale.Node("debt.id")!.GetValue<Guid>();
        var http = api.NewAppClient();
        var token = (await ApiFixture.AppLoginAsync(http, phone))["accessToken"]!.GetValue<string>();

        using var blocked = await SendAsync(http, HttpMethod.Delete, "/api/me", token);
        var blockedBody = (await blocked.Content.ReadFromJsonAsync<JsonObject>())!;
        (await pos.Post(pos.Owner, $"/api/pos/v1/debts/{debtId}/payments", new { amount = 3000m, method = "Cash" })).EnsureOk();
        using var deleted = await SendAsync(http, HttpMethod.Delete, "/api/me", token);
        var again = await ApiFixture.AppLoginAsync(api.NewAppClient(), phone);

        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        Assert.Contains("погасите долг", blockedBody["detail"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.True(again["isNewCustomer"]!.GetValue<bool>());
        // Өтелген қарыз кассаның есебінде қалады, ал клиент жазбасында жеке дерек жоқ.
        Assert.Equal(1, await api.ScalarAsync<int>(
            $"select count(*)::int as \"Value\" from pos.debts where \"Id\" = '{debtId}' and \"CustomerId\" = '{customerId}'"));
        Assert.Equal(1, await api.ScalarAsync<int>(
            $"select count(*)::int as \"Value\" from \"Customers\" where \"Id\" = '{customerId}' and \"Phone\" <> '{phone}' and \"FirstName\" = '' and \"BirthDate\" is null"));
        Assert.Equal(0, await api.ScalarAsync<int>(
            $"select count(*)::int as \"Value\" from pos.client_profiles where \"CustomerId\" = '{customerId}'"));
    }
}
