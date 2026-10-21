using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Pos.Clients;

namespace SalemBonus.Infrastructure.Persistence.Configurations;

internal static class ClientJson
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Күрделі мән — jsonb бағанында; өзгеріс мазмұны бойынша салыстырылады.</summary>
    public static PropertyBuilder<T> Jsonb<T>(this PropertyBuilder<T> p) where T : class, new() =>
        p.HasColumnType("jsonb").HasConversion(
            v => JsonSerializer.Serialize(v, Json),
            v => JsonSerializer.Deserialize<T>(v, Json) ?? new T(),
            new ValueComparer<T>((a, c) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(c, Json),
                v => JsonSerializer.Serialize(v, Json).GetHashCode(), v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, Json), Json)!));
}

public class ClientProfileConfiguration : IEntityTypeConfiguration<ClientProfile>
{
    public void Configure(EntityTypeBuilder<ClientProfile> b)
    {
        b.ToTable("client_profiles", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        b.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        b.Property(x => x.MiddleName).HasMaxLength(100);
        b.Property(x => x.Gender).HasConversion<string>().HasMaxLength(8);
        b.Property(x => x.Source).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.KatoCode).HasMaxLength(9);
        b.Property(x => x.Address).HasMaxLength(300);
        b.Property(x => x.Email).HasMaxLength(200);
        b.Property(x => x.Comment).HasMaxLength(1000);
        b.Property(x => x.ArchiveReason).HasMaxLength(500);
        b.Property(x => x.CustomFields).Jsonb();
        // Бір ұйымда бір адамға бір карточка; нөмір ұйым ішінде бірегей.
        b.HasIndex(x => new { x.OrganizationId, x.CustomerId }).IsUnique();
        b.HasIndex(x => new { x.OrganizationId, x.Number }).IsUnique();
        b.HasIndex(x => new { x.OrganizationId, x.CreatedAt });
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ClientNoteConfiguration : IEntityTypeConfiguration<ClientNote>
{
    public void Configure(EntityTypeBuilder<ClientNote> b)
    {
        b.ToTable("client_notes", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Text).HasMaxLength(2000).IsRequired();
        b.Property(x => x.Category).HasConversion<string>().HasMaxLength(16);
        b.HasIndex(x => new { x.ProfileId, x.CreatedAt });
        b.HasOne<ClientProfile>().WithMany().HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ClientSegmentConfiguration : IEntityTypeConfiguration<ClientSegment>
{
    public void Configure(EntityTypeBuilder<ClientSegment> b)
    {
        b.ToTable("client_segments", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.Filter).Jsonb();
        b.HasIndex(x => x.OrganizationId);
    }
}

public class ClientSettingsConfiguration : IEntityTypeConfiguration<ClientSettings>
{
    public void Configure(EntityTypeBuilder<ClientSettings> b)
    {
        b.ToTable("client_settings", Schemas.Pos);
        b.HasKey(x => x.OrganizationId);
        b.Property(x => x.OrganizationId).ValueGeneratedNever();
        b.Property(x => x.CustomFields).Jsonb();
    }
}

public class ClientCommunicationConfiguration : IEntityTypeConfiguration<ClientCommunication>
{
    public void Configure(EntityTypeBuilder<ClientCommunication> b)
    {
        b.ToTable("client_communications", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Channel).HasMaxLength(16);
        b.Property(x => x.Text).HasMaxLength(1000);
        b.Property(x => x.Status).HasMaxLength(16);
        b.HasIndex(x => new { x.ProfileId, x.CreatedAt });
    }
}

public class ClientMergeLogConfiguration : IEntityTypeConfiguration<ClientMergeLog>
{
    public void Configure(EntityTypeBuilder<ClientMergeLog> b)
    {
        b.ToTable("client_merge_log", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Summary).HasColumnType("jsonb");
        b.HasIndex(x => new { x.OrganizationId, x.CreatedAt });
    }
}

public class ClientImportConfiguration : IEntityTypeConfiguration<ClientImport>
{
    public void Configure(EntityTypeBuilder<ClientImport> b)
    {
        b.ToTable("client_imports", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.FileName).HasMaxLength(260);
        b.Property(x => x.Report).HasColumnType("jsonb");
        b.HasIndex(x => new { x.OrganizationId, x.CreatedAt });
    }
}
