using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalemBonus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InventoryDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Comment",
                schema: "pos",
                table: "warehouses",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                schema: "pos",
                table: "warehouses",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BalanceAfter",
                schema: "pos",
                table: "stock_movements",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Comment",
                schema: "pos",
                table: "stock_movements",
                type: "character varying(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentNumber",
                schema: "pos",
                table: "stock_movements",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "document_counters",
                schema: "pos",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LastNumber = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_counters", x => new { x.OrganizationId, x.Kind });
                });

            migrationBuilder.CreateTable(
                name: "stock_transfers",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Seq = table.Column<long>(type: "bigint", nullable: false),
                    SourceStoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceWarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationStoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationWarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReceivedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_transfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_transfers_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "core",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_transfers_warehouses_DestinationWarehouseId",
                        column: x => x.DestinationWarehouseId,
                        principalSchema: "pos",
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_transfers_warehouses_SourceWarehouseId",
                        column: x => x.SourceWarehouseId,
                        principalSchema: "pos",
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TaxId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppliers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_suppliers_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "core",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_transfer_lines",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Article = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    QuantitySent = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    QuantityReceived = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    DiscrepancyReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_transfer_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_transfer_lines_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "pos",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_transfer_lines_stock_transfers_TransferId",
                        column: x => x.TransferId,
                        principalSchema: "pos",
                        principalTable: "stock_transfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_documents",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Seq = table.Column<long>(type: "bigint", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierInvoiceNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Basis = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IsQuick = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    AttachmentUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Paid = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_stock_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_documents_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_documents_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "core",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_documents_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "pos",
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_documents_warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "pos",
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_document_lines",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Article = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    PurchasePrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RetailPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_document_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_document_lines_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "pos",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_document_lines_stock_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "pos",
                        principalTable: "stock_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_payments",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Reference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReversedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReversedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReversalReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_supplier_payments_stock_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "pos",
                        principalTable: "stock_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_payments_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "pos",
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_DocumentId",
                schema: "pos",
                table: "stock_movements",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_document_lines_DocumentId",
                schema: "pos",
                table: "stock_document_lines",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_document_lines_ProductId",
                schema: "pos",
                table: "stock_document_lines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_documents_OrganizationId_Type_Seq",
                schema: "pos",
                table: "stock_documents",
                columns: new[] { "OrganizationId", "Type", "Seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_documents_StoreId_Type_Date",
                schema: "pos",
                table: "stock_documents",
                columns: new[] { "StoreId", "Type", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_documents_SupplierId",
                schema: "pos",
                table: "stock_documents",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_documents_WarehouseId",
                schema: "pos",
                table: "stock_documents",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfer_lines_ProductId",
                schema: "pos",
                table: "stock_transfer_lines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfer_lines_TransferId",
                schema: "pos",
                table: "stock_transfer_lines",
                column: "TransferId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_DestinationStoreId_Status",
                schema: "pos",
                table: "stock_transfers",
                columns: new[] { "DestinationStoreId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_DestinationWarehouseId",
                schema: "pos",
                table: "stock_transfers",
                column: "DestinationWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_OrganizationId_Seq",
                schema: "pos",
                table: "stock_transfers",
                columns: new[] { "OrganizationId", "Seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_SourceStoreId_Status",
                schema: "pos",
                table: "stock_transfers",
                columns: new[] { "SourceStoreId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_SourceWarehouseId",
                schema: "pos",
                table: "stock_transfers",
                column: "SourceWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_payments_DocumentId",
                schema: "pos",
                table: "supplier_payments",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_payments_OrganizationId_ClientRequestId",
                schema: "pos",
                table: "supplier_payments",
                columns: new[] { "OrganizationId", "ClientRequestId" },
                unique: true,
                filter: "\"ClientRequestId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_payments_SupplierId",
                schema: "pos",
                table: "supplier_payments",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_OrganizationId_Name",
                schema: "pos",
                table: "suppliers",
                columns: new[] { "OrganizationId", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_counters",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "stock_document_lines",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "stock_transfer_lines",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "supplier_payments",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "stock_transfers",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "stock_documents",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "suppliers",
                schema: "pos");

            migrationBuilder.DropIndex(
                name: "IX_stock_movements_DocumentId",
                schema: "pos",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "Comment",
                schema: "pos",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "Type",
                schema: "pos",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "BalanceAfter",
                schema: "pos",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "Comment",
                schema: "pos",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "DocumentNumber",
                schema: "pos",
                table: "stock_movements");
        }
    }
}
