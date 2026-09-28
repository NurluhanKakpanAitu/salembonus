using Microsoft.EntityFrameworkCore;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Infrastructure.Persistence;

/// <summary>
/// Тек локал разработка: демо дүкенге аударым реквизиттері (ТЗ «Касса» §9.4), кассада «Перевод»
/// бірден тексерілсін. Нөмірлер ойдан шығарылған.
/// </summary>
public static class CashierDemoSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        var storeId = StaffSeeder.DemoStoreId;
        if (!await db.Stores.AnyAsync(s => s.Id == storeId, ct)) return;
        if (await db.TransferRecipients.AnyAsync(r => r.StoreId == storeId, ct)) return;

        db.TransferRecipients.AddRange(
            new TransferRecipient { Id = Guid.NewGuid(), StoreId = storeId, BankName = "Kaspi Bank", Account = "+7 700 000 00 11", HolderName = "Максатбек А.", SortOrder = 1 },
            new TransferRecipient { Id = Guid.NewGuid(), StoreId = storeId, BankName = "Kaspi Bank", Account = "+7 700 000 00 12", HolderName = "Алишер Р.", SortOrder = 2 },
            new TransferRecipient { Id = Guid.NewGuid(), StoreId = storeId, BankName = "Halyk Bank", Account = "+7 700 000 00 13", HolderName = "Камиль С.", SortOrder = 3 });
        await db.SaveChangesAsync(ct);
    }
}
