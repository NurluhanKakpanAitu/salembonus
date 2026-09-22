namespace SalemBonus.Domain.Entities;

public class Store
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    /// <summary>Сатып алу сомасынан есептелетін бонус пайызы.</summary>
    public decimal CashbackPercent { get; set; }
    /// <summary>Бір сатып алуда бонуспен төлеуге болатын ең жоғары үлес (%).</summary>
    public decimal MaxRedeemPercent { get; set; } = 30;
    /// <summary>Карта фонының түсі, hex (#RRGGBB).</summary>
    public string ThemeColor { get; set; } = "#111113";
    /// <summary>Иконка кілті (lucide атауы), мысалы "car", "coffee".</summary>
    public string Icon { get; set; } = "store";
    /// <summary>SalemPos / касса интеграциясы үшін API кілті.</summary>
    public string ApiKey { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
