using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalemBonus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPinAndRegisterSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PinFailedCount",
                schema: "core",
                table: "staff_users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PinLockedUntil",
                schema: "core",
                table: "staff_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActivatedAt",
                schema: "pos",
                table: "registers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AutoLockMinutes",
                schema: "pos",
                table: "registers",
                type: "integer",
                nullable: true);

            // ТЗ «Касса» §17.9: автоблок әдепкі бойынша қосулы, 10 минут. Бос мән «өшірулі» деген
            // сөз болғандықтан, бар кассаларды әдепкі мәнмен толтырамыз.
            migrationBuilder.Sql("""UPDATE pos.registers SET "AutoLockMinutes" = 10 WHERE "AutoLockMinutes" IS NULL;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PinFailedCount",
                schema: "core",
                table: "staff_users");

            migrationBuilder.DropColumn(
                name: "PinLockedUntil",
                schema: "core",
                table: "staff_users");

            migrationBuilder.DropColumn(
                name: "ActivatedAt",
                schema: "pos",
                table: "registers");

            migrationBuilder.DropColumn(
                name: "AutoLockMinutes",
                schema: "pos",
                table: "registers");
        }
    }
}
