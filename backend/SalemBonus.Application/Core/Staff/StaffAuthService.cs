using SalemBonus.Application.BonusCards;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Core.Staff.Dtos;
using SalemBonus.Domain.Core;

namespace SalemBonus.Application.Core.Staff;

/// <summary>
/// Қызметкердің кіруі: телефон + құпиясөз (ТЗ «Авторизация»). Сәтсіз әрекеттер санақталады,
/// шектен асса аккаунт уақытша бұғатталады. Кіру әрекеттерінің бәрі аудитке жазылады.
/// </summary>
public class StaffAuthService(
    IStaffRepository staff,
    IStaffRefreshTokenRepository refreshTokens,
    IPasswordHasher hasher,
    ITokenService tokens,
    IAuditLog audit,
    ICurrentStaff current,
    IUnitOfWork unitOfWork,
    ICurrentLanguage language) : IStaffAuthService
{
    public static readonly string[] SupportedLanguages = ["ru", "kk"];

    private AppLanguage Lang => language.Value;

    public async Task<StaffSession> LoginAsync(StaffLoginRequest request, CancellationToken ct = default)
    {
        // ТЗ §4: алдымен өрістер, сосын формат — серверге бос сұраныс жібермейміз.
        if (string.IsNullOrWhiteSpace(request.Phone))
            throw new ValidationException(Messages.StaffPhoneRequired(Lang), "phone");
        if (string.IsNullOrWhiteSpace(request.Password))
            throw new ValidationException(Messages.StaffPasswordRequired(Lang), "password");
        var phone = ParsePhone(request.Phone);

        var now = DateTime.UtcNow;
        var user = await staff.GetByPhoneForUpdateAsync(phone, ct);
        if (user is null)
        {
            await FailAsync(null, phone, "not_found", ct);
            throw new ValidationException(Messages.StaffNotFound(Lang), "phone");
        }
        if (user.IsLockedOut(now))
        {
            var minutes = (int)Math.Ceiling((user.LockedUntil!.Value - now).TotalMinutes);
            await FailAsync(user, phone, "locked", ct);
            throw new ValidationException(Messages.StaffLockedOut(Lang, minutes), "password");
        }
        if (!user.IsActive)
        {
            await FailAsync(user, phone, "disabled", ct);
            throw new ValidationException(Messages.StaffDisabled(Lang), "phone");
        }
        if (!hasher.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= StaffUser.MaxFailedLogins)
            {
                user.LockedUntil = now.Add(StaffUser.LockoutDuration);
                user.FailedLoginCount = 0;
            }
            await FailAsync(user, phone, "wrong_password", ct);
            throw new ValidationException(Messages.StaffWrongPassword(Lang), "password");
        }

        var me = ToMe(user);
        if (me.Stores.Count == 0)
        {
            await FailAsync(user, phone, "no_stores", ct);
            throw new ValidationException(Messages.StaffNoStores(Lang), "phone");
        }

        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.LastLoginAt = now;
        var session = Issue(user, me, request.Device, now);
        audit.Record(Entry(user, "auth.login", true, null));
        await unitOfWork.SaveChangesAsync(ct);
        return session;
    }

    public async Task<StaffSession> RefreshAsync(string? refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new UnauthorizedException(Messages.SessionExpired(Lang));

        var now = DateTime.UtcNow;
        var stored = await refreshTokens.GetByHashAsync(tokens.Hash(refreshToken), ct);
        if (stored is null || !stored.IsActive(now))
            throw new UnauthorizedException(Messages.SessionExpired(Lang));

        var user = await staff.GetByIdForUpdateAsync(stored.StaffUserId, ct);
        // Иесі қызметкерді өшірсе немесе дүкеннен алып тастаса, келесі жаңартуда сеанс жабылады.
        if (user is null || !user.IsActive)
            throw new UnauthorizedException(Messages.SessionExpired(Lang));
        var me = ToMe(user);
        if (me.Stores.Count == 0)
            throw new UnauthorizedException(Messages.SessionExpired(Lang));

        stored.RevokedAt = now;
        var session = Issue(user, me, stored.Device, now);
        await unitOfWork.SaveChangesAsync(ct);
        return session;
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;
        var stored = await refreshTokens.GetByHashAsync(tokens.Hash(refreshToken), ct);
        if (stored is null || stored.RevokedAt is not null) return;
        stored.RevokedAt = DateTime.UtcNow;
        audit.Record(new AuditEntry
        {
            Id = Guid.NewGuid(),
            StaffUserId = stored.StaffUserId,
            Action = "auth.logout",
            Ip = current.Ip,
        });
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<StaffMeDto> GetMeAsync(CancellationToken ct = default)
    {
        var user = await staff.GetByIdAsync(current.StaffUserId, ct)
            ?? throw new UnauthorizedException(Messages.SessionExpired(Lang));
        return ToMe(user);
    }

    public async Task<StaffMeDto> SetLanguageAsync(string language, CancellationToken ct = default)
    {
        var lang = language.Trim().ToLowerInvariant();
        if (!SupportedLanguages.Contains(lang))
            throw new ValidationException(Messages.StaffLanguageInvalid(Lang), "language");

        var user = await staff.GetByIdForUpdateAsync(current.StaffUserId, ct)
            ?? throw new UnauthorizedException(Messages.SessionExpired(Lang));
        user.Language = lang;
        await unitOfWork.SaveChangesAsync(ct);
        return ToMe(user);
    }

    private StaffSession Issue(StaffUser user, StaffMeDto me, string? device, DateTime now)
    {
        var access = tokens.CreateStaffAccessToken(user.Id, user.Phone);
        var refreshValue = tokens.CreateRefreshTokenValue();
        var refreshExpires = now.Add(tokens.RefreshTokenLifetime);
        refreshTokens.Add(new StaffRefreshToken
        {
            Id = Guid.NewGuid(),
            StaffUserId = user.Id,
            TokenHash = tokens.Hash(refreshValue),
            ExpiresAt = refreshExpires,
            Device = device is { Length: > 200 } ? device[..200] : device,
            CreatedAt = now,
        });
        return new StaffSession(access.Token, access.ExpiresAt, refreshValue, refreshExpires, me);
    }

    /// <summary>Сәтсіз әрекетті аудитке жазып, санақты сақтайды — қате лақтырылса да жоғалмауы керек.</summary>
    private async Task FailAsync(StaffUser? user, string phone, string reason, CancellationToken ct)
    {
        audit.Record(user is null
            ? new AuditEntry { Id = Guid.NewGuid(), Action = "auth.login", Success = false, Details = $"{reason}: {phone}", Ip = current.Ip }
            : Entry(user, "auth.login", false, reason));
        await unitOfWork.SaveChangesAsync(ct);
    }

    private AuditEntry Entry(StaffUser user, string action, bool success, string? details) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = user.OrganizationId,
        StaffUserId = user.Id,
        Action = action,
        Success = success,
        Details = details,
        Ip = current.Ip,
    };

    private static StaffMeDto ToMe(StaffUser user)
    {
        var stores = user.Memberships
            .Where(m => m.IsActive && m.Store is { IsActive: true })
            .OrderBy(m => m.Store!.Name)
            .Select(m => new StaffStoreDto(
                m.StoreId,
                m.Store!.Name,
                m.Store.Address,
                m.Role.ToString(),
                m.Role == StaffRole.Owner ? StaffPermissions.All : m.Permissions))
            .ToList();

        // ТЗ «Статистика» §3: иесі мен статистиканы көре алатын әкімші — Статистика, қалғаны — Касса.
        var startPage = stores.Any(s => s.Permissions.Contains(StaffPermissions.StatisticsView))
            ? "statistics"
            : "cashier";

        return new StaffMeDto(
            user.Id, user.Phone, user.FirstName, user.LastName, user.Language,
            user.OrganizationId, user.Organization?.Name ?? string.Empty, stores, startPage);
    }

    private string ParsePhone(string raw)
    {
        if (!BonusRules.LooksLikePhone(raw))
            throw new ValidationException(Messages.StaffPhoneInvalid(Lang), "phone");
        var phone = BonusRules.NormalizePhone(raw);
        if (phone.Length != 12 || !phone.StartsWith("+7"))
            throw new ValidationException(Messages.StaffPhoneInvalid(Lang), "phone");
        return phone;
    }
}
