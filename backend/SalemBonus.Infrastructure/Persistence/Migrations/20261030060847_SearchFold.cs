using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalemBonus.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// salem_fold(text): іздеу үшін кіші әріпке келтіру. lower() латынды келтіреді, translate — орыс және қазақ бас әріптерін
    /// (базаның локалі C болса, lower/ILIKE кириллицаны келтірмейді). IMMUTABLE — кейін индекс жасауға болады.
    /// </summary>
    public partial class SearchFold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION public.salem_fold(t text) RETURNS text
                LANGUAGE sql IMMUTABLE PARALLEL SAFE RETURNS NULL ON NULL INPUT AS $$
                    SELECT translate(lower(t),
                        'АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯӘҒҚҢӨҰҮҺІ',
                        'абвгдеёжзийклмнопрстуфхцчшщъыьэюяәғқңөұүһі')
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS public.salem_fold(text);");
        }
    }
}
