using Microsoft.EntityFrameworkCore;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Pos;
using SalemBonus.Infrastructure.Identity;

namespace SalemBonus.Infrastructure.Persistence;

/// <summary>
/// SalemPos демо қызметкерлері — ТЕК Development ортасында. MKM AUTO дүкеніне бизнес, иесі, кассир
/// және «Касса №1» жасалады. Бір рет қана: бизнес бар болса, ештеңе істемейді.
///
/// Тест құпиялары осы файлда тұр, чатқа не құжатқа жазылмайды.
/// Продакшнға шығар алдында бұл аккаунттарды өшіру керек (SALEMPOS.md, «Продакшнға шығар алдында»).
/// </summary>
public static class StaffSeeder
{
    public static readonly Guid DemoOrganizationId = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    public static readonly Guid DemoStoreId = Guid.Parse("11111111-1111-1111-1111-111111111111"); // MKM AUTO

    public const string OwnerPhone = "+77000000001";
    public const string OwnerPassword = "Demo-Owner-2026";
    public const string CashierPhone = "+77000000002";
    public const string CashierPassword = "Demo-Kassir-2026";

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Organizations.AnyAsync(ct)) return;

        var store = await db.Stores.FirstOrDefaultAsync(s => s.Id == DemoStoreId, ct);
        if (store is null) return;

        var hasher = new Pbkdf2PasswordHasher();
        var org = new Organization { Id = DemoOrganizationId, Name = "MKM AUTO", Bin = "123456789012" };
        db.Organizations.Add(org);
        store.OrganizationId = org.Id;

        db.StaffUsers.AddRange(
            Staff(org.Id, store.Id, OwnerPhone, "Максатбек", "Абдужаббаров", hasher.Hash(OwnerPassword), StaffRole.Owner, 100),
            Staff(org.Id, store.Id, CashierPhone, "Алишер", "Кассир", hasher.Hash(CashierPassword), StaffRole.Cashier, 5));

        db.Registers.Add(new Register { Id = Guid.NewGuid(), StoreId = store.Id, Name = "Касса №1" });

        await db.SaveChangesAsync(ct);
    }

    private static StaffUser Staff(
        Guid orgId, Guid storeId, string phone, string firstName, string lastName, string passwordHash,
        StaffRole role, decimal maxDiscount)
    {
        var id = Guid.NewGuid();
        return new StaffUser
        {
            Id = id,
            OrganizationId = orgId,
            Phone = phone,
            FirstName = firstName,
            LastName = lastName,
            PasswordHash = passwordHash,
            Memberships =
            [
                new StoreMembership
                {
                    Id = Guid.NewGuid(),
                    StaffUserId = id,
                    StoreId = storeId,
                    Role = role,
                    Permissions = [.. StaffPermissions.DefaultsFor(role)],
                    MaxDiscountPercent = maxDiscount,
                },
            ],
        };
    }
}
