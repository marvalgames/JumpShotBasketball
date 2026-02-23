using FluentAssertions;
using JumpShotBasketball.Core.Enums;

namespace JumpShotBasketball.Core.Tests;

public class EnumTests
{
    [Theory]
    [InlineData(GameType.Exhibition, 1)]
    [InlineData(GameType.League, 2)]
    [InlineData(GameType.SingleTeam, 3)]
    [InlineData(GameType.Playoff, 4)]
    [InlineData(GameType.AllStar, 5)]
    [InlineData(GameType.Rookie, 6)]
    public void GameType_Values_MatchCppConstants(GameType type, int expected)
    {
        ((int)type).Should().Be(expected);
    }

    [Theory]
    [InlineData(ShotType.Outside, 1)]
    [InlineData(ShotType.Penetration, 2)]
    [InlineData(ShotType.Inside, 3)]
    [InlineData(ShotType.Three, 5)]
    [InlineData(ShotType.Auto, 6)]
    [InlineData(ShotType.Fastbreak, 7)]
    public void ShotType_Values_MatchCppConstants(ShotType type, int expected)
    {
        ((int)type).Should().Be(expected);
    }

    [Theory]
    [InlineData(TeamSide.Visitor, 1)]
    [InlineData(TeamSide.Home, 2)]
    public void TeamSide_Values_MatchCppConstants(TeamSide side, int expected)
    {
        ((int)side).Should().Be(expected);
    }

    [Fact]
    public void Position_HasFiveValues()
    {
        Enum.GetValues<Position>().Should().HaveCount(5);
    }

    [Fact]
    public void StaffRole_HasThreeValues()
    {
        Enum.GetValues<StaffRole>().Should().HaveCount(3);
    }

    [Fact]
    public void SeasonStage_HasFourValues()
    {
        Enum.GetValues<SeasonStage>().Should().HaveCount(4);
    }
}
