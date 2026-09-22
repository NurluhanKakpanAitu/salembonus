using Microsoft.Extensions.Logging;
using SalemBonus.Application.Common.Interfaces;

namespace SalemBonus.Infrastructure.Sms;

/// <summary>SMS провайдері қосылғанша хабарламаны логқа жазады. Sms:Provider = "Log".</summary>
public class LogSmsSender(ILogger<LogSmsSender> logger) : ISmsSender
{
    public Task SendAsync(string phone, string text, CancellationToken ct = default)
    {
        logger.LogWarning("[SMS -> {Phone}] {Text}", phone, text);
        return Task.CompletedTask;
    }
}
