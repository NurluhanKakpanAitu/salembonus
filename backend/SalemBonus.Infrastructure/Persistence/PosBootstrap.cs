using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalemBonus.Application.BonusCards;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Pos;
using SalemBonus.Infrastructure.Identity;

namespace SalemBonus.Infrastructure.Persistence;

/// <summary>
/// Продакшнда SalemPos-тың бірінші иесін жасау: тіркелу беті жоқ, ал демо сидер тек Development-те.
/// Серверде бір рет шақырылады:
/// <code>
/// docker compose -f docker-compose.prod.yml exec api dotnet SalemBonus.Api.dll pos stores
/// docker compose -f docker-compose.prod.yml exec api dotnet SalemBonus.Api.dll pos create-owner \
///     --store &lt;id&gt; --phone +77001234567 --first-name Максатбек --last-name Абдужаббаров [--org "MKM AUTO"] [--bin 123456789012]
/// </code>
/// Дүкенге ұйым байланады (жоқ болса жасалады, әдепкі өлшем бірліктерімен), иесі мен «Касса №1» қосылады. Құпиясөз кездейсоқ
/// жасалып, экранға бір рет шығады — иесі кіргеннен кейін профильде ауыстырады.
/// <para>
/// WhatsApp қосылғанша қалпына келтіру үшін: <c>pos reset-password --phone ..</c> (жаңа уақытша құпиясөз,
/// PIN мен барлық сеанс өшеді), <c>pos deactivate --phone ..</c> (қызметкер кіре алмайды).
/// </para>
/// </summary>
public static class PosBootstrap
{
    public static async Task<int> RunAsync(IServiceProvider services, string[] args, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        switch (args.FirstOrDefault())
        {
            case "stores":
                return await ListStoresAsync(db, ct);
            case "create-owner":
                return await CreateOwnerAsync(db, Parse(args.Skip(1)), ct);
            case "reset-password":
                return await ResetPasswordAsync(db, Parse(args.Skip(1)), ct);
            case "deactivate":
                return await DeactivateAsync(db, Parse(args.Skip(1)), ct);
            default:
                Console.Error.WriteLine("""
                    Командалар:
                      pos stores
                      pos create-owner --store <id> --phone <+7...> --first-name <..> --last-name <..> [--org <..>] [--bin <..>]
                      pos reset-password --phone <+7...>
                      pos deactivate --phone <+7...>
                    """);
                return 2;
        }
    }

    private static async Task<int> ListStoresAsync(AppDbContext db, CancellationToken ct)
    {
        var stores = await db.Stores.AsNoTracking().OrderBy(s => s.Name).ToListAsync(ct);
        var orgs = await db.Organizations.AsNoTracking().ToDictionaryAsync(o => o.Id, o => o.Name, ct);
        foreach (var s in stores)
        {
            var org = s.OrganizationId is { } id && orgs.TryGetValue(id, out var name) ? name : "—";
            Console.WriteLine($"{s.Id}  {s.Name}  (ұйым: {org})");
        }
        return 0;
    }

    private static async Task<int> CreateOwnerAsync(AppDbContext db, Dictionary<string, string> o, CancellationToken ct)
    {
        string Required(string key) => o.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v)
            ? v.Trim()
            : throw new ArgumentException($"--{key} міндетті");

