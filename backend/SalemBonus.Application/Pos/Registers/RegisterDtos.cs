namespace SalemBonus.Application.Pos.Registers;

public record RegisterDto(
    Guid Id,
    string Name,
    Guid StoreId,
    string StoreName,
    string? StoreAddress,
    /// <summary>Әрекетсіз неше минуттан кейін бұғатталады; бос — автоблок өшірулі.</summary>
    int? AutoLockMinutes,
    /// <summary>Касса қандай да бір құрылғыға тіркелген бе.</summary>
    bool IsBound,
    DateTime? ActivatedAt);

/// <summary>Кассадағы кассирлер тізімінің жолы (ТЗ «Касса» §15.2).</summary>
public record RegisterCashierDto(Guid Id, string FirstName, string LastName, string Role, bool HasPin);

public record PinLoginRequest(Guid StaffUserId, string? Pin);

public record PinRequest(string? Pin);

/// <summary>Тіркеу нәтижесі: құрылғы кілтін контроллер httpOnly cookie-ге салады, JSON-да берілмейді.</summary>
public record RegisterActivation(RegisterDto Register, string DeviceToken);
