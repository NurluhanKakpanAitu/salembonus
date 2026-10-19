using Microsoft.Extensions.Options;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Core.Staff.Dtos;
using SalemBonus.Domain.Core;

namespace SalemBonus.Application.Core.Staff;

/// <summary>
/// PIN қою/ауыстыру және құпиясөзді ауыстыру (ТЗ «Касса» §17.7–17.8). Екеуі де қазіргі құпиямен
/// расталады: ашық қалған кассаны пайдаланып біреу PIN-ді өзгерте алмауы керек. Кассирде құпиясөз жоқ —
/// ол PIN-ді қазіргі PIN-мен ауыстырады, ал құпиясөз ауыстыру оған жабық.
/// </summary>
public class StaffCredentialsService(
    IStaffRepository staff,
    IStaffRefreshTokenRepository refreshTokens,
    IPasswordHasher hasher,
    IStaffAuthService auth,
    IAuditLog audit,
    ICurrentStaff current,
    IUnitOfWork unitOfWork,
    ICurrentLanguage language,
    IOptions<StaffAuthOptions> options) : IStaffCredentialsService
{
    private readonly StaffAuthOptions _opt = options.Value;
    private AppLanguage Lang => language.Value;

    public async Task<StaffMeDto> SetPinAsync(SetPinRequest request, CancellationToken ct = default)
    {
        var user = await LoadVerifiedAsync(request.CurrentPassword, request.CurrentPin, ct);

        var pin = request.Pin ?? string.Empty;
        CredentialRules.ValidatePin(pin, Lang);
        if (pin != request.ConfirmPin)
            throw new ValidationException(Messages.StaffPinMismatch(Lang), "confirmPin");

        var hadPin = user.HasPin;
        user.PinHash = hasher.Hash(pin);
        user.PinFailedCount = 0;
        user.PinLockedUntil = null;
        audit.Record(Entry(user, hadPin ? "auth.pin_changed" : "auth.pin_set"));
        await unitOfWork.SaveChangesAsync(ct);
        return await auth.GetMeAsync(ct);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await LoadVerifiedAsync(request.CurrentPassword, null, ct);

        var password = request.NewPassword ?? string.Empty;
        CredentialRules.ValidatePassword(password, _opt, Lang);
        if (password != request.ConfirmPassword)
            throw new ValidationException(Messages.StaffPasswordsMismatch(Lang), "confirmPassword");

        user.PasswordHash = hasher.Hash(password);
        // Басқа құрылғылардағы сеанстар жабылады; осы құрылғы access токенмен жұмысын жалғастырады,
        // ал келесі жаңартуда қайта кіруге тура келеді.
        await refreshTokens.RevokeAllAsync(user.Id, ct);
        audit.Record(Entry(user, "auth.password_changed"));
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Қызметкерді қазіргі құпиямен растау. currentPin null — тек құпиясөз қабылданады (құпиясөз ауыстыру):
    /// құпиясөзсіз кассирге бұл әрекет жабық.
    /// </summary>
    private async Task<StaffUser> LoadVerifiedAsync(string? currentPassword, string? currentPin, CancellationToken ct)
    {
        var user = await staff.GetByIdForUpdateAsync(current.StaffUserId, ct)
            ?? throw new UnauthorizedException(Messages.SessionExpired(Lang));

        if (user.PasswordHash is null)
        {
            if (currentPin is null)
                throw new ValidationException(Messages.StaffPinOnly(Lang), "currentPassword");
            if (currentPin.Length == 0)
                throw new ValidationException(Messages.StaffCurrentPinRequired(Lang), "currentPin");
            if (user.PinHash is null || !hasher.Verify(currentPin, user.PinHash))
                return await WrongAsync(user, "wrong_current_pin", Messages.StaffCurrentPinWrong(Lang), "currentPin", ct);
            return user;
        }

        if (string.IsNullOrEmpty(currentPassword))
            throw new ValidationException(Messages.StaffPasswordRequired(Lang), "currentPassword");
        if (!hasher.Verify(currentPassword, user.PasswordHash))
            return await WrongAsync(user, "wrong_current_password", Messages.StaffCurrentPasswordWrong(Lang), "currentPassword", ct);
        return user;
    }

    private async Task<StaffUser> WrongAsync(StaffUser user, string reason, string message, string field, CancellationToken ct)
    {
        audit.Record(new AuditEntry
        {
            Id = Guid.NewGuid(), OrganizationId = user.OrganizationId, StaffUserId = user.Id,
            Action = "auth.credentials_change", Success = false, Details = reason, Ip = current.Ip,
        });
        await unitOfWork.SaveChangesAsync(ct);
        throw new ValidationException(message, field);
    }

    private AuditEntry Entry(StaffUser user, string action) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = user.OrganizationId,
        StaffUserId = user.Id,
        Action = action,
        Ip = current.Ip,
    };
}
