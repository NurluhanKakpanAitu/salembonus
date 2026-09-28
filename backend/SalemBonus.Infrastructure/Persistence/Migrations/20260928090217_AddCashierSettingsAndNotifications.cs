using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalemBonus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCashierSettingsAndNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LowStockThreshold",
                schema: "pos",
                table: "store_cashier_settings",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyCashierChange",
                schema: "pos",
                table: "store_cashier_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyDebt",
                schema: "pos",
                table: "store_cashier_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyLowStock",
                schema: "pos",
                table: "store_cashier_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyOutOfStock",
                schema: "pos",
                table: "store_cashier_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyReturn",
                schema: "pos",
                table: "store_cashier_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "store_notifications",
                schema: "pos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    Number = table.Column<long>(type: "bigint", nullable: true),
                    StaffName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_store_notifications_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_store_notifications_StoreId_CreatedAt",
                schema: "pos",
                table: "store_notifications",
                columns: new[] { "StoreId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "store_notifications",
                schema: "pos");

            migrationBuilder.DropColumn(
                name: "LowStockThreshold",
                schema: "pos",
                table: "store_cashier_settings");

            migrationBuilder.DropColumn(
                name: "NotifyCashierChange",
                schema: "pos",
                table: "store_cashier_settings");

            migrationBuilder.DropColumn(
                name: "NotifyDebt",
                schema: "pos",
                table: "store_cashier_settings");

            migrationBuilder.DropColumn(
                name: "NotifyLowStock",
                schema: "pos",
                table: "store_cashier_settings");

            migrationBuilder.DropColumn(
                name: "NotifyOutOfStock",
                schema: "pos",
                table: "store_cashier_settings");

            migrationBuilder.DropColumn(
                name: "NotifyReturn",
                schema: "pos",
                table: "store_cashier_settings");
        }
    }
}
