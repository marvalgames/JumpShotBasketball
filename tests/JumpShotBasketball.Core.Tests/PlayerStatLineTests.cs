using FluentAssertions;
using JumpShotBasketball.Core.Models.Player;

namespace JumpShotBasketball.Core.Tests;

public class PlayerStatLineTests
{
    [Fact]
    public void Points_ComputedCorrectly()
    {
        var stats = new PlayerStatLine
        {
            FieldGoalsMade = 8,     // 16 pts from 2-pointers
            FreeThrowsMade = 5,     // 5 pts from free throws
            ThreePointersMade = 3   // 3 pts from 3-pointers (counted as bonus)
        };

        // Points = (FGM * 2) + FTM + 3PM = 16 + 5 + 3 = 24
        stats.Points.Should().Be(24);
    }

    [Fact]
    public void PerGameAverages_WithZeroGames_ReturnZero()
    {
        var stats = new PlayerStatLine { Games = 0, Minutes = 100, Assists = 50 };

        stats.PointsPerGame.Should().Be(0);
        stats.MinutesPerGame.Should().Be(0);
        stats.AssistsPerGame.Should().Be(0);
        stats.ReboundsPerGame.Should().Be(0);
        stats.StealsPerGame.Should().Be(0);
        stats.BlocksPerGame.Should().Be(0);
        stats.TurnoversPerGame.Should().Be(0);
    }

    [Fact]
    public void PerGameAverages_CalculateCorrectly()
    {
        var stats = new PlayerStatLine
        {
            Games = 10,
            Minutes = 350,
            Rebounds = 80,
            Assists = 55,
            Steals = 12,
            Blocks = 8,
            Turnovers = 25,
            OffensiveRebounds = 20,
            PersonalFouls = 30
        };

        stats.MinutesPerGame.Should().Be(35.0);
        stats.ReboundsPerGame.Should().Be(8.0);
        stats.AssistsPerGame.Should().Be(5.5);
        stats.StealsPerGame.Should().Be(1.2);
        stats.BlocksPerGame.Should().Be(0.8);
        stats.TurnoversPerGame.Should().Be(2.5);
        stats.OffensiveReboundsPerGame.Should().Be(2.0);
        stats.PersonalFoulsPerGame.Should().Be(3.0);
    }

    [Fact]
    public void ShootingPercentages_WithZeroAttempts_ReturnZero()
    {
        var stats = new PlayerStatLine();

        stats.FieldGoalPercentage.Should().Be(0);
        stats.FreeThrowPercentage.Should().Be(0);
        stats.ThreePointPercentage.Should().Be(0);
    }

    [Fact]
    public void ShootingPercentages_CalculateCorrectly()
    {
        var stats = new PlayerStatLine
        {
            FieldGoalsMade = 4,
            FieldGoalsAttempted = 10,
            FreeThrowsMade = 7,
            FreeThrowsAttempted = 8,
            ThreePointersMade = 2,
            ThreePointersAttempted = 5
        };

        stats.FieldGoalPercentage.Should().Be(0.4);
        stats.FreeThrowPercentage.Should().Be(0.875);
        stats.ThreePointPercentage.Should().Be(0.4);
    }

    [Fact]
    public void Reset_ClearsAllStats()
    {
        var stats = new PlayerStatLine
        {
            Games = 82,
            Minutes = 2800,
            FieldGoalsMade = 600,
            FieldGoalsAttempted = 1200,
            FreeThrowsMade = 200,
            FreeThrowsAttempted = 250,
            ThreePointersMade = 100,
            ThreePointersAttempted = 300,
            OffensiveRebounds = 50,
            Rebounds = 400,
            Assists = 300,
            Steals = 80,
            Turnovers = 150,
            Blocks = 30,
            PersonalFouls = 200
        };

        stats.Reset();

        stats.Games.Should().Be(0);
        stats.Minutes.Should().Be(0);
        stats.FieldGoalsMade.Should().Be(0);
        stats.Points.Should().Be(0);
    }
}
