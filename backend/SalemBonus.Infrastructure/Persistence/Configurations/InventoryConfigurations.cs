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
