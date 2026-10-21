using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using SalemBonus.Application.Common.Interfaces;

namespace SalemBonus.Infrastructure.Push;

/// <summary>
/// Apple Push Notification service: HTTP/2 және токен негізіндегі аутентификация (.p8 кілт, ES256 JWT).
/// Capacitor-дың push плагині iOS-та APNs құрылғы токенін береді — сол токенге тікелей жібереміз.
/// </summary>
internal sealed class ApnsClient(ApnsOptions options, HttpClient http)
{
    // Apple: провайдер токені 20 минуттан жиі жаңартылмасын және 60 минуттан ескі болмасын.
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(50);

    private readonly Lock _lock = new();
    private ECDsa? _key;
    private string? _jwt;
    private DateTime _jwtIssuedAt;

    private string Host => options.Sandbox ? "https://api.sandbox.push.apple.com" : "https://api.push.apple.com";

    public async Task<PushResult> SendAsync(string token, PushMessage message, CancellationToken ct)
    {
        var aps = new JsonObject
        {
            ["alert"] = new JsonObject { ["title"] = message.Title, ["body"] = message.Body },
            ["sound"] = "default",
        };
        if (message.Badge is { } badge) aps["badge"] = badge;
        var payload = new JsonObject { ["aps"] = aps };
        // Қосымшаның деректері aps-тан тыс, жоғарғы деңгейде (Capacitor оларды notification.data-ға береді).
        foreach (var (key, value) in message.Data) payload[key] = value;

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{Host}/3/device/{token}")
        {
            Version = HttpVersion.Version20,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("bearer", ProviderToken());
        req.Headers.Add("apns-topic", options.BundleId);
        req.Headers.Add("apns-push-type", "alert");
        req.Headers.Add("apns-priority", "10");

        using var res = await http.SendAsync(req, ct);
        if (res.IsSuccessStatusCode) return PushResult.Sent;

        var error = await res.Content.ReadAsStringAsync(ct);
        if (error.Contains("ExpiredProviderToken", StringComparison.Ordinal)) _jwt = null;
        return IsDeadToken(res.StatusCode, error)
            ? PushResult.InvalidToken
            : throw new HttpRequestException($"APNs {(int)res.StatusCode}: {error}");
    }

    /// <summary>
    /// 410 Unregistered — қосымша өшірілген; BadDeviceToken — токен бұзық не басқа ортанікі (sandbox/production);
    /// DeviceTokenNotForTopic — басқа қосымшаның токені.
    /// </summary>
    private static bool IsDeadToken(HttpStatusCode status, string error) =>
        status == HttpStatusCode.Gone
        || error.Contains("BadDeviceToken", StringComparison.Ordinal)
        || error.Contains("DeviceTokenNotForTopic", StringComparison.Ordinal);

    private string ProviderToken()
    {
        lock (_lock)
        {
            if (_jwt is { } cached && DateTime.UtcNow - _jwtIssuedAt < TokenLifetime) return cached;
            if (_key is null)
            {
                var pem = options.PrivateKey.Length > 0 ? options.PrivateKey : File.ReadAllText(options.PrivateKeyFile);
                var key = ECDsa.Create();
                key.ImportFromPem(pem);
                _key = key;
            }
            _jwtIssuedAt = DateTime.UtcNow;
            // ECDsa.SignData әдепкіде IEEE P1363 (r||s) қолтаңбасын береді — JWT-ге дәл сол керек.
            var signer = _key;
            return _jwt = Jwt.Create(
                new { alg = "ES256", kid = options.KeyId },
                new { iss = options.TeamId, iat = new DateTimeOffset(_jwtIssuedAt).ToUnixTimeSeconds() },
                bytes => signer.SignData(bytes, HashAlgorithmName.SHA256));
        }
    }
}
