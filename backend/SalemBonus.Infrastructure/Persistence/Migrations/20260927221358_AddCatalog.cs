using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalemBonus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "brands",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LogoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_brands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_brands_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "core",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "catalog_nodes",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Icon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Path = table.Column<string>(type: "character varying(444)", maxLength: 444, nullable: false),
                    Depth = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_nodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_nodes_catalog_nodes_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "pos",
                        principalTable: "catalog_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_catalog_nodes_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "core",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "characteristic_definitions",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_characteristic_definitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_characteristic_definitions_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "core",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "units",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ShortName = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_units", x => x.Id);
                    table.ForeignKey(
                        name: "FK_units_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "core",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "characteristic_options",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_characteristic_options", x => x.Id);
                    table.ForeignKey(
                        name: "FK_characteristic_options_characteristic_definitions_Definitio~",
                        column: x => x.DefinitionId,
                        principalSchema: "pos",
                        principalTable: "characteristic_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "products",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Article = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: true),
                    CatalogNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Country = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    WarrantyMonths = table.Column<int>(type: "integer", nullable: true),
                    ShelfLifeDays = table.Column<int>(type: "integer", nullable: true),
                    VatRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    IsMarked = table.Column<bool>(type: "boolean", nullable: false),
                    IsRecommended = table.Column<bool>(type: "boolean", nullable: false),
                    HideInCashier = table.Column<bool>(type: "boolean", nullable: false),
                    BonusEligible = table.Column<bool>(type: "boolean", nullable: false),
                    MaxDiscountPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_products_brands_BrandId",
                        column: x => x.BrandId,
                        principalSchema: "pos",
                        principalTable: "brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_catalog_nodes_CatalogNodeId",
                        column: x => x.CatalogNodeId,
                        principalSchema: "pos",
                        principalTable: "catalog_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "core",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_units_UnitId",
                        column: x => x.UnitId,
                        principalSchema: "pos",
                        principalTable: "units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_barcodes",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Barcode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_barcodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_barcodes_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "pos",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_characteristics",
                schema: "pos",
                columns: table => new
                {
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_characteristics", x => new { x.ProductId, x.DefinitionId });
                    table.ForeignKey(
                        name: "FK_product_characteristics_characteristic_definitions_Definiti~",
                        column: x => x.DefinitionId,
                        principalSchema: "pos",
                        principalTable: "characteristic_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_characteristics_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "pos",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_images",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_images", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_images_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "pos",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Бар бизнестерге әдепкі өлшем бірліктері (MeasureUnit.Defaults). Жаңа бизнеске —
            // CatalogDefaults арқылы. Реті CreatedAt бойынша сақталады.
            migrationBuilder.Sql("""
                INSERT INTO pos.units ("Id", "OrganizationId", "Name", "ShortName", "Status", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), o."Id", u.name, u.short, 'Active',
                       now() + (u.ord * interval '1 millisecond'), now()
                FROM core.organizations o
                CROSS JOIN (VALUES ('Штука', 'шт', 1), ('Килограмм', 'кг', 2), ('Грамм', 'г', 3), ('Литр', 'л', 4),
                                   ('Метр', 'м', 5), ('Комплект', 'компл', 6), ('Упаковка', 'уп', 7), ('Пара', 'пар', 8))
                     AS u(name, short, ord)
                WHERE NOT EXISTS (SELECT 1 FROM pos.units x WHERE x."OrganizationId" = o."Id");
                """);

            migrationBuilder.CreateIndex(
                name: "IX_brands_OrganizationId_SortOrder",
                schema: "pos",
                table: "brands",
                columns: new[] { "OrganizationId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_nodes_OrganizationId_ParentId_SortOrder",
                schema: "pos",
                table: "catalog_nodes",
                columns: new[] { "OrganizationId", "ParentId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_nodes_OrganizationId_Path",
                schema: "pos",
                table: "catalog_nodes",
                columns: new[] { "OrganizationId", "Path" });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_nodes_ParentId",
                schema: "pos",
                table: "catalog_nodes",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_characteristic_definitions_OrganizationId_SortOrder",
                schema: "pos",
                table: "characteristic_definitions",
                columns: new[] { "OrganizationId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_characteristic_options_DefinitionId",
                schema: "pos",
                table: "characteristic_options",
                column: "DefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_product_barcodes_OrganizationId_Barcode",
                schema: "pos",
                table: "product_barcodes",
                columns: new[] { "OrganizationId", "Barcode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_barcodes_ProductId",
                schema: "pos",
                table: "product_barcodes",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_product_characteristics_DefinitionId",
                schema: "pos",
                table: "product_characteristics",
                column: "DefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_product_images_ProductId",
                schema: "pos",
                table: "product_images",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_products_BrandId",
                schema: "pos",
                table: "products",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_products_CatalogNodeId",
                schema: "pos",
                table: "products",
                column: "CatalogNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_products_OrganizationId_Article",
                schema: "pos",
                table: "products",
                columns: new[] { "OrganizationId", "Article" });

            migrationBuilder.CreateIndex(
                name: "IX_products_OrganizationId_Name",
                schema: "pos",
                table: "products",
                columns: new[] { "OrganizationId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_products_UnitId",
                schema: "pos",
                table: "products",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_units_OrganizationId",
                schema: "pos",
                table: "units",
                column: "OrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "characteristic_options",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "product_barcodes",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "product_characteristics",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "product_images",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "characteristic_definitions",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "products",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "brands",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "catalog_nodes",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "units",
                schema: "pos");
        }
    }
}
