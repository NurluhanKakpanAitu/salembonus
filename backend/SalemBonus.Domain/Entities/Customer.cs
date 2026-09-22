namespace SalemBonus.Domain.Entities;

public class Customer
{
    public Guid Id { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateOnly? BirthDate { get; set; }
    /// <summary>Кассада көрсетілетін QR коды. Телефон нөмірінің орнына қолданылады.</summary>
    public string QrCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
