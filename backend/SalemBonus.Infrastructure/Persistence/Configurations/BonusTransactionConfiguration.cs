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
        // Жанатын партияларды іздеу үшін.
        b.HasIndex(x => new { x.ExpiresAt, x.Remaining });
        // Чекті нөмірі бойынша ашу үшін.
        b.HasIndex(x => x.ReceiptId);
        b.Property(x => x.PurchaseAmount).HasPrecision(14, 2);
        b.Property(x => x.Comment).HasMaxLength(500);
        b.Property(x => x.Source).HasMaxLength(16);
        b.Property(x => x.Percent).HasPrecision(5, 2);
        b.HasIndex(x => x.SourceTransactionId);
        // Қайталанбау: бір кілтке бір операция (тарату алушысы, қолмен операция сұранысы).
        b.HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL");
        // Бір партия бір рет қана жанады: фондық тапсырма қайта іске қосылса да (§4.3, тест 5).
        b.HasIndex(x => x.SourceTransactionId, "IX_BonusTransactions_ExpiredLot").IsUnique().HasFilter("\"Type\" = 4 AND \"SourceTransactionId\" IS NOT NULL");
        b.HasOne<BonusCard>().WithMany().HasForeignKey(x => x.BonusCardId);
    }
}
