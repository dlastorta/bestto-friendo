using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using TournamentManager.Domain.Games;
using TournamentManager.Domain.Players;
using TournamentManager.Domain.Decks;
using TournamentManager.Domain.Tournaments;

namespace TournamentManager.Infrastructure.Persistence;

// Todo lo concerniente a EF se mantiene encapsulado en Infrastructure.
public sealed class TournamentDbContext(DbContextOptions<TournamentDbContext> options)
    : IdentityDbContext<IdentityUser<Guid>, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Deck> Decks => Set<Deck>();
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<TournamentRegistration> Registrations => Set<TournamentRegistration>();
    public DbSet<TournamentMatch> Matches => Set<TournamentMatch>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        var match = modelBuilder.Entity<TournamentMatch>();
        match.ToTable("tournament_matches");
        match.HasKey(m => m.Id);
        match.Property(m => m.Id).ValueGeneratedNever();
        match.Property(m => m.Version).IsConcurrencyToken();
        match.HasIndex(m => new { m.TournamentId, m.Round });
        match.HasOne<Tournament>().WithMany().HasForeignKey(m => m.TournamentId).OnDelete(DeleteBehavior.Restrict);
        match.HasOne<Player>().WithMany().HasForeignKey(m => m.PlayerOneId).OnDelete(DeleteBehavior.Restrict);
        match.HasOne<Player>().WithMany().HasForeignKey(m => m.PlayerTwoId).OnDelete(DeleteBehavior.Restrict);
        match.Ignore(m => m.CurrentResult);
        match.HasMany(m => m.Results).WithOne().HasForeignKey(r => r.MatchId).OnDelete(DeleteBehavior.Restrict);
        match.Navigation(m => m.Results).HasField("_results").UsePropertyAccessMode(PropertyAccessMode.Field);
        var result = modelBuilder.Entity<MatchResultRevision>();
        result.ToTable("match_result_revisions");
        result.HasKey(r => new { r.MatchId, r.Number });
        result.Property(r => r.Number).ValueGeneratedNever();
        result.Property(r => r.Outcome).HasMaxLength(30).IsRequired();
        result.Property(r => r.Kind).HasMaxLength(30).IsRequired();
        result.Property(r => r.Reason).HasMaxLength(500).IsRequired();
        var tournament = modelBuilder.Entity<Tournament>();
        tournament.ToTable("tournaments");
        tournament.HasKey(item => item.Id);
        tournament.Property(item => item.Id).ValueGeneratedNever();
        tournament.Property(item => item.Name).HasMaxLength(120).IsRequired();
        tournament.Property(item => item.Status).HasConversion<string>().HasMaxLength(30).IsRequired().IsConcurrencyToken();
        tournament.HasOne(t => t.StartedRules).WithOne().HasForeignKey<FrozenTournamentRules>(r => r.TournamentId).OnDelete(DeleteBehavior.Restrict);
        var frozen = modelBuilder.Entity<FrozenTournamentRules>();
        frozen.ToTable("tournament_started_rules");
        frozen.HasKey(r => r.TournamentId);
        frozen.Property(r => r.TournamentId).ValueGeneratedNever();
        frozen.Property(r => r.WinPoints).HasPrecision(9, 2);
        frozen.Property(r => r.DrawPoints).HasPrecision(9, 2);
        frozen.Property(r => r.LossPoints).HasPrecision(9, 2);
        frozen.Property(r => r.ByePoints).HasPrecision(9, 2);
        frozen.Ignore(r => r.TieBreakers);
        frozen.Property<string[]>("_tieBreakers").HasColumnName("tie_breakers").HasColumnType("text[]").IsRequired();
        tournament.Property(item => item.RulesVersion).IsConcurrencyToken();
        tournament.Property(item => item.MatchesVersion).IsConcurrencyToken();
        tournament.Property(item => item.ParticipationVersion).IsConcurrencyToken();
        tournament.Property(item => item.RegistrationToken).IsConcurrencyToken();
        tournament.HasIndex(item => item.RegistrationToken).IsUnique();
        var registration = modelBuilder.Entity<TournamentRegistration>();
        registration.ToTable("tournament_registrations");
        registration.HasKey(r => r.Id);
        registration.Property(r => r.Id).ValueGeneratedNever();
        registration.HasIndex(r => new { r.TournamentId, r.PlayerId }).IsUnique();
        registration.HasOne<Tournament>().WithMany().HasForeignKey(r => r.TournamentId).OnDelete(DeleteBehavior.Restrict);
        registration.HasOne<Player>().WithMany().HasForeignKey(r => r.PlayerId).OnDelete(DeleteBehavior.Restrict);
        registration.Ignore(r => r.Snapshot);
        registration.HasMany(r => r.Snapshots).WithOne().HasForeignKey(s => s.RegistrationId).OnDelete(DeleteBehavior.Restrict);
        registration.Navigation(r => r.Snapshots).HasField("_snapshots").UsePropertyAccessMode(PropertyAccessMode.Field);
        var snapshot = modelBuilder.Entity<TournamentDeckSnapshot>();
        snapshot.ToTable("tournament_deck_snapshots");
        snapshot.HasKey(s => new { s.RegistrationId, s.Number });
        snapshot.Property(s => s.Number).ValueGeneratedNever();
        snapshot.Property(s => s.Reason).HasMaxLength(500).IsRequired();
        snapshot.Property(s => s.RegistrationId).ValueGeneratedNever();
        snapshot.Property(s => s.Name).HasMaxLength(120).IsRequired();
        snapshot.Property(s => s.ListText).IsRequired();
        snapshot.Ignore(s => s.FactionIds);
        snapshot.Property<Guid[]>("_factionIds").HasColumnName("faction_ids").HasColumnType("uuid[]").IsRequired();
        snapshot.HasOne<DeckRevision>().WithMany().HasForeignKey(s => new { s.SourceDeckId, s.SourceRevision }).OnDelete(DeleteBehavior.Restrict);
        tournament.HasOne(item => item.Rules).WithOne().HasForeignKey<TournamentRules>(item => item.TournamentId).OnDelete(DeleteBehavior.Cascade);
        var rules = modelBuilder.Entity<TournamentRules>();
        rules.ToTable("tournament_rules");
        rules.HasKey(item => item.TournamentId);
        rules.Property(item => item.TournamentId).ValueGeneratedNever();
        rules.Property(item => item.WinPoints).HasPrecision(9, 2);
        rules.Property(item => item.DrawPoints).HasPrecision(9, 2);
        rules.Property(item => item.LossPoints).HasPrecision(9, 2);
        rules.Property(item => item.ByePoints).HasPrecision(9, 2);
        rules.Ignore(item => item.TieBreakers);
        rules.Property<string[]>("_tieBreakers").HasColumnName("tie_breakers").HasColumnType("text[]").IsRequired();
        tournament.HasIndex(item => new { item.OrganizerPlayerId, item.GameId });
        tournament.HasOne<Player>().WithMany().HasForeignKey(item => item.OrganizerPlayerId).OnDelete(DeleteBehavior.Restrict);
        tournament.HasOne<Game>().WithMany().HasForeignKey(item => item.GameId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<IdentityRole<Guid>>().HasData(new IdentityRole<Guid>
        {
            Id = Guid.Parse("781621bb-bde8-442c-b945-d6767f49271a"),
            Name = "TournamentOrganizer", NormalizedName = "TOURNAMENTORGANIZER",
            ConcurrencyStamp = "dce35fe0-3322-40e2-8f26-1c37a3f901c6"
        });
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
