using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalemBonus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPosAndQr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApiKey",
                table: "Stores",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "MaxRedeemPercent",
                table: "Stores",
                type: "TEXT",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "QrCode",
                table: "Customers",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Stores_ApiKey",
                table: "Stores",
                column: "ApiKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_QrCode",
                table: "Customers",
                column: "QrCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Stores_ApiKey",
                table: "Stores");

            migrationBuilder.DropIndex(
                name: "IX_Customers_QrCode",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "ApiKey",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "MaxRedeemPercent",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "QrCode",
                table: "Customers");
        }
    }
}
