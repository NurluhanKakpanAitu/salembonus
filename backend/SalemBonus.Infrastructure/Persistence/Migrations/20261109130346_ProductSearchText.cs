using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalemBonus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductSearchText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SearchText",
                schema: "pos",
                table: "products",
                type: "text",
                nullable: true,
                computedColumnSql: "public.salem_fold(\"Name\" || ' ' || coalesce(\"Article\", '') || ' ' || coalesce(\"Description\", '') || ' ' || coalesce(\"Supplier\", '') || ' ' || coalesce(\"Manufacturer\", '') || ' ' || coalesce(\"Country\", ''))",
                stored: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SearchText",
                schema: "pos",
                table: "products");
        }
    }
}
