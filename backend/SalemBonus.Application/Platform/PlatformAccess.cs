using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Core.Staff;
using SalemBonus.Application.Pos.Sales;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Platform;

namespace SalemBonus.Application.Platform;

/// <summary>
/// SalemTech әкімшілігінің ортақ бөлігі: әкімшіні тексеру (өшірілген әкімшінің токені бірден жарамсыз), уақыт (Алматы), төлем күйі, журнал.
/// </summary>
public class PlatformAccess(ICurrentAdmin current, IPlatformRepository repo, ICurrentLanguage language, IOptions<StaffAuthOptions> authOptions)
{
    public AppLanguage Lang => language.Value;
    public StaffAuthOptions AuthOptions => authOptions.Value;
    public static readonly TimeZoneInfo Zone = StoreTime.Zone(new Store { TimeZone = "Asia/Almaty" });
    public DateOnly Today => StoreTime.Today(Zone);
    public string? Ip => current.Ip;

    public async Task<PlatformAdmin> RequireAsync(CancellationToken ct)
    {
        var admin = await repo.AdminAsync(current.AdminId, ct);
        if (admin is null || !admin.IsActive) throw new UnauthorizedException(Messages.SessionExpired(Lang));
        return admin;
    }

    public void Audit(PlatformAdmin admin, string action, Guid? orgId, string? entity, object? entityId, object? data) =>
        repo.AddAudit(new PlatformAuditEntry
        {
            Id = Guid.NewGuid(), AdminId = admin.Id, Action = action, OrganizationId = orgId, Entity = entity, EntityId = entityId?.ToString(),
            Data = data is null ? null : JsonSerializer.Serialize(data), Ip = current.Ip, CreatedAt = DateTime.UtcNow,
        });

    /// <summary>Төлем күйі: тоқтатылған; төленген (7 күн қалса — «жақында»); сынақ; мерзімі өткен; ешқашан төлемеген.</summary>
    public static string Status(Organization o, DateOnly today)
    {
        if (!o.IsActive) return "suspended";
        if (o.PaidUntil is { } paid && paid >= today) return paid <= today.AddDays(7) ? "dueSoon" : "active";
        if (o.TrialUntil is { } trial && trial >= today) return "trial";
        return o.PaidUntil is null ? "unpaid" : "overdue";
    }

    public static int? DaysOverdue(Organization o, DateOnly today)
    {
        var end = o.PaidUntil ?? o.TrialUntil;
        return end is { } e && e < today ? today.DayNumber - e.DayNumber : null;
    }

    public static decimal Fee(Organization o, PlatformPlan? plan) => o.MonthlyFee ?? plan?.MonthlyPrice ?? 0;

    public List<string> Modules(IReadOnlyList<string>? raw)
    {
        var list = new List<string>();
        foreach (var m in raw ?? [])
        {
            var key = m.Trim().ToLowerInvariant();
            if (!PlatformModules.All.Contains(key)) throw new ValidationException(Messages.BusinessModuleUnknown(Lang, m), "modules");
            if (!list.Contains(key)) list.Add(key);
        }
        return PlatformModules.All.Where(list.Contains).ToList();
    }

    public string Email(string? raw)
    {
        var email = raw?.Trim().ToLowerInvariant() ?? string.Empty;
        try
        {
            if (email.Length is < 5 or > 200 || new MailAddress(email).Address != email) throw new FormatException();
        }
        catch (FormatException) { throw new ValidationException(Messages.AdminEmailInvalid(Lang), "email"); }
        return email;
    }

    /// <summary>12 таңба: әріп пен цифр міндетті, шатастыратын таңбаларсыз (уақытша құпиясөз — бір рет көрсетіледі).</summary>
    public static string TempPassword()
    {
        const string letters = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ";
        const string digits = "23456789";
        const string all = letters + digits;
        var chars = new char[12];
        chars[0] = letters[RandomNumberGenerator.GetInt32(letters.Length)];
        chars[1] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        for (var i = 2; i < chars.Length; i++) chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        RandomNumberGenerator.Shuffle(chars.AsSpan());
        return new string(chars);
    }
}
