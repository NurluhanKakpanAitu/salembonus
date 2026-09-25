using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalemBonus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBonusExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BonusLifetimeDays",
                table: "Stores",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "BonusTransactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Remaining",
                table: "BonusTransactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_BonusTransactions_ExpiresAt_Remaining",
                table: "BonusTransactions",
                columns: new[] { "ExpiresAt", "Remaining" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BonusTransactions_ExpiresAt_Remaining",
                table: "BonusTransactions");

            migrationBuilder.DropColumn(
                name: "BonusLifetimeDays",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "BonusTransactions");

            migrationBuilder.DropColumn(
                name: "Remaining",
                table: "BonusTransactions");
        }
    }
}
