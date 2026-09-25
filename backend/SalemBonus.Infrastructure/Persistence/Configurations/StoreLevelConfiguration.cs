using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalemBonus.Domain.Entities;

namespace SalemBonus.Infrastructure.Persistence.Configurations;

public class StoreLevelConfiguration : IEntityTypeConfiguration<StoreLevel>
{
    public void Configure(EntityTypeBuilder<StoreLevel> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.StoreId, x.Level }).IsUnique();
        b.Property(x => x.FromAmount).HasPrecision(12, 2);
        b.Property(x => x.CashbackPercent).HasPrecision(5, 2);
    }
}
