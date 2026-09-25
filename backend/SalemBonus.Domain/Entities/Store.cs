namespace SalemBonus.Domain.Entities;

public class Store
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    /// <summary>Каталогта көрсетілетін қысқа сипаттама.</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>Жаңа клиентке берілетін базалық бонус пайызы. Мәртебе бойынша пайыз <see cref="Levels"/> ішінде.</summary>
    public decimal CashbackPercent { get; set; }
    /// <summary>Дүкеннің мекенжайы.</summary>
    public string? Address { get; set; }
    /// <summary>Байланыс телефоны.</summary>
    public string? Phone { get; set; }
    /// <summary>Дүкен беттегі суреті (URL немесе data URL). Бос болса түсті мұқаба көрсетіледі.</summary>
    public string? PhotoUrl { get; set; }
    /// <summary>Бір сатып алуда бонуспен төлеуге болатын ең жоғары үлес (%).</summary>
    public decimal MaxRedeemPercent { get; set; } = 30;
    /// <summary>Карта фонының түсі, hex (#RRGGBB).</summary>
    public string ThemeColor { get; set; } = "#111113";
    /// <summary>Иконка кілті (lucide атауы), мысалы "car", "coffee".</summary>
    public string Icon { get; set; } = "store";
    /// <summary>SalemPos / касса интеграциясы үшін API кілті.</summary>
    public string ApiKey { get; set; } = string.Empty;
    /// <summary>Дүкен плакатындағы QR-ға салынатын қысқа код. Тұтынушы сканерлеп карта ашады.</summary>
    public string JoinCode { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Мәртебелер баспалдағы. Бос болса үнсіз келісім шектері қолданылады.</summary>
    public List<StoreLevel> Levels { get; set; } = [];
}
