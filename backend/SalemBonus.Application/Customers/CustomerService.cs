using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Customers.Dtos;

namespace SalemBonus.Application.Customers;

public class CustomerService(
    ICustomerRepository customers,
    IBonusCardRepository cards,
    ICurrentUser currentUser) : ICustomerService
{
    public async Task<CustomerDto?> GetMeAsync(CancellationToken ct = default)
    {
        var customer = await customers.GetByIdAsync(currentUser.CustomerId, ct);
        if (customer is null) return null;

        var myCards = await cards.GetByCustomerAsync(customer.Id, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var firstName = customer.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? customer.FullName;

        return new CustomerDto(
            customer.Id,
            customer.Phone,
            customer.FullName,
            firstName,
            customer.Email,
            customer.BirthDate,
            customer.BirthDate is { } b && b.Month == today.Month && b.Day == today.Day,
            myCards.Sum(c => c.Balance),
            myCards.Count);
    }
}
