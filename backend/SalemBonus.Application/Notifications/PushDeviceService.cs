using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Notifications.Dtos;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Enums;

namespace SalemBonus.Application.Notifications;

public interface IPushDeviceService
{
    /// <summary>Кірген соң және токен жаңарғанда: құрылғыны ағымдағы клиентке байлайды (бар болса жаңартады).</summary>
    Task RegisterAsync(RegisterDeviceRequest request, CancellationToken ct = default);
    /// <summary>Шыққанда: бұл құрылғыға енді push келмейді.</summary>
    Task UnregisterAsync(string? token, CancellationToken ct = default);
}

public class PushDeviceService(
    IPushDeviceRepository devices,
    IUnitOfWork uow,
    ICurrentUser currentUser,
    ICurrentLanguage language) : IPushDeviceService
{
    // FCM токені ~160 таңба, APNs — 64 hex. Шек қор қалдырып қойылған.
    private const int MaxTokenLength = 512;

    private AppLanguage Lang => language.Value;

    public async Task RegisterAsync(RegisterDeviceRequest request, CancellationToken ct = default)
    {
        var token = NormalizeToken(request.Token);
        var platform = request.Platform?.Trim().ToLowerInvariant() switch
        {
            "android" => PushPlatform.Android,
            "ios" => PushPlatform.Ios,
            _ => throw new ValidationException(Messages.PushPlatformInvalid(Lang), "platform"),
        };
        var lang = request.Language?.Trim().ToLowerInvariant() switch
        {
            "ru" => "ru",
            "kk" => "kk",
            _ => Lang == AppLanguage.Ru ? "ru" : "kk",
        };

        var now = DateTime.UtcNow;
        var device = await devices.GetByTokenAsync(token, ct);
        if (device is null)
        {
            devices.Add(new PushDevice
            {
                Id = Guid.NewGuid(),
                CustomerId = currentUser.CustomerId,
                Platform = platform,
                Token = token,
                Language = lang,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
        else
        {
            // Сол телефоннан басқа аккаунт кірді — бұрынғы иесіне бұл құрылғыға push кетпеуі керек.
            device.CustomerId = currentUser.CustomerId;
            device.Platform = platform;
            device.Language = lang;
            device.UpdatedAt = now;
        }
        await uow.SaveChangesAsync(ct);
    }

    public async Task UnregisterAsync(string? token, CancellationToken ct = default) =>
        await devices.RemoveAsync(currentUser.CustomerId, NormalizeToken(token), ct);

    private string NormalizeToken(string? token)
    {
        var t = token?.Trim() ?? string.Empty;
        if (t.Length is 0 or > MaxTokenLength || t.Any(char.IsWhiteSpace))
            throw new ValidationException(Messages.PushTokenInvalid(Lang), "token");
        return t;
    }
}
