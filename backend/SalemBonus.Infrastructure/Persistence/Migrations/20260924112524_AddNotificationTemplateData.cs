using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalemBonus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationTemplateData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Amount",
                table: "Notifications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LevelKey",
                table: "Notifications",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PurchaseAmount",
                table: "Notifications",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateKey",
                table: "Notifications",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            // Бұрынғы хабарламалар мәтін күйінде сақталған. Сан мәндерін мәтіннен сенімді түрде
            // ажырата алсақ қана үлгі кілтін қоямыз — әйтпесе жазба ескі мәтінімен қала береді.
            migrationBuilder.Sql("""
                UPDATE "Notifications"
                SET "Amount" = NULLIF(regexp_replace(substring("Body" from '[0-9][^Б]*Б'), '[^0-9]', '', 'g'), '')::int,
                    "PurchaseAmount" = NULLIF(regexp_replace(coalesce(substring("Detail" from '[0-9][^₸]*₸'), ''), '[^0-9]', '', 'g'), '')::numeric,
                    "TemplateKey" = CASE "Type"
                        WHEN 0 THEN 'bonus_accrued'
                        WHEN 1 THEN 'bonus_redeemed'
                        WHEN 2 THEN 'birthday'
                    END
                WHERE "TemplateKey" IS NULL
                  AND "Type" IN (0, 1, 2)
                  AND "Body" ~ '[0-9][^Б]*Б';

                UPDATE "Notifications"
                SET "TemplateKey" = 'store_added'
                WHERE "TemplateKey" IS NULL AND "Type" = 4;

                UPDATE "Notifications"
                SET "TemplateKey" = 'profile_updated'
                WHERE "TemplateKey" IS NULL AND "Type" = 5;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Amount",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "LevelKey",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "PurchaseAmount",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "TemplateKey",
                table: "Notifications");
        }
    }
}
