using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SalemBonus.Application.Common.Interfaces;

namespace SalemBonus.Infrastructure.Storage;

public class R2Options
{
    public const string Section = "R2";
    public string AccountId { get; set; } = string.Empty;
    public string Bucket { get; set; } = string.Empty;
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
    /// <summary>Бакеттің жария адресі (r2.dev не өз домені), соңында «/» жоқ.</summary>
    public string PublicUrl { get; set; } = string.Empty;
    public int UploadExpiresSeconds { get; set; } = 600;
}

/// <summary>
/// R2 — S3-үйлесімді. Жүктеу сілтемесіне AWS Signature V4 (query-string) қолтаңбасы қойылады.
/// SDK қоспадық: бізге тек бір PUT сілтемесі керек, ал жаңа AWS SDK әдепкі checksum тақырыптарын
/// қосады, оларды R2 мен браузер жүктеуі қолдамайды.
/// </summary>
public class R2FileStorage(IOptions<R2Options> options) : IFileStorage
{
    private const string Region = "auto";
    private const string Service = "s3";
    private readonly R2Options _o = options.Value;

    public bool IsConfigured =>
        _o.AccountId.Length > 0 && _o.Bucket.Length > 0 && _o.AccessKeyId.Length > 0
        && _o.SecretAccessKey.Length > 0 && _o.PublicUrl.Length > 0;

    public PresignedUpload CreateUpload(string key, string contentType, long size)
    {
        if (!IsConfigured) throw new InvalidOperationException("R2 баптауы толық емес");

        var now = DateTime.UtcNow;
        var date = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var amzDate = now.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var host = $"{_o.AccountId}.r2.cloudflarestorage.com";
        var path = $"/{Encode(_o.Bucket)}/{string.Join('/', key.Split('/').Select(Encode))}";
        var scope = $"{date}/{Region}/{Service}/aws4_request";
        const string signedHeaders = "content-length;content-type;host";

        var query = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["X-Amz-Algorithm"] = "AWS4-HMAC-SHA256",
            ["X-Amz-Credential"] = $"{_o.AccessKeyId}/{scope}",
            ["X-Amz-Date"] = amzDate,
            ["X-Amz-Expires"] = _o.UploadExpiresSeconds.ToString(CultureInfo.InvariantCulture),
            ["X-Amz-SignedHeaders"] = signedHeaders,
        };
        var canonicalQuery = string.Join('&', query.Select(kv => $"{Encode(kv.Key)}={Encode(kv.Value)}"));

        var canonicalRequest = string.Join('\n',
            "PUT",
            path,
            canonicalQuery,
            $"content-length:{size.ToString(CultureInfo.InvariantCulture)}",
            $"content-type:{contentType}",
            $"host:{host}",
            "",
            signedHeaders,
            "UNSIGNED-PAYLOAD");

        var stringToSign = string.Join('\n', "AWS4-HMAC-SHA256", amzDate, scope, Hex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest))));

        var kDate = Hmac(Encoding.UTF8.GetBytes("AWS4" + _o.SecretAccessKey), date);
        var kRegion = Hmac(kDate, Region);
        var kService = Hmac(kRegion, Service);
        var kSigning = Hmac(kService, "aws4_request");
        var signature = Hex(Hmac(kSigning, stringToSign));

        return new PresignedUpload(
            $"https://{host}{path}?{canonicalQuery}&X-Amz-Signature={signature}",
            $"{_o.PublicUrl.TrimEnd('/')}/{key}",
            key,
            now.AddSeconds(_o.UploadExpiresSeconds));
    }

    public bool IsOwnUrl(string url, string keyPrefix) =>
        IsConfigured && url.StartsWith($"{_o.PublicUrl.TrimEnd('/')}/{keyPrefix}", StringComparison.Ordinal)
        && !url.Contains("..", StringComparison.Ordinal);

    private static byte[] Hmac(byte[] key, string data) => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(data));

    private static string Hex(byte[] bytes) => Convert.ToHexStringLower(bytes);

    /// <summary>RFC 3986 бойынша кодтау (SigV4 талабы): әріп, сан және «-_.~» ғана өзгеріссіз.</summary>
    private static string Encode(string value)
    {
        var sb = new StringBuilder();
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            var c = (char)b;
            if (c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_' or '.' or '~') sb.Append(c);
            else sb.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }
}
