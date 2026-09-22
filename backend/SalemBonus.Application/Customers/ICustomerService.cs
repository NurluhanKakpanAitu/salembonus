using SalemBonus.Application.Customers.Dtos;

namespace SalemBonus.Application.Customers;

public interface ICustomerService
{
    Task<CustomerDto?> GetMeAsync(CancellationToken ct = default);
}
