using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Core.Staff;
using SalemBonus.Application.Core.Staff.Dtos;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Pos;

namespace SalemBonus.Application.Pos.Registers;

public class RegisterService(
    IRegisterRepository registers,
    IStaffRepository staff,
    IStaffAuthService auth,
    IStoreContext storeContext,
    ICurrentStaff current,
    ITokenService tokens,
    IAuditLog audit,
    IUnitOfWork unitOfWork,
    ICurrentLanguage language) : IRegisterService
{
    private AppLanguage Lang => language.Value;

    public async Task<IReadOnlyList<RegisterDto>> ListAsync(CancellationToken ct = default)
    {
        var membership = await storeContext.RequireAsync(StaffPermissions.SettingsManage, ct);
        var list = await registers.ListByStoreAsync(membership.StoreId, ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<RegisterActivation> ActivateAsync(Guid registerId, CancellationToken ct = default)
    {
        var membership = await storeContext.RequireAsync(StaffPermissions.SettingsManage, ct);
        var register = await registers.GetForUpdateAsync(membership.StoreId, registerId, ct)
            ?? throw new NotFoundException(Messages.RegisterNotFound(Lang));

        // Бір касса — бір құрылғы: басқа құрылғыда тіркелген болса, ол құрылғы кассадан ажырайды.
        var token = tokens.CreateRefreshTokenValue();
        var rebound = register.DeviceTokenHash is not null;
        register.DeviceTokenHash = tokens.Hash(token);
        register.ActivatedAt = DateTime.UtcNow;
        audit.Record(Entry("register.activate", register, rebound ? "rebound" : null));
        await unitOfWork.SaveChangesAsync(ct);
        return new RegisterActivation(ToDto(register), token);
    }

    public async Task DeactivateAsync(string? deviceToken, CancellationToken ct = default)
    {
        var register = await RequireRegisterAsync(deviceToken, forUpdate: true, ct);
        var membership = await staff.GetMembershipAsync(current.StaffUserId, register.StoreId, ct);
        if (membership is null || !membership.Has(StaffPermissions.SettingsManage))
            throw new ForbiddenException(Messages.StaffPermissionDenied(Lang));

        register.DeviceTokenHash = null;
        register.ActivatedAt = null;
        audit.Record(Entry("register.deactivate", register, null));
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<RegisterDto?> GetCurrentAsync(string? deviceToken, CancellationToken ct = default)
    {
        var register = await FindAsync(deviceToken, forUpdate: false, ct);
        return register is null ? null : ToDto(register);
    }

    public async Task<IReadOnlyList<RegisterCashierDto>> GetCashiersAsync(string? deviceToken, CancellationToken ct = default)
    {
        var register = await RequireRegisterAsync(deviceToken, forUpdate: false, ct);
        var sellers = await registers.ListSellersAsync(register.StoreId, ct);
        return sellers
            .Where(m => m.Has(StaffPermissions.SalesCreate))
            .OrderBy(m => m.StaffUser!.FirstName).ThenBy(m => m.StaffUser!.LastName)
            .Select(m => new RegisterCashierDto(
                m.StaffUserId, m.StaffUser!.FirstName, m.StaffUser.LastName, m.Role.ToString(), m.StaffUser.HasPin))
            .ToList();
    }

    public async Task<StaffSession> PinLoginAsync(
        string? deviceToken, PinLoginRequest request, string? device, CancellationToken ct = default)
    {
        var register = await RequireRegisterAsync(deviceToken, forUpdate: false, ct);
        return await auth.SignInWithPinAsync(request.StaffUserId, request.Pin, register.StoreId, register.Id, device, ct);
    }

    public async Task UnlockAsync(string? deviceToken, string? pin, CancellationToken ct = default)
    {
        var register = await RequireRegisterAsync(deviceToken, forUpdate: false, ct);
        await auth.VerifyPinAsync(pin, register.StoreId, register.Id, ct);
    }

    public async Task LockAsync(string? deviceToken, CancellationToken ct = default)
    {
        // ТЗ «Касса» §16.6: автоблок та аудитке жазылады.
        var register = await RequireRegisterAsync(deviceToken, forUpdate: false, ct);
        audit.Record(Entry("register.lock", register, null));
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<Register?> FindAsync(string? deviceToken, bool forUpdate, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(deviceToken)) return null;
        var register = await registers.GetByDeviceHashAsync(tokens.Hash(deviceToken), forUpdate, ct);
        return register is { IsActive: true, Store.IsActive: true } ? register : null;
    }

    private async Task<Register> RequireRegisterAsync(string? deviceToken, bool forUpdate, CancellationToken ct) =>
        await FindAsync(deviceToken, forUpdate, ct)
        ?? throw new ForbiddenException(Messages.RegisterNotActivated(Lang));

    private AuditEntry Entry(string action, Register register, string? details) => new()
    {
        Id = Guid.NewGuid(),
        StoreId = register.StoreId,
        RegisterId = register.Id,
        StaffUserId = current.IsAuthenticated ? current.StaffUserId : null,
        Action = action,
        Details = details,
        Ip = current.Ip,
    };

    private static RegisterDto ToDto(Register r) => new(
        r.Id, r.Name, r.StoreId, r.Store?.Name ?? string.Empty, r.Store?.Address,
        r.AutoLockMinutes, r.DeviceTokenHash is not null, r.ActivatedAt);
}
