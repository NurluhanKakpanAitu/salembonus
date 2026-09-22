namespace SalemBonus.Application.Customers.Dtos;

public record CustomerDto(
    Guid Id,
    string Phone,
    string FullName,
    string FirstName,
    string? Email,
    DateOnly? BirthDate,
    bool IsBirthdayToday,
    int TotalBalance,
    int StoreCount);
