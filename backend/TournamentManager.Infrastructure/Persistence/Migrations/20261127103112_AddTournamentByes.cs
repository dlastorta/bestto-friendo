using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TournamentManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentByes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ByePolicyVersion",
                table: "tournament_started_rules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "tournament_byes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorPlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tournament_byes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tournament_byes_players_ActorPlayerId",
                        column: x => x.ActorPlayerId,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tournament_byes_players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tournament_byes_tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tournament_byes_ActorPlayerId",
                table: "tournament_byes",
                column: "ActorPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_tournament_byes_PlayerId",
                table: "tournament_byes",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_tournament_byes_TournamentId_PlayerId",
                table: "tournament_byes",
                columns: new[] { "TournamentId", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tournament_byes_TournamentId_Round",
                table: "tournament_byes",
                columns: new[] { "TournamentId", "Round" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM tournament_byes) OR EXISTS
                    (SELECT 1 FROM tournament_started_rules WHERE "ByePolicyVersion" <> 0) THEN
                    RAISE EXCEPTION 'No se pueden eliminar byes ni políticas históricas de torneos iniciados.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "tournament_byes");

            migrationBuilder.DropColumn(
                name: "ByePolicyVersion",
                table: "tournament_started_rules");
        }
    }
}
