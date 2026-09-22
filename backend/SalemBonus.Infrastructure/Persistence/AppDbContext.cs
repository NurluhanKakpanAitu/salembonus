using Microsoft.EntityFrameworkCore;
using SalemBonus.Domain.Entities;

namespace SalemBonus.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<BonusCard> BonusCards => Set<BonusCard>();
    public DbSet<BonusTransaction> BonusTransactions => Set<BonusTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
