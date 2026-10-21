using System.Text;
using System.Text.Json;

namespace SalemBonus.Infrastructure.Push;

/// <summary>FCM (RS256) мен APNs (ES256) провайдер токендеріне ортақ: base64url және «header.payload.signature» жинағы.</summary>
internal static class Jwt
{
    public static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Create(object header, object payload, Func<byte[], byte[]> sign)
    {
        var unsigned = Base64Url(JsonSerializer.SerializeToUtf8Bytes(header)) + "." + Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload));
        return unsigned + "." + Base64Url(sign(Encoding.ASCII.GetBytes(unsigned)));
    }
}
