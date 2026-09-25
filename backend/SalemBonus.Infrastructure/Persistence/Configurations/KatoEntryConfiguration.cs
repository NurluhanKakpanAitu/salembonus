using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalemBonus.Domain.Entities;

namespace SalemBonus.Infrastructure.Persistence.Configurations;

public class KatoEntryConfiguration : IEntityTypeConfiguration<KatoEntry>
{
    public void Configure(EntityTypeBuilder<KatoEntry> b)
    {
        b.HasKey(x => x.Code);
        b.Property(x => x.Code).HasMaxLength(9);
        b.Property(x => x.ParentCode).HasMaxLength(9);
        b.Property(x => x.NameKk).HasMaxLength(200).IsRequired();
        b.Property(x => x.NameRu).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.ParentCode);
        b.HasIndex(x => x.Level);
    }
}
