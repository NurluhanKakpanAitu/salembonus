using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalemBonus.Domain.Entities;

namespace SalemBonus.Infrastructure.Persistence.Configurations;

public class BonusTransactionConfiguration : IEntityTypeConfiguration<BonusTransaction>
{
    public void Configure(EntityTypeBuilder<BonusTransaction> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.BonusCardId, x.CreatedAt });
        b.Property(x => x.PurchaseAmount).HasPrecision(14, 2);
        b.Property(x => x.Comment).HasMaxLength(500);
        b.HasOne<BonusCard>().WithMany().HasForeignKey(x => x.BonusCardId);
    }
}
