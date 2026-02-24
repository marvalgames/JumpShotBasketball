using FluentAssertions;
using JumpShotBasketball.Core.Constants;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class DraftServiceTests
{
    // ── InitializeDraftChart ────────────────────────────────────────

    [Fact]
    public void InitializeDraftChart_SetsAllPicksToSelfOwnership()
    {
        var board = new DraftBoard();
        // Reinitialize with explicit team count
        DraftService.InitializeDraftChart(board, 30);

        for (int y = 0; y < LeagueConstants.MaxDraftYears; y++)
            for (int r = 0; r < LeagueConstants.MaxDraftRounds; r++)
                for (int p = 0; p <= 30; p++)
                    board.DraftChart[y, r, p].Should().Be(p,
                        $"year {y}, round {r}, team {p} should own its own pick");
    }

    [Fact]
    public void InitializeDraftChart_DoesNotTouchTeamsBeyondCount()
    {
        var board = new DraftBoard();
        // Set a known value beyond team count
        board.DraftChart[0, 0, 32] = 99;
        DraftService.InitializeDraftChart(board, 30);
        // Team 32 is beyond numTeams=30, should be untouched
        board.DraftChart[0, 0, 32].Should().Be(99);
    }

    // ── RollDraftChartForward ───────────────────────────────────────

    [Fact]
    public void RollDraftChartForward_ShiftsYear1ToYear0()
    {
        var board = new DraftBoard();
        // Team 5's year-1, round-0 pick is owned by team 10
        board.DraftChart[1, 0, 5] = 10;

        DraftService.RollDraftChartForward(board);

        board.DraftChart[0, 0, 5].Should().Be(10);
    }

    [Fact]
    public void RollDraftChartForward_ShiftsYear2ToYear1()
    {
        var board = new DraftBoard();
        board.DraftChart[2, 1, 3] = 15;

        DraftService.RollDraftChartForward(board);

        board.DraftChart[1, 1, 3].Should().Be(15);
    }

    [Fact]
    public void RollDraftChartForward_ResetsLastYearToSelfOwnership()
    {
        var board = new DraftBoard();
        int lastYear = LeagueConstants.MaxDraftYears - 1;
        board.DraftChart[lastYear, 0, 5] = 20;

        DraftService.RollDraftChartForward(board);

        board.DraftChart[lastYear, 0, 5].Should().Be(5);
    }

    [Fact]
    public void RollDraftChartForward_PreservesTradesInShiftedYears()
    {
        var board = new DraftBoard();
        // Team 1 traded year-2, round-0 pick to team 8
        board.DraftChart[2, 0, 1] = 8;
        // Team 3 traded year-3, round-1 pick to team 12
        board.DraftChart[3, 1, 3] = 12;

        DraftService.RollDraftChartForward(board);

        board.DraftChart[1, 0, 1].Should().Be(8, "year-2 trade should shift to year-1");
        board.DraftChart[2, 1, 3].Should().Be(12, "year-3 trade should shift to year-2");
    }

    // ── TransferPick ────────────────────────────────────────────────

    [Fact]
    public void TransferPick_UpdatesOwnership()
    {
        var board = new DraftBoard();
        DraftService.TransferPick(board, fromTeam: 5, toTeam: 10, year: 1, round: 0);

        board.DraftChart[1, 0, 5].Should().Be(10);
    }

    [Fact]
    public void TransferPick_InvalidYear_DoesNothing()
    {
        var board = new DraftBoard();
        DraftService.TransferPick(board, fromTeam: 5, toTeam: 10, year: -1, round: 0);
        DraftService.TransferPick(board, fromTeam: 5, toTeam: 10, year: 99, round: 0);

        board.DraftChart[0, 0, 5].Should().Be(5, "pick should remain self-owned");
    }

    [Fact]
    public void TransferPick_InvalidRound_DoesNothing()
    {
        var board = new DraftBoard();
        DraftService.TransferPick(board, fromTeam: 5, toTeam: 10, year: 0, round: -1);
        DraftService.TransferPick(board, fromTeam: 5, toTeam: 10, year: 0, round: 5);

        board.DraftChart[0, 0, 5].Should().Be(5);
    }

    // ── CalculatePickValue ──────────────────────────────────────────

    [Fact]
    public void CalculatePickValue_FirstPick_HighestValue()
    {
        int val = DraftService.CalculatePickValue(1);
        // 101 - 1 + (6-1)*5 = 100 + 25 = 125
        val.Should().Be(125);
    }

    [Fact]
    public void CalculatePickValue_FifthPick_HasBonus()
    {
        int val = DraftService.CalculatePickValue(5);
        // 101 - 5 + (6-5)*5 = 96 + 5 = 101
        val.Should().Be(101);
    }

    [Fact]
    public void CalculatePickValue_SixthPick_NoBonus()
    {
        int val = DraftService.CalculatePickValue(6);
        // 101 - 6 = 95
        val.Should().Be(95);
    }

    [Fact]
    public void CalculatePickValue_LastPick_MinimumValue()
    {
        int val = DraftService.CalculatePickValue(100);
        // 101 - 100 = 1
        val.Should().Be(1);
    }

    [Fact]
    public void CalculatePickValue_BeyondRange_ClampedTo1()
    {
        int val = DraftService.CalculatePickValue(200);
        val.Should().Be(1);
    }

    [Fact]
    public void CalculatePickValue_ZeroPick_Returns0()
    {
        DraftService.CalculatePickValue(0).Should().Be(0);
    }

    [Theory]
    [InlineData(1, 125)]
    [InlineData(2, 119)]
    [InlineData(3, 113)]
    [InlineData(4, 107)]
    [InlineData(5, 101)]
    [InlineData(6, 95)]
    [InlineData(10, 91)]
    [InlineData(30, 71)]
    public void CalculatePickValue_HigherPicksWorthMore(int pick, int expected)
    {
        DraftService.CalculatePickValue(pick).Should().Be(expected);
    }

    // ── GetPickOwner ────────────────────────────────────────────────

    [Fact]
    public void GetPickOwner_DefaultBoard_ReturnsSelf()
    {
        var board = new DraftBoard();
        DraftService.GetPickOwner(board, 0, 0, 5).Should().Be(5);
    }

    [Fact]
    public void GetPickOwner_AfterTransfer_ReturnsNewOwner()
    {
        var board = new DraftBoard();
        DraftService.TransferPick(board, 5, 10, 0, 0);
        DraftService.GetPickOwner(board, 0, 0, 5).Should().Be(10);
    }

    [Fact]
    public void GetPickOwner_InvalidArgs_ReturnsSelf()
    {
        var board = new DraftBoard();
        DraftService.GetPickOwner(board, -1, 0, 5).Should().Be(5);
        DraftService.GetPickOwner(board, 0, -1, 5).Should().Be(5);
    }

    // ── YRPP encoding/decoding ──────────────────────────────────────

    [Theory]
    [InlineData(1205, 1, 2, 5)]
    [InlineData(3110, 3, 1, 10)]
    [InlineData(0001, 0, 0, 1)]
    public void DecodeYrpp_ReturnsCorrectComponents(int yrpp, int year, int round, int team)
    {
        var (y, r, t) = DraftService.DecodeYrpp(yrpp);
        y.Should().Be(year);
        r.Should().Be(round);
        t.Should().Be(team);
    }

    [Fact]
    public void EncodeYrpp_RoundTrips()
    {
        int yrpp = DraftService.EncodeYrpp(2, 1, 15);
        yrpp.Should().Be(2115);
        var (y, r, t) = DraftService.DecodeYrpp(yrpp);
        y.Should().Be(2);
        r.Should().Be(1);
        t.Should().Be(15);
    }
}
