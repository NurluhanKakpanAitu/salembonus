using SalemBonus.Domain.Core;
using SalemBonus.Domain.Pos;

namespace SalemBonus.Application.Common.Interfaces;

public interface IRegisterRepository
{
    Task<IReadOnlyList<Register>> ListByStoreAsync(Guid storeId, CancellationToken ct = default);
    Task<Register?> GetForUpdateAsync(Guid storeId, Guid registerId, CancellationToken ct = default);
    /// <summary>Құрылғы кілтінің хеші бойынша (дүкенімен бірге). forUpdate — өзгерту үшін.</summary>
    Task<Register?> GetByDeviceHashAsync(string deviceTokenHash, bool forUpdate = false, CancellationToken ct = default);
    /// <summary>Дүкенде сата алатын белсенді қызметкерлер — кассадағы кассирлер тізімі үшін.</summary>
    Task<IReadOnlyList<StoreMembership>> ListSellersAsync(Guid storeId, CancellationToken ct = default);
}
