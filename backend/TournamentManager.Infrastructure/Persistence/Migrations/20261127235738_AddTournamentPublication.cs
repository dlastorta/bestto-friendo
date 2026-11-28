using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TournamentManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentPublication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PublicationVersion",
                table: "tournaments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PublishedAt",
                table: "tournaments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PublicSnapshotNumber",
                table: "tournament_registrations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VisibilityChangedAt",
                table: "tournament_registrations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VisibilityVersion",
                table: "tournament_registrations",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublicationVersion",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "PublicSnapshotNumber",
                table: "tournament_registrations");

            migrationBuilder.DropColumn(
                name: "VisibilityChangedAt",
                table: "tournament_registrations");

            migrationBuilder.DropColumn(
                name: "VisibilityVersion",
                table: "tournament_registrations");
        }
    }
}
