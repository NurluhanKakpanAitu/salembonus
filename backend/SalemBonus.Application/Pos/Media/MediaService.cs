using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Domain.Core;

namespace SalemBonus.Application.Pos.Media;

/// <summary>
/// Суреттерді жүктеуге рұқсат беру. Файл API арқылы өтпейді: браузер оны R2-ге тікелей жібереді,
/// ал сілтеме тек осы бизнестің бумасына, берілген түр мен көлемге ғана жарайды.
/// </summary>
public class MediaService(IFileStorage storage, CatalogAccess access)
{
    public const long MaxBytes = 5 * 1024 * 1024;

    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["image/webp"] = "webp",
    };

    public static string BrandPrefix(Guid orgId) => $"org/{orgId:N}/brands/";

    public async Task<UploadDto> CreateUploadAsync(UploadRequest request, CancellationToken ct = default)
    {
        var lang = access.Lang;
        var (orgId, prefix) = request.Kind switch
        {
            "product" => ((await access.RequireAnyAsync(ct, StaffPermissions.ProductsCreate, StaffPermissions.ProductsEdit)).OrgId, (Func<Guid, string>)ProductService.ImagePrefix),
            "brand" => ((await access.RequireAsync(StaffPermissions.CatalogDictionaries, ct)).OrgId, BrandPrefix),
            _ => throw new ValidationException(Messages.UploadKindInvalid(lang), "kind"),
        };

        var contentType = request.ContentType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!Extensions.TryGetValue(contentType, out var ext)) throw new ValidationException(Messages.UploadTypeInvalid(lang), "contentType");
        if (request.Size is <= 0 or > MaxBytes) throw new ValidationException(Messages.UploadTooLarge(lang, (int)(MaxBytes / 1024 / 1024)), "size");
        if (!storage.IsConfigured) throw new ValidationException(Messages.StorageNotConfigured(lang));

        var upload = storage.CreateUpload($"{prefix(orgId)}{Guid.NewGuid():N}.{ext}", contentType, request.Size);
        return new UploadDto(upload.UploadUrl, upload.PublicUrl, upload.ExpiresAt);
    }
}
