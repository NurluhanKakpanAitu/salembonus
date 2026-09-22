namespace SalemBonus.Application.Common.Interfaces;

public record AccessToken(string Token, DateTime ExpiresAt);

public interface ITokenService
{
    AccessToken CreateAccessToken(Guid customerId, string phone);
    /// <summary>Кездейсоқ refresh токен мәні (клиентке беріледі).</summary>
    string CreateRefreshTokenValue();
    /// <summary>OTP код пен refresh токенді базада сақтау үшін бір бағытты хэш.</summary>
    string Hash(string value);
    TimeSpan RefreshTokenLifetime { get; }
}
