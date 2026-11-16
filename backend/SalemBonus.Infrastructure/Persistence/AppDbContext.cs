using Microsoft.EntityFrameworkCore;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Pos;
using SalemBonus.Domain.Pos.Catalog;
using SalemBonus.Domain.Pos.Inventory;
using SalemBonus.Domain.Pos.Finance;
using SalemBonus.Domain.Pos.Bonus;
using SalemBonus.Domain.Pos.Clients;
using SalemBonus.Domain.Platform;
using SalemBonus.Domain.Pos.Sales;

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
    public DbSet<PushDevice> PushDevices => Set<PushDevice>();
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
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<StockDocument> StockDocuments => Set<StockDocument>();
    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();
    public DbSet<DocumentCounter> DocumentCounters => Set<DocumentCounter>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<SalePayment> SalePayments => Set<SalePayment>();
    public DbSet<ReceiptCounter> ReceiptCounters => Set<ReceiptCounter>();
    public DbSet<TransferRecipient> TransferRecipients => Set<TransferRecipient>();
    public DbSet<StoreCashierSettings> CashierSettings => Set<StoreCashierSettings>();
    public DbSet<Debt> Debts => Set<Debt>();
    public DbSet<DebtPayment> DebtPayments => Set<DebtPayment>();
    public DbSet<SaleReturn> SaleReturns => Set<SaleReturn>();
    public DbSet<SaleReturnItem> SaleReturnItems => Set<SaleReturnItem>();
    public DbSet<StoreNotification> StoreNotifications => Set<StoreNotification>();

    // Финанс (ТЗ «Финансы»)
    public DbSet<FinanceAccount> FinanceAccounts => Set<FinanceAccount>();
    public DbSet<FinanceCategory> FinanceCategories => Set<FinanceCategory>();
    public DbSet<FinanceOperation> FinanceOperations => Set<FinanceOperation>();
    public DbSet<FinanceEntry> FinanceEntries => Set<FinanceEntry>();
    public DbSet<LoyaltyStatus> LoyaltyStatuses => Set<LoyaltyStatus>();
    public DbSet<BonusSettings> BonusSettings => Set<BonusSettings>();
    public DbSet<BirthdayGrant> BirthdayGrants => Set<BirthdayGrant>();
    public DbSet<ExpiryWarning> ExpiryWarnings => Set<ExpiryWarning>();
    public DbSet<BonusCampaign> BonusCampaigns => Set<BonusCampaign>();
    public DbSet<CampaignRecipient> CampaignRecipients => Set<CampaignRecipient>();
    public DbSet<BonusTerms> BonusTerms => Set<BonusTerms>();
    public DbSet<ClientProfile> ClientProfiles => Set<ClientProfile>();
    public DbSet<ClientNote> ClientNotes => Set<ClientNote>();
    public DbSet<ClientSegment> ClientSegments => Set<ClientSegment>();
    public DbSet<ClientSettings> ClientSettings => Set<ClientSettings>();
    public DbSet<ClientCommunication> ClientCommunications => Set<ClientCommunication>();
    public DbSet<ClientMergeLog> ClientMergeLog => Set<ClientMergeLog>();
    public DbSet<ClientImport> ClientImports => Set<ClientImport>();
    public DbSet<PlatformAdmin> PlatformAdmins => Set<PlatformAdmin>();
    public DbSet<PlatformPlan> PlatformPlans => Set<PlatformPlan>();
    public DbSet<PlatformPayment> PlatformPayments => Set<PlatformPayment>();
    public DbSet<PlatformAuditEntry> PlatformAudit => Set<PlatformAuditEntry>();
    public DbSet<PlatformLead> PlatformLeads => Set<PlatformLead>();

    /// <summary>
    /// Іздеу үшін мәтінді кіші әріпке келтіру — латын, орыс және қазақ әріптері (SQL-де <c>salem_fold</c>, миграция SearchFold).
    /// Базаның локалі C болса, <c>lower</c>/<c>ILIKE</c> кириллицаны теңестірмейді; бұл функция локальға тәуелсіз.
    /// </summary>
    public static string Fold(string text) => throw new NotSupportedException("Тек LINQ сұранысында (salem_fold)");

    /// <summary>
    /// Кассаның жергілікті каталогы (CatalogSyncAsync) өзгерістерді уақыт белгісімен алады. Сондықтан бағасы өзгерсе — баға
    /// белгісі, штрихкод/сурет/сипаттама қосылса, өзгерсе не өшсе — тауардың белгісі мұнда өзі жаңарады (әр сервисте ұмытылмасын).
    /// </summary>
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        foreach (var e in ChangeTracker.Entries<ProductPrice>().Where(e => e.State == EntityState.Modified))
            e.Entity.UpdatedAt = now;
        var touched = ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => e.Entity switch
            {
                ProductBarcode b => (Guid?)b.ProductId,
                ProductImage i => i.ProductId,
                ProductCharacteristicValue c => c.ProductId,
                _ => null,
            })
            .OfType<Guid>().ToHashSet();
        foreach (var e in ChangeTracker.Entries<Product>().Where(e => e.State != EntityState.Deleted && (touched.Contains(e.Entity.Id) || e.State == EntityState.Modified)))
        {
            e.Entity.UpdatedAt = now;
            if (e.State != EntityState.Added) e.Property(p => p.UpdatedAt).IsModified = true; // тек белгі — басқа бағандар қайта жазылмайды
            touched.Remove(e.Entity.Id);
        }
        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
        // Тауардың өзі жүктелмеген болса (тек штрихкод не сурет өзгерді) — белгісі бөлек жаңарады.
        if (touched.Count > 0)
            await Products.Where(p => touched.Contains(p.Id)).ExecuteUpdateAsync(s => s.SetProperty(p => p.UpdatedAt, now), ct);
        return result;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        modelBuilder.HasDbFunction(typeof(AppDbContext).GetMethod(nameof(Fold))!).HasName("salem_fold").HasSchema("public");
    }
}
