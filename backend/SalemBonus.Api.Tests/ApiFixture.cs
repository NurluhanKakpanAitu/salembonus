using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SalemBonus.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SalemBonus.Api.Tests;

/// <summary>
/// Шын API + шын PostgreSQL 18 (Testcontainers, прод пен Neon-дағы нұсқа). Орта — «Testing»: user-secrets
/// жүктелмейді, сондықтан тест ешқашан Neon-ға қосылмайды. Демо дүкен, қызметкерлер мен каталогты
/// осы фикстура өзі толтырады (олар әдетте тек Development-те жасалады).
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder().WithImage("postgres:18-alpine").Build();
    private WebApplicationFactory<Program> _factory = null!;
    private (string Owner, string Cashier)? _tokens;

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        // Program конфигурацияны тіркеу кезінде бірден оқиды — айнымалылар хост құрылмай тұрып қойылады.
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _db.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__Key", "integration-tests-signing-key-0123456789-abcdefghij");

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Testing"));
        using var scope = _factory.Services.CreateScope(); // хост көтеріледі: миграциялар + дүкендер
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await StaffSeeder.SeedAsync(db);
        await CatalogDemoSeeder.SeedAsync(db);
        await CashierDemoSeeder.SeedAsync(db);
    }

    /// <summary>
    /// Жаңа «құрылғы»: өз cookie-лері бар клиент. Касса cookie-і Secure (Development емес), сондықтан https.
    /// </summary>
    public async Task<Pos> NewDeviceAsync(bool activateRegister = true)
    {
        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });
        // Кіруге IP бойынша минутына 10 рет шек бар — токендер бір рет алынып, барлық «құрылғыда» қолданылады.
        _tokens ??= (await LoginAsync(http, StaffSeeder.OwnerPhone, StaffSeeder.OwnerPassword),
                     await LoginAsync(http, StaffSeeder.CashierPhone, StaffSeeder.CashierPassword));
        var pos = new Pos(http) { Owner = _tokens.Value.Owner, Cashier = _tokens.Value.Cashier };
        if (activateRegister)
        {
            var registers = await pos.Get(pos.Owner, "/api/pos/v1/registers");
            var id = registers.Body!.AsArray()[0]!["id"]!.GetValue<Guid>();
            (await pos.Post(pos.Owner, $"/api/pos/v1/registers/{id}/activate", new { })).EnsureOk();
        }
        return pos;
    }

    private static async Task<string> LoginAsync(HttpClient http, string phone, string password)
    {
        var res = await http.PostAsJsonAsync("/api/staff/v1/auth/login", new { phone, password });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<JsonObject>())!["accessToken"]!.GetValue<string>();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _db.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}

public record ApiResult(HttpStatusCode Status, JsonNode? Body)
{
    public JsonNode Ok => Status == HttpStatusCode.OK ? Body! : throw new Xunit.Sdk.XunitException($"Күтілгені 200, келгені {(int)Status}: {Body?.ToJsonString()}");
    public ApiResult EnsureOk() { _ = Ok; return this; }
    public decimal Dec(string path) => Node(path)!.GetValue<decimal>();
    public JsonNode? Node(string path) => path.Split('.').Aggregate(Ok, (n, p) => n?[p]!);
}

/// <summary>Бір құрылғыдағы касса: иесі мен кассир бір cookie-ді бөліседі, токендері бөлек.</summary>
public sealed class Pos(HttpClient http)
{
    public required string Owner { get; init; }
    public required string Cashier { get; init; }

    public Task<ApiResult> Get(string token, string path) => Send(HttpMethod.Get, token, path, null);
    public Task<ApiResult> Post(string token, string path, object body) => Send(HttpMethod.Post, token, path, body);

    public async Task<ApiResult> Send(HttpMethod method, string token, string path, object? body, string? storeId = null)
    {
        using var req = new HttpRequestMessage(method, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("ru"));
        if (storeId is not null) req.Headers.Add("X-Store-Id", storeId);
        if (body is not null) req.Content = JsonContent.Create(body);
        using var res = await http.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        return new ApiResult(res.StatusCode, string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text));
    }

    // ---------- Жиі қадамдар ----------

    /// <summary>Жеке тауар: басқа тесттердің қалдығына тәуелді болмау үшін әр тест өзінікін жасайды.</summary>
    private readonly Dictionary<Guid, string> _names = [];

    public async Task<Guid> CreateProductAsync(decimal price, decimal stock, decimal purchase = 0)
    {
        var units = (await Get(Owner, "/api/pos/v1/catalog/units")).Ok.AsArray();
        var name = "Тест " + Guid.NewGuid().ToString("N")[..8];
        var res = await Post(Owner, "/api/pos/v1/products", new
        {
            name,
            unitId = units[0]!["id"]!.GetValue<Guid>(),
            opening = new { quantity = stock, purchasePrice = purchase, salePrice = price },
        });
        var id = res.Ok["id"]!.GetValue<Guid>();
        _names[id] = name;
        return id;
    }

    public async Task<decimal> StockAsync(Guid productId)
    {
        var res = await Get(Owner, "/api/pos/v1/cashier/catalog?search=" + Uri.EscapeDataString(_names[productId]));
        var item = res.Ok["items"]!.AsArray().First(i => i!["id"]!.GetValue<Guid>() == productId);
        return item!["stock"]!.GetValue<decimal>();
    }

    public async Task<JsonNode> CreateCustomerAsync()
    {
        var phone = "+7707" + Random.Shared.Next(1_000_000, 9_999_999);
        return (await Post(Owner, "/api/pos/v1/cashier/customers",
            new { phone, firstName = "Тест", lastName = "Клиент", birthDate = "1990-05-17" })).Ok;
    }

    public Task<ApiResult> SellAsync(string token, object[] items, object[] payments, Guid? customerId = null,
        int bonusRedeem = 0, object? discount = null, object? approval = null, Guid? requestId = null) =>
        Post(token, "/api/pos/v1/sales", new
        {
            clientRequestId = requestId ?? Guid.NewGuid(),
            items,
            customerId,
            bonusRedeem,
            discount,
            approval,
            payments,
        });

    public static object Line(Guid productId, decimal quantity) => new { productId, quantity };
    public static object Cash(decimal amount, decimal? received = null) => new { method = "Cash", amount, received = received ?? amount };
    public static object Card(decimal amount) => new { method = "Card", amount };
}
