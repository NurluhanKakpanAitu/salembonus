using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SalemBonus.Application.Common.Exceptions;

namespace SalemBonus.Api.Middleware;

/// <summary>Application қабатының қателерін HTTP ProblemDetails-ке айналдырады.</summary>
public class AppExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Табылмады"),
            ValidationException => (StatusCodes.Status400BadRequest, "Қате сұраныс"),
            UnauthorizedException => (StatusCodes.Status401Unauthorized, "Рұқсат жоқ"),
            _ => (0, string.Empty),
        };
        if (status == 0) return false;

        http.Response.StatusCode = status;
        await http.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = exception.Message,
            Instance = http.Request.Path,
        }, ct);
        return true;
    }
}
