using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TournamentManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RulesVersion",
                table: "tournaments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "tournament_rules",
                columns: table => new
                {
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    WinPoints = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    DrawPoints = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    LossPoints = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    ByesEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ByePoints = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    tie_breakers = table.Column<string[]>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tournament_rules", x => x.TournamentId);
                    table.ForeignKey(
                        name: "FK_tournament_rules_tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tournament_rules");

            migrationBuilder.DropColumn(
                name: "RulesVersion",
                table: "tournaments");
        }
    }
}
