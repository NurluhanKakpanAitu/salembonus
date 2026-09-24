using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalemBonus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreJoinCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JoinCode",
                table: "Stores",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            // Бар дүкендерге қосылу кодын беру: бос мән бірегей индексті бұзады.
            migrationBuilder.Sql("""
                UPDATE "Stores" SET "JoinCode" = 'MKMAUTO'  WHERE "Id" = '11111111-1111-1111-1111-111111111111';
                UPDATE "Stores" SET "JoinCode" = 'COFFEE'   WHERE "Id" = '22222222-2222-2222-2222-222222222222';
                UPDATE "Stores" SET "JoinCode" = 'BEAUTY'   WHERE "Id" = '33333333-3333-3333-3333-333333333333';
                UPDATE "Stores" SET "JoinCode" = 'SPORT'    WHERE "Id" = '44444444-4444-4444-4444-444444444444';
                UPDATE "Stores" SET "JoinCode" = 'DARIHANA' WHERE "Id" = '55555555-5555-5555-5555-555555555555';
                UPDATE "Stores" SET "JoinCode" = 'NANUI'    WHERE "Id" = '66666666-6666-6666-6666-666666666666';
                UPDATE "Stores" SET "JoinCode" = upper(left(replace("Id"::text, '-', ''), 10)) WHERE "JoinCode" = '';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Stores_JoinCode",
                table: "Stores",
                column: "JoinCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Stores_JoinCode",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "JoinCode",
                table: "Stores");
        }
    }
}
