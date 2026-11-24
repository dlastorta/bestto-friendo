using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TournamentManager.Infrastructure.Persistence.Migrations;

[DbContext(typeof(TournamentDbContext))]
[Migration("20261122000000_InitialBaseline")]
public sealed class InitialBaseline : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Baseline vacía; las tablas llegarán con los slices correspondientes.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
