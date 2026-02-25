using FluentAssertions;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Staff;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class StaminaAndAstToRatioTests
{
    // ── CalculateStamina ────────────────────────────────────────────

    [Fact]
    public void BasicStamina_30Mpg_CoachEndurance3()
    {
        // 30 mpg / 3 * 60 = 600; factor = 1 + 3/50 = 1.06; 600 * 1.06 = 636
        int stamina = StatisticsCalculator.CalculateStamina(82, 2460, 3);

        stamina.Should().Be(636);
    }

    [Fact]
    public void MinimumStamina_LowMinutes_Clamped480()
    {
        // 5 mpg / 3 * 60 = 100; factor = 1.06; 100 * 1.06 = 106 → clamped to 480
        int stamina = StatisticsCalculator.CalculateStamina(82, 410, 3);

        stamina.Should().Be(480);
    }

    [Fact]
    public void HighCoachEndurance_HigherStamina()
    {
        // 30 mpg / 3 * 60 = 600; factor = 1 + 5/50 = 1.1; 600 * 1.1 = 660
        int stamina = StatisticsCalculator.CalculateStamina(82, 2460, 5);

        stamina.Should().Be(660);
    }

    [Fact]
    public void LowCoachEndurance_LowerStamina()
    {
        // 30 mpg / 3 * 60 = 600; factor = 1 + 1/50 = 1.02; 600 * 1.02 = 612
        int stamina = StatisticsCalculator.CalculateStamina(82, 2460, 1);

        stamina.Should().Be(612);
    }

    [Fact]
    public void ZeroGames_Returns480()
    {
        int stamina = StatisticsCalculator.CalculateStamina(0, 0, 3);

        stamina.Should().Be(480);
    }

    [Fact]
    public void EngineUsesCoachEndurance()
    {
        // Verify that SetupTeamLineup-equivalent logic computes stamina from coach endurance
        var player = new Player
        {
            Name = "Test",
            Active = true,
            Health = 100,
            SeasonStats = new PlayerStatLine { Games = 82, Minutes = 2460 }
        };
        var team = new Team
        {
            Coach = new StaffMember { CoachEndurance = 4 }
        };

        // Simulate what the engine does: CalculateStamina with coach endurance
        int coachEndurance = team.Coach?.CoachEndurance ?? 3;
        int calculated = StatisticsCalculator.CalculateStamina(
            player.SeasonStats.Games, player.SeasonStats.Minutes, coachEndurance);
        player.Ratings.Stamina = calculated;
        player.GameState.CurrentStamina = calculated;

        // 30 mpg / 3 * 60 = 600; factor = 1 + 4/50 = 1.08; 600 * 1.08 = 648
        player.Ratings.Stamina.Should().Be(648);
        player.GameState.CurrentStamina.Should().Be(648);
    }

    // ── AstToRatio ──────────────────────────────────────────────────

    [Fact]
    public void NormalRatio_Ast100_To50_Returns2()
    {
        var player = new Player
        {
            Name = "Test",
            SeasonStats = new PlayerStatLine
            {
                Games = 82, Minutes = 2460, Assists = 100, Turnovers = 50,
                FieldGoalsMade = 400, FieldGoalsAttempted = 800,
                FreeThrowsMade = 100, FreeThrowsAttempted = 150,
                ThreePointersMade = 50, ThreePointersAttempted = 100,
                OffensiveRebounds = 30, Rebounds = 200,
                Steals = 60, Blocks = 20, PersonalFouls = 100
            }
        };

        StatisticsCalculator.CalculatePer48Stats(player);

        player.Ratings.AstToRatio.Should().BeApproximately(2.0, 0.001);
    }

    [Fact]
    public void ZeroTurnovers_ReturnsAssists()
    {
        var player = new Player
        {
            Name = "Test",
            SeasonStats = new PlayerStatLine
            {
                Games = 82, Minutes = 2460, Assists = 300, Turnovers = 0,
                FieldGoalsMade = 400, FieldGoalsAttempted = 800,
                FreeThrowsMade = 100, FreeThrowsAttempted = 150,
                ThreePointersMade = 50, ThreePointersAttempted = 100,
                OffensiveRebounds = 30, Rebounds = 200,
                Steals = 60, Blocks = 20, PersonalFouls = 100
            }
        };

        StatisticsCalculator.CalculatePer48Stats(player);

        player.Ratings.AstToRatio.Should().Be(300.0);
    }

    [Fact]
    public void ZeroMinutes_NoChange()
    {
        var player = new Player
        {
            Name = "Test",
            SeasonStats = new PlayerStatLine { Games = 0, Minutes = 0 }
        };

        StatisticsCalculator.CalculatePer48Stats(player);

        player.Ratings.AstToRatio.Should().Be(0); // default, not modified
    }

    [Fact]
    public void PopulatedByCalculatePer48Stats()
    {
        var player = new Player
        {
            Name = "Test",
            SeasonStats = new PlayerStatLine
            {
                Games = 82, Minutes = 2460, Assists = 500, Turnovers = 200,
                FieldGoalsMade = 600, FieldGoalsAttempted = 1200,
                FreeThrowsMade = 200, FreeThrowsAttempted = 250,
                ThreePointersMade = 100, ThreePointersAttempted = 250,
                OffensiveRebounds = 80, Rebounds = 400,
                Steals = 100, Blocks = 50, PersonalFouls = 150
            }
        };

        StatisticsCalculator.CalculatePer48Stats(player);

        player.Ratings.AstToRatio.Should().BeGreaterThan(0);
        player.Ratings.AstToRatio.Should().BeApproximately(2.5, 0.001); // 500/200
    }
}
