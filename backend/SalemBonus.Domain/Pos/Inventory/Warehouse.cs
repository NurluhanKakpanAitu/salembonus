namespace SalemBonus.Domain.Pos.Inventory;

/// <summary>
/// Қойма (ТЗ «Склад» §3, §6). Әр дүкенде дәл бір негізгі қойма болады: касса мен приёмка әдепкіде соны
/// алады. Архивтегі қойма жаңа операцияға таңдалмайды, тарихы қалады; физикалық өшірілмейді.
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
    /// <summary>Түрі: негізгі, сауда залы, резерв не өз атауы (міндетті емес).</summary>
    public string? Type { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
