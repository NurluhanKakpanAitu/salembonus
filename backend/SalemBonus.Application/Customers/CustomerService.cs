using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Customers.Dtos;
using SalemBonus.Domain.Entities;

namespace SalemBonus.Application.Customers;

public class CustomerService(
    ICustomerRepository customers,
    IBonusCardRepository cards,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    ICurrentLanguage language) : ICustomerService
{
    private AppLanguage Lang => language.Value;

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
            customer.AvatarUrl,
            customer.BirthDate is { } b && b.Month == today.Month && b.Day == today.Day,
            myCards.Sum(c => c.Balance),
            myCards.Count);
    }

    public async Task<CustomerDto> UpdateMeAsync(UpdateProfileRequest request, CancellationToken ct = default)
    {
        var name = (request.FullName ?? string.Empty).Trim();
        if (name.Length < 2) throw new ValidationException(Messages.NameRequired(Lang));
        if (name.Length > 200) throw new ValidationException(Messages.NameTooLong(Lang));
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        if (email is not null && (!email.Contains('@') || email.Length > 200))
            throw new ValidationException(Messages.EmailInvalid(Lang));
        if (request.BirthDate is { } b && (b > DateOnly.FromDateTime(DateTime.UtcNow) || b.Year < 1900))
            throw new ValidationException(Messages.BirthDateInvalid(Lang));

        var customer = await customers.GetForUpdateAsync(currentUser.CustomerId, ct)
            ?? throw new NotFoundException(Messages.CustomerNotFound(Lang));
        customer.FullName = name;
        customer.Email = email;
        customer.BirthDate = request.BirthDate;
        await unitOfWork.SaveChangesAsync(ct);

        return (await GetMeAsync(ct))!;
    }

    public async Task<CustomerDto> UpdateAvatarAsync(UpdateAvatarRequest request, CancellationToken ct = default)
    {
        var customer = await customers.GetForUpdateAsync(currentUser.CustomerId, ct)
            ?? throw new NotFoundException(Messages.CustomerNotFound(Lang));
        customer.AvatarUrl = NormalizeAvatar(request.AvatarUrl);
        await unitOfWork.SaveChangesAsync(ct);
        return (await GetMeAsync(ct))!;
    }

    /// <summary>Бос мән фотоны өшіреді.</summary>
    private string? NormalizeAvatar(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var url = value.Trim();
        if (!CustomerAvatar.IsAllowedFormat(url))
            throw new ValidationException(Messages.AvatarFormat(Lang));
        if (url.Length > CustomerAvatar.MaxLength)
            throw new ValidationException(Messages.AvatarTooLarge(Lang));
        return url;
    }

    public async Task<QrCodeDto> GetMyQrAsync(CancellationToken ct = default)
    {
        var customer = await customers.GetByIdAsync(currentUser.CustomerId, ct)
            ?? throw new NotFoundException(Messages.CustomerNotFound(Lang));
        return new QrCodeDto(customer.QrCode, QrPrefix + customer.QrCode);
    }
}
