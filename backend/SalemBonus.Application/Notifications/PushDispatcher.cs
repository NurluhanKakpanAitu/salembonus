using Microsoft.Extensions.Logging;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Enums;

namespace SalemBonus.Application.Notifications;

/// <summary>
/// Сақталған хабарламаны клиенттің барлық құрылғысына push етіп жібереді. Хабарлама базадан қайта оқылады:
/// транзакция кері қайтарылса, жоқ хабарлама жіберілмейді. Жарамсыз болған токендер өшіріледі.
/// </summary>
public class PushDispatcher(
    INotificationRepository notifications,
    IPushDeviceRepository devices,
    IPushSender sender,
    ILogger<PushDispatcher> log)
{
    /// <summary>
    /// Клиенттің өз әрекетінің жаңғырығы push етілмейді: профильді өзі өзгертті, дүкенді өзі қосты (не кассада
    /// қасында тұрып қосылды). Бонус, туған күн, акция және жүйелік ескертулер (деңгей, жану) — жіберіледі.
    /// </summary>
    public static bool ShouldPush(NotificationType type) =>
        type is not (NotificationType.ProfileUpdated or NotificationType.StoreAdded);

    public async Task SendAsync(Guid notificationId, CancellationToken ct = default)
    {
        var n = await notifications.GetWithStoreAsync(notificationId, ct);
        if (n is null || !ShouldPush(n.Type)) return;

        var targets = (await devices.GetByCustomerAsync(n.CustomerId, ct))
            .Where(d => sender.IsConfigured(d.Platform))
            .ToList();
        if (targets.Count == 0) return;

        var unread = await notifications.CountUnreadAsync(n.CustomerId, ct);
        // Қосымша басқанда сол дүкеннің хабарламаларын ашады (NotificationStorePage: /notifications/{storeId|system}).
        var data = new Dictionary<string, string>
        {
            ["notificationId"] = n.Id.ToString(),
            ["type"] = n.Type.ToString(),
            ["storeId"] = n.StoreId?.ToString() ?? "",
            ["path"] = $"/notifications/{n.StoreId?.ToString() ?? "system"}",
        };

        var invalid = new List<Guid>();
        foreach (var device in targets)
        {
            var (title, body) = PushText.Render(n, device.Language);
            PushResult result;
            try
            {
                result = await sender.SendAsync(device.Platform, device.Token, new PushMessage(title, body, unread, data), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Push жіберілмеді ({Platform}, хабарлама {Id})", device.Platform, n.Id);
                result = PushResult.Failed;
            }
            if (result == PushResult.InvalidToken) invalid.Add(device.Id);
        }

        if (invalid.Count > 0) await devices.RemoveByIdsAsync(invalid, ct);
    }
}
