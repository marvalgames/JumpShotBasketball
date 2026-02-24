using FluentAssertions;
using JumpShotBasketball.Core.Constants;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class TradeEvaluationServiceTests
{
    // ── CheckSalaryMatching ─────────────────────────────────────────

    [Fact]
    public void CheckSalaryMatching_EqualSalaries_ReturnsTrue()
    {
        TradeEvaluationService.CheckSalaryMatching(100, 100).Should().BeTrue();
    }

    [Fact]
    public void CheckSalaryMatching_WithinUpperBound_ReturnsTrue()
    {
        // salary1=100, max = 100*1.15 + 10 = 125, use 120 safely within range
        TradeEvaluationService.CheckSalaryMatching(100, 120).Should().BeTrue();
    }

    [Fact]
    public void CheckSalaryMatching_WithinLowerBound_ReturnsTrue()
    {
        // salary1=100, min = 100*0.85 - 10 = 75
        TradeEvaluationService.CheckSalaryMatching(100, 75).Should().BeTrue();
    }

    [Fact]
    public void CheckSalaryMatching_AboveUpperBound_ReturnsFalse()
    {
        // salary1=100, max = 125, salary2=126 → false
        TradeEvaluationService.CheckSalaryMatching(100, 126).Should().BeFalse();
    }

    [Fact]
    public void CheckSalaryMatching_BelowLowerBound_ReturnsFalse()
    {
        // salary1=100, min = 75, salary2=74 → false
        TradeEvaluationService.CheckSalaryMatching(100, 74).Should().BeFalse();
    }

    [Fact]
    public void CheckSalaryMatching_ZeroSalary1_AllowsSmallSalary2()
    {
        // salary1=0, min = 0*0.85 - 10 = -10, max = 0*1.15 + 10 = 10
        TradeEvaluationService.CheckSalaryMatching(0, 5).Should().BeTrue();
    }

    [Fact]
    public void CheckSalaryMatching_BothZero_ReturnsTrue()
    {
        TradeEvaluationService.CheckSalaryMatching(0, 0).Should().BeTrue();
    }

    // ── CalculateRosterValue ────────────────────────────────────────

    [Fact]
    public void CalculateRosterValue_EmptyRoster_ReturnsZero()
    {
        TradeEvaluationService.CalculateRosterValue(new List<Player>()).Should().Be(0);
    }

    [Fact]
    public void CalculateRosterValue_SinglePlayer_ReturnsFullWeight()
    {
        var roster = new List<Player>
        {
            CreatePlayer("Star", 10.0)
        };

        TradeEvaluationService.CalculateRosterValue(roster).Should().Be(10.0);
    }

    [Fact]
    public void CalculateRosterValue_Top5GetFullWeight_6To10Get75Pct()
    {
        var roster = new List<Player>();
        for (int i = 0; i < 10; i++)
            roster.Add(CreatePlayer($"P{i}", 10.0));

        double expected = 5 * 10.0 + 5 * 10.0 * 0.75; // 50 + 37.5 = 87.5
        TradeEvaluationService.CalculateRosterValue(roster).Should().Be(expected);
    }

    [Fact]
    public void CalculateRosterValue_ExcludesInjured()
    {
        var roster = new List<Player>
        {
            CreatePlayer("Healthy", 10.0),
            CreatePlayer("Injured", 20.0, injury: 5)
        };

        TradeEvaluationService.CalculateRosterValue(roster).Should().Be(10.0);
    }

    [Fact]
    public void CalculateRosterValue_ExcludesInactive()
    {
        var p = CreatePlayer("Inactive", 15.0);
        p.Active = false;
        var roster = new List<Player> { p, CreatePlayer("Active", 8.0) };

        TradeEvaluationService.CalculateRosterValue(roster).Should().Be(8.0);
    }

    // ── CalculateDraftPickTradeValue ────────────────────────────────

    [Fact]
    public void CalculateDraftPickTradeValue_ZeroPick_ReturnsZero()
    {
        var board = new DraftBoard();
        TradeEvaluationService.CalculateDraftPickTradeValue(board, 0, 5.0, 5.0, 10, 10, 82)
            .Should().Be(0);
    }

    [Fact]
    public void CalculateDraftPickTradeValue_LosingTeam_HigherValue()
    {
        var board = new DraftBoard();
        int pickYrpp = DraftService.EncodeYrpp(1, 0, 1); // year 1, round 0, team 1

        double valueLosingTeam = TradeEvaluationService.CalculateDraftPickTradeValue(
            board, pickYrpp, 5.0, 5.0, 10, 72, 82);
        double valueWinningTeam = TradeEvaluationService.CalculateDraftPickTradeValue(
            board, pickYrpp, 5.0, 5.0, 72, 10, 82);

        valueLosingTeam.Should().BeGreaterThan(valueWinningTeam);
    }

    [Fact]
    public void CalculateDraftPickTradeValue_Round1MoreValuableThanRound2()
    {
        var board = new DraftBoard();
        int round1Pick = DraftService.EncodeYrpp(1, 0, 5);
        int round2Pick = DraftService.EncodeYrpp(1, 1, 5);

        double round1Value = TradeEvaluationService.CalculateDraftPickTradeValue(
            board, round1Pick, 5.0, 5.0, 30, 52, 82);
        double round2Value = TradeEvaluationService.CalculateDraftPickTradeValue(
            board, round2Pick, 5.0, 5.0, 30, 52, 82);

        round1Value.Should().BeGreaterThan(round2Value);
    }

    [Fact]
    public void CalculateDraftPickTradeValue_EarlierYearMoreValuable()
    {
        var board = new DraftBoard();
        int year1Pick = DraftService.EncodeYrpp(1, 0, 5);
        int year3Pick = DraftService.EncodeYrpp(3, 0, 5);

        double year1Value = TradeEvaluationService.CalculateDraftPickTradeValue(
            board, year1Pick, 5.0, 5.0, 30, 52, 82);
        double year3Value = TradeEvaluationService.CalculateDraftPickTradeValue(
            board, year3Pick, 5.0, 5.0, 30, 52, 82);

        year1Value.Should().BeGreaterThan(year3Value);
    }

    // ── CollectTeamOwnedPicks ──────────────────────────────────────

    [Fact]
    public void CollectTeamOwnedPicks_SelfOwnership_ReturnsAllOwnPicks()
    {
        var board = new DraftBoard();
        DraftService.InitializeDraftChart(board, 4);

        var picks = TradeEvaluationService.CollectTeamOwnedPicks(board, 1, 4);

        // Year 1-3, round 0-1, own pick each = 3 years * 2 rounds = 6 picks
        picks.Count.Should().Be(6);
    }

    [Fact]
    public void CollectTeamOwnedPicks_TradedPick_IncludesAcquired()
    {
        var board = new DraftBoard();
        DraftService.InitializeDraftChart(board, 4);

        // Team 2's year-1, round-0 pick goes to team 1
        DraftService.TransferPick(board, 2, 1, 1, 0);

        var picks = TradeEvaluationService.CollectTeamOwnedPicks(board, 1, 4);
        picks.Count.Should().Be(7); // 6 own + 1 acquired
    }

    // ── CountPicksInYear ───────────────────────────────────────────

    [Fact]
    public void CountPicksInYear_DefaultOwnership_ReturnsRoundsCount()
    {
        var board = new DraftBoard();
        DraftService.InitializeDraftChart(board, 4);

        // Team 1 owns their own picks: 1 per round (rounds 0,1,2) = 3 max
        // But the chart has 3 rounds, so team 1 has 1 pick per round = 3
        int count = TradeEvaluationService.CountPicksInYear(board, 1, 1, 4);
        count.Should().Be(LeagueConstants.MaxDraftRounds); // 3
    }

    // ── ValidateDraftPickLimits ────────────────────────────────────

    [Fact]
    public void ValidateDraftPickLimits_UnderLimit_ReturnsTrue()
    {
        var board = new DraftBoard();
        DraftService.InitializeDraftChart(board, 4);

        int pick = DraftService.EncodeYrpp(1, 0, 2);
        TradeEvaluationService.ValidateDraftPickLimits(board, 1, pick, 4)
            .Should().BeTrue();
    }

    [Fact]
    public void ValidateDraftPickLimits_ZeroPick_ReturnsTrue()
    {
        var board = new DraftBoard();
        TradeEvaluationService.ValidateDraftPickLimits(board, 1, 0, 4)
            .Should().BeTrue();
    }

    // ── CalculateValueFactor ───────────────────────────────────────

    [Fact]
    public void CalculateValueFactor_UnderpaidPlayer_ReturnsPositive()
    {
        // totalTrue=10, totalSalary=500, value=1750
        // underpaid = 10 - 500/100 = 5
        // factor = 5 * 400 / 1750 ≈ 1.143
        double result = TradeEvaluationService.CalculateValueFactor(10, 500, 1750);
        result.Should().BeApproximately(1.143, 0.01);
    }

    [Fact]
    public void CalculateValueFactor_OverpaidPlayer_ReturnsNegative()
    {
        // totalTrue=2, totalSalary=500, value=1750
        // underpaid = 2 - 5 = -3
        double result = TradeEvaluationService.CalculateValueFactor(2, 500, 1750);
        result.Should().BeNegative();
    }

    [Fact]
    public void CalculateValueFactor_ZeroTeamValue_ReturnsZero()
    {
        TradeEvaluationService.CalculateValueFactor(10, 500, 0).Should().Be(0);
    }

    // ── CalculatePlayerTradeWorth ──────────────────────────────────

    [Fact]
    public void CalculatePlayerTradeWorth_MultipliesRatingByYearsLeft()
    {
        var player = CreatePlayer("Test", 10.0);
        player.Contract.ContractYears = 3;
        player.Contract.CurrentContractYear = 1;
        player.Contract.RemainingSalary = 300;

        var (trueC, cappedSal) = TradeEvaluationService.CalculatePlayerTradeWorth(player);

        // yearsLeft = 1 + 3 - 1 = 3
        trueC.Should().Be(30.0); // 10.0 * 3
    }

    [Fact]
    public void CalculatePlayerTradeWorth_CapsSalaryAtMax10()
    {
        var player = CreatePlayer("Test", 10.0);
        player.Contract.ContractYears = 3;
        player.Contract.CurrentContractYear = 1;
        player.Contract.RemainingSalary = 99999; // very high

        var (_, cappedSal) = TradeEvaluationService.CalculatePlayerTradeWorth(player);

        int yearsLeft = 3; // 1 + 3 - 1
        int maxCapped = LeagueConstants.SalaryMaximumByYos[10] * yearsLeft;
        cappedSal.Should().Be(maxCapped);
    }

    // ── EvaluateAcceptance ─────────────────────────────────────────

    [Fact]
    public void EvaluateAcceptance_ComputerVsComputer_BothInRange_Accepts()
    {
        // Both teams improve slightly: ratio ~1.02 (within 0.98-1.06)
        TradeEvaluationService.EvaluateAcceptance(
            100, 102, 100, 102, "Computer", "Computer")
            .Should().BeTrue();
    }

    [Fact]
    public void EvaluateAcceptance_ComputerVsComputer_OneSideTooLow_Rejects()
    {
        // Team1 gets worse (ratio 0.90), team2 improves (1.10)
        TradeEvaluationService.EvaluateAcceptance(
            100, 90, 100, 110, "Computer", "Computer")
            .Should().BeFalse();
    }

    [Fact]
    public void EvaluateAcceptance_ComputerVsComputer_OneSideTooHigh_Rejects()
    {
        // Team1 improves too much (ratio 1.10 > 1.06)
        TradeEvaluationService.EvaluateAcceptance(
            100, 110, 100, 102, "Computer", "Computer")
            .Should().BeFalse();
    }

    [Fact]
    public void EvaluateAcceptance_PlayerToComputer_ComputerBenefits_Accepts()
    {
        // Computer (team2) gets ratio 1.10 (in 1.08-1.16), player (team1) ratio 0.95 (<0.98)
        TradeEvaluationService.EvaluateAcceptance(
            100, 95, 100, 110, "Player", "Computer")
            .Should().BeTrue();
    }

    [Fact]
    public void EvaluateAcceptance_PlayerToComputer_ComputerDoesntBenefit_Rejects()
    {
        // Computer ratio 1.02 (not > 1.08)
        TradeEvaluationService.EvaluateAcceptance(
            100, 95, 100, 102, "Player", "Computer")
            .Should().BeFalse();
    }

    [Fact]
    public void EvaluateAcceptance_PlayerVsPlayer_AlwaysRejects()
    {
        TradeEvaluationService.EvaluateAcceptance(
            100, 110, 100, 110, "Player", "Player")
            .Should().BeFalse();
    }

    [Fact]
    public void EvaluateAcceptance_ZeroOldTrue_Rejects()
    {
        TradeEvaluationService.EvaluateAcceptance(
            0, 10, 100, 105, "Computer", "Computer")
            .Should().BeFalse();
    }

    // ── Helper ──────────────────────────────────────────────────────

    private static Player CreatePlayer(string name, double tradeTrueRating, int injury = 0)
    {
        return new Player
        {
            Name = name,
            Position = "SF",
            Active = true,
            Injury = injury,
            Ratings = new PlayerRatings { TradeTrueRating = tradeTrueRating },
            SeasonStats = new PlayerStatLine { Games = 10, Minutes = 200 },
            Contract = new PlayerContract { ContractYears = 2, CurrentContractYear = 1 }
        };
    }
}
