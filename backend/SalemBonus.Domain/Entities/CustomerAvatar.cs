using System.Text.RegularExpressions;

namespace SalemBonus.Domain.Entities;

/// <summary>
/// Профиль суреті екі түрде сақталады:
/// өз фотосы — base64 data URL (MVP үшін бөлек файл қоймасы қажет емес, клиент алдын ала кішірейтеді),
/// дайын аватар — "dicebear:notionists:seed" түріндегі қысқа белгі, суретті клиент өзі құрастырады.
/// </summary>
public static partial class CustomerAvatar
{
    public const int MaxLength = 600_000;

    private static readonly string[] AllowedPrefixes =
    [
        "data:image/jpeg;base64,",
        "data:image/png;base64,",
        "data:image/webp;base64,",
    ];

    /// <summary>Дайын аватар: генератор аты, стилі және тұқымы. Тұқым — қауіпсіз таңбалар ғана.</summary>
    [GeneratedRegex(@"^dicebear:[a-z0-9-]{1,32}:[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex PresetAvatar();

    public static bool IsPreset(string url) => PresetAvatar().IsMatch(url);

    public static bool IsAllowedFormat(string url) =>
        IsPreset(url) || AllowedPrefixes.Any(p => url.StartsWith(p, StringComparison.OrdinalIgnoreCase));
}
