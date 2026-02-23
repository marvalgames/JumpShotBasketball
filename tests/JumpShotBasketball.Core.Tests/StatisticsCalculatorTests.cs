using FluentAssertions;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class StatisticsCalculatorTests
{
    // Reference stat line: ~34 mpg starter over 82 games
    private static PlayerStatLine CreateStarterStatLine() => new()
    {
        Games = 82,
        Minutes = 2800,
        FieldGoalsMade = 500,
        FieldGoalsAttempted = 1100,
        FreeThrowsMade = 300,
        FreeThrowsAttempted = 350,
        ThreePointersMade = 200,
        ThreePointersAttempted = 500,
        OffensiveRebounds = 100,
        Rebounds = 500,
        Assists = 300,
        Steals = 100,
        Turnovers = 50,
        Blocks = 50,
        PersonalFouls = 150
    };

    // Low-volume bench player: ~15 mpg
    private static PlayerStatLine CreateBenchStatLine() => new()
    {
        Games = 60,
        Minutes = 900,
        FieldGoalsMade = 100,
        FieldGoalsAttempted = 250,
        FreeThrowsMade = 40,
        FreeThrowsAttempted = 60,
        ThreePointersMade = 20,
        ThreePointersAttempted = 60,
        OffensiveRebounds = 30,
        Rebounds = 120,
        Assists = 60,
        Steals = 30,
        Turnovers = 20,
        Blocks = 10,
        PersonalFouls = 80
    };

    private static PlayerStatLine CreateEmptyStatLine() => new();

    // ───────────────────────────────────────────────────────────────
    // Zero-minutes guard tests
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void AllOdptMethods_ReturnZero_WhenZeroMinutes()
    {
        var stats = CreateEmptyStatLine();

        StatisticsCalculator.CalculateOutsideRating(stats).Should().Be(0);
        StatisticsCalculator.CalculateDrivingRating(stats).Should().Be(0);
        StatisticsCalculator.CalculatePostRating(stats).Should().Be(0);
        StatisticsCalculator.CalculateTransitionRating(stats).Should().Be(0);
        StatisticsCalculator.CalculateOutsideDefenseRating(stats).Should().Be(0);
        StatisticsCalculator.CalculateDrivingDefenseRating(stats).Should().Be(0);
        StatisticsCalculator.CalculatePostDefenseRating(stats).Should().Be(0);
        StatisticsCalculator.CalculateTransitionDefenseRating(stats).Should().Be(0);
    }

    [Fact]
    public void TrueRating_ReturnsZero_WhenZeroMinutes()
    {
        var stats = CreateEmptyStatLine();
        StatisticsCalculator.CalculateTrueRating(stats, 10.0).Should().Be(0);
    }

    [Fact]
    public void MvpRating_ReturnsZero_WhenZeroMinutes()
    {
        var stats = CreateEmptyStatLine();
        StatisticsCalculator.CalculateMvpRating(stats, 10.0, 0, 0, 0, 0).Should().Be(0);
    }

    // ───────────────────────────────────────────────────────────────
    // Offensive ODPT known-value tests
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void CalculateOutsideRating_ReturnsExpectedValue_ForStarter()
    {
        var stats = CreateStarterStatLine();
        double result = StatisticsCalculator.CalculateOutsideRating(stats);

        // fgm*2 + tfgm*3 - fta - oreb - (fga-fgm) - (tfga-tfgm)
        // = 1000 + 600 - 350 - 100 - 600 - 300 = 250
        // / 2800 * 48 + 300/350 = 4.286 + 0.857 = 5.143
        // (5.143 + 10) / 2 = 7.571
        result.Should().BeApproximately(7.571, 0.01);
    }

    [Fact]
    public void CalculateDrivingRating_ReturnsExpectedValue_ForStarter()
    {
        var stats = CreateStarterStatLine();
        double result = StatisticsCalculator.CalculateDrivingRating(stats);

        // (500 + 350) / 6 + 300 / 2 = 141.67 + 150 = 291.67
        // / 2800 * 48 = 4.999 + 0.2*2 = 5.399
        result.Should().BeApproximately(5.399, 0.01);
    }

    [Fact]
    public void CalculatePostRating_ReturnsExpectedValue_ForStarter()
    {
        var stats = CreateStarterStatLine();
        double result = StatisticsCalculator.CalculatePostRating(stats);

        // oreb*4/5 + reb/3 = 80 + 166.67 = 246.67
        // * fgPct (500/1100 = .4545) = 112.12
        // + fta*3/5 = 210 - to*4/5 = 40 → 282.12
        // / 2800 * 48 + (1 - 0.2) = 4.836 + 0.8 = 5.636
        // (5.636 + 1) * 9/8 = 7.466
        result.Should().BeApproximately(7.466, 0.01);
    }

    [Fact]
    public void CalculateTransitionRating_ReturnsExpectedValue_ForStarter()
    {
        var stats = CreateStarterStatLine();
        double result = StatisticsCalculator.CalculateTransitionRating(stats);

        // fta/2 + oreb/2 + ast/2 - reb/3
        // = 175 + 50 + 150 - 166.67 = 208.33
        // / 2800 * 48 = 3.571
        result.Should().BeApproximately(3.571, 0.01);
    }

    // ───────────────────────────────────────────────────────────────
    // Defensive ODPT known-value tests
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void CalculateOutsideDefenseRating_ReturnsPositive_ForStarter()
    {
        var stats = CreateStarterStatLine();
        double result = StatisticsCalculator.CalculateOutsideDefenseRating(stats);
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateDrivingDefenseRating_ReturnsPositive_ForStarter()
    {
        var stats = CreateStarterStatLine();
        double result = StatisticsCalculator.CalculateDrivingDefenseRating(stats);
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculatePostDefenseRating_ReturnsPositive_ForStarter()
    {
        var stats = CreateStarterStatLine();
        double result = StatisticsCalculator.CalculatePostDefenseRating(stats);
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateTransitionDefenseRating_ReturnsPositive_ForStarter()
    {
        var stats = CreateStarterStatLine();
        double result = StatisticsCalculator.CalculateTransitionDefenseRating(stats);
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateOutsideDefenseRating_VerifyFormula_ForStarter()
    {
        var stats = CreateStarterStatLine();
        double result = StatisticsCalculator.CalculateOutsideDefenseRating(stats);

        // stl=100, blk=50, pf=150, oreb=100, reb=500/3=166.67, fta=350, fga=1100, min=2800
        double reb = 500.0 / 3.0;
        double rebRatio = 100.0 / (reb * 3); // = 0.2
        double stlblkRatio = 100.0 / 150.0;  // = 0.667
        double blkpfRatio = 50.0 / 150.0;    // = 0.333
        double ftRatio = 350.0 / 1100.0;     // = 0.318
        double orebFactor = (100 + reb * 3) / 2800.0 * 10; // = (100+500)/2800*10 = 2.143

        double baseVal = 100 + 50.0 / 4.0 - 150.0 / 4.0; // = 75
        double expected = baseVal / 2800.0 * 48 + ftRatio * 2
            + stlblkRatio + (1 - rebRatio) * 2 + blkpfRatio + orebFactor;

        result.Should().BeApproximately(expected, 0.001);
    }

    [Fact]
    public void CalculateTransitionDefenseRating_VerifyFormula_ForStarter()
    {
        var stats = CreateStarterStatLine();
        double result = StatisticsCalculator.CalculateTransitionDefenseRating(stats);

        double reb = 500.0 / 3.0;
        double rebRatio = 100.0 / (reb * 3);
        double ftRatio = 350.0 / 1100.0;
        double stlblkRatio = 100.0 / 150.0;
        double baseVal = 100.0 - 150.0 / 4.0; // = 62.5
        double inner = baseVal / 2800.0 * 48 + ftRatio + (1 - rebRatio) * 2 + stlblkRatio;
        double expected = (inner + 0.5) * 1.5;

        result.Should().BeApproximately(expected, 0.001);
    }

    // ───────────────────────────────────────────────────────────────
    // Bench player produces lower ratings than starter
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void StarterProducesHigherOutsideRating_ThanBench()
    {
        var starter = StatisticsCalculator.CalculateOutsideRating(CreateStarterStatLine());
        var bench = StatisticsCalculator.CalculateOutsideRating(CreateBenchStatLine());
        starter.Should().BeGreaterThan(bench);
    }

    // ───────────────────────────────────────────────────────────────
    // Composite rating tests
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void CalculateTrueRatingSimple_ReturnsNonZero_ForStarter()
    {
        var stats = CreateStarterStatLine();
        double result = StatisticsCalculator.CalculateTrueRatingSimple(stats);
        result.Should().NotBe(0);
    }

    [Fact]
    public void CalculateTrueRatingSimple_VerifyFormula()
    {
        var stats = CreateStarterStatLine();
        double result = StatisticsCalculator.CalculateTrueRatingSimple(stats);

        // gun = 500 + 200 - (1100-500)*2/3 + 300 - 350/2 + (350-300)/6
        // = 500 + 200 - 400 + 300 - 175 + 8.333 = 433.333
        // gun * 1.5 = 650.0
        double gun = 500 + 200 - (600.0 * 2.0 / 3.0) + 300 - 175 + 50.0 / 6.0;
        gun *= 1.5;

        // skill = 100*2/3 + (500-100)/3 + 100 - 50 + 50 + 300*4/5
        // = 66.67 + 133.33 + 100 - 50 + 50 + 240 = 540.0
        // skill * 0.75 = 405.0
        double skill = 100.0 * 2.0 / 3.0 + 400.0 / 3.0 + 100 - 50 + 50 + 240;
        skill *= 0.75;

        result.Should().BeApproximately(gun + skill, 0.01);
    }

    [Fact]
    public void CalculateTrueRating_ReturnsValue_ForStarter()
    {
        var stats = CreateStarterStatLine();
        double result = StatisticsCalculator.CalculateTrueRating(stats, 10.0);

        // Should produce a meaningful value for 82-game starter
        result.Should().NotBe(0);
    }

    [Fact]
    public void CalculateTrueRating_ReturnsZero_WhenZeroGames()
    {
        var stats = new PlayerStatLine { Games = 0, Minutes = 100 };
        StatisticsCalculator.CalculateTrueRating(stats, 10.0).Should().Be(0);
    }

    [Fact]
    public void CalculateTradeTrueRating_ReturnsZero_WhenInjured()
    {
        double result = StatisticsCalculator.CalculateTradeTrueRating(100, 5, 5, 5, 5, 2800, 82, 5);
        result.Should().Be(0);
    }

    [Fact]
    public void CalculateTradeTrueRating_ReturnsZero_WhenZeroGames()
    {
        double result = StatisticsCalculator.CalculateTradeTrueRating(100, 5, 5, 5, 5, 2800, 0, 0);
        result.Should().Be(0);
    }

    [Fact]
    public void CalculateTradeTrueRating_ReturnsNonZero_ForHealthyPlayer()
    {
        double result = StatisticsCalculator.CalculateTradeTrueRating(100, 5, 5, 5, 5, 2800, 82, 0);
        result.Should().NotBe(0);
    }

    [Fact]
    public void CalculateTradeValue_VerifyFormula()
    {
        // age=25, pot1=5, pot2=5, effort=5, tradeTru=10.0
        double result = StatisticsCalculator.CalculateTradeValue(10.0, 25, 5, 5, 5);

        // overTheHill = 1.0 - (25 - 28) / 100 = 1.03 → capped at 1.0
        // pot = ((6/4) + (6/4)) / 2 = 1.5
        double factor = 1.0 + ((28.0 - 25) * 3.0 / 100.0) * 1.5; // = 1 + 0.09 * 1.5 = 1.135
        double misc = (100.0 + 5 + 5 + 5 - 9.0) / 100.0; // = 1.06

        double expected = 10.0 * factor * misc * 1.0; // overTheHill capped at 1.0
        result.Should().BeApproximately(expected, 0.001);
    }

    [Fact]
    public void CalculateTradeValue_OverTheHill_ReducesValue_WhenOld()
    {
        double youngValue = StatisticsCalculator.CalculateTradeValue(10.0, 25, 5, 5, 5);
        double oldValue = StatisticsCalculator.CalculateTradeValue(10.0, 35, 5, 5, 5);

        oldValue.Should().BeLessThan(youngValue);
    }

    [Fact]
    public void CalculateMvpRating_ReturnsNonZero_ForStarter()
    {
        var stats = CreateStarterStatLine();
        double result = StatisticsCalculator.CalculateMvpRating(stats, 10.0, 5, 5, 5, 5);
        result.Should().NotBe(0);
    }

    // ───────────────────────────────────────────────────────────────
    // CalculateAllRatings orchestrator
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void CalculateAllRatings_SetsMovementRawRatings()
    {
        var player = new Player
        {
            Age = 25,
            SeasonStats = CreateStarterStatLine(),
            Ratings = new PlayerRatings
            {
                Potential1 = 5,
                Potential2 = 5,
                Effort = 5
            }
        };

        StatisticsCalculator.CalculateAllRatings(player);

        player.Ratings.MovementOffenseRaw.Should().BeInRange(1, 9);
        player.Ratings.MovementDefenseRaw.Should().BeInRange(1, 9);
        player.Ratings.PenetrationOffenseRaw.Should().BeInRange(1, 9);
        player.Ratings.PenetrationDefenseRaw.Should().BeInRange(1, 9);
        player.Ratings.PostOffenseRaw.Should().BeInRange(1, 9);
        player.Ratings.PostDefenseRaw.Should().BeInRange(1, 9);
        player.Ratings.TransitionOffenseRaw.Should().BeInRange(1, 9);
        player.Ratings.TransitionDefenseRaw.Should().BeInRange(1, 9);
    }

    [Fact]
    public void CalculateAllRatings_SetsDoubleRatings()
    {
        var player = new Player
        {
            Age = 25,
            SeasonStats = CreateStarterStatLine(),
            Ratings = new PlayerRatings { Potential1 = 5, Potential2 = 5, Effort = 5 }
        };

        StatisticsCalculator.CalculateAllRatings(player);

        player.Ratings.Outside.Should().NotBe(0);
        player.Ratings.Driving.Should().NotBe(0);
        player.Ratings.Post.Should().NotBe(0);
        player.Ratings.Transition.Should().NotBe(0);
        player.Ratings.TrueRatingSimple.Should().NotBe(0);
        player.Ratings.TradeValue.Should().NotBe(0);
    }

    [Fact]
    public void CalculateAllRatings_ClampsToOneToNine()
    {
        // Very low stats — should clamp to 1, not go below
        var player = new Player
        {
            Age = 25,
            SeasonStats = new PlayerStatLine
            {
                Games = 82,
                Minutes = 2800,
                FieldGoalsMade = 10,
                FieldGoalsAttempted = 100,
                FreeThrowsMade = 5,
                FreeThrowsAttempted = 50,
                ThreePointersMade = 2,
                ThreePointersAttempted = 20,
                OffensiveRebounds = 5,
                Rebounds = 20,
                Assists = 5,
                Steals = 5,
                Turnovers = 30,
                Blocks = 2,
                PersonalFouls = 200
            },
            Ratings = new PlayerRatings { Potential1 = 3, Potential2 = 3, Effort = 3 }
        };

        StatisticsCalculator.CalculateAllRatings(player);

        player.Ratings.MovementOffenseRaw.Should().BeGreaterThanOrEqualTo(1);
        player.Ratings.MovementDefenseRaw.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void CalculateAllRatings_RookieAdjustment_IncreasesOffense()
    {
        var stats = CreateStarterStatLine();

        var normal = new Player
        {
            Age = 22,
            SeasonStats = stats,
            Ratings = new PlayerRatings { Potential1 = 5, Potential2 = 5, Effort = 5 }
        };
        StatisticsCalculator.CalculateAllRatings(normal, rookieImport: false);

        var rookie = new Player
        {
            Age = 22,
            SeasonStats = stats,
            Ratings = new PlayerRatings { Potential1 = 5, Potential2 = 5, Effort = 5 }
        };
        StatisticsCalculator.CalculateAllRatings(rookie, rookieImport: true);

        // Rookie import adds +1 offense, -2 defense
        rookie.Ratings.MovementOffenseRaw.Should().BeGreaterThanOrEqualTo(normal.Ratings.MovementOffenseRaw);
        rookie.Ratings.MovementDefenseRaw.Should().BeLessThanOrEqualTo(normal.Ratings.MovementDefenseRaw);
    }

    [Fact]
    public void CalculateAllRatings_ZeroMinutes_AllRawRatingsClampedTo1()
    {
        var player = new Player
        {
            Age = 25,
            SeasonStats = CreateEmptyStatLine(),
            Ratings = new PlayerRatings { Potential1 = 5, Potential2 = 5, Effort = 5 }
        };

        StatisticsCalculator.CalculateAllRatings(player);

        // All doubles should be 0 (zero minutes)
        player.Ratings.Outside.Should().Be(0);
        // Raw ints clamped from Round(0) = 0 → 1
        player.Ratings.MovementOffenseRaw.Should().Be(1);
    }

    // ───────────────────────────────────────────────────────────────
    // Standings tests
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void CalculateWinPercentage_ReturnsCorrectValue()
    {
        StatisticsCalculator.CalculateWinPercentage(50, 32).Should().BeApproximately(0.6098, 0.001);
    }

    [Fact]
    public void CalculateWinPercentage_ReturnsZero_WhenNoGames()
    {
        StatisticsCalculator.CalculateWinPercentage(0, 0).Should().Be(0);
    }

    [Fact]
    public void CalculateGamesBack_ReturnsCorrectValue()
    {
        // Leader: 50-32 diff=18. Team: 45-37 diff=8. GB = (18-8)/2 = 5
        StatisticsCalculator.CalculateGamesBack(50, 32, 45, 37).Should().Be(5.0);
    }

    [Fact]
    public void CalculateGamesBack_ReturnsHalfGames()
    {
        // Leader: 50-31 diff=19. Team: 45-37 diff=8. GB = (19-8)/2 = 5.5
        StatisticsCalculator.CalculateGamesBack(50, 31, 45, 37).Should().Be(5.5);
    }

    [Fact]
    public void UnpackRecord_ExtractsWinsAndLosses()
    {
        var (wins, losses) = StatisticsCalculator.UnpackRecord(5032);
        wins.Should().Be(50);
        losses.Should().Be(32);
    }

    [Fact]
    public void UpdateStandings_RecordsVisitorWin()
    {
        var league = CreateTwoTeamLeague();

        StatisticsCalculator.UpdateStandings(league, 0, 110, 1, 100);

        league.Teams[0].Record.Wins.Should().Be(1);
        league.Teams[0].Record.Losses.Should().Be(0);
        league.Teams[1].Record.Wins.Should().Be(0);
        league.Teams[1].Record.Losses.Should().Be(1);
    }

    [Fact]
    public void UpdateStandings_RecordsHomeWin()
    {
        var league = CreateTwoTeamLeague();

        StatisticsCalculator.UpdateStandings(league, 0, 100, 1, 110);

        league.Teams[0].Record.Wins.Should().Be(0);
        league.Teams[0].Record.Losses.Should().Be(1);
        league.Teams[1].Record.Wins.Should().Be(1);
        league.Teams[1].Record.Losses.Should().Be(0);
    }

    [Fact]
    public void UpdateStandings_UpdatesPercentage()
    {
        var league = CreateTwoTeamLeague();

        StatisticsCalculator.UpdateStandings(league, 0, 110, 1, 100);
        StatisticsCalculator.UpdateStandings(league, 0, 105, 1, 110);

        league.Teams[0].Record.LeaguePercentage.Should().BeApproximately(0.5, 0.01);
        league.Teams[1].Record.LeaguePercentage.Should().BeApproximately(0.5, 0.01);
    }

    [Fact]
    public void UpdateStandings_UpdatesConferenceRecord_WhenSameConference()
    {
        var league = CreateTwoTeamLeague();
        league.Teams[0].Record.Conference = "East";
        league.Teams[1].Record.Conference = "East";

        StatisticsCalculator.UpdateStandings(league, 0, 110, 1, 100);

        var (cw, cl) = StatisticsCalculator.UnpackRecord(league.Teams[0].Record.ConferenceRecord);
        cw.Should().Be(1);
    }

    [Fact]
    public void UpdateStandings_DoesNotUpdateConferenceRecord_WhenDifferentConference()
    {
        var league = CreateTwoTeamLeague();
        league.Teams[0].Record.Conference = "East";
        league.Teams[1].Record.Conference = "West";

        StatisticsCalculator.UpdateStandings(league, 0, 110, 1, 100);

        league.Teams[0].Record.ConferenceRecord.Should().Be(0);
    }

    [Fact]
    public void UpdateStandings_UpdatesHeadToHead()
    {
        var league = CreateTwoTeamLeague();

        StatisticsCalculator.UpdateStandings(league, 0, 110, 1, 100);

        league.Teams[0].Record.VsOpponent.Should().ContainKey(1);
        var (w, l) = StatisticsCalculator.UnpackRecord(league.Teams[0].Record.VsOpponent[1]);
        w.Should().Be(1);
        l.Should().Be(0);
    }

    [Fact]
    public void RecalculateStandings_ComputesGamesBack()
    {
        var league = CreateTwoTeamLeague();
        league.Teams[0].Record.LeagueRecord = 5032; // 50-32
        league.Teams[1].Record.LeagueRecord = 4537; // 45-37

        StatisticsCalculator.RecalculateStandings(league);

        league.Teams[0].Record.Wins.Should().Be(50);
        league.Teams[0].Record.LeagueGamesBack.Should().Be(0);
        league.Teams[1].Record.LeagueGamesBack.Should().Be(10); // diff: 18 vs 8 = 10
    }

    // ───────────────────────────────────────────────────────────────
    // League average factor
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void CalculateLeagueAverageFactor_ReturnsNonZero_ForPlayers()
    {
        var players = new[] { CreateStarterStatLine(), CreateBenchStatLine() };
        double factor = StatisticsCalculator.CalculateLeagueAverageFactor(players);
        factor.Should().NotBe(0);
    }

    [Fact]
    public void CalculateLeagueAverageFactor_ReturnsZero_WhenNoMinutes()
    {
        var players = new[] { CreateEmptyStatLine() };
        double factor = StatisticsCalculator.CalculateLeagueAverageFactor(players);
        factor.Should().Be(0);
    }

    [Fact]
    public void CalculateLeagueAverageFactor_SkipsZeroMinutePlayers()
    {
        var starter = CreateStarterStatLine();
        var empty = CreateEmptyStatLine();

        double withEmpty = StatisticsCalculator.CalculateLeagueAverageFactor(new[] { starter, empty });
        double withoutEmpty = StatisticsCalculator.CalculateLeagueAverageFactor(new[] { starter });

        withEmpty.Should().BeApproximately(withoutEmpty, 0.001);
    }

    // ───────────────────────────────────────────────────────────────
    // Helpers
    // ───────────────────────────────────────────────────────────────

    private static Models.League.League CreateTwoTeamLeague()
    {
        var league = new Models.League.League();
        league.Teams.Add(new Models.Team.Team
        {
            Id = 0,
            Name = "Team A",
            Record = new Models.Team.TeamRecord { Conference = "East", Division = "Atlantic" }
        });
        league.Teams.Add(new Models.Team.Team
        {
            Id = 1,
            Name = "Team B",
            Record = new Models.Team.TeamRecord { Conference = "East", Division = "Atlantic" }
        });
        return league;
    }
}
