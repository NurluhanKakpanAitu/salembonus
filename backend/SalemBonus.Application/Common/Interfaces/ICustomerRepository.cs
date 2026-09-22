using SalemBonus.Domain.Entities;

namespace SalemBonus.Application.Common.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
