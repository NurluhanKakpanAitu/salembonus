using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalemBonus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DebtManualAndClose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Method",
                schema: "pos",
                table: "supplier_payments",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AddColumn<bool>(
                name: "IsWriteOff",
                schema: "pos",
                table: "supplier_payments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "WrittenOff",
                schema: "pos",
                table: "stock_documents",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Comment",
                schema: "pos",
                table: "debt_payments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsWriteOff",
                schema: "pos",
                table: "debt_payments",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsWriteOff",
                schema: "pos",
                table: "supplier_payments");

            migrationBuilder.DropColumn(
                name: "WrittenOff",
                schema: "pos",
                table: "stock_documents");

            migrationBuilder.DropColumn(
                name: "Comment",
                schema: "pos",
                table: "debt_payments");

            migrationBuilder.DropColumn(
                name: "IsWriteOff",
                schema: "pos",
                table: "debt_payments");

            migrationBuilder.AlterColumn<string>(
                name: "Method",
                schema: "pos",
                table: "supplier_payments",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldNullable: true);
        }
    }
}
