using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalemBonus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlatformAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "platform");

            migrationBuilder.AddColumn<string>(
                name: "City",
                schema: "core",
                table: "organizations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Comment",
                schema: "core",
                table: "organizations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                schema: "core",
                table: "organizations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactName",
                schema: "core",
                table: "organizations",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                schema: "core",
                table: "organizations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "Modules",
                schema: "core",
                table: "organizations",
                type: "text[]",
                nullable: false,
                // Бар бизнестерде бәрі қосулы қалады — көшу кезінде ешкімнің мүмкіндігі азаймайды.
                defaultValueSql: "ARRAY['cashier','products','warehouse','sales','statistics','finance','bonus','clients']::text[]");

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyFee",
                schema: "core",
                table: "organizations",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PaidUntil",
                schema: "core",
                table: "organizations",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PlanId",
                schema: "core",
                table: "organizations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuspendReason",
                schema: "core",
                table: "organizations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedAt",
                schema: "core",
                table: "organizations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "TrialUntil",
                schema: "core",
                table: "organizations",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "admins",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    FailedLoginCount = table.Column<int>(type: "integer", nullable: false),
                    LockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admins", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "audit_log",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Entity = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    EntityId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Data = table.Column<string>(type: "jsonb", nullable: true),
                    Ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidOn = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodTo = table.Column<DateOnly>(type: "date", nullable: false),
                    Method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VoidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payments_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "core",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plans",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    MonthlyPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Modules = table.Column<List<string>>(type: "text[]", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plans", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_organizations_PaidUntil",
                schema: "core",
                table: "organizations",
                column: "PaidUntil");

            migrationBuilder.CreateIndex(
                name: "IX_admins_Email",
                schema: "platform",
                table: "admins",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_CreatedAt",
                schema: "platform",
                table: "audit_log",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_OrganizationId_CreatedAt",
                schema: "platform",
                table: "audit_log",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_payments_OrganizationId_PaidOn",
                schema: "platform",
                table: "payments",
                columns: new[] { "OrganizationId", "PaidOn" });

            migrationBuilder.CreateIndex(
                name: "IX_payments_PaidOn",
                schema: "platform",
                table: "payments",
                column: "PaidOn");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admins",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "audit_log",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "payments",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "plans",
                schema: "platform");

            migrationBuilder.DropIndex(
                name: "IX_organizations_PaidUntil",
                schema: "core",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "City",
                schema: "core",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "Comment",
                schema: "core",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "ContactEmail",
                schema: "core",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "ContactName",
                schema: "core",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "ContactPhone",
                schema: "core",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "Modules",
                schema: "core",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "MonthlyFee",
                schema: "core",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "PaidUntil",
                schema: "core",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "PlanId",
                schema: "core",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "SuspendReason",
                schema: "core",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "SuspendedAt",
                schema: "core",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "TrialUntil",
                schema: "core",
                table: "organizations");
        }
    }
}
