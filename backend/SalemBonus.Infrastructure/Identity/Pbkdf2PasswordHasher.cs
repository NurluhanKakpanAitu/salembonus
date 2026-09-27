using System.Security.Cryptography;
using SalemBonus.Application.Common.Interfaces;

namespace SalemBonus.Infrastructure.Identity;

/// <summary>
/// PBKDF2-HMAC-SHA256, OWASP ұсынған 600 000 итерация, әр құпиясөзге жеке тұз.
/// Формат: "pbkdf2-sha256$итерация$тұз$хеш" — кейін параметрді күшейтсек, ескі хештер де тексеріле береді.
/// </summary>
public class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string Scheme = "pbkdf2-sha256";
    private const int Iterations = 600_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public string Hash(string secret)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(secret, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"{Scheme}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public bool Verify(string secret, string hash)
    {
        var parts = hash.Split('$');
        if (parts.Length != 4 || parts[0] != Scheme || !int.TryParse(parts[1], out var iterations))
            return false;
        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(secret, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            // Тұрақты уақытпен салыстыру: жауап уақытынан хешті болжауға болмайды.
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
