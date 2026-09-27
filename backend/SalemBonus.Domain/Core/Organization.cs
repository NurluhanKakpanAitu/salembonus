namespace SalemBonus.Domain.Core;

/// <summary>
/// Бизнес — бір иесінің компаниясы. Оның дүкендері, қызметкерлері және (кейін) каталогы осыған байланады.
/// Барлық өнімге ортақ: SalemPos та, кейін SalemZapis та осы бизнесті қолданады.
/// </summary>
public class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Бизнес-сәйкестендіру нөмірі (12 цифр). Чекте көрсетіледі.</summary>
    public string? Bin { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
