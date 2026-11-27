using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TournamentManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentLifecycleAndRoundRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tournament_byes_TournamentId_PlayerId",
                table: "tournament_byes");

            migrationBuilder.DropIndex(
                name: "IX_tournament_byes_TournamentId_Round",
                table: "tournament_byes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_swiss_rounds",
                table: "swiss_rounds");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FinishedAt",
                table: "tournaments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LifecycleVersion",
                table: "tournaments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsSuperseded",
                table: "tournament_matches",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RoundRevision",
                table: "tournament_matches",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "IsSuperseded",
                table: "tournament_byes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RoundRevision",
                table: "tournament_byes",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "swiss_rounds",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "IsSuperseded",
                table: "swiss_rounds",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "swiss_rounds",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_swiss_rounds",
                table: "swiss_rounds",
                columns: new[] { "TournamentId", "Number", "Revision" });

            migrationBuilder.CreateTable(
                name: "tournament_lifecycle_events",
                columns: table => new
                {
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ActorPlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    bye_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    match_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    result_numbers = table.Column<int[]>(type: "integer[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tournament_lifecycle_events", x => new { x.TournamentId, x.Number });
                    table.ForeignKey(
                        name: "FK_tournament_lifecycle_events_tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tournament_byes_TournamentId_PlayerId",
                table: "tournament_byes",
                columns: new[] { "TournamentId", "PlayerId" },
                unique: true,
                filter: "NOT \"IsSuperseded\"");

            migrationBuilder.CreateIndex(
                name: "IX_tournament_byes_TournamentId_Round",
                table: "tournament_byes",
                columns: new[] { "TournamentId", "Round" },
                unique: true,
                filter: "NOT \"IsSuperseded\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM tournament_lifecycle_events)
                     OR EXISTS (SELECT 1 FROM swiss_rounds WHERE "Revision" > 1 OR "IsSuperseded")
                     OR EXISTS (SELECT 1 FROM tournament_matches WHERE "RoundRevision" > 1 OR "IsSuperseded")
                     OR EXISTS (SELECT 1 FROM tournament_byes WHERE "RoundRevision" > 1 OR "IsSuperseded") THEN
                    RAISE EXCEPTION 'No se puede revertir Slice 13 sin perder historial de cierres o rondas.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "tournament_lifecycle_events");

            migrationBuilder.DropIndex(
                name: "IX_tournament_byes_TournamentId_PlayerId",
                table: "tournament_byes");

            migrationBuilder.DropIndex(
                name: "IX_tournament_byes_TournamentId_Round",
                table: "tournament_byes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_swiss_rounds",
                table: "swiss_rounds");

            migrationBuilder.DropColumn(
                name: "FinishedAt",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "LifecycleVersion",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "IsSuperseded",
                table: "tournament_matches");

            migrationBuilder.DropColumn(
                name: "RoundRevision",
                table: "tournament_matches");

            migrationBuilder.DropColumn(
                name: "IsSuperseded",
                table: "tournament_byes");

            migrationBuilder.DropColumn(
                name: "RoundRevision",
                table: "tournament_byes");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "swiss_rounds");

            migrationBuilder.DropColumn(
                name: "IsSuperseded",
                table: "swiss_rounds");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "swiss_rounds");

            migrationBuilder.AddPrimaryKey(
                name: "PK_swiss_rounds",
                table: "swiss_rounds",
                columns: new[] { "TournamentId", "Number" });

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
    }
}
