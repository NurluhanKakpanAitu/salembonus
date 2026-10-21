using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Domain.Core;

namespace SalemBonus.Infrastructure.Identity;

public class HttpCurrentStaff(IHttpContextAccessor accessor, ICurrentLanguage language) : ICurrentStaff
{
    private Guid? Read()
    {
        var user = accessor.HttpContext?.User;
        // Тек қызметкер токені: клиенттің токенінде "typ" жоқ.
        if (user?.FindFirst(JwtTokenService.StaffTypeClaim)?.Value != JwtTokenService.StaffTypeValue) return null;
        var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    public bool IsAuthenticated => Read() is not null;

    public Guid StaffUserId => Read() ?? throw new UnauthorizedException(Messages.LoginRequired(language.Value));

    public string? Ip => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}

/// <summary>
/// X-Store-Id тақырыбынан дүкенді алып, қызметкердің сонда белсенді мүшелігін тексереді.
/// Тақырып жоқ болса — алғашқы белсенді дүкен. Бір сұраныс ішінде нәтиже сақталады.
/// </summary>
/// <summary>SalemTech әкімшісі: тек admin токені («typ» = admin, өз audience-і).</summary>
public class HttpCurrentAdmin(IHttpContextAccessor accessor, ICurrentLanguage language) : ICurrentAdmin
{
    private Guid? Read()
    {
        var user = accessor.HttpContext?.User;
        if (user?.FindFirst(JwtTokenService.StaffTypeClaim)?.Value != JwtTokenService.AdminTypeValue) return null;
        var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    public bool IsAuthenticated => Read() is not null;

    public Guid AdminId => Read() ?? throw new UnauthorizedException(Messages.LoginRequired(language.Value));

    public string? Ip => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}

public class HttpStoreContext(
    IHttpContextAccessor accessor,
    ICurrentStaff current,
    IStaffRepository staff,
    ICurrentLanguage language) : IStoreContext
{
    public const string Header = "X-Store-Id";

    private StoreMembership? _cached;

    public async Task<StoreMembership> GetAsync(CancellationToken ct = default)
    {
        if (_cached is not null) return _cached;

        var lang = language.Value;
        var staffId = current.StaffUserId;
        var raw = accessor.HttpContext?.Request.Headers[Header].ToString();

        StoreMembership? membership;
        if (Guid.TryParse(raw, out var storeId))
        {
            membership = await staff.GetMembershipAsync(staffId, storeId, ct);
        }
        else
        {
            var user = await staff.GetByIdAsync(staffId, ct);
            // Өшірілген қызметкердің access токені әлі жарамды болуы мүмкін — дүкенге жібермейміз.
            membership = user is not { IsActive: true } ? null : user.Memberships
                .Where(m => m.IsActive && m.Store is { IsActive: true })
                .OrderBy(m => m.Store!.Name)
                .FirstOrDefault();
        }

        _cached = membership ?? throw new ForbiddenException(Messages.StaffStoreForbidden(lang));
        // SalemTech бизнесті тоқтатса — ашық сеанстар да келесі сұраныста тоқтайды.
        if (_cached.Store?.Organization is { IsActive: false })
        {
            _cached = null;
            throw new ForbiddenException(Messages.OrganizationSuspended(lang));
        }
        return _cached;
    }

    public async Task<StoreMembership> RequireAsync(string permission, CancellationToken ct = default)
    {
        var membership = await GetAsync(ct);
        if (!membership.Has(permission))
            throw new ForbiddenException(Messages.StaffPermissionDenied(language.Value));
        return membership;
    }
}
