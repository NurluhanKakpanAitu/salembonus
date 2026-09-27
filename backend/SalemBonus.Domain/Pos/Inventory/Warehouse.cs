namespace SalemBonus.Domain.Pos.Inventory;

/// <summary>
/// Қойма. Әр дүкенде кемінде біреу («Негізгі қойма») болады. Толық «Склад» модулі кейін;
/// қазір тек тауар жасағанда бастапқы қалдықты енгізу үшін керек (ТЗ «Товар» §6.2).
/// </summary>
public class Warehouse
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid StoreId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Дүкеннің әдепкі қоймасы: касса қалдықты осыдан алады.</summary>
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
