using SalemBonus.Application.Pos.Dtos;

namespace SalemBonus.Application.Pos;

public interface IPosService
{
    /// <summary>API кілті бойынша дүкенді анықтайды. Кілт жарамсыз болса UnauthorizedException.</summary>
    Task<PosStoreDto> AuthenticateAsync(string? apiKey, CancellationToken ct = default);

    /// <summary>Тұтынушыны QR коды ("SB:XXXX" немесе "XXXX") немесе телефон нөмірі бойынша табады.</summary>
    Task<PosCustomerDto> LookupCustomerAsync(Guid storeId, string code, CancellationToken ct = default);

    /// <summary>
    /// Сатып алуды тіркейді: керек болса бонус шегереді, қалған сомаға бонус есептейді,
    /// деңгейді қайта есептейді, транзакция мен хабарлама жазады.
    /// Тұтынушы телефонмен келсе және әлі тіркелмесе, автоматты тіркеледі.
    /// </summary>
    Task<PurchaseResultDto> RegisterPurchaseAsync(Guid storeId, PurchaseRequest request, CancellationToken ct = default);
}
