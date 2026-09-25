using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Notifications;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Enums;

namespace SalemBonus.Application.BonusCards;

/// <summary>
/// Мерзімі өткен бонустарды өшіреді: әр партия өз күнінде жанады,
/// картаның балансынан сол қалдық шегеріледі және клиентке хабарлама жазылады.
/// </summary>
public class BonusExpiryService(
    IBonusTransactionRepository transactions,
    IBonusCardRepository cards,
    INotificationRepository notifications,
    IUnitOfWork unitOfWork) : IBonusExpiryService
{
    private const int BatchSize = 200;

    public async Task<int> BurnExpiredAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var lots = await transactions.GetExpiredLotsAsync(now, BatchSize, ct);
        if (lots.Count == 0) return 0;

        foreach (var lot in lots)
        {
            var card = await cards.GetByIdForUpdateAsync(lot.BonusCardId, ct);
            if (card is null) continue;

            // Балансы бұрын басқа жолмен азайған болса, одан артық шегермейміз.
            var burn = Math.Min(lot.Remaining, card.Balance);
            lot.Remaining = 0;
            if (burn <= 0) continue;

            card.Balance -= burn;
            transactions.Add(new BonusTransaction
            {
                Id = Guid.NewGuid(),
                BonusCardId = card.Id,
                Type = BonusTransactionType.Expiration,
                Amount = -burn,
                CreatedAt = now,
            });

            notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                CustomerId = card.CustomerId,
                StoreId = card.StoreId,
                Type = NotificationType.System,
                Title = "Бонус мерзімі өтті",
                Body = $"{card.Store?.Name} — {burn} Б бонустың мерзімі аяқталды.",
                TemplateKey = NotificationTemplates.BonusExpired,
                Amount = burn,
                CreatedAt = now,
            });
        }

        await unitOfWork.SaveChangesAsync(ct);
        return lots.Count;
    }
}
