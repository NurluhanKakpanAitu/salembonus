namespace SalemBonus.Domain.Entities;

/// <summary>
/// Профиль фотосы base64 data URL күйінде базада сақталады: MVP үшін бөлек файл қоймасы қажет емес.
/// Клиент суретті жібермес бұрын кішірейтеді, сондықтан шектеу шағын.
/// </summary>
public static class CustomerAvatar
{
    public const int MaxLength = 600_000;

    private static readonly string[] AllowedPrefixes =
    [
        "data:image/jpeg;base64,",
        "data:image/png;base64,",
        "data:image/webp;base64,",
    ];

    public static bool IsAllowedFormat(string url) =>
        AllowedPrefixes.Any(p => url.StartsWith(p, StringComparison.OrdinalIgnoreCase));
}
