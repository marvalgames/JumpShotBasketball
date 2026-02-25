using FluentAssertions;
using JumpShotBasketball.Core.Constants;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Staff;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class ScoutingServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────

    /// <summary>Identity chart [0,1,2,3,4,5] — perfect scout, no distortion.</summary>
    private static int[] IdentityChart() => new[] { 0, 1, 2, 3, 4, 5 };

    /// <summary>Bad scout chart — values skewed from identity.</summary>
    private static int[] BadChart() => new[] { 0, 3, 4, 1, 2, 5 };

    /// <summary>Good coach chart — slightly above average (chart[1] > 3).</summary>
    private static int[] GoodCoachChart() => new[] { 0, 4, 3, 4, 5, 5 };

    /// <summary>Bad coach chart — below average (chart[1] &lt;= 3).</summary>
    private static int[] BadCoachChart() => new[] { 0, 1, 4, 1, 2, 5 };

    private static StaffMember CreateScout(int[]? chart = null)
    {
        chart ??= IdentityChart();
        var scout = new StaffMember();
        scout.ScoringHistory = (int[])chart.Clone();
        scout.ShootingHistory = (int[])chart.Clone();
        scout.ReboundingHistory = (int[])chart.Clone();
        scout.PassingHistory = (int[])chart.Clone();
        scout.DefenseHistory = (int[])chart.Clone();
        scout.Pot1History = (int[])chart.Clone();
        scout.Pot2History = (int[])chart.Clone();
        scout.EffortHistory = (int[])chart.Clone();
        return scout;
    }

    private static StaffMember CreateCoach(int[]? chart = null)
    {
        chart ??= IdentityChart();
        var coach = new StaffMember();
        coach.ScoringHistory = (int[])chart.Clone();
        coach.ShootingHistory = (int[])chart.Clone();
        coach.ReboundingHistory = (int[])chart.Clone();
        coach.PassingHistory = (int[])chart.Clone();
        coach.DefenseHistory = (int[])chart.Clone();
        coach.Pot1History = (int[])chart.Clone();
        coach.Pot2History = (int[])chart.Clone();
        coach.EffortHistory = (int[])chart.Clone();
        return coach;
    }

    private static Player CreatePlayer(double tradeTrueRating = 10.0, int age = 25)
    {
        var player = new Player
        {
            Name = "Test Player",
            Position = "SG",
            Age = age,
            Active = true,
            Health = 100,
            Ratings = new PlayerRatings
            {
                TrueRatingSimple = tradeTrueRating * 5,
                TradeTrueRating = tradeTrueRating,
                TradeValue = tradeTrueRating * 1.5,
                Potential1 = 3,
                Potential2 = 4,
                Effort = 5,
                ProjectionFieldGoalsAttempted = 50,
                ProjectionFieldGoalPercentage = 45,
                ProjectionFreeThrowsAttempted = 30,
                ProjectionFreeThrowPercentage = 80,
                ProjectionThreePointersAttempted = 20,
                ProjectionThreePointPercentage = 35,
                ProjectionOffensiveRebounds = 15,
                ProjectionDefensiveRebounds = 40,
                ProjectionAssists = 25,
                ProjectionSteals = 10,
                ProjectionTurnovers = 12,
                ProjectionBlocks = 8
            },
            SeasonStats = new PlayerStatLine
            {
                Games = 82,
                Minutes = 2800,
                FieldGoalsMade = 500,
                FieldGoalsAttempted = 1100,
                FreeThrowsMade = 200,
                FreeThrowsAttempted = 250,
                ThreePointersMade = 100,
                ThreePointersAttempted = 300,
                OffensiveRebounds = 60,
                Rebounds = 400,
                Assists = 300,
                Steals = 80,
                Turnovers = 150,
                Blocks = 30,
                PersonalFouls = 120
            }
        };
        return player;
    }

    private static League CreateSmallLeague(int teamCount = 2)
    {
        var league = new League();
        league.Settings.NumberOfTeams = teamCount;
        for (int i = 0; i < teamCount; i++)
        {
            var team = new Team
            {
                Id = i,
                Name = $"Team{i}",
                Scout = CreateScout(),
                Coach = CreateCoach()
            };
            for (int p = 0; p < 3; p++)
            {
                var player = CreatePlayer();
                player.Id = i * 3 + p;
                player.Name = $"Player{i}_{p}";
                player.TeamIndex = i;
                team.Roster.Add(player);
            }
            league.Teams.Add(team);
        }
        return league;
    }

    // ── CalculatePrRating Tests ──────────────────────────────────────

    [Fact]
    public void PrRating_IdentityChart_ReturnsOriginal()
    {
        // Identity chart [0,1,2,3,4,5] → no distortion
        int result = ScoutingService.CalculatePrRating(false, 50, IdentityChart());
        result.Should().Be(50);
    }

    [Fact]
    public void PrRating_ShootingMode_ReducedDeviation()
    {
        // shooting=true → 1/4 factor → less deviation from original
        var chart = BadChart();
        int shootResult = ScoutingService.CalculatePrRating(true, 50, chart);
        int nonShootResult = ScoutingService.CalculatePrRating(false, 50, chart);

        // Shooting mode should have less deviation than non-shooting
        int shootDev = Math.Abs(shootResult - 50);
        int nonShootDev = Math.Abs(nonShootResult - 50);
        shootDev.Should().BeLessThanOrEqualTo(nonShootDev);
    }

    [Fact]
    public void PrRating_NonShootingMode_FullDeviation()
    {
        // Bad chart with non-shooting → full deviation applied
        var chart = BadChart();
        int result = ScoutingService.CalculatePrRating(false, 50, chart);
        // With a bad chart, result should differ from 50
        result.Should().NotBe(50);
    }

    [Fact]
    public void PrRating_SpecialCase_PotentialLookup()
    {
        // r > 200 → direct chart[r-200] lookup
        var chart = new[] { 0, 5, 4, 3, 2, 1 };  // Reversed chart
        int result = ScoutingService.CalculatePrRating(false, 203, chart);
        // 203 - 200 = 3 → chart[3] = 3
        result.Should().Be(3);
    }

    [Fact]
    public void PrRating_ClampedAbove99()
    {
        // r between 100-200 → clamped to 99
        int result99 = ScoutingService.CalculatePrRating(false, 99, IdentityChart());
        int result150 = ScoutingService.CalculatePrRating(false, 150, IdentityChart());
        // Both should produce the same result since 150 is clamped to 99
        result150.Should().Be(result99);
    }

    [Fact]
    public void PrRating_BoundaryValues()
    {
        // r=0 and r=99 should both work without exceptions
        int result0 = ScoutingService.CalculatePrRating(false, 0, IdentityChart());
        int result99 = ScoutingService.CalculatePrRating(false, 99, IdentityChart());

        result0.Should().Be(0);
        result99.Should().Be(99);
    }

    // ── ApplyScoutAdjustments Tests ──────────────────────────────────

    [Fact]
    public void ApplyScoutAdjustments_SetsAllAdjustedFields()
    {
        var player = CreatePlayer();
        var scout = CreateScout(BadChart());

        ScoutingService.ApplyScoutAdjustments(player, scout);

        // All 15 adjusted fields should be populated (non-zero for non-zero inputs)
        player.Ratings.AdjustedProjectionFieldGoalsAttempted.Should().NotBe(0);
        player.Ratings.AdjustedProjectionFieldGoalPercentage.Should().NotBe(0);
        player.Ratings.AdjustedProjectionFreeThrowsAttempted.Should().NotBe(0);
        player.Ratings.AdjustedProjectionFreeThrowPercentage.Should().NotBe(0);
        player.Ratings.AdjustedProjectionThreePointersAttempted.Should().NotBe(0);
        player.Ratings.AdjustedProjectionThreePointPercentage.Should().NotBe(0);
        player.Ratings.AdjustedProjectionOffensiveRebounds.Should().NotBe(0);
        player.Ratings.AdjustedProjectionDefensiveRebounds.Should().NotBe(0);
        player.Ratings.AdjustedProjectionAssists.Should().NotBe(0);
        player.Ratings.AdjustedProjectionSteals.Should().NotBe(0);
        player.Ratings.AdjustedProjectionTurnovers.Should().NotBe(0);
        player.Ratings.AdjustedProjectionBlocks.Should().NotBe(0);
        // Pot1/Pot2/Effort go through +200 path → direct chart lookup
        player.Ratings.AdjustedPotential1.Should().BeGreaterThanOrEqualTo(0);
        player.Ratings.AdjustedPotential2.Should().BeGreaterThanOrEqualTo(0);
        player.Ratings.AdjustedEffort.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void ApplyScoutAdjustments_PerfectScout_MinimalChange()
    {
        var player = CreatePlayer();
        var scout = CreateScout(IdentityChart());

        ScoutingService.ApplyScoutAdjustments(player, scout);

        // Identity chart → values should equal originals
        player.Ratings.AdjustedProjectionFieldGoalsAttempted.Should().Be(player.Ratings.ProjectionFieldGoalsAttempted);
        player.Ratings.AdjustedProjectionFieldGoalPercentage.Should().Be(player.Ratings.ProjectionFieldGoalPercentage);
        player.Ratings.AdjustedProjectionOffensiveRebounds.Should().Be(player.Ratings.ProjectionOffensiveRebounds);
        player.Ratings.AdjustedProjectionAssists.Should().Be(player.Ratings.ProjectionAssists);
    }

    [Fact]
    public void ApplyScoutAdjustments_BadScout_LargerDeviation()
    {
        var player = CreatePlayer();
        var badScout = CreateScout(BadChart());

        ScoutingService.ApplyScoutAdjustments(player, badScout);

        // Bad scout → at least some values should differ from originals
        bool anyDifferent =
            player.Ratings.AdjustedProjectionFieldGoalsAttempted != player.Ratings.ProjectionFieldGoalsAttempted ||
            player.Ratings.AdjustedProjectionAssists != player.Ratings.ProjectionAssists ||
            player.Ratings.AdjustedProjectionSteals != player.Ratings.ProjectionSteals;

        anyDifferent.Should().BeTrue();
    }

    [Fact]
    public void ApplyScoutAdjustments_PotentialsUse200Offset()
    {
        var player = CreatePlayer();
        player.Ratings.Potential1 = 3;  // 3 + 200 = 203 → chart[3]

        var chart = new[] { 0, 5, 4, 2, 1, 3 };
        var scout = CreateScout(chart);

        ScoutingService.ApplyScoutAdjustments(player, scout);

        // Pot1 = 3 → r = 203 → chart[3] = 2
        player.Ratings.AdjustedPotential1.Should().Be(2);
    }

    [Fact]
    public void ApplyScoutAdjustments_NullScout_NoChange()
    {
        var player = CreatePlayer();
        int origFga = player.Ratings.AdjustedProjectionFieldGoalsAttempted;

        ScoutingService.ApplyScoutAdjustments(player, null);

        // No crash, no changes
        player.Ratings.AdjustedProjectionFieldGoalsAttempted.Should().Be(origFga);
    }

    // ── CalculateCoachSkillFactor Tests ──────────────────────────────

    [Fact]
    public void CoachSkillFactor_PerfectChart_Returns1()
    {
        double factor = ScoutingService.CalculateCoachSkillFactor(IdentityChart());
        factor.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void CoachSkillFactor_AboveAverage_GreaterThan1()
    {
        // chart[1] = 4 > 3 → above average → factor > 1
        double factor = ScoutingService.CalculateCoachSkillFactor(GoodCoachChart());
        factor.Should().BeGreaterThan(1.0);
    }

    [Fact]
    public void CoachSkillFactor_BelowAverage_LessThan1()
    {
        // chart[1] = 1 <= 3 → below average → factor < 1
        double factor = ScoutingService.CalculateCoachSkillFactor(BadCoachChart());
        factor.Should().BeLessThan(1.0);
    }

    [Fact]
    public void CoachSkillFactor_AllSame_Predictable()
    {
        // Uniform chart [0,3,3,3,3,3] → sum of deviations: |3-1|+|3-2|+|3-3|+|3-4|+|3-5| = 2+1+0+1+2 = 6
        // chart[1]=3 ≤ 3 → factor = 1 - 6/100 = 0.94
        var chart = new[] { 0, 3, 3, 3, 3, 3 };
        double factor = ScoutingService.CalculateCoachSkillFactor(chart);
        factor.Should().BeApproximately(0.94, 0.001);
    }

    // ── CalculateCoachAdjustedTrueRating Tests ──────────────────────

    [Fact]
    public void CoachAdjTrueRating_GoodCoach_HigherRating()
    {
        var player = CreatePlayer();
        double baseTrueRating = StatisticsCalculator.CalculateTrueRatingSimple(player.SeasonStats);

        var coach = CreateCoach(GoodCoachChart());
        ScoutingService.CalculateCoachAdjustedTrueRating(player, coach);

        // Good coach → higher TrueRatingSimple
        player.Ratings.TrueRatingSimple.Should().BeGreaterThan(baseTrueRating);
    }

    [Fact]
    public void CoachAdjTrueRating_BadCoach_LowerRating()
    {
        var player = CreatePlayer();
        double baseTrueRating = StatisticsCalculator.CalculateTrueRatingSimple(player.SeasonStats);

        var coach = CreateCoach(BadCoachChart());
        ScoutingService.CalculateCoachAdjustedTrueRating(player, coach);

        // Bad coach → lower TrueRatingSimple
        player.Ratings.TrueRatingSimple.Should().BeLessThan(baseTrueRating);
    }

    [Fact]
    public void CoachAdjTrueRating_NeutralCoach_SameAsBase()
    {
        var player = CreatePlayer();
        double baseTrueRating = StatisticsCalculator.CalculateTrueRatingSimple(player.SeasonStats);

        var coach = CreateCoach(IdentityChart());
        ScoutingService.CalculateCoachAdjustedTrueRating(player, coach);

        // Identity coach → matches base calc
        player.Ratings.TrueRatingSimple.Should().BeApproximately(baseTrueRating, 0.001);
    }

    [Fact]
    public void CoachAdjTrueRating_UsesSimulatedStats()
    {
        var player = CreatePlayer();
        // Give different SimulatedStats
        player.SimulatedStats = new PlayerStatLine
        {
            Games = 82, Minutes = 3000,
            FieldGoalsMade = 600, FieldGoalsAttempted = 1200,
            FreeThrowsMade = 250, FreeThrowsAttempted = 300,
            ThreePointersMade = 120, ThreePointersAttempted = 350,
            OffensiveRebounds = 70, Rebounds = 450,
            Assists = 350, Steals = 90, Turnovers = 160, Blocks = 40, PersonalFouls = 130
        };

        var coach = CreateCoach(IdentityChart());
        ScoutingService.CalculateCoachAdjustedTrueRating(player, coach);

        // Should use SimulatedStats, not SeasonStats
        double expectedFromSim = StatisticsCalculator.CalculateTrueRatingSimple(player.SimulatedStats);
        player.Ratings.TrueRatingSimple.Should().BeApproximately(expectedFromSim, 0.001);
    }

    [Fact]
    public void CoachAdjTrueRating_ZeroMinutes_NoChange()
    {
        var player = CreatePlayer();
        player.SeasonStats = new PlayerStatLine(); // 0 minutes
        player.SimulatedStats = new PlayerStatLine(); // 0 minutes
        double originalValue = player.Ratings.TrueRatingSimple;

        var coach = CreateCoach(GoodCoachChart());
        ScoutingService.CalculateCoachAdjustedTrueRating(player, coach);

        // 0 minutes → no change (early return)
        player.Ratings.TrueRatingSimple.Should().Be(originalValue);
    }

    // ── CalculateCoachAdjustedTradeValue Tests ──────────────────────

    [Fact]
    public void CoachAdjTradeValue_YoungHighPotential_Higher()
    {
        var player = CreatePlayer(tradeTrueRating: 10.0, age: 22);
        player.Ratings.Potential1 = 5;
        player.Ratings.Potential2 = 5;

        // Coach chart that maps 5→5 (accurate assessment)
        var coach = CreateCoach(IdentityChart());
        ScoutingService.CalculateCoachAdjustedTradeValue(player, coach);

        // Young + high potential → high trade value (should be > base rating)
        player.Ratings.TradeValue.Should().BeGreaterThan(player.Ratings.TradeTrueRating);
    }

    [Fact]
    public void CoachAdjTradeValue_OldPlayer_Decline()
    {
        var youngPlayer = CreatePlayer(tradeTrueRating: 10.0, age: 22);
        var oldPlayer = CreatePlayer(tradeTrueRating: 10.0, age: 35);

        var coach = CreateCoach(IdentityChart());
        ScoutingService.CalculateCoachAdjustedTradeValue(youngPlayer, coach);
        ScoutingService.CalculateCoachAdjustedTradeValue(oldPlayer, coach);

        // Old player decline factor → lower trade value
        oldPlayer.Ratings.TradeValue.Should().BeLessThan(youngPlayer.Ratings.TradeValue);
    }

    [Fact]
    public void CoachAdjTradeValue_LowEffort_Lower()
    {
        var highEffortPlayer = CreatePlayer(tradeTrueRating: 10.0, age: 25);
        highEffortPlayer.Ratings.Effort = 5;
        var lowEffortPlayer = CreatePlayer(tradeTrueRating: 10.0, age: 25);
        lowEffortPlayer.Ratings.Effort = 1;

        var coach = CreateCoach(IdentityChart());
        ScoutingService.CalculateCoachAdjustedTradeValue(highEffortPlayer, coach);
        ScoutingService.CalculateCoachAdjustedTradeValue(lowEffortPlayer, coach);

        // Low effort → lower misc factor → lower trade value
        lowEffortPlayer.Ratings.TradeValue.Should().BeLessThan(highEffortPlayer.Ratings.TradeValue);
    }

    [Fact]
    public void CoachAdjTradeValue_CoachChartLookup()
    {
        var player = CreatePlayer(tradeTrueRating: 10.0, age: 25);
        player.Ratings.Potential1 = 3;
        player.Ratings.Potential2 = 3;
        player.Ratings.Effort = 3;

        // Coach chart maps index 3→5 (overestimates potential)
        var chart = new[] { 0, 1, 2, 5, 4, 5 };
        var coach = CreateCoach(chart);
        ScoutingService.CalculateCoachAdjustedTradeValue(player, coach);

        // With inflated potential perception, trade value should differ from identity
        var player2 = CreatePlayer(tradeTrueRating: 10.0, age: 25);
        player2.Ratings.Potential1 = 3;
        player2.Ratings.Potential2 = 3;
        player2.Ratings.Effort = 3;
        var identityCoach = CreateCoach(IdentityChart());
        ScoutingService.CalculateCoachAdjustedTradeValue(player2, identityCoach);

        player.Ratings.TradeValue.Should().NotBeApproximately(player2.Ratings.TradeValue, 0.01);
    }

    [Fact]
    public void CoachAdjTradeValue_BoundsChecked()
    {
        var player = CreatePlayer(tradeTrueRating: 10.0, age: 25);
        player.Ratings.Potential1 = 99; // Way out of range

        var coach = CreateCoach(IdentityChart());

        // Should not throw — clamped to array bounds
        var act = () => ScoutingService.CalculateCoachAdjustedTradeValue(player, coach);
        act.Should().NotThrow();
    }

    // ── Integration Tests ────────────────────────────────────────────

    [Fact]
    public void ApplyAllScoutAdjustments_AllTeams()
    {
        var league = CreateSmallLeague(3);

        ScoutingService.ApplyAllScoutAdjustments(league);

        // Every player on every team should have adjusted projections set
        foreach (var team in league.Teams)
        {
            foreach (var player in team.Roster)
            {
                // With identity chart, adjusted should equal original
                player.Ratings.AdjustedProjectionFieldGoalsAttempted
                    .Should().Be(player.Ratings.ProjectionFieldGoalsAttempted);
            }
        }
    }

    [Fact]
    public void ApplyAllCoachAdjustments_AllTeams()
    {
        var league = CreateSmallLeague(3);

        ScoutingService.ApplyAllCoachAdjustments(league);

        // Every player should have TradeValue updated
        foreach (var team in league.Teams)
        {
            foreach (var player in team.Roster)
            {
                player.Ratings.TradeValue.Should().NotBe(0);
            }
        }
    }

    [Fact]
    public void ScoutAdjustments_SkipNullScout()
    {
        var league = CreateSmallLeague(2);
        league.Teams[0].Scout = null; // No scout on first team

        // Should not throw
        var act = () => ScoutingService.ApplyAllScoutAdjustments(league);
        act.Should().NotThrow();
    }

    [Fact]
    public void CoachAdjustments_SkipNullCoach()
    {
        var league = CreateSmallLeague(2);
        league.Teams[0].Coach = null; // No coach on first team

        // Should not throw
        var act = () => ScoutingService.ApplyAllCoachAdjustments(league);
        act.Should().NotThrow();
    }

    [Fact]
    public void LeagueCreation_ScoutAdjustmentsApplied()
    {
        var random = new Random(42);
        var league = LeagueCreationService.CreateNewLeague(
            new LeagueCreationOptions { NumberOfTeams = 4, GamesPerSeason = 10 }, random);

        // After creation, adjusted projections should be populated
        // (scouts are initialized with random charts, not identity, so some deviation expected)
        foreach (var team in league.Teams)
        {
            foreach (var player in team.Roster)
            {
                // AdjustedProjection fields should be non-zero (populated by scout adjustments)
                if (player.Ratings.ProjectionFieldGoalsAttempted > 0)
                {
                    player.Ratings.AdjustedProjectionFieldGoalsAttempted.Should().BeGreaterThan(0);
                }
            }
        }
    }

    // ── StatisticsCalculator.CalculateCoachAdjustedTrueRatingSimple ─

    [Fact]
    public void CoachAdjTrueRatingSimple_AllFactors1_MatchesBase()
    {
        var stats = new PlayerStatLine
        {
            Games = 82, Minutes = 2800,
            FieldGoalsMade = 500, FieldGoalsAttempted = 1100,
            FreeThrowsMade = 200, FreeThrowsAttempted = 250,
            ThreePointersMade = 100, ThreePointersAttempted = 300,
            OffensiveRebounds = 60, Rebounds = 400,
            Assists = 300, Steals = 80, Turnovers = 150, Blocks = 30, PersonalFouls = 120
        };

        double baseRating = StatisticsCalculator.CalculateTrueRatingSimple(stats);
        double coachRating = StatisticsCalculator.CalculateCoachAdjustedTrueRatingSimple(
            stats, 1.0, 1.0, 1.0, 1.0, 1.0);

        coachRating.Should().BeApproximately(baseRating, 0.001);
    }

    [Fact]
    public void CoachAdjTrueRatingSimple_HighFactors_IncreasesRating()
    {
        var stats = new PlayerStatLine
        {
            Games = 82, Minutes = 2800,
            FieldGoalsMade = 500, FieldGoalsAttempted = 1100,
            FreeThrowsMade = 200, FreeThrowsAttempted = 250,
            ThreePointersMade = 100, ThreePointersAttempted = 300,
            OffensiveRebounds = 60, Rebounds = 400,
            Assists = 300, Steals = 80, Turnovers = 150, Blocks = 30, PersonalFouls = 120
        };

        double baseRating = StatisticsCalculator.CalculateTrueRatingSimple(stats);
        double coachRating = StatisticsCalculator.CalculateCoachAdjustedTrueRatingSimple(
            stats, 1.1, 1.1, 1.1, 1.1, 1.1);

        coachRating.Should().BeGreaterThan(baseRating);
    }
}
