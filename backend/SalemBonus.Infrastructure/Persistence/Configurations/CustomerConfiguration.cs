using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalemBonus.Domain.Entities;

namespace SalemBonus.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Phone).HasMaxLength(20).IsRequired();
        b.HasIndex(x => x.Phone).IsUnique();
        b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Email).HasMaxLength(200);
        b.Property(x => x.QrCode).HasMaxLength(16).IsRequired();
        b.HasIndex(x => x.QrCode).IsUnique();
    }
}
