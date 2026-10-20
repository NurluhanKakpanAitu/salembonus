using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalemBonus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AnalyticsPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Бұрыннан бар әкімшілерге «Статистика» экспорты мен клиент контактілері (жаңа әдепкі жиын тек жаңа қызметкерге беріледі).
            migrationBuilder.Sql("""
                UPDATE core.store_memberships m
                SET "Permissions" = (SELECT array_agg(DISTINCT p) FROM unnest(m."Permissions" || ARRAY['statistics.export', 'customers.contacts']) AS p)
                WHERE m."Role" = 'Admin';
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
