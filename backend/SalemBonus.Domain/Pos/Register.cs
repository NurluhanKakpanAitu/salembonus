using SalemBonus.Domain.Entities;

namespace SalemBonus.Domain.Pos;

/// <summary>Дүкендегі касса, мысалы «Касса №1». Кассир PIN-мен тек тіркелген кассада кіре алады.</summary>
public class Register
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Кассаға тіркелген құрылғының токен хеші. Бос болса — құрылғы әлі тіркелмеген.</summary>
    public string? DeviceTokenHash { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Store? Store { get; set; }
}
