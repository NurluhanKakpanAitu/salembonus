using Microsoft.EntityFrameworkCore;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Entities;

namespace SalemBonus.Infrastructure.Persistence.Repositories;

public class CustomerRepository(AppDbContext db) : ICustomerRepository
{
    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Customer?> GetByPhoneAsync(string phone, CancellationToken ct = default) =>
        db.Customers.FirstOrDefaultAsync(c => c.Phone == phone, ct);

    public Task<Customer?> GetByQrCodeAsync(string qrCode, CancellationToken ct = default) =>
        db.Customers.FirstOrDefaultAsync(c => c.QrCode == qrCode, ct);

    public void Add(Customer customer) => db.Customers.Add(customer);
}
