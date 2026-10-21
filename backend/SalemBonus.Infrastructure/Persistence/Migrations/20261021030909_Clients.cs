using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalemBonus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Clients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Бұрыннан бар әкімшілерге «Клиенты» құқықтары (жаңа әдепкі жиын тек жаңа қызметкерге беріледі).
            migrationBuilder.Sql("""
                UPDATE core.store_memberships m
                SET "Permissions" = (SELECT array_agg(DISTINCT p) FROM unnest(m."Permissions" || ARRAY['clients.view', 'clients.edit', 'clients.archive', 'clients.export', 'clients.merge', 'clients.debts']) AS p)
                WHERE m."Role" = 'Admin';
                """);

            migrationBuilder.CreateTable(
                name: "client_communications",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    StaffUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_communications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "client_imports",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    Rows = table.Column<int>(type: "integer", nullable: false),
                    Created = table.Column<int>(type: "integer", nullable: false),
                    Updated = table.Column<int>(type: "integer", nullable: false),
                    Skipped = table.Column<int>(type: "integer", nullable: false),
                    Errors = table.Column<int>(type: "integer", nullable: false),
                    Report = table.Column<string>(type: "jsonb", nullable: false),
                    StaffUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_imports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "client_merge_log",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrimaryProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    DuplicateProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrimaryCustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    DuplicateCustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Summary = table.Column<string>(type: "jsonb", nullable: false),
                    StaffUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_merge_log", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "client_profiles",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<long>(type: "bigint", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MiddleName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Gender = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    KatoCode = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ConsentApp = table.Column<bool>(type: "boolean", nullable: false),
                    ConsentSms = table.Column<bool>(type: "boolean", nullable: false),
                    ConsentWhatsApp = table.Column<bool>(type: "boolean", nullable: false),
                    ConsentEmail = table.Column<bool>(type: "boolean", nullable: false),
                    ConsentUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CustomFields = table.Column<string>(type: "jsonb", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ArchiveReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MergedIntoId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_profiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_client_profiles_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "client_segments",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Filter = table.Column<string>(type: "jsonb", nullable: false),
                    IncludeIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    IsShared = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_segments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "client_settings",
                schema: "pos",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequireLastName = table.Column<bool>(type: "boolean", nullable: false),
                    RequireBirthDate = table.Column<bool>(type: "boolean", nullable: false),
                    ActiveDays = table.Column<int>(type: "integer", nullable: false),
                    PassiveDays = table.Column<int>(type: "integer", nullable: false),
                    NewDays = table.Column<int>(type: "integer", nullable: false),
                    DueSoonDays = table.Column<int>(type: "integer", nullable: false),
                    DefaultColumns = table.Column<List<string>>(type: "text[]", nullable: false),
                    CustomFields = table.Column<string>(type: "jsonb", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_settings", x => x.OrganizationId);
                });

            migrationBuilder.CreateTable(
                name: "client_notes",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Category = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_client_notes_client_profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalSchema: "pos",
                        principalTable: "client_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_client_communications_ProfileId_CreatedAt",
                schema: "pos",
                table: "client_communications",
                columns: new[] { "ProfileId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_client_imports_OrganizationId_CreatedAt",
                schema: "pos",
                table: "client_imports",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_client_merge_log_OrganizationId_CreatedAt",
                schema: "pos",
                table: "client_merge_log",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_client_notes_ProfileId_CreatedAt",
                schema: "pos",
                table: "client_notes",
                columns: new[] { "ProfileId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_client_profiles_CustomerId",
                schema: "pos",
                table: "client_profiles",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_client_profiles_OrganizationId_CreatedAt",
                schema: "pos",
                table: "client_profiles",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_client_profiles_OrganizationId_CustomerId",
                schema: "pos",
                table: "client_profiles",
                columns: new[] { "OrganizationId", "CustomerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_client_profiles_OrganizationId_Number",
                schema: "pos",
                table: "client_profiles",
                columns: new[] { "OrganizationId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_client_segments_OrganizationId",
                schema: "pos",
                table: "client_segments",
                column: "OrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "client_communications",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "client_imports",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "client_merge_log",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "client_notes",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "client_segments",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "client_settings",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "client_profiles",
                schema: "pos");
        }
    }
}
