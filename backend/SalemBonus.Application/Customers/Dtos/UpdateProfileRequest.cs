namespace SalemBonus.Application.Customers.Dtos;

public record UpdateProfileRequest(string FirstName, string? LastName, string? Email, DateOnly? BirthDate, string? KatoCode);

/// <summary>Профиль фотосы. Бос жіберілсе фото өшіріледі.</summary>
public record UpdateAvatarRequest(string? AvatarUrl);
