using SalemBonus.Application.Core.Staff.Dtos;

namespace SalemBonus.Application.Core.Staff;

/// <summary>Кірген қызметкердің өз PIN-і мен құпиясөзін ауыстыруы.</summary>
public interface IStaffCredentialsService
{
    Task<StaffMeDto> SetPinAsync(SetPinRequest request, CancellationToken ct = default);
    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default);
}
