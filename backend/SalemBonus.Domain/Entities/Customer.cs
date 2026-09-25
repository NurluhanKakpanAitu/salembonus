namespace SalemBonus.Domain.Entities;

public class Customer
{
    public Guid Id { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    /// <summary>Көрсету үшін біріктірілген аты-жөні.</summary>
    public string FullName => string.Join(' ', new[] { FirstName, LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
    /// <summary>Профиль толтырылған ба — аты берілген бе.</summary>
    public bool IsProfileCompleted => !string.IsNullOrWhiteSpace(FirstName);
    public string? Email { get; set; }
    public DateOnly? BirthDate { get; set; }
    /// <summary>Тұтынушы көрсеткен елді мекеннің КАТО коды (ең төменгі таңдалған деңгей).</summary>
    public string? KatoCode { get; set; }
    /// <summary>Профиль фотосы data URL түрінде (data:image/jpeg;base64,...). Бос болса аты-жөнінің әріптері көрсетіледі.</summary>
    public string? AvatarUrl { get; set; }
    /// <summary>Кассада көрсетілетін QR коды. Телефон нөмірінің орнына қолданылады.</summary>
    public string QrCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
