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

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_opt.RefreshTokenDays);

    public AccessToken CreateAccessToken(Guid customerId, string phone)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_opt.AccessTokenMinutes);
        var creds = new SigningCredentials(SigningKey(_opt.Key), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, customerId.ToString()),
                new Claim("phone", phone),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
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
