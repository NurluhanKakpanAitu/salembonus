using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using SalemBonus.Domain.Enums;
using static SalemBonus.Api.Tests.Pos;

namespace SalemBonus.Api.Tests;

/// <summary>
/// Push-хабарламалар (STORE.md 7): қосымша құрылғы токенін тіркейді, жаңа хабарлама (бонус есептелді/шегерілді,
/// туған күн, акция) клиенттің барлық құрылғысына оның тілінде кетеді. FCM мен APNs орнына — <see cref="FakePushSender"/>.
/// </summary>
[Collection(ApiCollection.Name)]
public class PushTests(ApiFixture api)
{
    private const string B = "/api/pos/v1/bonus";

    private static string NewPhone() => "+7706" + Random.Shared.Next(1_000_000, 9_999_999);
    private static string NewToken(string prefix) => prefix + "-" + Guid.NewGuid().ToString("N");

    private static async Task<HttpResponseMessage> SendAsync(HttpClient http, HttpMethod method, string path, string? token, object? body = null)
    {
        using var req = new HttpRequestMessage(method, path);
        if (token is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("ru"));
        if (body is not null) req.Content = JsonContent.Create(body);
        return await http.SendAsync(req);
    }

    /// <summary>Кассада тіркелген клиент сол нөмірмен қосымшаға кіреді. Нәтиже — клиент id-і мен қосымшаның токені.</summary>
    private async Task<(Guid CustomerId, string AppToken)> AppCustomerAsync(Pos pos, HttpClient http)
    {
        var phone = NewPhone();
        var id = (await pos.Post(pos.Owner, "/api/pos/v1/cashier/customers",
            new { phone, firstName = "Пуш", lastName = "Клиент", birthDate = "1991-04-12" })).Ok["id"]!.GetValue<Guid>();
        var login = await ApiFixture.AppLoginAsync(http, phone);
        return (id, login["accessToken"]!.GetValue<string>());
    }

