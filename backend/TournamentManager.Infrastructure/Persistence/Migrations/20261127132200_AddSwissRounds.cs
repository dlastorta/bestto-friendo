using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TournamentManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSwissRounds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ResultsVersion",
                table: "tournaments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PairingAlgorithmVersion",
                table: "tournament_started_rules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "swiss_rounds",
                columns: table => new
                {
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    AlgorithmVersion = table.Column<int>(type: "integer", nullable: false),
                    ActorPlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SourceMatchesVersion = table.Column<int>(type: "integer", nullable: false),
                    SourceResultsVersion = table.Column<int>(type: "integer", nullable: false),
                    participant_order = table.Column<Guid[]>(type: "uuid[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_swiss_rounds", x => new { x.TournamentId, x.Number });
                    table.ForeignKey(
                        name: "FK_swiss_rounds_players_ActorPlayerId",
                        column: x => x.ActorPlayerId,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_swiss_rounds_tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_swiss_rounds_ActorPlayerId",
                table: "swiss_rounds",
                column: "ActorPlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM swiss_rounds) OR EXISTS
                    (SELECT 1 FROM tournament_started_rules WHERE "PairingAlgorithmVersion" <> 0) THEN
                    RAISE EXCEPTION 'No se pueden eliminar rondas ni algoritmos históricos de torneos iniciados.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "swiss_rounds");

            migrationBuilder.DropColumn(
                name: "ResultsVersion",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "PairingAlgorithmVersion",
                table: "tournament_started_rules");
        }
    }
}
