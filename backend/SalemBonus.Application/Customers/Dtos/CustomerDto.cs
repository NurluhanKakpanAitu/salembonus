using SalemBonus.Application.Kato.Dtos;

namespace SalemBonus.Application.Customers.Dtos;

public record CustomerDto(
    Guid Id,
    string Phone,
    string FullName,
    string FirstName,
    string LastName,
    string? Email,
    DateOnly? BirthDate,
    string? AvatarUrl,
    /// <summary>Таңдалған елді мекеннің КАТО коды.</summary>
    string? KatoCode,
    /// <summary>Облыстан бастап толық жол, көрсетуге дайын.</summary>
    IReadOnlyList<KatoNodeDto> KatoPath,
    bool IsBirthdayToday,
    int TotalBalance,
    int StoreCount);
