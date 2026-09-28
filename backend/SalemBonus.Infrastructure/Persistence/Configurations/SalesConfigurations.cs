using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Pos;
using SalemBonus.Domain.Pos.Catalog;
using SalemBonus.Domain.Pos.Sales;

namespace SalemBonus.Infrastructure.Persistence.Configurations;

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> b)
    {
        b.ToTable("sales", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Subtotal).HasPrecision(18, 2);
        b.Property(x => x.DiscountKind).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.DiscountValue).HasPrecision(18, 2);
        b.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        b.Property(x => x.Total).HasPrecision(18, 2);
        b.Property(x => x.ReturnedAmount).HasPrecision(18, 2);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
        b.Ignore(x => x.DebtAmount);
        // ТЗ «Касса» §21.7: нөмірдің бірегейлігін база қорғайды.
        b.HasIndex(x => new { x.StoreId, x.Number }).IsUnique();
        b.HasIndex(x => new { x.StoreId, x.ClientRequestId }).IsUnique();
        b.HasIndex(x => new { x.StoreId, x.CreatedAt });
        b.HasIndex(x => x.CustomerId);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Register>().WithMany().HasForeignKey(x => x.RegisterId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<StaffUser>().WithMany().HasForeignKey(x => x.StaffUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Payments).WithOne().HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
    public void Configure(EntityTypeBuilder<SaleItem> b)
    {
        b.ToTable("sale_items", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Article).HasMaxLength(64);
        b.Property(x => x.UnitShortName).HasMaxLength(20);
        b.Property(x => x.Quantity).HasPrecision(18, 3);
        b.Property(x => x.Price).HasPrecision(18, 2);
        b.Property(x => x.LineTotal).HasPrecision(18, 2);
        b.Property(x => x.Discount).HasPrecision(18, 2);
        b.Property(x => x.ReturnedQuantity).HasPrecision(18, 3);
        b.Property(x => x.UnitCost).HasPrecision(18, 2);
        b.HasIndex(x => x.ProductId);
        b.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SalePaymentConfiguration : IEntityTypeConfiguration<SalePayment>
{
    public void Configure(EntityTypeBuilder<SalePayment> b)
    {
        b.ToTable("sale_payments", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Method).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Received).HasPrecision(18, 2);
        b.Property(x => x.Change).HasPrecision(18, 2);
        b.Property(x => x.TransferRecipient).HasMaxLength(200);
    }
}

public class ReceiptCounterConfiguration : IEntityTypeConfiguration<ReceiptCounter>
{
    public void Configure(EntityTypeBuilder<ReceiptCounter> b)
    {
        b.ToTable("receipt_counters", Schemas.Pos);
        b.HasKey(x => x.StoreId);
        b.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class TransferRecipientConfiguration : IEntityTypeConfiguration<TransferRecipient>
{
    public void Configure(EntityTypeBuilder<TransferRecipient> b)
    {
        b.ToTable("transfer_recipients", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.BankName).HasMaxLength(60).IsRequired();
        b.Property(x => x.Account).HasMaxLength(40).IsRequired();
        b.Property(x => x.HolderName).HasMaxLength(100).IsRequired();
        b.Ignore(x => x.Display);
        b.HasIndex(x => x.StoreId);
        b.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class StoreCashierSettingsConfiguration : IEntityTypeConfiguration<StoreCashierSettings>
{
    public void Configure(EntityTypeBuilder<StoreCashierSettings> b)
    {
        b.ToTable("store_cashier_settings", Schemas.Pos);
        b.HasKey(x => x.StoreId);
        b.Property(x => x.EnabledMethods)
            .HasConversion(
                v => v.Select(m => m.ToString()).ToArray(),
                v => v.Select(Enum.Parse<PaymentMethod>).ToList(),
                new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<PaymentMethod>>(
                    (a, c) => a!.SequenceEqual(c!), v => v.Aggregate(0, (h, m) => HashCode.Combine(h, m)), v => v.ToList()));
        b.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class DebtConfiguration : IEntityTypeConfiguration<Debt>
{
    public void Configure(EntityTypeBuilder<Debt> b)
    {
        b.ToTable("debts", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Paid).HasPrecision(18, 2);
        b.Property(x => x.Comment).HasMaxLength(300);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Ignore(x => x.Remaining);
        b.HasIndex(x => new { x.StoreId, x.Status });
        b.HasIndex(x => x.CustomerId);
        b.HasIndex(x => x.SaleId);
        b.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Sale>().WithMany().HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Payments).WithOne().HasForeignKey(x => x.DebtId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class DebtPaymentConfiguration : IEntityTypeConfiguration<DebtPayment>
{
    public void Configure(EntityTypeBuilder<DebtPayment> b)
    {
        b.ToTable("debt_payments", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.RemainingAfter).HasPrecision(18, 2);
        b.Property(x => x.Method).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.TransferRecipient).HasMaxLength(200);
        b.HasIndex(x => new { x.DebtId, x.CreatedAt });
    }
}

public class SaleReturnConfiguration : IEntityTypeConfiguration<SaleReturn>
{
    public void Configure(EntityTypeBuilder<SaleReturn> b)
    {
        b.ToTable("sale_returns", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Refunded).HasPrecision(18, 2);
        b.Property(x => x.DebtReduced).HasPrecision(18, 2);
        b.Property(x => x.RefundMethod).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Reason).HasMaxLength(300);
        b.HasIndex(x => new { x.SaleId, x.ClientRequestId }).IsUnique();
        b.HasIndex(x => new { x.StoreId, x.CreatedAt });
        b.HasOne<Sale>().WithMany().HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.ReturnId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SaleReturnItemConfiguration : IEntityTypeConfiguration<SaleReturnItem>
{
    public void Configure(EntityTypeBuilder<SaleReturnItem> b)
    {
        b.ToTable("sale_return_items", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Quantity).HasPrecision(18, 3);
        b.Property(x => x.Amount).HasPrecision(18, 2);
    }
}

public class StoreNotificationConfiguration : IEntityTypeConfiguration<StoreNotification>
{
    public void Configure(EntityTypeBuilder<StoreNotification> b)
    {
        b.ToTable("store_notifications", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(24);
        b.Property(x => x.Subject).HasMaxLength(200);
        b.Property(x => x.StaffName).HasMaxLength(120);
        b.Property(x => x.Amount).HasPrecision(18, 3);
        b.HasIndex(x => new { x.StoreId, x.CreatedAt });
        b.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Cascade);
    }
}
