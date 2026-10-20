using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalemBonus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SalesHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Condition",
                schema: "pos",
                table: "sale_returns",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "FromBackOffice",
                schema: "pos",
                table: "sale_returns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReasonCode",
                schema: "pos",
                table: "sale_returns",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StockAction",
                schema: "pos",
                table: "sale_returns",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            // Бұрыннан бар әкімшілерге жаңа бөлімдердің құқықтары: әдепкі жиын тек жаңа қызметкерге беріледі,
            // ал «Склад» пен «Продажи» иесі әлі шешім қабылдамаған жаңа кілттер — оларды қосамыз, басқасына тимейміз.
            migrationBuilder.Sql("""
                UPDATE core.store_memberships m
                SET "Permissions" = (SELECT array_agg(DISTINCT p) FROM unnest(m."Permissions" || ARRAY[
                    'inventory.view', 'inventory.receive', 'inventory.post', 'inventory.transfer', 'inventory.writeoff',
                    'inventory.warehouses', 'inventory.suppliers', 'sales.view', 'sales.export']) AS p)
                WHERE m."Role" = 'Admin';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Condition",
                schema: "pos",
                table: "sale_returns");

            migrationBuilder.DropColumn(
                name: "FromBackOffice",
                schema: "pos",
                table: "sale_returns");

            migrationBuilder.DropColumn(
                name: "ReasonCode",
                schema: "pos",
                table: "sale_returns");

            migrationBuilder.DropColumn(
                name: "StockAction",
                schema: "pos",
                table: "sale_returns");
        }
    }
}