    private static async Task RegisterAsync(HttpClient http, string appToken, string deviceToken, string platform, string language)
    {
        var res = await SendAsync(http, HttpMethod.Post, "/api/me/devices", appToken, new { token = deviceToken, platform, language });
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    private Task<int> DevicesAsync(string deviceToken) =>
        api.ScalarAsync<int>($"select count(*)::int as \"Value\" from \"PushDevices\" where \"Token\" = '{deviceToken}'");

    private static Task<ApiResult> ManualAccrueAsync(Pos pos, Guid customerId, int amount) =>
        pos.Post(pos.Owner, $"{B}/operations/accrue", new { customerId, amount, reason = "Компенсация", unlimited = false });

    [Fact]
    public async Task Sale_bonus_reaches_every_device_of_the_customer_in_its_own_language()
    {
        var pos = await api.NewDeviceAsync();
        var http = api.NewAppClient();
        var (customer, appToken) = await AppCustomerAsync(pos, http);
        var android = NewToken("fcm");
        var iphone = NewToken("apns");
        await RegisterAsync(http, appToken, android, "android", "ru");
        await RegisterAsync(http, appToken, iphone, "ios", "kk");

        var product = await pos.CreateProductAsync(price: 2000, stock: 5);
        var sale = (await pos.SellAsync(pos.Owner, [Line(product, 1)], [Cash(2000)], customerId: customer)).Ok;
        var accrued = sale["bonusAccrued"]!.GetValue<int>();
        Assert.True(accrued > 0, "Демо дүкен сатылымға бонус есептеуі керек");

        var ru = await api.Push.WaitAsync(android, m => m.Title == "Бонус начислен!");
        var kk = await api.Push.WaitAsync(iphone, m => m.Title == "Бонус есептелді!");
        Assert.Equal(PushPlatform.Android, ru.Platform);
        Assert.Equal(PushPlatform.Ios, kk.Platform);
        Assert.Contains("вам начислено", ru.Message.Body);
        Assert.Contains("Сізге", kk.Message.Body);
        Assert.Contains(accrued.ToString(), ru.Message.Body.Replace(" ", ""));

        // Басқанда қосымша сол дүкеннің хабарламаларын ашады; id — клиенттің өз хабарламасы.
        var data = ru.Message.Data;
        Assert.Equal(nameof(NotificationType.BonusAccrued), data["type"]);
        Assert.Equal($"/notifications/{data["storeId"]}", data["path"]);
        var mine = await SendAsync(http, HttpMethod.Get, "/api/notifications?take=50", appToken);
        var items = (await mine.Content.ReadFromJsonAsync<JsonObject>())!["items"]!.AsArray();
        Assert.Contains(items, n => n!["id"]!.GetValue<string>() == data["notificationId"]);
        Assert.True(ru.Message.Badge >= 1); // iOS белгісі — оқылмағандар саны

        // Бонуспен төлеу — «шегерілді» push-ы.
        (await pos.SellAsync(pos.Owner, [Line(product, 1)], [Cash(2000 - accrued)], customerId: customer, bonusRedeem: accrued)).EnsureOk();
        var redeemed = await api.Push.WaitAsync(android, m => m.Title == "Бонус списан");
        Assert.Contains("списано", redeemed.Message.Body);
    }

    [Fact]
    public async Task Store_promo_message_is_pushed_as_written()
    {
        var pos = await api.NewDeviceAsync();
        var http = api.NewAppClient();
        var phone = NewPhone();
        var client = (await pos.Post(pos.Owner, "/api/pos/v1/clients",
            new { phone, firstName = "Акция", lastName = "Пуш", birthDate = "1993-02-01", consentApp = true })).Ok;
        var clientId = client["client"]!["id"]!.GetValue<Guid>();
        var appToken = (await ApiFixture.AppLoginAsync(http, phone))["accessToken"]!.GetValue<string>();
        var device = NewToken("apns");
        await RegisterAsync(http, appToken, device, "ios", "ru");

        var text = "Скидка 15% на всё до пятницы " + Guid.NewGuid().ToString("N")[..4];
        (await pos.Post(pos.Owner, $"/api/pos/v1/clients/{clientId}/message", new { channel = "app", text })).EnsureOk();

        // Үлгі кілті жоқ — дүкен жазғаны күйінде, тақырыбы — дүкен атауы.
        var push = await api.Push.WaitAsync(device, m => m.Body == text);
        Assert.Equal(nameof(NotificationType.Promo), push.Message.Data["type"]);
        Assert.False(string.IsNullOrWhiteSpace(push.Message.Title));
    }

    [Fact]
    public async Task Same_phone_signed_in_to_another_account_moves_the_token_and_logout_removes_it()
    {
        var pos = await api.NewDeviceAsync();
        var http = api.NewAppClient();
        var (first, firstApp) = await AppCustomerAsync(pos, http);
        var (second, secondApp) = await AppCustomerAsync(pos, http);
        var device = NewToken("fcm");

        await RegisterAsync(http, firstApp, device, "android", "ru");
        await RegisterAsync(http, firstApp, device, "android", "kk"); // қайта тіркеу — жаңа жазба емес
        await RegisterAsync(http, secondApp, device, "android", "ru");

        Assert.Equal(1, await DevicesAsync(device));
        Assert.Equal(second, await api.ScalarAsync<Guid>(
            $"select \"CustomerId\" as \"Value\" from \"PushDevices\" where \"Token\" = '{device}'"));

        // Бірінші аккаунттың хабарламасы бұл телефонға енді келмейді, екіншісінікі — келеді.
        (await ManualAccrueAsync(pos, first, 111)).EnsureOk();
        (await ManualAccrueAsync(pos, second, 222)).EnsureOk();
        await api.Push.WaitAsync(device, m => m.Body.Contains("222"));
        await Task.Delay(300);
        Assert.DoesNotContain(api.Push.To(device), s => s.Message.Body.Contains("111"));

        // Бөтен токенді өшіру ештеңе істемейді; өз токенін — өшіреді (шығу).
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(http, HttpMethod.Delete, "/api/me/devices", firstApp, new { token = device })).StatusCode);
        Assert.Equal(1, await DevicesAsync(device));
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(http, HttpMethod.Delete, "/api/me/devices", secondApp, new { token = device })).StatusCode);
        Assert.Equal(0, await DevicesAsync(device));
    }

    [Fact]
    public async Task Token_rejected_by_fcm_or_apns_is_forgotten()
    {
        var pos = await api.NewDeviceAsync();
        var http = api.NewAppClient();
        var (customer, appToken) = await AppCustomerAsync(pos, http);
        var device = NewToken("apns");
        api.Push.DeadTokens[device] = true;
        await RegisterAsync(http, appToken, device, "ios", "kk");

        (await ManualAccrueAsync(pos, customer, 50)).EnsureOk();
        await api.Push.WaitAsync(device, _ => true);

        for (var i = 0; i < 100 && await DevicesAsync(device) > 0; i++) await Task.Delay(50);
        Assert.Equal(0, await DevicesAsync(device));
    }

    [Fact]
    public async Task Without_push_keys_nothing_is_sent()
    {
        var pos = await api.NewDeviceAsync();
        var http = api.NewAppClient();
        var (customer, appToken) = await AppCustomerAsync(pos, http);
        var device = NewToken("fcm");
        await RegisterAsync(http, appToken, device, "android", "ru");

        api.Push.Configured = false;
        try
        {
            (await ManualAccrueAsync(pos, customer, 77)).EnsureOk();
            await Task.Delay(500);
            Assert.Empty(api.Push.To(device));
        }
        finally
        {
            api.Push.Configured = true;
        }
    }

    [Fact]
    public async Task Account_deletion_removes_its_devices()
    {
        var http = api.NewAppClient();
        var login = await ApiFixture.AppLoginAsync(http, NewPhone());
        var appToken = login["accessToken"]!.GetValue<string>();
        var device = NewToken("fcm");
        await RegisterAsync(http, appToken, device, "android", "kk");
        Assert.Equal(1, await DevicesAsync(device));

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(http, HttpMethod.Delete, "/api/me", appToken)).StatusCode);

        Assert.Equal(0, await DevicesAsync(device));
    }

    [Fact]
    public async Task Device_registration_is_validated_and_requires_sign_in()
    {
        var http = api.NewAppClient();
        var login = await ApiFixture.AppLoginAsync(http, NewPhone());
        var appToken = login["accessToken"]!.GetValue<string>();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await SendAsync(http, HttpMethod.Post, "/api/me/devices", null, new { token = NewToken("fcm"), platform = "android" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await SendAsync(http, HttpMethod.Post, "/api/me/devices", appToken, new { token = "", platform = "android" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await SendAsync(http, HttpMethod.Post, "/api/me/devices", appToken, new { token = "a b", platform = "ios" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await SendAsync(http, HttpMethod.Post, "/api/me/devices", appToken, new { token = NewToken("web"), platform = "web" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await SendAsync(http, HttpMethod.Post, "/api/me/devices", appToken, new { token = new string('x', 600), platform = "android" })).StatusCode);

        // Тіл берілмесе — сұраныстың тілі (Accept-Language: ru).
        var device = NewToken("fcm");
        Assert.Equal(HttpStatusCode.NoContent,
            (await SendAsync(http, HttpMethod.Post, "/api/me/devices", appToken, new { token = device, platform = "Android" })).StatusCode);
        Assert.Equal("ru", await api.ScalarAsync<string>(
            $"select \"Language\" as \"Value\" from \"PushDevices\" where \"Token\" = '{device}'"));
    }
}
