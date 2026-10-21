using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Infrastructure.Identity;
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
    /// <summary>
    /// Демо дүкеннің «қазіргі» уақыты — сервер сияқты Asia/Almaty белдеуі ОЖ деректерінен (Windows-та ескі деректер +6 береді):
    /// тесттің «бүгіні» сервердің «бүгінімен» әрқашан бірдей болады, тәуліктің кез келген сағатында.
    /// </summary>
    public static DateTime StoreNow => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Almaty"));
    public static DateOnly StoreToday => DateOnly.FromDateTime(StoreNow);

    /// <summary>Docker жоқ машинада: SALEMBONUS_TEST_DB=<connection string> — дайын (бос) базаға қосылады, контейнер көтерілмейді.</summary>
    private static readonly string? ExternalDb = Environment.GetEnvironmentVariable("SALEMBONUS_TEST_DB");
    private readonly PostgreSqlContainer? _db = ExternalDb is null ? new PostgreSqlBuilder("postgres:18-alpine").Build() : null;
    private WebApplicationFactory<Program> _factory = null!;
    public FakeInvoiceRecognizer Recognizer { get; } = new();
    private (string Owner, string Cashier)? _tokens;

    public async Task InitializeAsync()
    {
        if (_db is not null) await _db.StartAsync();
        // Program конфигурацияны тіркеу кезінде бірден оқиды — айнымалылар хост құрылмай тұрып қойылады.
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", ExternalDb ?? _db!.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__Key", "integration-tests-signing-key-0123456789-abcdefghij");
        // Тест шығысы SQL логына толмасын: тек ескерту мен қате.
        Environment.SetEnvironmentVariable("Logging__LogLevel__Default", "Warning");

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            // Накладнаяны тану (§8): тесттер Claude-қа бармайды — жалған танушы, тест не қойса соны қайтарады.
            b.ConfigureTestServices(s => { s.RemoveAll<IInvoiceRecognizer>(); s.AddSingleton<IInvoiceRecognizer>(Recognizer); });
        });
        using var scope = _factory.Services.CreateScope(); // хост көтеріледі: миграциялар + дүкендер
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await StaffSeeder.SeedAsync(db);
        await CatalogDemoSeeder.SeedAsync(db);
        await CashierDemoSeeder.SeedAsync(db);
        // Нақты өмірде кассирде құпиясөз жоқ (тек PIN). Тесттерде кассирдің токенін құпиясөзбен алу оңай:
        // PIN-мен кіру үшін әр «құрылғыда» кассаны алдын ала қосу керек болар еді.
        var cashier = await db.StaffUsers.SingleAsync(u => u.Phone == StaffSeeder.CashierPhone);
        cashier.PasswordHash = new Pbkdf2PasswordHasher().Hash(CashierPassword);
        await db.SaveChangesAsync();
    }

    private const string CashierPassword = "Test-Kassir-2026";

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
                     await LoginAsync(http, StaffSeeder.CashierPhone, CashierPassword));
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

    /// <summary>Тестке дерек дайындау: мысалы, сатылымды «өткен айға» жылжыту (API бұған жол бермейді — дұрыс та).</summary>
    public async Task SqlAsync(string sql)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync(sql);
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        if (_db is not null) await _db.DisposeAsync();
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
    public Task<ApiResult> Put(string token, string path, object body) => Send(HttpMethod.Put, token, path, body);
    public Task<ApiResult> Delete(string token, string path) => Send(HttpMethod.Delete, token, path, null);

    /// <summary>Файл жауабы (экспорт): мәртебе, түрі, көлемі.</summary>
    public async Task<(HttpStatusCode Status, string? ContentType, long Length)> Raw(string token, string path)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var res = await http.SendAsync(req);
        var bytes = await res.Content.ReadAsByteArrayAsync();
        return (res.StatusCode, res.Content.Headers.ContentType?.MediaType, bytes.LongLength);
    }

    /// <summary>POST, жауабы — файл (экспорт): күйі, түрі, мазмұны.</summary>
    public async Task<(HttpStatusCode Status, string? ContentType, byte[] Content)> PostRaw(string token, string path, object body)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("ru"));
        req.Content = JsonContent.Create(body);
        using var res = await http.SendAsync(req);
        return (res.StatusCode, res.Content.Headers.ContentType?.MediaType, await res.Content.ReadAsByteArrayAsync());
    }

    /// <summary>Файл жүктеу (multipart): тану эндпоинті үшін.</summary>
    public async Task<ApiResult> PostFile(string token, string path, byte[] content, string contentType, string fileName, params (string Name, string Value)[] fields)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("ru"));
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        foreach (var (name, value) in fields) form.Add(new StringContent(value), name);
        req.Content = form;
        using var res = await http.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        return new ApiResult(res.StatusCode, string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text));
    }

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
    public string NameOf(Guid productId) => _names[productId];

    public async Task<Guid> CreateProductAsync(decimal price, decimal stock, decimal purchase = 0, string? article = null, string? barcode = null)
    {
        var units = (await Get(Owner, "/api/pos/v1/catalog/units")).Ok.AsArray();
        var name = "Тест " + Guid.NewGuid().ToString("N")[..8];
        var res = await Post(Owner, "/api/pos/v1/products", new
        {
            name,
            article,
            barcodes = barcode is null ? null : new[] { new { barcode, isPrimary = true } },
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
