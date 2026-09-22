namespace SalemBonus.Application.Customers.Dtos;

public record UpdateProfileRequest(string FullName, string? Email, DateOnly? BirthDate);
