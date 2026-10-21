using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Platform;

namespace SalemBonus.Infrastructure.Persistence.Configurations;

public class PlatformAdminConfiguration : IEntityTypeConfiguration<PlatformAdmin>
{
    public void Configure(EntityTypeBuilder<PlatformAdmin> b)
    {
        b.ToTable("admins", Schemas.Platform);
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Email).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();
    }
}

public class PlatformPlanConfiguration : IEntityTypeConfiguration<PlatformPlan>
{
    public void Configure(EntityTypeBuilder<PlatformPlan> b)
    {
        b.ToTable("plans", Schemas.Platform);
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.MonthlyPrice).HasPrecision(18, 2);
        b.Property(x => x.Description).HasMaxLength(500);
    }
}

public class PlatformPaymentConfiguration : IEntityTypeConfiguration<PlatformPayment>
{
    public void Configure(EntityTypeBuilder<PlatformPayment> b)
    {
        b.ToTable("payments", Schemas.Platform);
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Method).HasMaxLength(16).IsRequired();
        b.Property(x => x.Reference).HasMaxLength(120);
        b.Property(x => x.Comment).HasMaxLength(500);
        b.Property(x => x.VoidReason).HasMaxLength(500);
        b.HasIndex(x => new { x.OrganizationId, x.PaidOn });
        b.HasIndex(x => x.PaidOn);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PlatformAuditConfiguration : IEntityTypeConfiguration<PlatformAuditEntry>
{
    public void Configure(EntityTypeBuilder<PlatformAuditEntry> b)
    {
        b.ToTable("audit_log", Schemas.Platform);
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Action).HasMaxLength(64).IsRequired();
        b.Property(x => x.Entity).HasMaxLength(64);
        b.Property(x => x.EntityId).HasMaxLength(64);
        b.Property(x => x.Data).HasColumnType("jsonb");
        b.Property(x => x.Ip).HasMaxLength(64);
        b.HasIndex(x => new { x.OrganizationId, x.CreatedAt });
        b.HasIndex(x => x.CreatedAt);
    }
}
