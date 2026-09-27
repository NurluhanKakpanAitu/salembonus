using SalemBonus.Application.Core.Staff.Dtos;

namespace SalemBonus.Application.Core.Staff;

/// <summary>Құпиясөзді WhatsApp коды арқылы қалпына келтіру: нөмір → код → жаңа құпиясөз.</summary>
public interface IStaffPasswordResetService
{
    PasswordPolicy Policy { get; }
    Task<PasswordResetRequested> RequestAsync(PasswordResetRequest request, CancellationToken ct = default);
    Task<PasswordResetVerified> VerifyAsync(PasswordResetVerify request, CancellationToken ct = default);
    Task CompleteAsync(PasswordResetComplete request, CancellationToken ct = default);
}
