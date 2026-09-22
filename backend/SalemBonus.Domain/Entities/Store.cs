namespace SalemBonus.Domain.Entities;

public class Store
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal CashbackPercent { get; set; }
    /// <summary>Карта фонының түсі, hex (#RRGGBB).</summary>
    public string ThemeColor { get; set; } = "#111113";
    /// <summary>Иконка кілті (lucide атауы), мысалы "car", "coffee".</summary>
    public string Icon { get; set; } = "store";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
