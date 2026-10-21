using SalemBonus.Domain.Enums;

namespace SalemBonus.Application.Common.Interfaces;

/// <summary>Бір құрылғыға кететін push: тақырыбы, мәтіні, белгі саны (iOS badge) және қосымшаға арналған деректер.</summary>
public record PushMessage(string Title, string Body, int? Badge, IReadOnlyDictionary<string, string> Data);

public enum PushResult
{
    Sent,
    /// <summary>Токен енді жарамсыз (қосымша өшірілген, токен ауысқан) — құрылғыны базадан алып тастау керек.</summary>
    InvalidToken,
    /// <summary>Уақытша қате (желі, лимит): құрылғы қалады.</summary>
    Failed,
}

/// <summary>
/// Push жіберуші: Android — FCM HTTP v1, iOS — APNs. Кілттер конфигурацияда жоқ болса, сол платформаға
/// ештеңе жіберілмейді. Тесттерде жалған іске асыру қойылады — желіге шықпайды.
/// </summary>
public interface IPushSender
{
    bool IsConfigured(PushPlatform platform);
    Task<PushResult> SendAsync(PushPlatform platform, string token, PushMessage message, CancellationToken ct = default);
}
