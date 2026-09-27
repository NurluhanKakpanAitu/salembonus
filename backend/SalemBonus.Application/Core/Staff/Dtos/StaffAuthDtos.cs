namespace SalemBonus.Application.Core.Staff.Dtos;

public record StaffLoginRequest(string? Phone, string? Password, string? Device = null);

/// <summary>
/// Кіру нәтижесі. RefreshToken клиентке JSON-мен берілмейді — контроллер оны httpOnly cookie-ге салады.
/// </summary>
public record StaffSession(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    StaffMeDto Me);

public record StaffStoreDto(
    Guid Id,
    string Name,
    string? Address,
    string Role,
    IReadOnlyList<string> Permissions);

public record StaffMeDto(
    Guid Id,
    string Phone,
    string FirstName,
    string LastName,
    string Language,
    Guid OrganizationId,
    string OrganizationName,
    IReadOnlyList<StaffStoreDto> Stores,
    /// <summary>PIN қойылған ба — кассада PIN-мен кіру мен бұғатты ашу үшін керек.</summary>
    bool HasPin,
    /// <summary>Кіргеннен кейін ашылатын бет: "statistics" (иесі, әкімші) немесе "cashier" (кассир).</summary>
    string StartPage);

public record StaffLanguageRequest(string Language);

public record PasswordResetRequest(string? Phone);

/// <summary>DevCode тек Development-те толады — WhatsApp шаблоны бекітілгенше тест үшін.</summary>
public record PasswordResetRequested(int CodeLength, int ExpiresInSeconds, int RetryAfterSeconds, string? DevCode);

public record PasswordResetVerify(string? Phone, string? Code);

/// <summary>Код расталды: жаңа құпиясөз тек осы токенмен қабылданады.</summary>
public record PasswordResetVerified(string ResetToken, int ExpiresInSeconds);

public record PasswordResetComplete(string? ResetToken, string? NewPassword, string? ConfirmPassword);

/// <summary>Кіру бетіне керек құпиясөз талаптары (фронт алдын ала тексереді, соңғы шешім — серверде).</summary>
public record PasswordPolicy(int MinLength, bool RequireLetterAndDigit);

/// <summary>PIN қою немесе ауыстыру — қазіргі құпиясөзбен расталады (ТЗ «Касса» §17.8).</summary>
public record SetPinRequest(string? CurrentPassword, string? Pin, string? ConfirmPin);

/// <summary>Құпиясөзді ауыстыру — қазіргісін растап (ТЗ «Касса» §17.7).</summary>
public record ChangePasswordRequest(string? CurrentPassword, string? NewPassword, string? ConfirmPassword);
