namespace SalemBonus.Application.Common.Interfaces;

/// <summary>Браузер файлды тікелей сақтау қоймасына жүктейтін қолтаңбалы сілтеме.</summary>
public record PresignedUpload(string UploadUrl, string PublicUrl, string Key, DateTime ExpiresAt);

/// <summary>
/// Файл қоймасы (Cloudflare R2). Файл API арқылы өтпейді: сервер тек қысқа мерзімді қолтаңба береді,
/// браузер файлды тікелей жүктейді. Қолтаңбаға түрі мен нақты көлемі кіреді — басқа файл жүктелмейді.
/// </summary>
public interface IFileStorage
{
    bool IsConfigured { get; }
    PresignedUpload CreateUpload(string key, string contentType, long size);
    /// <summary>Сілтеме осы қоймадағы берілген префикстің ішінде ме (бөтен URL сақталмауы үшін).</summary>
    bool IsOwnUrl(string url, string keyPrefix);
}
