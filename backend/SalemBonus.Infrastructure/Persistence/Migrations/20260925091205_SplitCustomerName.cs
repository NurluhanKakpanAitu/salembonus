using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalemBonus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SplitCustomerName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Customers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "Customers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // Бұрынғы аты-жөнін бірінші бос орын бойынша бөлеміз.
            // Аты толтырылмаған тұтынушыда FullName телефон нөмірі болған — ол бос қалады.
            migrationBuilder.Sql("""
                UPDATE "Customers"
                SET "FirstName" = split_part("FullName", ' ', 1),
                    "LastName"  = trim(substring("FullName" from position(' ' in "FullName") + 1))
                WHERE "FullName" IS NOT NULL
                  AND position(' ' in "FullName") > 0
                  AND "FullName" <> "Phone";

                UPDATE "Customers"
                SET "FirstName" = "FullName"
                WHERE "FullName" IS NOT NULL
                  AND position(' ' in "FullName") = 0
                  AND "FullName" <> "Phone";
                """);

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "Customers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "Customers");

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "Customers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }
    }
}