        try
        {
            var storeId = Guid.Parse(Required("store"));
            var phone = BonusRules.NormalizePhone(Required("phone"));
            if (phone.Length != 12) throw new ArgumentException("--phone: +7XXXXXXXXXX пішімінде болсын");
            var firstName = Required("first-name");
            var lastName = Required("last-name");

            var store = await db.Stores.FirstOrDefaultAsync(s => s.Id == storeId, ct)
                        ?? throw new ArgumentException("Мұндай дүкен жоқ. Тізім: pos stores");
            if (await db.StaffUsers.AnyAsync(u => u.Phone == phone, ct))
                throw new ArgumentException($"{phone} нөмірімен қызметкер бар");

            await using var tx = await db.Database.BeginTransactionAsync(ct);

            var org = store.OrganizationId is { } orgId ? await db.Organizations.FirstAsync(x => x.Id == orgId, ct) : null;
            if (org is null)
            {
                org = new Organization
                {
                    Id = Guid.NewGuid(),
                    Name = o.GetValueOrDefault("org")?.Trim() is { Length: > 0 } n ? n : store.Name,
                    Bin = o.GetValueOrDefault("bin")?.Trim(),
                };
                db.Organizations.Add(org);
                store.OrganizationId = org.Id;
            }

            // Әдепкі өлшем бірліктері (шт, кг, л…): онсыз тауар жасау мүмкін емес. Бар болса — қосылмайды.
            await CatalogDefaults.EnsureUnitsAsync(db, org.Id, ct);

            var password = GeneratePassword();
            var userId = Guid.NewGuid();
            db.StaffUsers.Add(new StaffUser
            {
                Id = userId,
                OrganizationId = org.Id,
                Phone = phone,
                FirstName = firstName,
                LastName = lastName,
                PasswordHash = new Pbkdf2PasswordHasher().Hash(password),
                Memberships =
                [
                    new StoreMembership
                    {
                        Id = Guid.NewGuid(),
                        StaffUserId = userId,
                        StoreId = store.Id,
                        Role = StaffRole.Owner,
                        Permissions = [.. StaffPermissions.DefaultsFor(StaffRole.Owner)],
                        MaxDiscountPercent = 100,
                    },
                ],
            });

            var registerAdded = false;
            if (!await db.Registers.AnyAsync(r => r.StoreId == store.Id, ct))
            {
                db.Registers.Add(new Register { Id = Guid.NewGuid(), StoreId = store.Id, Name = "Касса №1" });
                registerAdded = true;
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            Console.WriteLine($"Дайын: {firstName} {lastName} ({phone}) — «{store.Name}» дүкенінің иесі, ұйым «{org.Name}».");
            if (registerAdded) Console.WriteLine("«Касса №1» қосылды.");
            Console.WriteLine($"Уақытша құпиясөз: {password}");
            Console.WriteLine("Иесіне жеке беріңіз. Кіргеннен кейін профильде ауыстырып, PIN-код қойсын.");
            return 0;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
    }

    private static async Task<int> ResetPasswordAsync(AppDbContext db, Dictionary<string, string> o, CancellationToken ct)
    {
        var user = await FindStaffAsync(db, o, ct);
        if (user is null) return 2;

        var password = GeneratePassword();
        user.PasswordHash = new Pbkdf2PasswordHasher().Hash(password);
        user.IsActive = true;
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        // PIN де ескі құпиямен бірге жарамсыз болсын: иесі кіргеннен кейін жаңасын қояды.
        user.PinHash = null;
        user.PinFailedCount = 0;
        user.PinLockedUntil = null;
        await RevokeSessionsAsync(db, user.Id, ct);
        await db.SaveChangesAsync(ct);

        Console.WriteLine($"{user.FirstName} {user.LastName} ({user.Phone}): барлық сеанс жабылды, PIN өшірілді.");
        Console.WriteLine($"Уақытша құпиясөз: {password}");
        return 0;
    }

    private static async Task<int> DeactivateAsync(AppDbContext db, Dictionary<string, string> o, CancellationToken ct)
    {
        var user = await FindStaffAsync(db, o, ct);
        if (user is null) return 2;

        user.IsActive = false;
        await RevokeSessionsAsync(db, user.Id, ct);
        await db.SaveChangesAsync(ct);
        Console.WriteLine($"{user.FirstName} {user.LastName} ({user.Phone}) өшірілді, барлық сеанс жабылды.");
        return 0;
    }

    private static async Task<StaffUser?> FindStaffAsync(AppDbContext db, Dictionary<string, string> o, CancellationToken ct)
    {
        if (!o.TryGetValue("phone", out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            Console.Error.WriteLine("--phone міндетті");
            return null;
        }
        var phone = BonusRules.NormalizePhone(raw);
        var user = await db.StaffUsers.FirstOrDefaultAsync(u => u.Phone == phone, ct);
        if (user is null) Console.Error.WriteLine($"{phone} нөмірімен қызметкер жоқ");
        return user;
    }

    private static async Task RevokeSessionsAsync(AppDbContext db, Guid userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await db.StaffRefreshTokens.Where(t => t.StaffUserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(x => x.SetProperty(t => t.RevokedAt, now), ct);
    }

    private static Dictionary<string, string> Parse(IEnumerable<string> args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? key = null;
        foreach (var a in args)
        {
            if (a.StartsWith("--")) key = a[2..];
            else if (key is not null) { result[key] = result.TryGetValue(key, out var prev) ? $"{prev} {a}" : a; }
        }
        return result;
    }

    /// <summary>12 таңба: әріп пен цифр міндетті (құпиясөз саясатына сай), шатастыратын таңбаларсыз.</summary>
    private static string GeneratePassword()
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
