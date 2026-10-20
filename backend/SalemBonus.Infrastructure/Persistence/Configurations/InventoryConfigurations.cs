using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Pos.Catalog;
using SalemBonus.Domain.Pos.Inventory;

namespace SalemBonus.Infrastructure.Persistence.Configurations;

public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> b)
    {
        b.ToTable("warehouses", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Type).HasMaxLength(50);
        b.Property(x => x.Comment).HasMaxLength(500);
        b.HasIndex(x => x.StoreId);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> b)
    {
        b.ToTable("stock_movements", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(24);
        b.Property(x => x.Quantity).HasPrecision(18, 3);
        b.Property(x => x.UnitCost).HasPrecision(18, 2);
        b.Property(x => x.BalanceAfter).HasPrecision(18, 3);
        b.Property(x => x.DocumentNumber).HasMaxLength(40);
        b.Property(x => x.Comment).HasMaxLength(600);
        b.HasIndex(x => x.DocumentId);
        b.HasIndex(x => new { x.ProductId, x.CreatedAt });
        b.HasIndex(x => new { x.WarehouseId, x.CreatedAt });
        b.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class StockBalanceConfiguration : IEntityTypeConfiguration<StockBalance>
{
    public void Configure(EntityTypeBuilder<StockBalance> b)
    {
        b.ToTable("stock_balances", Schemas.Pos);
        b.HasKey(x => new { x.WarehouseId, x.ProductId });
        b.Property(x => x.Quantity).HasPrecision(18, 3);
        b.HasIndex(x => x.ProductId);
        b.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductPriceConfiguration : IEntityTypeConfiguration<ProductPrice>
{
    public void Configure(EntityTypeBuilder<ProductPrice> b)
    {
        b.ToTable("product_prices", Schemas.Pos);
        b.HasKey(x => new { x.ProductId, x.StoreId });
        b.Property(x => x.SalePrice).HasPrecision(18, 2);
        b.Property(x => x.PurchasePrice).HasPrecision(18, 2);
        b.HasIndex(x => x.StoreId);
        b.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> b)
    {
        b.ToTable("suppliers", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.TaxId).HasMaxLength(20);
        b.Property(x => x.Phone).HasMaxLength(32);
        b.Property(x => x.Email).HasMaxLength(120);
        b.Property(x => x.Address).HasMaxLength(300);
        b.Property(x => x.Comment).HasMaxLength(1000);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.HasIndex(x => new { x.OrganizationId, x.Name });
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class StockDocumentConfiguration : IEntityTypeConfiguration<StockDocument>
{
    public void Configure(EntityTypeBuilder<StockDocument> b)
    {
        b.ToTable("stock_documents", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Reason).HasConversion<string>().HasMaxLength(24);
        b.Property(x => x.Number).HasMaxLength(20).IsRequired();
        b.Property(x => x.SupplierInvoiceNumber).HasMaxLength(64);
        b.Property(x => x.Basis).HasMaxLength(120);
        b.Property(x => x.AttachmentUrl).HasMaxLength(500);
        b.Property(x => x.Comment).HasMaxLength(1000);
        b.Property(x => x.ReversalReason).HasMaxLength(500);
        b.Property(x => x.Total).HasPrecision(18, 2);
        b.Property(x => x.Paid).HasPrecision(18, 2);
        b.Ignore(x => x.Remaining);
        b.HasIndex(x => new { x.OrganizationId, x.Type, x.Seq }).IsUnique();
        b.HasIndex(x => new { x.StoreId, x.Type, x.Date });
        b.HasIndex(x => x.SupplierId);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.DocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class StockDocumentLineConfiguration : IEntityTypeConfiguration<StockDocumentLine>
{
    public void Configure(EntityTypeBuilder<StockDocumentLine> b)
    {
        b.ToTable("stock_document_lines", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Article).HasMaxLength(64);
        b.Property(x => x.Quantity).HasPrecision(18, 3);
        b.Property(x => x.PurchasePrice).HasPrecision(18, 2);
        b.Property(x => x.RetailPrice).HasPrecision(18, 2);
        b.Property(x => x.LineTotal).HasPrecision(18, 2);
        b.HasIndex(x => x.ProductId);
        b.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class StockTransferConfiguration : IEntityTypeConfiguration<StockTransfer>
{
    public void Configure(EntityTypeBuilder<StockTransfer> b)
    {
        b.ToTable("stock_transfers", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Number).HasMaxLength(20).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Comment).HasMaxLength(1000);
        b.HasIndex(x => new { x.OrganizationId, x.Seq }).IsUnique();
        b.HasIndex(x => new { x.SourceStoreId, x.Status });
        b.HasIndex(x => new { x.DestinationStoreId, x.Status });
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.SourceWarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.DestinationWarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.TransferId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class StockTransferLineConfiguration : IEntityTypeConfiguration<StockTransferLine>
{
    public void Configure(EntityTypeBuilder<StockTransferLine> b)
    {
        b.ToTable("stock_transfer_lines", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Article).HasMaxLength(64);
        b.Property(x => x.QuantitySent).HasPrecision(18, 3);
        b.Property(x => x.QuantityReceived).HasPrecision(18, 3);
        b.Property(x => x.DiscrepancyReason).HasMaxLength(300);
        b.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SupplierPaymentConfiguration : IEntityTypeConfiguration<SupplierPayment>
{
    public void Configure(EntityTypeBuilder<SupplierPayment> b)
    {
        b.ToTable("supplier_payments", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Method).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Reference).HasMaxLength(64);
        b.Property(x => x.Comment).HasMaxLength(500);
        b.Property(x => x.ReversalReason).HasMaxLength(500);
        b.HasIndex(x => x.SupplierId);
        b.HasIndex(x => x.DocumentId);
        b.HasIndex(x => new { x.OrganizationId, x.ClientRequestId }).IsUnique().HasFilter("\"ClientRequestId\" IS NOT NULL");
        b.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<StockDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class DocumentCounterConfiguration : IEntityTypeConfiguration<DocumentCounter>
{
    public void Configure(EntityTypeBuilder<DocumentCounter> b)
    {
        b.ToTable("document_counters", Schemas.Pos);
        b.HasKey(x => new { x.OrganizationId, x.Kind });
        b.Property(x => x.Kind).HasMaxLength(20);
    }
}
