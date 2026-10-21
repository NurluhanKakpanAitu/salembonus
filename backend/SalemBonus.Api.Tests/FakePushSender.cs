using System.Collections.Concurrent;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Enums;

namespace SalemBonus.Api.Tests;

/// <summary>
/// Тесттегі FCM/APNs: желіге шықпайды, жіберілгенін жазып алады. Бір данасы бүкіл хостқа ортақ (singleton),
/// сондықтан әр тест өз токенін жасайды және тек соны сүзеді.
/// </summary>
public sealed class FakePushSender : IPushSender
{
    public sealed record Sent(PushPlatform Platform, string Token, PushMessage Message);

    private readonly ConcurrentQueue<Sent> _sent = new();

    /// <summary>false — кілттер жоқ сияқты: ештеңе жіберілмеуі керек.</summary>
    public bool Configured { get; set; } = true;
    /// <summary>Бұл токендерге FCM/APNs «токен жарамсыз» деп жауап береді.</summary>
    public ConcurrentDictionary<string, bool> DeadTokens { get; } = new();

    public bool IsConfigured(PushPlatform platform) => Configured;

    public Task<PushResult> SendAsync(PushPlatform platform, string token, PushMessage message, CancellationToken ct = default)
    {
        _sent.Enqueue(new Sent(platform, token, message));
        return Task.FromResult(DeadTokens.ContainsKey(token) ? PushResult.InvalidToken : PushResult.Sent);
    }

    public IReadOnlyList<Sent> To(string token) => _sent.Where(s => s.Token == token).ToList();

    /// <summary>Push фонда жіберіледі — шарт орындалғанша күтеміз (ең көбі 10 с).</summary>
    public async Task<Sent> WaitAsync(string token, Func<PushMessage, bool> match)
    {
        for (var deadline = DateTime.UtcNow.AddSeconds(10); DateTime.UtcNow < deadline; await Task.Delay(50))
            if (To(token).FirstOrDefault(s => match(s.Message)) is { } found) return found;
        throw new Xunit.Sdk.XunitException(
            $"Push келмеді ({token}). Келгендері: {string.Join(" | ", To(token).Select(s => s.Message.Title + ": " + s.Message.Body))}");
    }
}
