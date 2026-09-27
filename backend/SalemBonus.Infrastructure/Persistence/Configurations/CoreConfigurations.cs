using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Pos;

namespace SalemBonus.Infrastructure.Persistence.Configurations;

/// <summary>
/// Барлық өнімге ортақ кестелер "core" схемасында, касса кестелері "pos" схемасында.
/// Бар бонус кестелері әзірге "public"-та қалады (SALEMPOS.md, 1а-бөлім).
/// </summary>
internal static class Schemas
{
    public const string Core = "core";
    public const string Pos = "pos";
}

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> b)
    {
        b.ToTable("organizations", Schemas.Core);
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Bin).HasMaxLength(12);
    }
}

public class StaffUserConfiguration : IEntityTypeConfiguration<StaffUser>
{
    public void Configure(EntityTypeBuilder<StaffUser> b)
    {
        b.ToTable("staff_users", Schemas.Core);
        b.HasKey(x => x.Id);
        b.Property(x => x.Phone).HasMaxLength(20).IsRequired();
        b.HasIndex(x => x.Phone).IsUnique();
        b.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        b.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        b.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();
        b.Property(x => x.PinHash).HasMaxLength(200);
        b.Property(x => x.Language).HasMaxLength(5).IsRequired();
        b.Ignore(x => x.FullName);
        b.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Memberships).WithOne(x => x.StaffUser!).HasForeignKey(x => x.StaffUserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class StoreMembershipConfiguration : IEntityTypeConfiguration<StoreMembership>
{
    public void Configure(EntityTypeBuilder<StoreMembership> b)
    {
        b.ToTable("store_memberships", Schemas.Core);
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.StaffUserId, x.StoreId }).IsUnique();
        b.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.MaxDiscountPercent).HasPrecision(5, 2);
        b.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class StaffRefreshTokenConfiguration : IEntityTypeConfiguration<StaffRefreshToken>
{
    public void Configure(EntityTypeBuilder<StaffRefreshToken> b)
    {
        b.ToTable("staff_refresh_tokens", Schemas.Core);
        b.HasKey(x => x.Id);
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.Property(x => x.Device).HasMaxLength(200);
        b.HasOne<StaffUser>().WithMany().HasForeignKey(x => x.StaffUserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class StaffOtpCodeConfiguration : IEntityTypeConfiguration<StaffOtpCode>
{
    public void Configure(EntityTypeBuilder<StaffOtpCode> b)
    {
        b.ToTable("staff_otp_codes", Schemas.Core);
        b.HasKey(x => x.Id);
        b.Property(x => x.Purpose).HasMaxLength(32).IsRequired();
        b.Property(x => x.CodeHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.ResetTokenHash).HasMaxLength(64);
        b.HasIndex(x => x.ResetTokenHash).IsUnique();
        b.Property(x => x.Ip).HasMaxLength(64);
        b.HasIndex(x => new { x.StaffUserId, x.Purpose, x.CreatedAt });
        b.HasOne<StaffUser>().WithMany().HasForeignKey(x => x.StaffUserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> b)
    {
        b.ToTable("audit_log", Schemas.Core);
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).HasMaxLength(64).IsRequired();
        b.Property(x => x.Entity).HasMaxLength(64);
        b.Property(x => x.EntityId).HasMaxLength(64);
        b.Property(x => x.OldValues).HasColumnType("jsonb");
        b.Property(x => x.NewValues).HasColumnType("jsonb");
        b.Property(x => x.Amount).HasPrecision(14, 2);
        b.Property(x => x.Details).HasMaxLength(500);
        b.Property(x => x.Ip).HasMaxLength(64);
        // Журнал дүкен мен уақыт бойынша, сондай-ақ бір адамның әрекеттері бойынша қаралады.
        b.HasIndex(x => new { x.StoreId, x.CreatedAt });
        b.HasIndex(x => new { x.StaffUserId, x.CreatedAt });
    }
}

public class RegisterConfiguration : IEntityTypeConfiguration<Register>
{
    public void Configure(EntityTypeBuilder<Register> b)
    {
        b.ToTable("registers", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.DeviceTokenHash).HasMaxLength(64);
        b.HasIndex(x => x.DeviceTokenHash).IsUnique();
        b.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Cascade);
    }
}
