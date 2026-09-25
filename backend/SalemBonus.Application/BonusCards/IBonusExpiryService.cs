namespace SalemBonus.Application.BonusCards;

public interface IBonusExpiryService
{
    /// <summary>Мерзімі өткен бонустарды өшіреді. Өшірілген партиялар санын қайтарады.</summary>
    Task<int> BurnExpiredAsync(CancellationToken ct = default);
}
