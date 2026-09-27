using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SalemBonus.Application.Common.Interfaces;

namespace SalemBonus.Infrastructure.Identity;

public class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    private readonly JwtOptions _opt = options.Value;

    public const string StaffTypeClaim = "typ";
    public const string StaffTypeValue = "staff";

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_opt.RefreshTokenDays);

    public AccessToken CreateAccessToken(Guid customerId, string phone) =>
        Create(_opt.Audience, customerId, phone, []);

    public AccessToken CreateStaffAccessToken(Guid staffUserId, string phone) =>
        Create(_opt.StaffAudience, staffUserId, phone, [new Claim(StaffTypeClaim, StaffTypeValue)]);

    private AccessToken Create(string audience, Guid subject, string phone, Claim[] extra)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_opt.AccessTokenMinutes);
        var creds = new SigningCredentials(SigningKey(_opt.Key), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, subject.ToString()),
                new Claim("phone", phone),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                .. extra,
            ],
            notBefore: now,
            expires: expires,
            signingCredentials: creds);
        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public string CreateRefreshTokenValue() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    public string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static SymmetricSecurityKey SigningKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
            throw new InvalidOperationException("Jwt:Key кемінде 32 таңба болуы керек (appsettings немесе Jwt__Key орта айнымалысы)");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }
}
