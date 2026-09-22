using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Customers.Dtos;

namespace SalemBonus.Application.Customers;

public class CustomerService(
    ICustomerRepository customers,
    IBonusCardRepository cards,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : ICustomerService
{
    public const string QrPrefix = "SB:";

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

    public async Task<CustomerDto> UpdateMeAsync(UpdateProfileRequest request, CancellationToken ct = default)
    {
        var name = (request.FullName ?? string.Empty).Trim();
        if (name.Length < 2) throw new ValidationException("Атыңызды енгізіңіз");
        if (name.Length > 200) throw new ValidationException("Аты тым ұзын");
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        if (email is not null && (!email.Contains('@') || email.Length > 200))
            throw new ValidationException("Email дұрыс емес");
        if (request.BirthDate is { } b && (b > DateOnly.FromDateTime(DateTime.UtcNow) || b.Year < 1900))
            throw new ValidationException("Туған күн дұрыс емес");

        var customer = await customers.GetForUpdateAsync(currentUser.CustomerId, ct)
            ?? throw new NotFoundException("Тұтынушы табылмады");
        customer.FullName = name;
        customer.Email = email;
        customer.BirthDate = request.BirthDate;
        await unitOfWork.SaveChangesAsync(ct);

        return (await GetMeAsync(ct))!;
    }

    public async Task<QrCodeDto> GetMyQrAsync(CancellationToken ct = default)
    {
        var customer = await customers.GetByIdAsync(currentUser.CustomerId, ct)
            ?? throw new NotFoundException("Тұтынушы табылмады");
        return new QrCodeDto(customer.QrCode, QrPrefix + customer.QrCode);
    }
}
