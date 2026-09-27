using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Pos.Catalog;

namespace SalemBonus.Infrastructure.Persistence.Configurations;

public class CatalogNodeConfiguration : IEntityTypeConfiguration<CatalogNode>
{
    public void Configure(EntityTypeBuilder<CatalogNode> b)
    {
        b.ToTable("catalog_nodes", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Icon).HasMaxLength(50);
        b.Property(x => x.ImageUrl).HasMaxLength(500);
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        // Жол — 12 деңгейге дейін (36 таңбалы Guid + бөлгіш).
        b.Property(x => x.Path).HasMaxLength(CatalogNode.MaxDepth * 37).IsRequired();
        b.Ignore(x => x.IsRoot);
        b.HasIndex(x => new { x.OrganizationId, x.ParentId, x.SortOrder });
        // Ұрпақтарды "path LIKE 'a/b/%'" арқылы іздеу үшін.
        b.HasIndex(x => new { x.OrganizationId, x.Path });
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CatalogNode>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> b)
    {
        b.ToTable("brands", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.LogoUrl).HasMaxLength(500);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.HasIndex(x => new { x.OrganizationId, x.SortOrder });
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class MeasureUnitConfiguration : IEntityTypeConfiguration<MeasureUnit>
{
    public void Configure(EntityTypeBuilder<MeasureUnit> b)
    {
        b.ToTable("units", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(50).IsRequired();
        b.Property(x => x.ShortName).HasMaxLength(12).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.HasIndex(x => x.OrganizationId);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class CharacteristicDefinitionConfiguration : IEntityTypeConfiguration<CharacteristicDefinition>
{
    public void Configure(EntityTypeBuilder<CharacteristicDefinition> b)
    {
        b.ToTable("characteristic_definitions", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.HasIndex(x => new { x.OrganizationId, x.SortOrder });
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Options).WithOne().HasForeignKey(x => x.DefinitionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CharacteristicOptionConfiguration : IEntityTypeConfiguration<CharacteristicOption>
{
    public void Configure(EntityTypeBuilder<CharacteristicOption> b)
    {
        b.ToTable("characteristic_options", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Value).HasMaxLength(100).IsRequired();
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("products", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Article).HasMaxLength(64);
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.Supplier).HasMaxLength(200);
        b.Property(x => x.Manufacturer).HasMaxLength(200);
        b.Property(x => x.Country).HasMaxLength(64);
        b.Property(x => x.VatRate).HasPrecision(5, 2);
        b.Property(x => x.MaxDiscountPercent).HasPrecision(5, 2);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasIndex(x => new { x.OrganizationId, x.Name });
        b.HasIndex(x => new { x.OrganizationId, x.Article });
        b.HasIndex(x => x.CatalogNodeId);
        b.HasIndex(x => x.BrandId);
        b.HasIndex(x => x.UnitId);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MeasureUnit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Brand>().WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
        // Санатты өшіру тауарды өшірмейді: қауіпсіз өшіру сервисте тексеріледі.
        b.HasOne<CatalogNode>().WithMany().HasForeignKey(x => x.CatalogNodeId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Barcodes).WithOne().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Images).WithOne().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Characteristics).WithOne().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProductBarcodeConfiguration : IEntityTypeConfiguration<ProductBarcode>
{
    public void Configure(EntityTypeBuilder<ProductBarcode> b)
    {
        b.ToTable("product_barcodes", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Barcode).HasMaxLength(64).IsRequired();
        b.Property(x => x.Description).HasMaxLength(100);
        // ТЗ §18: бір штрихкод бір тауарға ғана. Каталог бизнес деңгейінде — бірегейлік те сонда.
        b.HasIndex(x => new { x.OrganizationId, x.Barcode }).IsUnique();
    }
}

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> b)
    {
        b.ToTable("product_images", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Url).HasMaxLength(500).IsRequired();
    }
}

public class ProductCharacteristicValueConfiguration : IEntityTypeConfiguration<ProductCharacteristicValue>
{
    public void Configure(EntityTypeBuilder<ProductCharacteristicValue> b)
    {
        b.ToTable("product_characteristics", Schemas.Pos);
        b.HasKey(x => new { x.ProductId, x.DefinitionId });
        b.Property(x => x.Value).HasMaxLength(200).IsRequired();
        b.HasOne<CharacteristicDefinition>().WithMany().HasForeignKey(x => x.DefinitionId).OnDelete(DeleteBehavior.Restrict);
    }
}
