namespace SalemBonus.Application.Common.Interfaces;

/// <summary>
/// WhatsApp арқылы хабарлама. Кодты жіберу Meta-ның Authentication шаблоны арқылы жүреді —
/// шаблон бекітілгенше разработкада логқа жазылады. Жіберілмесе — <see cref="MessageDeliveryException"/>.
/// </summary>
public interface IWhatsAppSender
{
    /// <param name="language">"ru" немесе "kk" — шаблон сол тілде болса, сол тілде жіберіледі.</param>
    Task SendOtpAsync(string phone, string code, string language, CancellationToken ct = default);
}

public sealed class MessageDeliveryException(string message, Exception? inner = null) : Exception(message, inner);
