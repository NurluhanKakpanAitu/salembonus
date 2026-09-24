using SalemBonus.Application.Stores.Dtos;

namespace SalemBonus.Application.Stores;

public interface IStoreService
{
    /// <summary>Барлық белсенді дүкендер. search берілсе атауы мен санаты бойынша сүзіледі.</summary>
    Task<IReadOnlyList<StoreListItemDto>> GetAllAsync(string? search, CancellationToken ct = default);
    Task<StoreDetailDto?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>QR коды немесе дүкен идентификаторы бойынша карта ашады.</summary>
    Task<JoinStoreResultDto> JoinAsync(string code, CancellationToken ct = default);
}
