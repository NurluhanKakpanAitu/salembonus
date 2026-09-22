using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalemBonus.Domain.Entities;

namespace SalemBonus.Infrastructure.Persistence.Configurations;

public class OtpCodeConfiguration : IEntityTypeConfiguration<OtpCode>
{
    public void Configure(EntityTypeBuilder<OtpCode> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Phone).HasMaxLength(20).IsRequired();
        b.Property(x => x.CodeHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => new { x.Phone, x.CreatedAt });
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.Property(x => x.Device).HasMaxLength(200);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId);
    }
}
