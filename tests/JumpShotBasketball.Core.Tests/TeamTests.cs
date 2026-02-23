using FluentAssertions;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Constants;

namespace JumpShotBasketball.Core.Tests;

public class TeamTests
{
    [Fact]
    public void NewTeam_HasEmptyRoster()
    {
        var team = new Team();
        team.Roster.Should().BeEmpty();
    }

    [Fact]
    public void NewTeam_HasDefaultSubObjects()
    {
        var team = new Team();
        team.Record.Should().NotBeNull();
        team.Financial.Should().NotBeNull();
        team.DraftBoard.Should().NotBeNull();
    }

    [Fact]
    public void TeamFinancial_HasCorrectDefaults()
    {
        var financial = new TeamFinancial();

        financial.TicketPrice.Should().Be(LeagueConstants.DefaultTicketPrice);
        financial.SuitePrice.Should().Be(LeagueConstants.DefaultSuitePrice);
        financial.Capacity.Should().Be(LeagueConstants.DefaultCapacity);
        financial.Suites.Should().Be(LeagueConstants.DefaultSuites);
        financial.FanSupport.Should().Be(LeagueConstants.DefaultCityRating);
        financial.Economy.Should().Be(LeagueConstants.DefaultCityRating);
    }

    [Fact]
    public void TeamRecord_HeadToHead_UsesDictionary()
    {
        var record = new TeamRecord();

        record.VsOpponent[5] = 3;
        record.VsOpponent[12] = 1;

        record.VsOpponent[5].Should().Be(3);
        record.VsOpponent[12].Should().Be(1);
        record.VsOpponent.Should().HaveCount(2);
    }
}
