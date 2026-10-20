using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalemBonus.Domain.Pos.Bonus;

namespace SalemBonus.Infrastructure.Persistence.Configurations;

public class LoyaltyStatusConfiguration : IEntityTypeConfiguration<LoyaltyStatus>
{
    public void Configure(EntityTypeBuilder<LoyaltyStatus> b)
    {
        b.ToTable("loyalty_statuses", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Name).HasMaxLength(60).IsRequired();
        b.Property(x => x.Icon).HasMaxLength(32);
        b.Property(x => x.Color).HasMaxLength(9);
        b.Property(x => x.Rule).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Period).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.ThresholdAmount).HasPrecision(14, 2);
        b.Property(x => x.AccrualPercent).HasPrecision(5, 2);
        b.Property(x => x.Description).HasMaxLength(1000);
        b.HasIndex(x => new { x.StoreId, x.SortOrder });
    }
}

public class BonusSettingsConfiguration : IEntityTypeConfiguration<BonusSettings>
{
    public void Configure(EntityTypeBuilder<BonusSettings> b)
    {
        b.ToTable("bonus_settings", Schemas.Pos);
        b.HasKey(x => x.StoreId);
        b.Property(x => x.StoreId).ValueGeneratedNever();
        b.Property(x => x.ExpiryUnit).HasConversion<string>().HasMaxLength(8);
        b.Property(x => x.WarnChannel).HasMaxLength(16);
        b.Property(x => x.BirthdayTiming).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.BirthdayValidity).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.BirthdayMinPurchaseAmount).HasPrecision(14, 2);
        b.Property(x => x.BirthdayMessage).HasMaxLength(1000);
    }
}

public class BirthdayGrantConfiguration : IEntityTypeConfiguration<BirthdayGrant>
{
    public void Configure(EntityTypeBuilder<BirthdayGrant> b)
    {
        b.ToTable("bonus_birthday_grants", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(12);
        b.Property(x => x.Reason).HasMaxLength(32);
        // Бір клиентке бір жылда бір өңдеу: қайта іске қосу екінші сыйлық жасамайды (§18, тест 7).
        b.HasIndex(x => new { x.StoreId, x.CustomerId, x.Year }).IsUnique();
    }
}

public class ExpiryWarningConfiguration : IEntityTypeConfiguration<ExpiryWarning>
{
    public void Configure(EntityTypeBuilder<ExpiryWarning> b)
    {
        b.ToTable("bonus_expiry_warnings", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.HasIndex(x => new { x.LotId, x.DaysBefore }).IsUnique();
    }
}

public class BonusCampaignConfiguration : IEntityTypeConfiguration<BonusCampaign>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<BonusCampaign> b)
    {
        b.ToTable("bonus_campaigns", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
        b.Property(x => x.AudienceMode).HasConversion<string>().HasMaxLength(8);
        b.Property(x => x.Channel).HasMaxLength(16);
        b.Property(x => x.Message).HasMaxLength(1000);
        b.Property(x => x.Filter).HasColumnType("jsonb").HasConversion(
            v => JsonSerializer.Serialize(v, Json),
            v => JsonSerializer.Deserialize<AudienceFilter>(v, Json) ?? new AudienceFilter(),
            new ValueComparer<AudienceFilter>((a, c) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(c, Json),
                v => JsonSerializer.Serialize(v, Json).GetHashCode(), v => JsonSerializer.Deserialize<AudienceFilter>(JsonSerializer.Serialize(v, Json), Json)!));
        b.HasIndex(x => new { x.StoreId, x.CreatedAt });
        b.HasIndex(x => new { x.Status, x.ScheduledAt });
    }
}

public class CampaignRecipientConfiguration : IEntityTypeConfiguration<CampaignRecipient>
{
    public void Configure(EntityTypeBuilder<CampaignRecipient> b)
    {
        b.ToTable("bonus_campaign_recipients", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Contact).HasMaxLength(32);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(12);
        b.Property(x => x.Reason).HasMaxLength(64);
        // Бір таратуда бір клиент бір рет: қайта іске қосу қайталанған хабар не бонус жасамайды.
        b.HasIndex(x => new { x.CampaignId, x.CustomerId }).IsUnique();
    }
}

public class BonusTermsConfiguration : IEntityTypeConfiguration<BonusTerms>
{
    public void Configure(EntityTypeBuilder<BonusTerms> b)
    {
        b.ToTable("bonus_terms", Schemas.Pos);
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(12);
        b.Property(x => x.Comment).HasMaxLength(500);
        b.HasIndex(x => new { x.StoreId, x.Version });
        // Дүкенде бір ғана черновик және бір ғана жарияланған нұсқа.
        b.HasIndex(x => new { x.StoreId, x.Status }).IsUnique().HasFilter("\"Status\" IN ('Draft', 'Published')");
    }
}
