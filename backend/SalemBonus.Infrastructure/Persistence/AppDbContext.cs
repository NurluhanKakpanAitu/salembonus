using Microsoft.EntityFrameworkCore;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Pos;
using SalemBonus.Domain.Pos.Catalog;
using SalemBonus.Domain.Pos.Inventory;

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

    // Ортақ ядро (core)
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<StaffUser> StaffUsers => Set<StaffUser>();
    public DbSet<StoreMembership> StoreMemberships => Set<StoreMembership>();
    public DbSet<StaffRefreshToken> StaffRefreshTokens => Set<StaffRefreshToken>();
    public DbSet<StaffOtpCode> StaffOtpCodes => Set<StaffOtpCode>();
    public DbSet<AuditEntry> AuditLog => Set<AuditEntry>();

    // SalemPos (pos)
    public DbSet<Register> Registers => Set<Register>();
    public DbSet<CatalogNode> CatalogNodes => Set<CatalogNode>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<MeasureUnit> Units => Set<MeasureUnit>();
    public DbSet<CharacteristicDefinition> Characteristics => Set<CharacteristicDefinition>();
    public DbSet<CharacteristicOption> CharacteristicOptions => Set<CharacteristicOption>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductBarcode> ProductBarcodes => Set<ProductBarcode>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductCharacteristicValue> ProductCharacteristics => Set<ProductCharacteristicValue>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<StockBalance> StockBalances => Set<StockBalance>();
    public DbSet<ProductPrice> ProductPrices => Set<ProductPrice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
