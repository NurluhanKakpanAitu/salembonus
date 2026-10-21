using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalemBonus.Domain.Entities;

namespace SalemBonus.Infrastructure.Persistence.Configurations;

public class PushDeviceConfiguration : IEntityTypeConfiguration<PushDevice>
{
    public void Configure(EntityTypeBuilder<PushDevice> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Token).HasMaxLength(512).IsRequired();
        // Бір токен — бір құрылғы: басқа аккаунт кірсе, жазба соған көшеді (PushDeviceService).
        b.HasIndex(x => x.Token).IsUnique();
        b.HasIndex(x => x.CustomerId);
        b.Property(x => x.Platform).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.Language).HasMaxLength(5).IsRequired();
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
    }
}
