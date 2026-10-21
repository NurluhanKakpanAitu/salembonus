using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using SalemBonus.Application.Common.Interfaces;

namespace SalemBonus.Infrastructure.Push;

/// <summary>
/// Firebase Cloud Messaging HTTP v1. Service account кілтімен OAuth2 токен алынады (JWT bearer grant, сағатқа),
/// сосын әр құрылғыға бір messages:send сұранысы.
/// </summary>
internal sealed class FcmClient(FcmOptions options, HttpClient http)
{
    private const string Scope = "https://www.googleapis.com/auth/firebase.messaging";
    /// <summary>Android хабарлама арнасы — қосымша да осы id-мен жасайды (frontend/src/lib/push.ts).</summary>
    public const string ChannelId = "salembonus";

    private readonly SemaphoreSlim _lock = new(1, 1);
    private ServiceAccount? _account;
    private string? _accessToken;
    private DateTime _accessTokenExpiresAt;

    private sealed record ServiceAccount(string ProjectId, string ClientEmail, string PrivateKey, string TokenUri);

    public async Task<PushResult> SendAsync(string token, PushMessage message, CancellationToken ct)
    {
        var account = Account();
        var data = new JsonObject();
        foreach (var (key, value) in message.Data) data[key] = value;
        var body = new JsonObject
        {
            ["message"] = new JsonObject
            {
                ["token"] = token,
                ["notification"] = new JsonObject { ["title"] = message.Title, ["body"] = message.Body },
                ["data"] = data,
                ["android"] = new JsonObject
                {
                    ["priority"] = "high",
                    ["notification"] = new JsonObject
                    {
                        ["channel_id"] = ChannelId,
                        ["sound"] = "default",
                        ["notification_count"] = message.Badge,
                    },
                },
            },
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, $"https://fcm.googleapis.com/v1/projects/{account.ProjectId}/messages:send")
        {
            Content = JsonContent.Create(body),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await AccessTokenAsync(account, ct));
        using var res = await http.SendAsync(req, ct);
        if (res.IsSuccessStatusCode) return PushResult.Sent;

        var error = await res.Content.ReadAsStringAsync(ct);
        if (res.StatusCode == HttpStatusCode.Unauthorized) _accessToken = null; // келесі жолы жаңа токен
        return IsDeadToken(res.StatusCode, error)
            ? PushResult.InvalidToken
            : throw new HttpRequestException($"FCM {(int)res.StatusCode}: {error}");
    }

    /// <summary>
    /// UNREGISTERED (404) — қосымша өшірілген не токен ескірген; SENDER_ID_MISMATCH (403) — токен басқа Firebase жобасынікі;
    /// INVALID_ARGUMENT (400) токен туралы болса — токен бұзық. Қалғаны уақытша не біздің қате: құрылғы қалады.
    /// </summary>
    private static bool IsDeadToken(HttpStatusCode status, string error) =>
        status == HttpStatusCode.NotFound
        || error.Contains("UNREGISTERED", StringComparison.Ordinal)
        || error.Contains("SENDER_ID_MISMATCH", StringComparison.Ordinal)
        || (status == HttpStatusCode.BadRequest && error.Contains("registration token", StringComparison.OrdinalIgnoreCase));

    private ServiceAccount Account()
    {
        if (_account is not null) return _account;
        var json = options.ServiceAccountJson.Length > 0 ? options.ServiceAccountJson : File.ReadAllText(options.ServiceAccountFile);
        var root = JsonNode.Parse(json) ?? throw new InvalidOperationException("Push:Fcm service account JSON бос");
        string Field(string name) => root[name]?.GetValue<string>() is { Length: > 0 } v
            ? v
            : throw new InvalidOperationException($"Push:Fcm service account JSON-да «{name}» жоқ");
        return _account = new ServiceAccount(Field("project_id"), Field("client_email"), Field("private_key"),
            root["token_uri"]?.GetValue<string>() ?? "https://oauth2.googleapis.com/token");
    }

    private async Task<string> AccessTokenAsync(ServiceAccount account, CancellationToken ct)
    {
        if (_accessToken is { } cached && DateTime.UtcNow < _accessTokenExpiresAt) return cached;
        await _lock.WaitAsync(ct);
        try
        {
            if (_accessToken is { } again && DateTime.UtcNow < _accessTokenExpiresAt) return again;

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            using var rsa = RSA.Create();
            rsa.ImportFromPem(account.PrivateKey);
            var assertion = Jwt.Create(
                new { alg = "RS256", typ = "JWT" },
                new { iss = account.ClientEmail, scope = Scope, aud = account.TokenUri, iat = now, exp = now + 3600 },
                bytes => rsa.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

            using var res = await http.PostAsync(account.TokenUri, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = assertion,
            }), ct);
            var text = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode) throw new HttpRequestException($"FCM OAuth {(int)res.StatusCode}: {text}");

            using var doc = JsonDocument.Parse(text);
            var token = doc.RootElement.GetProperty("access_token").GetString()!;
            var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 3600;
            // Мерзімі бітерден 5 минут бұрын жаңартамыз.
            _accessTokenExpiresAt = DateTime.UtcNow.AddSeconds(Math.Max(60, expiresIn - 300));
            return _accessToken = token;
        }
        finally
        {
            _lock.Release();
        }
    }
}
