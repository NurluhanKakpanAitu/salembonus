namespace SalemBonus.Application.Common.Exceptions;

public abstract class AppException(string message) : Exception(message);

public sealed class NotFoundException(string message) : AppException(message);

public sealed class ValidationException(string message) : AppException(message);

public sealed class UnauthorizedException(string message) : AppException(message);
