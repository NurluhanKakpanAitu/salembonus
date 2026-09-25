using Microsoft.Extensions.Options;
using SalemBonus.Application.Auth.Dtos;
using SalemBonus.Application.BonusCards;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Entities;

namespace SalemBonus.Application.Auth;

public class AuthService(
    IOtpRepository otps,
    IRefreshTokenRepository refreshTokens,
    ICustomerRepository customers,
    ISmsSender sms,
    ITokenService tokens,
    IUnitOfWork unitOfWork,
    IOptions<AuthOptions> options,
    ICurrentLanguage language) : IAuthService
{
    private readonly AuthOptions _opt = options.Value;

    private AppLanguage Lang => language.Value;

    public async Task<RequestCodeResult> RequestCodeAsync(string rawPhone, CancellationToken ct = default)
    {
        var phone = ParsePhone(rawPhone);
        var now = DateTime.UtcNow;

        var latest = await otps.GetLatestAsync(phone, ct);
        if (latest is not null && latest.CreatedAt.AddSeconds(_opt.OtpResendSeconds) > now)
        {
            var wait = (int)Math.Ceiling((latest.CreatedAt.AddSeconds(_opt.OtpResendSeconds) - now).TotalSeconds);
            throw new ValidationException(Messages.CodeRetryAfter(Lang, wait));
        }

        var code = string.IsNullOrWhiteSpace(_opt.StaticOtpCode) ? GenerateCode(_opt.OtpLength) : _opt.StaticOtpCode.Trim();
        otps.Add(new OtpCode
        {
            Id = Guid.NewGuid(),
            Phone = phone,
            CodeHash = tokens.Hash(phone + ":" + code),
            ExpiresAt = now.AddSeconds(_opt.OtpLifetimeSeconds),
            CreatedAt = now,
        });
        await unitOfWork.SaveChangesAsync(ct);

        await sms.SendAsync(phone, $"SalemBonus: кіру коды {code}. Ешкімге айтпаңыз.", ct);

        return new RequestCodeResult(phone, _opt.OtpLifetimeSeconds, _opt.OtpResendSeconds,
            _opt.ReturnCodeInResponse ? code : null);
    }

    public async Task<AuthTokens> VerifyCodeAsync(VerifyCodeRequest request, CancellationToken ct = default)
    {
        var phone = ParsePhone(request.Phone);
        var now = DateTime.UtcNow;

        var otp = await otps.GetLatestAsync(phone, ct);
        if (otp is null || !otp.IsActive(now))
            throw new ValidationException(Messages.CodeExpired(Lang));
        if (otp.Attempts >= _opt.OtpMaxAttempts)
            throw new ValidationException(Messages.CodeAttemptsExceeded(Lang));

        otp.Attempts++;
        var code = new string(request.Code.Where(char.IsDigit).ToArray());
        if (tokens.Hash(phone + ":" + code) != otp.CodeHash)
        {
            await unitOfWork.SaveChangesAsync(ct);
            var left = _opt.OtpMaxAttempts - otp.Attempts;
            throw new ValidationException(Messages.CodeWrong(Lang, left));
        }
        otp.ConsumedAt = now;

        var customer = await customers.GetByPhoneAsync(phone, ct);
        var isNew = customer is null;
        if (customer is null)
        {
            customer = new Customer
            {
                Id = Guid.NewGuid(),
                Phone = phone,
                QrCode = BonusRules.GenerateQrCode(),
                CreatedAt = now,
            };
            customers.Add(customer);
        }

        var result = IssueTokens(customer, request.Device, now, isNew);
        await unitOfWork.SaveChangesAsync(ct);
        return result;
    }

    public async Task<AuthTokens> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var stored = await refreshTokens.GetByHashAsync(tokens.Hash(refreshToken), ct);
        if (stored is null || !stored.IsActive(now))
            throw new UnauthorizedException(Messages.SessionExpired(Lang));

        var customer = await customers.GetByIdAsync(stored.CustomerId, ct)
            ?? throw new UnauthorizedException(Messages.CustomerNotFound(Lang));

        stored.RevokedAt = now;
        var result = IssueTokens(customer, stored.Device, now, false);
        await unitOfWork.SaveChangesAsync(ct);
        return result;
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var stored = await refreshTokens.GetByHashAsync(tokens.Hash(refreshToken), ct);
        if (stored is null) return;
        stored.RevokedAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(ct);
    }

    private AuthTokens IssueTokens(Customer customer, string? device, DateTime now, bool isNew)
    {
        var access = tokens.CreateAccessToken(customer.Id, customer.Phone);
        var refreshValue = tokens.CreateRefreshTokenValue();
        var refreshExpires = now.Add(tokens.RefreshTokenLifetime);
        refreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            TokenHash = tokens.Hash(refreshValue),
            ExpiresAt = refreshExpires,
            Device = device,
            CreatedAt = now,
        });
        var profileCompleted = customer.IsProfileCompleted;
        return new AuthTokens(access.Token, access.ExpiresAt, refreshValue, refreshExpires, isNew, profileCompleted);
    }

    private string ParsePhone(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || !BonusRules.LooksLikePhone(raw))
            throw new ValidationException(Messages.PhoneInvalid(Lang));
        var phone = BonusRules.NormalizePhone(raw);
        if (phone.Length != 12 || !phone.StartsWith("+7"))
            throw new ValidationException(Messages.PhoneNotKz(Lang));
        return phone;
    }

    private static string GenerateCode(int length)
    {
        var max = (int)Math.Pow(10, length);
        return System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, max).ToString().PadLeft(length, '0');
    }
}
