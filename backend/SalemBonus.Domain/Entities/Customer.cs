namespace SalemBonus.Domain.Entities;

public class Customer
{
    public Guid Id { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateOnly? BirthDate { get; set; }
    /// <summary>Профиль фотосы data URL түрінде (data:image/jpeg;base64,...). Бос болса аты-жөнінің әріптері көрсетіледі.</summary>
    public string? AvatarUrl { get; set; }
    /// <summary>Кассада көрсетілетін QR коды. Телефон нөмірінің орнына қолданылады.</summary>
    public string QrCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
