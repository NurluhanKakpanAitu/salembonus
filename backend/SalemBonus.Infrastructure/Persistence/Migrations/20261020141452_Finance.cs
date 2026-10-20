using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalemBonus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Finance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Бұрыннан бар әкімшілерге «Финансы» құқықтары (жаңа әдепкі жиын тек жаңа қызметкерге беріледі).
            migrationBuilder.Sql("""
                UPDATE core.store_memberships m
                SET "Permissions" = (SELECT array_agg(DISTINCT p) FROM unnest(m."Permissions" || ARRAY['finance.operations', 'finance.reverse', 'finance.accounts']) AS p)
                WHERE m."Role" = 'Admin';
                """);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PaymentDueDate",
                schema: "pos",
                table: "stock_documents",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "finance_accounts",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Methods = table.Column<string[]>(type: "text[]", nullable: false),
                    Balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OpeningDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "finance_categories",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PnlClass = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "finance_operations",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Seq = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    CounterpartyType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    CounterpartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CounterpartyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FromAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    DocumentNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AttachmentUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SourceModule = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    SourceDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceOperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReversalOfId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PostedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PostedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReversedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReversedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReversalReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_operations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "finance_entries",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_finance_entries_finance_accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "pos",
                        principalTable: "finance_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finance_entries_finance_operations_OperationId",
                        column: x => x.OperationId,
                        principalSchema: "pos",
                        principalTable: "finance_operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_finance_accounts_OrganizationId",
                schema: "pos",
                table: "finance_accounts",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_accounts_StoreId_Name",
                schema: "pos",
                table: "finance_accounts",
                columns: new[] { "StoreId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finance_categories_OrganizationId_Type_Name",
                schema: "pos",
                table: "finance_categories",
                columns: new[] { "OrganizationId", "Type", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finance_entries_AccountId_Date",
                schema: "pos",
                table: "finance_entries",
                columns: new[] { "AccountId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_entries_OperationId",
                schema: "pos",
                table: "finance_entries",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_operations_CategoryId",
                schema: "pos",
                table: "finance_operations",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_operations_CounterpartyId",
                schema: "pos",
                table: "finance_operations",
                column: "CounterpartyId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_operations_OrganizationId_ClientRequestId",
                schema: "pos",
                table: "finance_operations",
                columns: new[] { "OrganizationId", "ClientRequestId" },
                unique: true,
                filter: "\"ClientRequestId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_finance_operations_OrganizationId_Seq",
                schema: "pos",
                table: "finance_operations",
                columns: new[] { "OrganizationId", "Seq" },
                unique: true,
                filter: "\"Seq\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_finance_operations_OrganizationId_SourceOperationId",
                schema: "pos",
                table: "finance_operations",
                columns: new[] { "OrganizationId", "SourceOperationId" },
                unique: true,
                filter: "\"SourceOperationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_finance_operations_StoreId_Date",
                schema: "pos",
                table: "finance_operations",
                columns: new[] { "StoreId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_operations_StoreId_Status_Type",
                schema: "pos",
                table: "finance_operations",
                columns: new[] { "StoreId", "Status", "Type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "finance_categories",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "finance_entries",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "finance_accounts",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "finance_operations",
                schema: "pos");

            migrationBuilder.DropColumn(
                name: "PaymentDueDate",
                schema: "pos",
                table: "stock_documents");
        }
    }
}
