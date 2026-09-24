using Microsoft.AspNetCore.Http;
using SalemBonus.Application.Common.Interfaces;

namespace SalemBonus.Infrastructure.Identity;

public class HttpCurrentLanguage(IHttpContextAccessor accessor) : ICurrentLanguage
{
    public AppLanguage Value
    {
        get
        {
            var header = accessor.HttpContext?.Request.Headers.AcceptLanguage.ToString();
            return header is not null && header.TrimStart().StartsWith("ru", StringComparison.OrdinalIgnoreCase)
                ? AppLanguage.Ru
                : AppLanguage.Kk;
        }
    }
}
