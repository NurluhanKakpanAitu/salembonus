namespace SalemBonus.Domain.Entities;

/// <summary>
/// ҚР әкімшілік-аумақтық объектілер жіктеуішінің (КАТО) бір жазбасы.
/// Деңгейлер: 1 — облыс/республикалық маңызы бар қала, 2 — аудан/қала әкімдігі,
/// 3 — ауылдық округ/қала ауданы, 4 — елді мекен.
/// </summary>
public class KatoEntry
{
    /// <summary>Тоғыз таңбалы КАТО коды.</summary>
    public string Code { get; set; } = string.Empty;
    public string? ParentCode { get; set; }
    public int Level { get; set; }
    public string NameKk { get; set; } = string.Empty;
    public string NameRu { get; set; } = string.Empty;
}
