using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalemBonus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKatoAndCustomerLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KatoCode",
                table: "Customers",
                type: "character varying(9)",
                maxLength: 9,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Kato",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    ParentCode = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    NameKk = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NameRu = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kato", x => x.Code);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Kato_Level",
                table: "Kato",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_Kato_ParentCode",
                table: "Kato",
                column: "ParentCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Kato");

            migrationBuilder.DropColumn(
                name: "KatoCode",
                table: "Customers");
        }
    }
}
