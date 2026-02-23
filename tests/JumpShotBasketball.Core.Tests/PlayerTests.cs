using FluentAssertions;
using JumpShotBasketball.Core.Models.Player;

namespace JumpShotBasketball.Core.Tests;

public class PlayerTests
{
    [Fact]
    public void NewPlayer_HasDefaultSubObjects()
    {
        var player = new Player();

        player.SeasonStats.Should().NotBeNull();
        player.SimulatedStats.Should().NotBeNull();
        player.CareerStats.Should().NotBeNull();
        player.PlayoffStats.Should().NotBeNull();
        player.Ratings.Should().NotBeNull();
        player.Contract.Should().NotBeNull();
        player.GameState.Should().NotBeNull();
        player.SeasonHighs.Should().NotBeNull();
    }

    [Fact]
    public void NewPlayer_HasCorrectDefaults()
    {
        var player = new Player();

        player.Health.Should().Be(100);
        player.Number.Should().Be(1);
        player.Retired.Should().BeFalse();
        player.Name.Should().BeEmpty();
    }

    [Fact]
    public void Player_FourStatLines_AreIndependent()
    {
        var player = new Player();

        player.SeasonStats.Games = 82;
        player.PlayoffStats.Games = 16;
        player.CareerStats.Games = 500;
        player.SimulatedStats.Games = 82;

        player.SeasonStats.Games.Should().Be(82);
        player.PlayoffStats.Games.Should().Be(16);
        player.CareerStats.Games.Should().Be(500);
        player.SimulatedStats.Games.Should().Be(82);
    }
}
