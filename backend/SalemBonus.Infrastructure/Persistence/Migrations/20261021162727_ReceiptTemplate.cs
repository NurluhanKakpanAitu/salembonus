using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalemBonus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReceiptTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReceiptFooter",
                schema: "pos",
                table: "store_cashier_settings",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptHeader",
                schema: "pos",
                table: "store_cashier_settings",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReceiptShowAddress",
                schema: "pos",
                table: "store_cashier_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReceiptShowBin",
                schema: "pos",
                table: "store_cashier_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ReceiptShowPhone",
                schema: "pos",
                table: "store_cashier_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceiptFooter",
                schema: "pos",
                table: "store_cashier_settings");

            migrationBuilder.DropColumn(
                name: "ReceiptHeader",
                schema: "pos",
                table: "store_cashier_settings");

            migrationBuilder.DropColumn(
                name: "ReceiptShowAddress",
                schema: "pos",
                table: "store_cashier_settings");

            migrationBuilder.DropColumn(
                name: "ReceiptShowBin",
                schema: "pos",
                table: "store_cashier_settings");

            migrationBuilder.DropColumn(
                name: "ReceiptShowPhone",
                schema: "pos",
                table: "store_cashier_settings");
        }
    }
}
