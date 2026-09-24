namespace SalemBonus.Application.Customers.Dtos;

public record UpdateProfileRequest(string FullName, string? Email, DateOnly? BirthDate);

/// <summary>Профиль фотосы. Бос жіберілсе фото өшіріледі.</summary>
public record UpdateAvatarRequest(string? AvatarUrl);
