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
    /// <summary>Кіргеннен кейін ашылатын бет: "statistics" (иесі, әкімші) немесе "cashier" (кассир).</summary>
    string StartPage);

public record StaffLanguageRequest(string Language);
