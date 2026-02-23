using FluentAssertions;
using JumpShotBasketball.Core.Constants;

namespace JumpShotBasketball.Core.Tests;

public class ConstantsTests
{
    [Fact]
    public void GameConstants_ScreenDimensions_MatchCpp()
    {
        GameConstants.ScreenWidth.Should().Be(1366);
        GameConstants.ScreenHeight.Should().Be(720);
    }

    [Fact]
    public void GameConstants_MinutesThresholds_MatchCpp()
    {
        GameConstants.MinutesThresholds.Should().HaveCount(11);
        GameConstants.MinutesThresholds[0].Should().Be(35);   // MIN0
        GameConstants.MinutesThresholds[10].Should().Be(103);  // MIN10
    }

    [Fact]
    public void GameConstants_MaxRatingCaps_MatchCpp()
    {
        GameConstants.MaxRatingCaps.Should().HaveCount(11);
        GameConstants.MaxRatingCaps[0].Should().Be(1063);   // MAX0
        GameConstants.MaxRatingCaps[7].Should().Be(1275);   // MAX7
        GameConstants.MaxRatingCaps[10].Should().Be(1488);  // MAX10
    }

    [Fact]
    public void LeagueConstants_DefaultValues_MatchCpp()
    {
        LeagueConstants.DefaultTicketPrice.Should().Be(420);
        LeagueConstants.DefaultSuitePrice.Should().Be(3000);
        LeagueConstants.DefaultCapacity.Should().Be(19_000);
        LeagueConstants.DefaultSalaryCap.Should().Be(3550);
    }
}
