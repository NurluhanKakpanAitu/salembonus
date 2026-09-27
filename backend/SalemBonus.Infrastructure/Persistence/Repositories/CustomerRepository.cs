using Microsoft.EntityFrameworkCore;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Entities;

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

        var cardIds = db.BonusCards.Where(c => c.CustomerId == customerId).Select(c => c.Id);
        await db.BonusTransactions.Where(t => cardIds.Contains(t.BonusCardId)).ExecuteDeleteAsync(ct);
        await db.BonusCards.Where(c => c.CustomerId == customerId).ExecuteDeleteAsync(ct);
        await db.Notifications.Where(n => n.CustomerId == customerId).ExecuteDeleteAsync(ct);
        await db.RefreshTokens.Where(r => r.CustomerId == customerId).ExecuteDeleteAsync(ct);
        if (phone is not null)
            await db.OtpCodes.Where(o => o.Phone == phone).ExecuteDeleteAsync(ct);
        await db.Customers.Where(c => c.Id == customerId).ExecuteDeleteAsync(ct);
    }
}
