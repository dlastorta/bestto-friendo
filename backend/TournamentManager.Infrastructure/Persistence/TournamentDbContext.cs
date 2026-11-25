using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using TournamentManager.Domain.Games;
using TournamentManager.Domain.Players;
using TournamentManager.Domain.Decks;

namespace TournamentManager.Infrastructure.Persistence;

// Todo lo concerniente a EF se mantiene encapsulado en Infrastructure.
public sealed class TournamentDbContext(DbContextOptions<TournamentDbContext> options)
    : IdentityDbContext<IdentityUser<Guid>, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Deck> Decks => Set<Deck>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        var deck = modelBuilder.Entity<Deck>();
        deck.ToTable("decks");
        deck.HasKey(item => item.Id);
        deck.Property(item => item.Id).ValueGeneratedNever();
        deck.Property(item => item.Version).IsConcurrencyToken();
        deck.Ignore(item => item.CurrentRevision);
        deck.HasIndex(item => new { item.OwnerPlayerId, item.GameId, item.IsArchived });
        deck.HasOne<Player>().WithMany().HasForeignKey(item => item.OwnerPlayerId).OnDelete(DeleteBehavior.Restrict);
        deck.HasOne<Game>().WithMany().HasForeignKey(item => item.GameId).OnDelete(DeleteBehavior.Restrict);
        deck.HasMany(item => item.Revisions).WithOne().HasForeignKey(item => item.DeckId).OnDelete(DeleteBehavior.Restrict);
        deck.Navigation(item => item.Revisions).HasField("_revisions").UsePropertyAccessMode(PropertyAccessMode.Field);
        var revision = modelBuilder.Entity<DeckRevision>();
        revision.ToTable("deck_revisions");
        revision.HasKey(item => new { item.DeckId, item.Number });
        revision.Property(item => item.Number).ValueGeneratedNever();
        revision.Property(item => item.Name).HasMaxLength(120).IsRequired();
        revision.Property(item => item.ListText).IsRequired();
        revision.Ignore(item => item.FactionIds);
        revision.Property<Guid[]>("_factionIds").HasColumnName("faction_ids").HasColumnType("uuid[]").IsRequired();
        modelBuilder.Entity<IdentityRole<Guid>>().HasData(new IdentityRole<Guid>
        {
            Id = Guid.Parse("663147a8-7ec9-44dd-955c-ce06653cdf45"),
            Name = "CatalogAdmin", NormalizedName = "CATALOGADMIN",
            ConcurrencyStamp = "31a9e412-2e67-4420-bcb1-9cb2f84e11cc"
        });
        modelBuilder.Entity<IdentityUser<Guid>>().HasIndex(account => account.NormalizedEmail).IsUnique();
        var player = modelBuilder.Entity<Player>();
        player.ToTable("players");
        player.HasKey(item => item.Id);
        player.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        player.Property(item => item.AccountId).HasColumnName("account_id");
        player.Property(item => item.DisplayName).HasColumnName("display_name").HasMaxLength(80).IsRequired();
        player.HasIndex(item => item.AccountId).IsUnique();
        player.HasOne<IdentityUser<Guid>>().WithOne().HasForeignKey<Player>(item => item.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        var game = modelBuilder.Entity<Game>();
        game.ToTable("games");
        game.HasKey(item => item.Id);
        game.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        game.Property(item => item.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        game.Property(item => item.NormalizedName).HasColumnName("normalized_name").HasMaxLength(120).IsRequired();
        game.Property(item => item.MinimumFactions).HasColumnName("minimum_factions");
        game.Property(item => item.MaximumFactions).HasColumnName("maximum_factions");
        game.HasIndex(item => item.NormalizedName).IsUnique().HasDatabaseName("ux_games_normalized_name");

        game.HasMany(item => item.Factions)
            .WithOne()
            .HasForeignKey(item => item.GameId)
            .OnDelete(DeleteBehavior.Cascade);
        game.Navigation(item => item.Factions).HasField("_factions").UsePropertyAccessMode(PropertyAccessMode.Field);

        var faction = modelBuilder.Entity<GameFaction>();
        faction.ToTable("game_factions");
        faction.HasKey(item => item.Id);
        faction.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        faction.Property(item => item.GameId).HasColumnName("game_id");
        faction.Property(item => item.Name).HasColumnName("name").HasMaxLength(80).IsRequired();
        faction.Property(item => item.NormalizedName).HasColumnName("normalized_name").HasMaxLength(80).IsRequired();
        faction.HasIndex(item => new { item.GameId, item.NormalizedName })
            .IsUnique().HasDatabaseName("ux_game_factions_game_name");
    }
}
