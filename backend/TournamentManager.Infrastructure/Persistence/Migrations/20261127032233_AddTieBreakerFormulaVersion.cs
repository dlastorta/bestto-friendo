using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TournamentManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTieBreakerFormulaVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TieBreakerFormulaVersion",
                table: "tournament_started_rules",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM tournament_started_rules WHERE "TieBreakerFormulaVersion" <> 0) THEN
                    RAISE EXCEPTION 'No se puede perder la versión histórica de desempates. Usar una migración correctiva hacia adelante.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropColumn(
                name: "TieBreakerFormulaVersion",
                table: "tournament_started_rules");
        }
    }
}
