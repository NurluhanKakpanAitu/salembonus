using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;

namespace SalemBonus.Infrastructure.Identity;

public class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private Guid? Read()
    {
        var user = accessor.HttpContext?.User;
        var sub = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user?.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    public bool IsAuthenticated => Read() is not null;

    public Guid CustomerId => Read() ?? throw new UnauthorizedException("Кіру қажет");
}
