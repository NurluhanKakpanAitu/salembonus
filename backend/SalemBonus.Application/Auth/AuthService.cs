using Microsoft.Extensions.Options;
using SalemBonus.Application.Auth.Dtos;
using SalemBonus.Application.BonusCards;
using SalemBonus.Application.Common.Exceptions;
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
    IOptions<AuthOptions> options) : IAuthService
{
    private readonly AuthOptions _opt = options.Value;

    public async Task<RequestCodeResult> RequestCodeAsync(string rawPhone, CancellationToken ct = default)
    {
        var phone = ParsePhone(rawPhone);
        var now = DateTime.UtcNow;

        var latest = await otps.GetLatestAsync(phone, ct);
        if (latest is not null && latest.CreatedAt.AddSeconds(_opt.OtpResendSeconds) > now)
        {
            var wait = (int)Math.Ceiling((latest.CreatedAt.AddSeconds(_opt.OtpResendSeconds) - now).TotalSeconds);
            throw new ValidationException($"Жаңа кодты {wait} секундтан кейін сұраңыз");
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
            throw new ValidationException("Кодтың мерзімі өтті, жаңа код сұраңыз");
        if (otp.Attempts >= _opt.OtpMaxAttempts)
            throw new ValidationException("Әрекет саны асып кетті, жаңа код сұраңыз");

        otp.Attempts++;
        var code = new string(request.Code.Where(char.IsDigit).ToArray());
        if (tokens.Hash(phone + ":" + code) != otp.CodeHash)
        {
            await unitOfWork.SaveChangesAsync(ct);
            var left = _opt.OtpMaxAttempts - otp.Attempts;
            throw new ValidationException(left > 0 ? $"Код қате, {left} әрекет қалды" : "Код қате, жаңа код сұраңыз");
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
                FullName = phone,
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
            throw new UnauthorizedException("Сессияның мерзімі өтті, қайта кіріңіз");

        var customer = await customers.GetByIdAsync(stored.CustomerId, ct)
            ?? throw new UnauthorizedException("Тұтынушы табылмады");

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
        var profileCompleted = customer.FullName != customer.Phone;
        return new AuthTokens(access.Token, access.ExpiresAt, refreshValue, refreshExpires, isNew, profileCompleted);
    }

    private static string ParsePhone(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || !BonusRules.LooksLikePhone(raw))
            throw new ValidationException("Телефон нөмірі дұрыс емес");
        var phone = BonusRules.NormalizePhone(raw);
        if (phone.Length != 12 || !phone.StartsWith("+7"))
            throw new ValidationException("Қазақстан нөмірін енгізіңіз: +7 7XX XXX XX XX");
        return phone;
    }

    private static string GenerateCode(int length)
    {
        var max = (int)Math.Pow(10, length);
        return System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, max).ToString().PadLeft(length, '0');
    }
}
