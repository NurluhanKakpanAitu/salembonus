using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Notifications;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Enums;

namespace SalemBonus.Application.BonusCards;

/// <summary>
/// Мерзімі өткен бонустарды өшіреді (ТЗ «Бонусы» §4.3): әр партия өз күнінде жанады, жану операциясы партияға сілтейді
/// (бірегей индекс — қайта іске қосу екінші жану жасамайды), картаның балансы мен партия қалдығы бір транзакцияда азаяды.
/// Жану өшірілген дүкенде жалпы партиялар жанбайды.
/// </summary>
public class BonusExpiryService(
    IBonusTransactionRepository transactions,
    IBonusCardRepository cards,
    IBonusRepository bonus,
    INotificationRepository notifications,
    IUnitOfWork unitOfWork) : IBonusExpiryService
{
    private const int BatchSize = 200;

    public async Task<int> BurnExpiredAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var off = (await bonus.AllSettingsAsync(ct)).Where(s => !s.ExpiryEnabled).Select(s => s.StoreId).ToList();
        return await unitOfWork.InTransactionAsync(async () =>
        {
            var lots = await transactions.GetExpiredLotsAsync(now, BatchSize, off, ct);
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
                    BalanceAfter = card.Balance,
                    Source = "expiry",
                    SourceTransactionId = lot.Id,
                    Comment = lot.ExpiresAt is { } at ? $"Срок действия до {at:dd.MM.yyyy HH:mm} UTC" : null,
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
        }, ct);
    }
}
