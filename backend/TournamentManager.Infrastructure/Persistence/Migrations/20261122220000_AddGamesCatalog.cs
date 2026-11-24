using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TournamentManager.Infrastructure.Persistence.Migrations;

[DbContext(typeof(TournamentDbContext))]
[Migration("20261122220000_AddGamesCatalog")]
public sealed class AddGamesCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "games",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                normalized_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                minimum_factions = table.Column<int>(type: "integer", nullable: false),
                maximum_factions = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_games", row => row.id));

        migrationBuilder.CreateTable(
            name: "game_factions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                game_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                normalized_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_game_factions", row => row.id);
                table.ForeignKey("FK_game_factions_games_game_id", row => row.game_id,
                    principalTable: "games", principalColumn: "id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("ux_games_normalized_name", "games", "normalized_name", unique: true);
        migrationBuilder.CreateIndex("ux_game_factions_game_name", "game_factions",
            new[] { "game_id", "normalized_name" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("game_factions");
        migrationBuilder.DropTable("games");
    }
}
