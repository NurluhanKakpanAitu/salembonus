using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalemBonus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationReceiptId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReceiptId",
                table: "Notifications",
                type: "uuid",
                nullable: true);

            // Бұрынғы хабарламаларды өз чегімен байланыстырамыз: сол клиенттің сол дүкендегі,
            // сомасы мен уақыты сәйкес келетін операциясын іздейміз.
            migrationBuilder.Sql("""
                WITH matched AS (
                    SELECT DISTINCT ON (n."Id") n."Id" AS notification_id, t."ReceiptId" AS receipt_id
                    FROM "Notifications" n
                    JOIN "BonusCards" c
                      ON c."CustomerId" = n."CustomerId" AND c."StoreId" = n."StoreId"
                    JOIN "BonusTransactions" t
                      ON t."BonusCardId" = c."Id"
                     AND t."ReceiptId" IS NOT NULL
                     AND t."PurchaseAmount" = n."PurchaseAmount"
                     AND abs(t."Amount") = n."Amount"
                     AND t."CreatedAt" BETWEEN n."CreatedAt" - interval '5 seconds'
                                           AND n."CreatedAt" + interval '5 seconds'
                    WHERE n."ReceiptId" IS NULL
                      AND n."PurchaseAmount" IS NOT NULL
                      AND n."Amount" IS NOT NULL
                    ORDER BY n."Id", abs(extract(epoch FROM t."CreatedAt" - n."CreatedAt"))
                )
                UPDATE "Notifications" x
                SET "ReceiptId" = m.receipt_id
                FROM matched m
                WHERE x."Id" = m.notification_id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceiptId",
                table: "Notifications");
        }
    }
}
