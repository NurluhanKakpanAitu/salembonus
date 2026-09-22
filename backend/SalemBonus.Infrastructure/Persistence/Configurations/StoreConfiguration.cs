using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalemBonus.Domain.Entities;

namespace SalemBonus.Infrastructure.Persistence.Configurations;

public class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Category).HasMaxLength(100).IsRequired();
        b.Property(x => x.CashbackPercent).HasPrecision(5, 2);
        b.Property(x => x.ThemeColor).HasMaxLength(9).IsRequired();
        b.Property(x => x.Icon).HasMaxLength(50).IsRequired();
        b.Property(x => x.MaxRedeemPercent).HasPrecision(5, 2);
        b.Property(x => x.ApiKey).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.ApiKey).IsUnique();
    }
}
