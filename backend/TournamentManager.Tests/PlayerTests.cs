using TournamentManager.Domain.Players;
using Xunit;

namespace TournamentManager.Tests;

public sealed class PlayerTests
{
    [Fact]
    public void Rename_PreservesAccountAndPlayerIdentity()
    {
        var accountId = Guid.NewGuid();
        var player = Player.Create(accountId, "  Ana  ");
        var playerId = player.Id;
        Assert.Equal("Ana", player.DisplayName);
        Assert.NotEqual(Guid.Empty, playerId);

        player.Rename("  Ana María  ");

        Assert.Equal("Ana María", player.DisplayName);
        Assert.Equal(accountId, player.AccountId);
        Assert.Equal(playerId, player.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("A")]
    public void Create_RejectsMissingOrShortDisplayName(string? name) =>
        Assert.Throws<PlayerRuleViolationException>(() => Player.Create(Guid.NewGuid(), name));

    [Fact]
    public void Rename_RejectsLongNameWithoutChangingProfile()
    {
        var player = Player.Create(Guid.NewGuid(), "Ana");
        Assert.Throws<PlayerRuleViolationException>(() => player.Rename(new string('a', 81)));
        Assert.Equal("Ana", player.DisplayName);
    }

    [Fact]
    public void Create_RejectsEmptyAccount() =>
        Assert.Throws<PlayerRuleViolationException>(() => Player.Create(Guid.Empty, "Ana"));
}
