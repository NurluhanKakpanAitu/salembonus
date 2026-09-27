namespace SalemBonus.Domain.Core;

/// <summary>
/// Әрекет журналы: кім, қашан, не істеді, нәтижесі қандай. Тек қосылады — өзгерту мен өшіру жоқ.
/// </summary>
public class AuditEntry
{
    public Guid Id { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? StoreId { get; set; }
    public Guid? StaffUserId { get; set; }
    public Guid? RegisterId { get; set; }
    /// <summary>Әрекет кілті, мысалы "auth.login", "sale.create".</summary>
    public string Action { get; set; } = string.Empty;
    public string? Entity { get; set; }
    public string? EntityId { get; set; }
    /// <summary>Өзгергенге дейінгі мәндер (JSON).</summary>
    public string? OldValues { get; set; }
    /// <summary>Өзгергеннен кейінгі мәндер (JSON).</summary>
    public string? NewValues { get; set; }
    public decimal? Amount { get; set; }
    public bool Success { get; set; } = true;
    /// <summary>Сәтсіз болса — себебі.</summary>
    public string? Details { get; set; }
    public string? Ip { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
