using FluentAssertions;
using JumpShotBasketball.Core.Constants;
using JumpShotBasketball.Core.Models.League;

namespace JumpShotBasketball.Core.Tests;

public class LeagueTests
{
    [Fact]
    public void NewLeague_HasEmptyCollections()
    {
        var league = new League();

        league.Teams.Should().BeEmpty();
        league.Transactions.Should().BeEmpty();
        league.StaffPool.Should().BeEmpty();
        league.Schedule.Should().NotBeNull();
        league.Settings.Should().NotBeNull();
    }

    [Fact]
    public void LeagueSettings_HasCorrectDefaults()
    {
        var settings = new LeagueSettings();

        settings.SalaryCap.Should().Be(LeagueConstants.DefaultSalaryCap);
        settings.ScoutsEnabled.Should().BeTrue();
        settings.FinancialEnabled.Should().BeTrue();
    }

    [Fact]
    public void DraftBoard_InitializesWithSelfOwnership()
    {
        var board = new DraftBoard();

        // Each team should own its own pick by default
        for (int y = 0; y < LeagueConstants.MaxDraftYears; y++)
            for (int r = 0; r < LeagueConstants.MaxDraftRounds; r++)
                for (int p = 0; p <= LeagueConstants.MaxTeams; p++)
                    board.DraftChart[y, r, p].Should().Be(p);
    }

    [Fact]
    public void DraftBoard_TradePickOwnership()
    {
        var board = new DraftBoard();

        // Team 5 trades its year-0, round-0 pick to team 12
        board.DraftChart[0, 0, 5] = 12;

        board.DraftChart[0, 0, 5].Should().Be(12);
        // Other picks unaffected
        board.DraftChart[0, 0, 6].Should().Be(6);
    }

    [Fact]
    public void Schedule_CanAddGames()
    {
        var schedule = new Schedule();
        schedule.Games.Add(new ScheduledGame
        {
            GameNumber = 1,
            Week = 1,
            HomeTeamIndex = 1,
            VisitorTeamIndex = 2,
            Type = Enums.GameType.League
        });

        schedule.Games.Should().HaveCount(1);
        schedule.Games[0].HomeTeamIndex.Should().Be(1);
    }
}
