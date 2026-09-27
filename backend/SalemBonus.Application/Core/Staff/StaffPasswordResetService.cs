using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using SalemBonus.Application.BonusCards;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Core.Staff.Dtos;
using SalemBonus.Domain.Core;

namespace SalemBonus.Application.Core.Staff;

/// <summary>
/// ТЗ «Авторизация» §5–7, §11. Код хештеліп, қысқа мерзімге сақталады; сұрау жиілігі мен енгізу
/// әрекеттері шектеулі; жаңа құпиясөз тек код расталғаннан кейін берілген токенмен қабылданады.
/// Құпиясөз ауысқанда қызметкердің барлық ашық сеансы жабылады. Әр қадам аудитке жазылады.
/// </summary>
public class StaffPasswordResetService(
    IStaffRepository staff,
    IStaffOtpRepository otps,
    IStaffRefreshTokenRepository refreshTokens,
    IPasswordHasher hasher,
    ITokenService tokens,
    IWhatsAppSender whatsApp,
    IAuditLog audit,
    ICurrentStaff current,
    IUnitOfWork unitOfWork,
    ICurrentLanguage language,
    IOptions<StaffAuthOptions> options) : IStaffPasswordResetService
{
    private readonly StaffAuthOptions _opt = options.Value;
    private AppLanguage Lang => language.Value;

    public PasswordPolicy Policy => new(_opt.MinPasswordLength, _opt.RequireLetterAndDigit);

    public async Task<PasswordResetRequested> RequestAsync(PasswordResetRequest request, CancellationToken ct = default)
    {
        var user = await FindUserAsync(request.Phone, ct);
        var now = DateTime.UtcNow;

        var latest = await otps.GetLatestAsync(user.Id, StaffOtpCode.PasswordReset, ct);
        if (latest is not null && latest.CreatedAt.AddSeconds(_opt.OtpResendSeconds) > now)
        {
            var wait = (int)Math.Ceiling((latest.CreatedAt.AddSeconds(_opt.OtpResendSeconds) - now).TotalSeconds);
            throw new ValidationException(Messages.CodeRetryAfter(Lang, wait), "phone");
        }
        if (await otps.CountSinceAsync(user.Id, StaffOtpCode.PasswordReset, now.AddHours(-1), ct) >= _opt.OtpMaxPerHour)
        {
            await RecordAsync(user, "auth.reset_request", false, "hourly_limit", ct);
            throw new ValidationException(Messages.StaffCodeTooMany(Lang), "phone");
        }

        var code = RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, _opt.OtpLength))
            .ToString().PadLeft(_opt.OtpLength, '0');

        try
        {
            await whatsApp.SendOtpAsync(user.Phone, code, user.Language, ct);
        }
        catch (MessageDeliveryException)
        {
            await RecordAsync(user, "auth.reset_request", false, "delivery_failed", ct);
            throw new ValidationException(Messages.StaffCodeSendFailed(Lang), "phone");
        }

        otps.Add(new StaffOtpCode
        {
            Id = Guid.NewGuid(),
            StaffUserId = user.Id,
            Purpose = StaffOtpCode.PasswordReset,
            CodeHash = HashCode(user.Id, code),
            ExpiresAt = now.AddSeconds(_opt.OtpLifetimeSeconds),
            Ip = current.Ip,
            CreatedAt = now,
        });
        audit.Record(Entry(user, "auth.reset_request", true, null));
        await unitOfWork.SaveChangesAsync(ct);

        return new PasswordResetRequested(_opt.OtpLength, _opt.OtpLifetimeSeconds, _opt.OtpResendSeconds,
            _opt.ReturnCodeInResponse ? code : null);
    }

    public async Task<PasswordResetVerified> VerifyAsync(PasswordResetVerify request, CancellationToken ct = default)
    {
        var user = await FindUserAsync(request.Phone, ct);
        var code = new string((request.Code ?? string.Empty).Where(char.IsDigit).ToArray());
        if (code.Length != _opt.OtpLength)
            throw new ValidationException(Messages.StaffCodeRequired(Lang), "code");

        var now = DateTime.UtcNow;
        var otp = await otps.GetLatestAsync(user.Id, StaffOtpCode.PasswordReset, ct);
        if (otp is null || !otp.IsActive(now))
            throw new ValidationException(Messages.StaffCodeExpired(Lang), "code");
        if (otp.Attempts >= _opt.OtpMaxAttempts)
            throw new ValidationException(Messages.CodeAttemptsExceeded(Lang), "code");

        otp.Attempts++;
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(HashCode(user.Id, code)), Convert.FromHexString(otp.CodeHash)))
        {
            await RecordAsync(user, "auth.reset_verify", false, "wrong_code", ct);
            throw new ValidationException(
                otp.Attempts >= _opt.OtpMaxAttempts ? Messages.CodeAttemptsExceeded(Lang) : Messages.StaffCodeWrong(Lang),
                "code");
        }

        var resetToken = tokens.CreateRefreshTokenValue();
        otp.ConsumedAt = now;
        otp.ResetTokenHash = tokens.Hash(resetToken);
        otp.ResetTokenExpiresAt = now.AddMinutes(_opt.ResetTokenMinutes);
        audit.Record(Entry(user, "auth.reset_verify", true, null));
        await unitOfWork.SaveChangesAsync(ct);

        return new PasswordResetVerified(resetToken, _opt.ResetTokenMinutes * 60);
    }

    public async Task CompleteAsync(PasswordResetComplete request, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var otp = string.IsNullOrWhiteSpace(request.ResetToken)
            ? null
            : await otps.GetByResetTokenHashAsync(tokens.Hash(request.ResetToken), ct);
        if (otp is null || !otp.CanReset(now))
            throw new ValidationException(Messages.StaffResetExpired(Lang));

        var password = request.NewPassword ?? string.Empty;
        CredentialRules.ValidatePassword(password, _opt, Lang);
        if (password != request.ConfirmPassword)
            throw new ValidationException(Messages.StaffPasswordsMismatch(Lang), "confirmPassword");

        var user = await staff.GetByIdForUpdateAsync(otp.StaffUserId, ct)
            ?? throw new ValidationException(Messages.StaffResetExpired(Lang));

        user.PasswordHash = hasher.Hash(password);
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        otp.ResetTokenUsedAt = now;
        // Құпиясөз ауысты — басқа құрылғыдағы ескі сеанстар жарамсыз болуы керек.
        await refreshTokens.RevokeAllAsync(user.Id, ct);
        audit.Record(Entry(user, "auth.password_changed", true, "reset"));
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<StaffUser> FindUserAsync(string? rawPhone, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawPhone))
            throw new ValidationException(Messages.StaffPhoneRequired(Lang), "phone");
        if (!BonusRules.LooksLikePhone(rawPhone))
            throw new ValidationException(Messages.StaffPhoneInvalid(Lang), "phone");
        var phone = BonusRules.NormalizePhone(rawPhone);
        if (phone.Length != 12 || !phone.StartsWith("+7"))
            throw new ValidationException(Messages.StaffPhoneInvalid(Lang), "phone");

        var user = await staff.GetByPhoneForUpdateAsync(phone, ct);
        // ТЗ §5.7 нөмірдің тіркелмегенін ашық айтуды талап етеді.
        if (user is null || !user.IsActive)
            throw new ValidationException(Messages.StaffNotFound(Lang), "phone");
        return user;
    }

    /// <summary>Код тек сол қызметкерге байланған хешпен сақталады.</summary>
    private string HashCode(Guid staffUserId, string code) => tokens.Hash($"staff-reset:{staffUserId}:{code}");

    private async Task RecordAsync(StaffUser user, string action, bool success, string details, CancellationToken ct)
    {
        audit.Record(Entry(user, action, success, details));
        await unitOfWork.SaveChangesAsync(ct);
    }

    private AuditEntry Entry(StaffUser user, string action, bool success, string? details) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = user.OrganizationId,
        StaffUserId = user.Id,
        Action = action,
        Success = success,
        Details = details,
        Ip = current.Ip,
    };
}
