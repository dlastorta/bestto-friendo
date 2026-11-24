using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TournamentManager.Domain.Games;

namespace TournamentManager.Infrastructure.Persistence.Migrations;

[DbContext(typeof(TournamentDbContext))]
public sealed class TournamentDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.3");

        modelBuilder.Entity<Game>(game =>
        {
            game.ToTable("games");
            game.HasKey(item => item.Id);
            game.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
            game.Property(item => item.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
            game.Property(item => item.NormalizedName).HasColumnName("normalized_name").HasMaxLength(120).IsRequired();
            game.Property(item => item.MinimumFactions).HasColumnName("minimum_factions");
            game.Property(item => item.MaximumFactions).HasColumnName("maximum_factions");
            game.HasIndex(item => item.NormalizedName).IsUnique().HasDatabaseName("ux_games_normalized_name");

            game.HasMany(item => item.Factions).WithOne().HasForeignKey(item => item.GameId)
                .OnDelete(DeleteBehavior.Cascade);
            game.Navigation(item => item.Factions).HasField("_factions").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<GameFaction>(faction =>
        {
            faction.ToTable("game_factions");
            faction.HasKey(item => item.Id);
            faction.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
            faction.Property(item => item.GameId).HasColumnName("game_id");
            faction.Property(item => item.Name).HasColumnName("name").HasMaxLength(80).IsRequired();
            faction.Property(item => item.NormalizedName).HasColumnName("normalized_name").HasMaxLength(80).IsRequired();
            faction.HasIndex(item => new { item.GameId, item.NormalizedName }).IsUnique()
                .HasDatabaseName("ux_game_factions_game_name");
        });
    }
}
