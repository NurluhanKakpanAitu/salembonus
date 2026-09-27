using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalemBonus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionReceiptId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReceiptId",
                table: "BonusTransactions",
                type: "uuid",
                nullable: true);

            // Бұрынғы операцияларға чек нөмірін береміз. Бір сатып алуда бонус шегеру мен
            // есептеу бөлек жазылған: бір картада, бір сомамен, бір-екі секунд ішінде —
            // соларды жұптап, бір чекке біріктіреміз.
            migrationBuilder.Sql("""
                WITH pairs AS (
                    SELECT DISTINCT ON (r."Id")
                           r."Id" AS redemption_id, a."Id" AS accrual_id, gen_random_uuid() AS receipt_id
                    FROM "BonusTransactions" r
                    JOIN "BonusTransactions" a
                      ON a."BonusCardId" = r."BonusCardId"
                     AND a."PurchaseAmount" = r."PurchaseAmount"
                     AND a."Amount" > 0
                     AND a."CreatedAt" BETWEEN r."CreatedAt" - interval '5 seconds'
                                           AND r."CreatedAt" + interval '5 seconds'
                    WHERE r."Amount" < 0 AND r."PurchaseAmount" IS NOT NULL
                    ORDER BY r."Id", abs(extract(epoch FROM a."CreatedAt" - r."CreatedAt"))
                )
                UPDATE "BonusTransactions" t
                SET "ReceiptId" = p.receipt_id
                FROM pairs p
                WHERE t."Id" = p.redemption_id OR t."Id" = p.accrual_id;
                """);

            // Жұбы жоқ операциялар — өз бетінше бір чек.
            migrationBuilder.Sql("""
                UPDATE "BonusTransactions"
                SET "ReceiptId" = gen_random_uuid()
                WHERE "PurchaseAmount" IS NOT NULL AND "ReceiptId" IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_BonusTransactions_ReceiptId",
                table: "BonusTransactions",
                column: "ReceiptId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BonusTransactions_ReceiptId",
                table: "BonusTransactions");

            migrationBuilder.DropColumn(
                name: "ReceiptId",
                table: "BonusTransactions");
        }
    }
}
