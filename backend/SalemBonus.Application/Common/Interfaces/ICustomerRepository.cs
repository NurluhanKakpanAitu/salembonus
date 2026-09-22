using SalemBonus.Domain.Entities;

namespace SalemBonus.Application.Common.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Customer?> GetByPhoneAsync(string phone, CancellationToken ct = default);
    Task<Customer?> GetByQrCodeAsync(string qrCode, CancellationToken ct = default);
    void Add(Customer customer);
}
