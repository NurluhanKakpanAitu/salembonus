using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalemBonus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductsAndStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Manufacturer",
                schema: "pos",
                table: "products",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Supplier",
                schema: "pos",
                table: "products",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "product_prices",
                schema: "pos",
                columns: table => new
                {
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalePrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PurchasePrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_prices", x => new { x.ProductId, x.StoreId });
                    table.ForeignKey(
                        name: "FK_product_prices_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_prices_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "pos",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "warehouses",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_warehouses_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warehouses_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "core",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_balances",
                schema: "pos",
                columns: table => new
                {
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_balances", x => new { x.WarehouseId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_stock_balances_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "pos",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_balances_warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "pos",
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_movements",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    StaffUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_movements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_movements_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "pos",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_movements_warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "pos",
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_prices_StoreId",
                schema: "pos",
                table: "product_prices",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_balances_ProductId",
                schema: "pos",
                table: "stock_balances",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_ProductId_CreatedAt",
                schema: "pos",
                table: "stock_movements",
                columns: new[] { "ProductId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_WarehouseId_CreatedAt",
                schema: "pos",
                table: "stock_movements",
                columns: new[] { "WarehouseId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_warehouses_OrganizationId",
                schema: "pos",
                table: "warehouses",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_warehouses_StoreId",
                schema: "pos",
                table: "warehouses",
                column: "StoreId");

            // Бизнеске байланған әр дүкенге «Основной склад». Жаңа дүкенге — WarehouseService.EnsureDefaultAsync.
            migrationBuilder.Sql("""
                INSERT INTO pos.warehouses ("Id", "OrganizationId", "StoreId", "Name", "IsDefault", "IsActive", "CreatedAt")
                SELECT gen_random_uuid(), s."OrganizationId", s."Id", 'Основной склад', TRUE, TRUE, now()
                FROM "Stores" s
                WHERE s."OrganizationId" IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM pos.warehouses w WHERE w."StoreId" = s."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_prices",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "stock_balances",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "stock_movements",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "warehouses",
                schema: "pos");

            migrationBuilder.DropColumn(
                name: "Manufacturer",
                schema: "pos",
                table: "products");

            migrationBuilder.DropColumn(
                name: "Supplier",
                schema: "pos",
                table: "products");
        }
    }
}
