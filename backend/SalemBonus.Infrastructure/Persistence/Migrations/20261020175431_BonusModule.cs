using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalemBonus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BonusModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Бұрыннан бар әкімшілерге «Бонусы» құқықтары (жаңа әдепкі жиын тек жаңа қызметкерге беріледі).
            migrationBuilder.Sql("""
                UPDATE core.store_memberships m
                SET "Permissions" = (SELECT array_agg(DISTINCT p) FROM unnest(m."Permissions" || ARRAY['bonus.view', 'bonus.manage', 'bonus.campaigns', 'bonus.accrue', 'bonus.writeoff', 'bonus.reverse', 'bonus.terms']) AS p)
                WHERE m."Role" = 'Admin';
                """);

            migrationBuilder.DropIndex(
                name: "IX_BonusCards_StoreId",
                table: "BonusCards");

            migrationBuilder.AddColumn<int>(
                name: "BalanceAfter",
                table: "BonusTransactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IdempotencyKey",
                table: "BonusTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Percent",
                table: "BonusTransactions",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReversedAt",
                table: "BonusTransactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "BonusTransactions",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceId",
                table: "BonusTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceTransactionId",
                table: "BonusTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StaffUserId",
                table: "BonusTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotificationsOptOut",
                table: "BonusCards",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "StatusAssignedAt",
                table: "BonusCards",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StatusExpiresAt",
                table: "BonusCards",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StatusId",
                table: "BonusCards",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "StatusManual",
                table: "BonusCards",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "StatusReason",
                table: "BonusCards",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "bonus_birthday_grants",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    BirthdayDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Manual = table.Column<bool>(type: "boolean", nullable: false),
                    StaffUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bonus_birthday_grants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "bonus_campaign_recipients",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BonusCardId = table.Column<Guid>(type: "uuid", nullable: false),
                    Contact = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Reason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    BonusTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bonus_campaign_recipients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "bonus_campaigns",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AudienceMode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Filter = table.Column<string>(type: "jsonb", nullable: false),
                    CustomerIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    BonusAmount = table.Column<int>(type: "integer", nullable: true),
                    BonusValidityDays = table.Column<int>(type: "integer", nullable: true),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RecipientsCount = table.Column<int>(type: "integer", nullable: false),
                    SentCount = table.Column<int>(type: "integer", nullable: false),
                    FailedCount = table.Column<int>(type: "integer", nullable: false),
                    ExcludedCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bonus_campaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "bonus_expiry_warnings",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LotId = table.Column<Guid>(type: "uuid", nullable: false),
                    DaysBefore = table.Column<int>(type: "integer", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bonus_expiry_warnings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "bonus_settings",
                schema: "pos",
                columns: table => new
                {
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiryEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ExpiryValue = table.Column<int>(type: "integer", nullable: false),
                    ExpiryUnit = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ExpiryStatusIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    ExpiryExcludedTypes = table.Column<List<string>>(type: "text[]", nullable: false),
                    WarnEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    WarnDays = table.Column<List<int>>(type: "integer[]", nullable: false),
                    WarnChannel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    BirthdayEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    BirthdayAmount = table.Column<int>(type: "integer", nullable: false),
                    BirthdayTiming = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    BirthdayTimingDays = table.Column<int>(type: "integer", nullable: false),
                    BirthdayTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    BirthdayValidity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BirthdayValidityDays = table.Column<int>(type: "integer", nullable: true),
                    BirthdayValidityDate = table.Column<DateOnly>(type: "date", nullable: true),
                    BirthdayStatusIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    BirthdayMinPurchaseAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    BirthdayMinPurchaseCount = table.Column<int>(type: "integer", nullable: true),
                    BirthdayPurchasePeriodDays = table.Column<int>(type: "integer", nullable: false),
                    BirthdayExcludedCustomerIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    BirthdayMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bonus_settings", x => x.StoreId);
                });

            migrationBuilder.CreateTable(
                name: "bonus_terms",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    BasedOnVersion = table.Column<int>(type: "integer", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublishedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bonus_terms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "loyalty_statuses",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Icon = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    Rule = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ThresholdAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    ThresholdCount = table.Column<int>(type: "integer", nullable: true),
                    Period = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AccrualPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    CountReturns = table.Column<bool>(type: "boolean", nullable: false),
                    ValidityDays = table.Column<int>(type: "integer", nullable: true),
                    AutoDowngrade = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loyalty_statuses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BonusTransactions_ExpiredLot",
                table: "BonusTransactions",
                column: "SourceTransactionId",
                unique: true,
                filter: "\"Type\" = 4 AND \"SourceTransactionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BonusTransactions_IdempotencyKey",
                table: "BonusTransactions",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BonusTransactions_SourceTransactionId",
                table: "BonusTransactions",
                column: "SourceTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_BonusCards_StoreId_StatusId",
                table: "BonusCards",
                columns: new[] { "StoreId", "StatusId" });

            migrationBuilder.CreateIndex(
                name: "IX_bonus_birthday_grants_StoreId_CustomerId_Year",
                schema: "pos",
                table: "bonus_birthday_grants",
                columns: new[] { "StoreId", "CustomerId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bonus_campaign_recipients_CampaignId_CustomerId",
                schema: "pos",
                table: "bonus_campaign_recipients",
                columns: new[] { "CampaignId", "CustomerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bonus_campaigns_Status_ScheduledAt",
                schema: "pos",
                table: "bonus_campaigns",
                columns: new[] { "Status", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bonus_campaigns_StoreId_CreatedAt",
                schema: "pos",
                table: "bonus_campaigns",
                columns: new[] { "StoreId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bonus_expiry_warnings_LotId_DaysBefore",
                schema: "pos",
                table: "bonus_expiry_warnings",
                columns: new[] { "LotId", "DaysBefore" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bonus_terms_StoreId_Status",
                schema: "pos",
                table: "bonus_terms",
                columns: new[] { "StoreId", "Status" },
                unique: true,
                filter: "\"Status\" IN ('Draft', 'Published')");

            migrationBuilder.CreateIndex(
                name: "IX_bonus_terms_StoreId_Version",
                schema: "pos",
                table: "bonus_terms",
                columns: new[] { "StoreId", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_loyalty_statuses_StoreId_SortOrder",
                schema: "pos",
                table: "loyalty_statuses",
                columns: new[] { "StoreId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bonus_birthday_grants",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "bonus_campaign_recipients",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "bonus_campaigns",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "bonus_expiry_warnings",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "bonus_settings",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "bonus_terms",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "loyalty_statuses",
                schema: "pos");

            migrationBuilder.DropIndex(
                name: "IX_BonusTransactions_ExpiredLot",
                table: "BonusTransactions");

            migrationBuilder.DropIndex(
                name: "IX_BonusTransactions_IdempotencyKey",
                table: "BonusTransactions");

            migrationBuilder.DropIndex(
                name: "IX_BonusTransactions_SourceTransactionId",
                table: "BonusTransactions");

            migrationBuilder.DropIndex(
                name: "IX_BonusCards_StoreId_StatusId",
                table: "BonusCards");

            migrationBuilder.DropColumn(
                name: "BalanceAfter",
                table: "BonusTransactions");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "BonusTransactions");

            migrationBuilder.DropColumn(
                name: "Percent",
                table: "BonusTransactions");

            migrationBuilder.DropColumn(
                name: "ReversedAt",
                table: "BonusTransactions");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "BonusTransactions");

            migrationBuilder.DropColumn(
                name: "SourceId",
                table: "BonusTransactions");

            migrationBuilder.DropColumn(
                name: "SourceTransactionId",
                table: "BonusTransactions");

            migrationBuilder.DropColumn(
                name: "StaffUserId",
                table: "BonusTransactions");

            migrationBuilder.DropColumn(
                name: "NotificationsOptOut",
                table: "BonusCards");

            migrationBuilder.DropColumn(
                name: "StatusAssignedAt",
                table: "BonusCards");

            migrationBuilder.DropColumn(
                name: "StatusExpiresAt",
                table: "BonusCards");

            migrationBuilder.DropColumn(
                name: "StatusId",
                table: "BonusCards");

            migrationBuilder.DropColumn(
                name: "StatusManual",
                table: "BonusCards");

            migrationBuilder.DropColumn(
                name: "StatusReason",
                table: "BonusCards");

            migrationBuilder.CreateIndex(
                name: "IX_BonusCards_StoreId",
                table: "BonusCards",
                column: "StoreId");
        }
    }
}
