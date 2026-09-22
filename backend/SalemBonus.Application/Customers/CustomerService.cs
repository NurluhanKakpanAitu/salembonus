using SalemBonus.Application.BonusCards;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Customers.Dtos;
using SalemBonus.Domain.Enums;

namespace SalemBonus.Application.Customers;

public class CustomerService(
    ICustomerRepository customers,
    IBonusCardRepository cards,
    ICurrentUser currentUser) : ICustomerService
{
    public const string QrPrefix = "SB:";

    public async Task<CustomerDto?> GetMeAsync(CancellationToken ct = default)
    {
        var customer = await customers.GetByIdAsync(currentUser.CustomerId, ct);
        if (customer is null) return null;

        var myCards = await cards.GetByCustomerAsync(customer.Id, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var firstName = customer.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? customer.FullName;
        var topLevel = myCards.Count == 0 ? CustomerLevel.New : myCards.Max(c => c.Level);

        return new CustomerDto(
            customer.Id,
            customer.Phone,
            customer.FullName,
            firstName,
            customer.Email,
            customer.BirthDate,
            customer.BirthDate is { } b && b.Month == today.Month && b.Day == today.Day,
            CustomerLevels.Name(topLevel),
            myCards.Sum(c => c.Balance),
            myCards.Count);
    }

    public async Task<QrCodeDto> GetMyQrAsync(CancellationToken ct = default)
    {
        var customer = await customers.GetByIdAsync(currentUser.CustomerId, ct)
            ?? throw new NotFoundException("Тұтынушы табылмады");
        return new QrCodeDto(customer.QrCode, QrPrefix + customer.QrCode);
    }
}
