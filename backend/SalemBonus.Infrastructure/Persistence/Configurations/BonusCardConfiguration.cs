using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalemBonus.Domain.Entities;

namespace SalemBonus.Infrastructure.Persistence.Configurations;

public class BonusCardConfiguration : IEntityTypeConfiguration<BonusCard>
{
    public void Configure(EntityTypeBuilder<BonusCard> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.CustomerId, x.StoreId }).IsUnique();
        b.Property(x => x.TotalSpent).HasPrecision(14, 2);
        b.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId);
    }
}
