using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Customers.Dtos;
using SalemBonus.Application.Kato;
using SalemBonus.Domain.Entities;

namespace SalemBonus.Application.Customers;

public class CustomerService(
    ICustomerRepository customers,
    IBonusCardRepository cards,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    ICurrentLanguage language,
    IKatoService kato) : ICustomerService
{
    private AppLanguage Lang => language.Value;

    public const string QrPrefix = "SB:";

    public async Task<CustomerDto?> GetMeAsync(CancellationToken ct = default)
    {
        var customer = await customers.GetByIdAsync(currentUser.CustomerId, ct);
        if (customer is null) return null;

        var myCards = await cards.GetByCustomerAsync(customer.Id, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return new CustomerDto(
            customer.Id,
            customer.Phone,
            customer.FullName,
            customer.FirstName,
            customer.LastName,
            customer.Email,
            customer.BirthDate,
            customer.AvatarUrl,
            customer.KatoCode,
            await kato.GetPathAsync(customer.KatoCode, ct),
            customer.BirthDate is { } b && b.Month == today.Month && b.Day == today.Day,
            myCards.Sum(c => c.Balance),
            myCards.Count);
    }

    public async Task<CustomerDto> UpdateMeAsync(UpdateProfileRequest request, CancellationToken ct = default)
    {
        var firstName = (request.FirstName ?? string.Empty).Trim();
        var lastName = (request.LastName ?? string.Empty).Trim();
        if (firstName.Length < 2) throw new ValidationException(Messages.NameRequired(Lang));
        if (firstName.Length > 100 || lastName.Length > 100) throw new ValidationException(Messages.NameTooLong(Lang));
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        if (email is not null && (!email.Contains('@') || email.Length > 200))
            throw new ValidationException(Messages.EmailInvalid(Lang));
        if (request.BirthDate is { } b && (b > DateOnly.FromDateTime(DateTime.UtcNow) || b.Year < 1900))
            throw new ValidationException(Messages.BirthDateInvalid(Lang));

        var customer = await customers.GetForUpdateAsync(currentUser.CustomerId, ct)
            ?? throw new NotFoundException(Messages.CustomerNotFound(Lang));
        customer.FirstName = firstName;
        customer.LastName = lastName;
        customer.Email = email;
        customer.BirthDate = request.BirthDate;
        customer.KatoCode = await NormalizeKatoAsync(request.KatoCode, ct);
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

    /// <summary>Жарамсыз кодты сақтамаймыз — тізім қосымшаның өзінен таңдалады.</summary>
    private async Task<string?> NormalizeKatoAsync(string? code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var path = await kato.GetPathAsync(code.Trim(), ct);
        if (path.Count == 0) throw new ValidationException(Messages.KatoInvalid(Lang));
        return code.Trim();
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
