using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TournamentManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentStartAndSubmissionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_tournament_deck_snapshots",
                table: "tournament_deck_snapshots");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartedAt",
                table: "tournaments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Number",
                table: "tournament_deck_snapshots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ActorPlayerId",
                table: "tournament_deck_snapshots",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "tournament_deck_snapshots",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_tournament_deck_snapshots",
                table: "tournament_deck_snapshots",
                columns: new[] { "RegistrationId", "Number" });

            migrationBuilder.Sql("""
                UPDATE tournament_deck_snapshots AS s
                SET "Number" = 1, "ActorPlayerId" = r."PlayerId", "Reason" = 'Presentación inicial'
                FROM tournament_registrations AS r
                WHERE r."Id" = s."RegistrationId";
                """);

            migrationBuilder.CreateTable(
                name: "tournament_started_rules",
                columns: table => new
                {
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceVersion = table.Column<int>(type: "integer", nullable: false),
                    WinPoints = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    DrawPoints = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    LossPoints = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    ByePoints = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    ByesEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MaximumByesPerParticipant = table.Column<int>(type: "integer", nullable: false),
                    tie_breakers = table.Column<string[]>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tournament_started_rules", x => x.TournamentId);
                    table.ForeignKey(
                        name: "FK_tournament_started_rules_tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM tournament_started_rules)
                    OR EXISTS (SELECT 1 FROM tournament_deck_snapshots WHERE "Number" > 1) THEN
                    RAISE EXCEPTION 'No se puede revertir sin perder reglas históricas o presentaciones. Conservar respaldo y migrar hacia adelante.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "tournament_started_rules");

            migrationBuilder.DropPrimaryKey(
                name: "PK_tournament_deck_snapshots",
                table: "tournament_deck_snapshots");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "Number",
                table: "tournament_deck_snapshots");

            migrationBuilder.DropColumn(
                name: "ActorPlayerId",
                table: "tournament_deck_snapshots");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "tournament_deck_snapshots");

            migrationBuilder.AddPrimaryKey(
                name: "PK_tournament_deck_snapshots",
                table: "tournament_deck_snapshots",
                column: "RegistrationId");
        }
    }
}
