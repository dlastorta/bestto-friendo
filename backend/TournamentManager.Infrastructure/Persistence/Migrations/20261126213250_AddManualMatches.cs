using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TournamentManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManualMatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MatchesVersion",
                table: "tournaments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "tournament_matches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    PlayerOneId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerTwoId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByPlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tournament_matches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tournament_matches_players_PlayerOneId",
                        column: x => x.PlayerOneId,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tournament_matches_players_PlayerTwoId",
                        column: x => x.PlayerTwoId,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tournament_matches_tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "match_result_revisions",
                columns: table => new
                {
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ActorPlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_match_result_revisions", x => new { x.MatchId, x.Number });
                    table.ForeignKey(
                        name: "FK_match_result_revisions_tournament_matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "tournament_matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tournament_matches_PlayerOneId",
                table: "tournament_matches",
                column: "PlayerOneId");

            migrationBuilder.CreateIndex(
                name: "IX_tournament_matches_PlayerTwoId",
                table: "tournament_matches",
                column: "PlayerTwoId");

            migrationBuilder.CreateIndex(
                name: "IX_tournament_matches_TournamentId_Round",
                table: "tournament_matches",
                columns: new[] { "TournamentId", "Round" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM tournament_matches) THEN
                    RAISE EXCEPTION 'No se puede revertir sin perder partidas e historial. Usar una migración correctiva hacia adelante.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "match_result_revisions");

            migrationBuilder.DropTable(
                name: "tournament_matches");

            migrationBuilder.DropColumn(
                name: "MatchesVersion",
                table: "tournaments");
        }
    }
}
