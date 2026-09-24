using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalemBonus.Domain.Entities;

namespace SalemBonus.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.CustomerId, x.CreatedAt });
        b.HasIndex(x => new { x.CustomerId, x.IsRead });
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Body).HasMaxLength(1000).IsRequired();
        b.Property(x => x.TemplateKey).HasMaxLength(40);
        b.Property(x => x.LevelKey).HasMaxLength(20);
        b.Property(x => x.Detail).HasMaxLength(500);
        b.Ignore(x => x.Category);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId);
        b.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.SetNull);
    }
}
