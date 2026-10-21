using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Core.Staff;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Domain.Platform;

namespace SalemBonus.Application.Platform;

/// <summary>
/// Әкімшінің кіруі (admin.salemtech.kz): e-mail + құпиясөз, 5 қатеден кейін 15 минут бұғат, токен — жұмыс күніне (refresh-сіз).
/// Әкімшілерді басқару: қосу (уақытша құпиясөз бір рет көрсетіледі), өшіру, құпиясөзді қалпына келтіру.
/// </summary>
public class PlatformAuthService(PlatformAccess access, IPlatformRepository repo, IPasswordHasher hasher, ITokenService tokens, IUnitOfWork unitOfWork)
{
    private AppLanguage Lang => access.Lang;

    public static AdminDto Dto(PlatformAdmin a) => new(a.Id, a.Email, a.Name, a.IsActive, a.LastLoginAt, a.CreatedAt);

    public async Task<AdminSessionDto> LoginAsync(AdminLoginRequest r, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(r.Email) || string.IsNullOrWhiteSpace(r.Password))
            throw new ValidationException(Messages.AdminCredentialsRequired(Lang), "email");
        var email = r.Email.Trim().ToLowerInvariant();
        var admin = await repo.AdminByEmailAsync(email, ct);
        var now = DateTime.UtcNow;
        if (admin is null || !admin.IsActive) throw new ValidationException(Messages.AdminWrongCredentials(Lang), "password");
        if (admin.IsLockedOut(now))
            throw new ValidationException(Messages.AdminLockedOut(Lang, (int)Math.Ceiling((admin.LockedUntil!.Value - now).TotalMinutes)), "password");
        if (!hasher.Verify(r.Password, admin.PasswordHash))
        {
            admin.FailedLoginCount++;
            if (admin.FailedLoginCount >= PlatformAdmin.MaxFailedLogins)
            {
                admin.LockedUntil = now.Add(PlatformAdmin.LockoutDuration);
                admin.FailedLoginCount = 0;
            }
            repo.AddAudit(new PlatformAuditEntry { Id = Guid.NewGuid(), AdminId = admin.Id, Action = "admin.login.failed", Ip = access.Ip, CreatedAt = now });
            await unitOfWork.SaveChangesAsync(ct);
            throw new ValidationException(Messages.AdminWrongCredentials(Lang), "password");
        }
        admin.FailedLoginCount = 0;
        admin.LockedUntil = null;
        admin.LastLoginAt = now;
        repo.AddAudit(new PlatformAuditEntry { Id = Guid.NewGuid(), AdminId = admin.Id, Action = "admin.login", Ip = access.Ip, CreatedAt = now });
        await unitOfWork.SaveChangesAsync(ct);
        var token = tokens.CreateAdminAccessToken(admin.Id, admin.Email);
        return new AdminSessionDto(token.Token, token.ExpiresAt, Dto(admin));
    }

    public async Task<AdminDto> MeAsync(CancellationToken ct = default) => Dto(await access.RequireAsync(ct));

    public async Task ChangePasswordAsync(AdminChangePasswordRequest r, CancellationToken ct = default)
    {
        var admin = await access.RequireAsync(ct);
        if (string.IsNullOrEmpty(r.CurrentPassword) || !hasher.Verify(r.CurrentPassword, admin.PasswordHash))
            throw new ValidationException(Messages.AdminWrongPassword(Lang), "currentPassword");
        var password = r.NewPassword ?? string.Empty;
        CredentialRules.ValidatePassword(password, access.AuthOptions, Lang);
        admin.PasswordHash = hasher.Hash(password);
        access.Audit(admin, "admin.password.change", null, "admin", admin.Id, null);
        await unitOfWork.SaveChangesAsync(ct);
    }

    // ---------- Әкімшілер ----------

    public async Task<IReadOnlyList<AdminDto>> AdminsAsync(CancellationToken ct = default)
    {
        await access.RequireAsync(ct);
        return (await repo.AdminsAsync(ct)).Select(Dto).ToList();
    }

    public async Task<AdminCreatedDto> CreateAdminAsync(CreateAdminRequest r, CancellationToken ct = default)
    {
        var me = await access.RequireAsync(ct);
        var email = access.Email(r.Email);
        var name = CatalogAccess.Optional(r.Name, 120) ?? throw new ValidationException(Messages.AdminNameRequired(Lang), "name");
        if (await repo.AdminByEmailAsync(email, ct) is not null) throw new ValidationException(Messages.AdminEmailExists(Lang), "email");
        var password = PlatformAccess.TempPassword();
        var admin = new PlatformAdmin { Id = Guid.NewGuid(), Email = email, Name = name, PasswordHash = hasher.Hash(password), CreatedAt = DateTime.UtcNow };
        repo.AddAdmin(admin);
        access.Audit(me, "admin.create", null, "admin", admin.Id, new { email, name });
        await unitOfWork.SaveChangesAsync(ct);
        return new AdminCreatedDto(Dto(admin), password);
    }

    public async Task<AdminDto> SetAdminActiveAsync(Guid id, bool active, CancellationToken ct = default)
    {
        var me = await access.RequireAsync(ct);
        if (id == me.Id && !active) throw new ValidationException(Messages.AdminSelf(Lang));
        var admin = await repo.AdminAsync(id, ct) ?? throw new NotFoundException(Messages.AdminNotFound(Lang));
        admin.IsActive = active;
        access.Audit(me, active ? "admin.activate" : "admin.deactivate", null, "admin", admin.Id, new { admin.Email });
        await unitOfWork.SaveChangesAsync(ct);
        return Dto(admin);
    }

    public async Task<AdminCreatedDto> ResetAdminPasswordAsync(Guid id, CancellationToken ct = default)
    {
        var me = await access.RequireAsync(ct);
        var admin = await repo.AdminAsync(id, ct) ?? throw new NotFoundException(Messages.AdminNotFound(Lang));
        var password = PlatformAccess.TempPassword();
        admin.PasswordHash = hasher.Hash(password);
        admin.FailedLoginCount = 0;
        admin.LockedUntil = null;
        access.Audit(me, "admin.password.reset", null, "admin", admin.Id, new { admin.Email });
        await unitOfWork.SaveChangesAsync(ct);
        return new AdminCreatedDto(Dto(admin), password);
    }
}
