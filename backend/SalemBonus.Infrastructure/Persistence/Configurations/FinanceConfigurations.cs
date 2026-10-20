using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalemBonus.Domain.Pos.Finance;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Infrastructure.Persistence.Configurations;

public class FinanceAccountConfiguration : IEntityTypeConfiguration<FinanceAccount>
{
    public void Configure(EntityTypeBuilder<FinanceAccount> b)
    {
        b.ToTable("finance_accounts", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever(); // кілт қолмен беріледі: навигация арқылы қосылған жаңа жазба Added болып қалады (Modified емес)
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.Methods)
            .HasConversion(
                v => v.Select(m => m.ToString()).ToArray(),
                v => v.Select(Enum.Parse<PaymentMethod>).ToList(),
                new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<PaymentMethod>>(
                    (a, c) => a!.SequenceEqual(c!), v => v.Aggregate(0, (h, m) => HashCode.Combine(h, m)), v => v.ToList()));
        b.Property(x => x.Balance).HasPrecision(18, 2);
        b.Property(x => x.OpeningBalance).HasPrecision(18, 2);
        b.Property(x => x.Comment).HasMaxLength(500);
        b.HasIndex(x => new { x.StoreId, x.Name }).IsUnique();
        b.HasIndex(x => x.OrganizationId);
    }
}

public class FinanceCategoryConfiguration : IEntityTypeConfiguration<FinanceCategory>
{
    public void Configure(EntityTypeBuilder<FinanceCategory> b)
    {
        b.ToTable("finance_categories", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever(); // кілт қолмен беріледі: навигация арқылы қосылған жаңа жазба Added болып қалады (Modified емес)
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.PnlClass).HasConversion<string>().HasMaxLength(24);
        b.HasIndex(x => new { x.OrganizationId, x.Type, x.Name }).IsUnique();
    }
}

public class FinanceOperationConfiguration : IEntityTypeConfiguration<FinanceOperation>
{
    public void Configure(EntityTypeBuilder<FinanceOperation> b)
    {
        b.ToTable("finance_operations", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever(); // кілт қолмен беріледі: навигация арқылы қосылған жаңа жазба Added болып қалады (Modified емес)
        b.Property(x => x.Number).HasMaxLength(20).IsRequired();
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Description).HasMaxLength(250).IsRequired();
        b.Property(x => x.CounterpartyType).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.CounterpartyName).HasMaxLength(200);
        b.Property(x => x.Method).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.DocumentNumber).HasMaxLength(64);
        b.Property(x => x.AttachmentUrl).HasMaxLength(500);
        b.Property(x => x.Comment).HasMaxLength(1000);
        b.Property(x => x.SourceModule).HasMaxLength(32);
        b.Property(x => x.ReversalReason).HasMaxLength(500);
        b.HasIndex(x => new { x.OrganizationId, x.Seq }).IsUnique().HasFilter("\"Seq\" > 0");
        b.HasIndex(x => new { x.OrganizationId, x.SourceOperationId }).IsUnique().HasFilter("\"SourceOperationId\" IS NOT NULL");
        b.HasIndex(x => new { x.OrganizationId, x.ClientRequestId }).IsUnique().HasFilter("\"ClientRequestId\" IS NOT NULL");
        b.HasIndex(x => new { x.StoreId, x.Date });
        b.HasIndex(x => new { x.StoreId, x.Status, x.Type });
        b.HasIndex(x => x.CategoryId);
        b.HasIndex(x => x.CounterpartyId);
        b.HasMany(x => x.Entries).WithOne().HasForeignKey(x => x.OperationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class FinanceEntryConfiguration : IEntityTypeConfiguration<FinanceEntry>
{
    public void Configure(EntityTypeBuilder<FinanceEntry> b)
    {
        b.ToTable("finance_entries", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever(); // кілт қолмен беріледі: навигация арқылы қосылған жаңа жазба Added болып қалады (Modified емес)
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.BalanceAfter).HasPrecision(18, 2);
        b.HasIndex(x => new { x.AccountId, x.Date });
        b.HasOne<FinanceAccount>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
