using SalemBonus.Application.Customers.Dtos;

namespace SalemBonus.Application.Customers;

public interface ICustomerService
{
    Task<CustomerDto?> GetMeAsync(CancellationToken ct = default);
    Task<QrCodeDto> GetMyQrAsync(CancellationToken ct = default);
    Task<CustomerDto> UpdateMeAsync(UpdateProfileRequest request, CancellationToken ct = default);
    Task<CustomerDto> UpdateAvatarAsync(UpdateAvatarRequest request, CancellationToken ct = default);
}
