using Microsoft.Extensions.Logging;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;

namespace SalemBonus.Infrastructure.WhatsApp;

/// <summary>
/// WhatsApp Cloud API қосылғанша хабарламаны логқа жазады. Нақты жіберуші Meta-ның Authentication
/// шаблонын қолданады (мәтіні Meta-да бекітілген, біз тек кодты береміз).
/// </summary>
public class LogWhatsAppSender(ILogger<LogWhatsAppSender> logger) : IWhatsAppSender
{
    public Task SendOtpAsync(string phone, string code, string language, CancellationToken ct = default)
    {
        logger.LogWarning("[WhatsApp -> {Phone}] {Text}", phone, Messages.StaffOtpText(language, code));
        return Task.CompletedTask;
    }
}
