using Microsoft.EntityFrameworkCore;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Infrastructure.Persistence.Repositories;

public class CustomerRepository(AppDbContext db) : ICustomerRepository
{
    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Customer?> GetForUpdateAsync(Guid id, CancellationToken ct = default) =>
        db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Customer?> GetByPhoneAsync(string phone, CancellationToken ct = default) =>
        db.Customers.FirstOrDefaultAsync(c => c.Phone == phone, ct);

    public Task<Customer?> GetByQrCodeAsync(string qrCode, CancellationToken ct = default) =>
        db.Customers.FirstOrDefaultAsync(c => c.QrCode == qrCode, ct);

    public void Add(Customer customer) => db.Customers.Add(customer);

    public async Task DeleteAsync(Guid customerId, CancellationToken ct = default)
    {
        // Реті маңызды: алдымен сыртқы кілтпен байланысқандары, соңында клиенттің өзі.
        var phone = await db.Customers
            .Where(c => c.Id == customerId)
            .Select(c => c.Phone)
            .FirstOrDefaultAsync(ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var cardIds = db.BonusCards.Where(c => c.CustomerId == customerId).Select(c => c.Id);
        await db.BonusTransactions.Where(t => cardIds.Contains(t.BonusCardId)).ExecuteDeleteAsync(ct);
        await db.BonusCards.Where(c => c.CustomerId == customerId).ExecuteDeleteAsync(ct);
        await db.Notifications.Where(n => n.CustomerId == customerId).ExecuteDeleteAsync(ct);
        await db.RefreshTokens.Where(r => r.CustomerId == customerId).ExecuteDeleteAsync(ct);
        // Құрылғылар да: клиент жазбасы (қарызы болса) қалса да, өшірілген аккаунтқа push кетпейді.
        await db.PushDevices.Where(d => d.CustomerId == customerId).ExecuteDeleteAsync(ct);
        if (phone is not null)
            await db.OtpCodes.Where(o => o.Phone == phone).ExecuteDeleteAsync(ct);
        await db.ClientProfiles.Where(p => p.CustomerId == customerId).ExecuteDeleteAsync(ct);
        // Чек дүкеннің есебінде қалады, тек кімдікі екені өшеді.
        await db.Sales.Where(s => s.CustomerId == customerId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CustomerId, (Guid?)null), ct);

        if (await db.Debts.AnyAsync(d => d.CustomerId == customerId, ct))
        {
            // Өтелген қарыз бен оның төлемдері — кассаның есебі, өшірілмейді. Сондықтан клиент
            // жазбасы қалады, бірақ жеке деректерсіз: нөмір мен QR басқаға беріле алатындай ауыстырылады.
            var tombstone = customerId.ToString("N");
            var phoneStub = "x" + tombstone[..19];
            var qrStub = "X" + tombstone[..15];
            await db.Customers.Where(c => c.Id == customerId).ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Phone, phoneStub)
                .SetProperty(x => x.QrCode, qrStub)
                .SetProperty(x => x.FirstName, string.Empty)
                .SetProperty(x => x.LastName, string.Empty)
                .SetProperty(x => x.Email, (string?)null)
                .SetProperty(x => x.BirthDate, (DateOnly?)null)
                .SetProperty(x => x.KatoCode, (string?)null)
                .SetProperty(x => x.AvatarUrl, (string?)null), ct);
        }
        else
        {
            await db.Customers.Where(c => c.Id == customerId).ExecuteDeleteAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetOpenDebtStoresAsync(Guid customerId, CancellationToken ct = default) =>
        await db.Debts
            .Where(d => d.CustomerId == customerId && d.Status == DebtStatus.Open)
            .Join(db.Stores, d => d.StoreId, s => s.Id, (d, s) => s.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(ct);
}
