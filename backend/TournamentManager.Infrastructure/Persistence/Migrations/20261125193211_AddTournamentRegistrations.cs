using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TournamentManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentRegistrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParticipationVersion",
                table: "tournaments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "RegistrationToken",
                table: "tournaments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tournament_registrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegisteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tournament_registrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tournament_registrations_players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tournament_registrations_tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tournament_deck_snapshots",
                columns: table => new
                {
                    RegistrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceDeckId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceRevision = table.Column<int>(type: "integer", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ListText = table.Column<string>(type: "text", nullable: false),
                    PresentedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    faction_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tournament_deck_snapshots", x => x.RegistrationId);
                    table.ForeignKey(
                        name: "FK_tournament_deck_snapshots_deck_revisions_SourceDeckId_Sourc~",
                        columns: x => new { x.SourceDeckId, x.SourceRevision },
                        principalTable: "deck_revisions",
                        principalColumns: new[] { "DeckId", "Number" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tournament_deck_snapshots_tournament_registrations_Registra~",
                        column: x => x.RegistrationId,
                        principalTable: "tournament_registrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tournaments_RegistrationToken",
                table: "tournaments",
                column: "RegistrationToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tournament_deck_snapshots_SourceDeckId_SourceRevision",
                table: "tournament_deck_snapshots",
                columns: new[] { "SourceDeckId", "SourceRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_tournament_registrations_PlayerId",
                table: "tournament_registrations",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_tournament_registrations_TournamentId_PlayerId",
                table: "tournament_registrations",
                columns: new[] { "TournamentId", "PlayerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tournament_deck_snapshots");

            migrationBuilder.DropTable(
                name: "tournament_registrations");

            migrationBuilder.DropIndex(
                name: "IX_tournaments_RegistrationToken",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "ParticipationVersion",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "RegistrationToken",
                table: "tournaments");
        }
    }
}
