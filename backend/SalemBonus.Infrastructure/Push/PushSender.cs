using Microsoft.Extensions.Options;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Enums;

namespace SalemBonus.Infrastructure.Push;

/// <summary>Платформа бойынша: Android — FCM, iOS — APNs. Кілті жоқ платформаға жіберілмейді.</summary>
public sealed class PushSender : IPushSender, IDisposable
{
    private readonly PushOptions _o;
    private readonly HttpClient _http;
    private readonly FcmClient _fcm;
    private readonly ApnsClient _apns;

    public PushSender(IOptions<PushOptions> options)
    {
        _o = options.Value;
        // Бір ұзақ өмірлі клиент: APNs HTTP/2 қосылымын қайта қолдануды күтеді (әр жіберуге жаңа TLS болмасын).
        _http = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(30) })
        {
            Timeout = TimeSpan.FromSeconds(20),
        };
        _fcm = new FcmClient(_o.Fcm, _http);
        _apns = new ApnsClient(_o.Apns, _http);
    }

    public bool IsConfigured(PushPlatform platform) => platform switch
    {
        PushPlatform.Android => _o.Fcm.IsConfigured,
        PushPlatform.Ios => _o.Apns.IsConfigured,
        _ => false,
    };

    public Task<PushResult> SendAsync(PushPlatform platform, string token, PushMessage message, CancellationToken ct = default)
    {
        if (!IsConfigured(platform)) return Task.FromResult(PushResult.Failed);
        return platform == PushPlatform.Ios ? _apns.SendAsync(token, message, ct) : _fcm.SendAsync(token, message, ct);
    }

    public void Dispose() => _http.Dispose();
}
