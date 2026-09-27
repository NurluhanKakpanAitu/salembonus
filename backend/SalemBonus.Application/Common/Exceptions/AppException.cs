namespace SalemBonus.Application.Common.Exceptions;

public abstract class AppException(string message) : Exception(message);

public sealed class NotFoundException(string message) : AppException(message);

/// <summary>
/// Енгізілген дерек дұрыс емес. <paramref name="field"/> берілсе, фронт қатені сол өрістің астында көрсетеді.
/// </summary>
public sealed class ValidationException(string message, string? field = null) : AppException(message)
{
    public string? Field { get; } = field;
}

public sealed class UnauthorizedException(string message) : AppException(message);

/// <summary>Пайдаланушы кірген, бірақ бұл әрекетке рұқсаты жоқ.</summary>
public sealed class ForbiddenException(string message) : AppException(message);
