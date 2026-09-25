using Microsoft.EntityFrameworkCore;
using SalemBonus.Domain.Entities;

namespace SalemBonus.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<StoreLevel> StoreLevels => Set<StoreLevel>();
    public DbSet<KatoEntry> Kato => Set<KatoEntry>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<BonusCard> BonusCards => Set<BonusCard>();
    public DbSet<BonusTransaction> BonusTransactions => Set<BonusTransaction>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
