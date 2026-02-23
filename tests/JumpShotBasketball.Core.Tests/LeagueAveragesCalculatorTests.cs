using FluentAssertions;
using JumpShotBasketball.Core.Models.Game;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class LeagueAveragesCalculatorTests
{
    [Fact]
    public void Calculate_EmptyLeague_ReturnsDefaults()
    {
        var league = new League();
        var avg = LeagueAveragesCalculator.Calculate(league);

        avg.FieldGoalsAttempted.Should().BeGreaterThan(0);
        avg.Steals.Should().BeGreaterThan(0);
        avg.Blocks.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Calculate_WithPlayers_ReturnsReasonableValues()
    {
        var league = new League();
        var team = new Team { Name = "Test" };

        for (int i = 0; i < 12; i++)
        {
            team.Roster.Add(new Player
            {
                Name = $"Player {i}",
                Position = i < 3 ? "PG" : (i < 6 ? "SF" : "C"),
                Active = true,
                SeasonStats = new PlayerStatLine
                {
                    Games = 40,
                    Minutes = 40 * 28 * 60,
                    FieldGoalsMade = 200,
                    FieldGoalsAttempted = 440,
                    FreeThrowsMade = 80,
                    FreeThrowsAttempted = 100,
                    ThreePointersMade = 50,
                    ThreePointersAttempted = 140,
                    OffensiveRebounds = 50,
                    Rebounds = 250,
                    Assists = 120,
                    Steals = 40,
                    Turnovers = 60,
                    Blocks = 25,
                    PersonalFouls = 90
                }
            });
        }

        league.Teams.Add(team);
        var avg = LeagueAveragesCalculator.Calculate(league);

        avg.FieldGoalsAttempted.Should().BeGreaterThan(5, "FGA per 48 should be > 5");
        avg.FieldGoalsAttempted.Should().BeLessThan(50, "FGA per 48 should be < 50");
        avg.Steals.Should().BeGreaterThan(0.1);
        avg.Blocks.Should().BeGreaterThan(0.1);
        avg.Turnovers.Should().BeGreaterThan(0.5);
        avg.OffensiveRebounds.Should().BeGreaterThan(0.1);
        avg.DefensiveRebounds.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public void GetDefaults_ReturnsNonZeroValues()
    {
        var defaults = LeagueAveragesCalculator.GetDefaults();

        defaults.FieldGoalsAttempted.Should().Be(18.0);
        defaults.ThreePointersAttempted.Should().Be(6.0);
        defaults.OffensiveRebounds.Should().Be(2.0);
        defaults.DefensiveRebounds.Should().Be(7.0);
        defaults.Steals.Should().Be(1.5);
        defaults.Turnovers.Should().Be(2.5);
        defaults.Blocks.Should().Be(1.0);
        defaults.PersonalFouls.Should().Be(4.0);
        defaults.FreeThrowsAttempted.Should().Be(5.0);
    }

    [Fact]
    public void GetDefaults_HasPositionalData()
    {
        var defaults = LeagueAveragesCalculator.GetDefaults();

        defaults.FieldGoalPercentageByPosition.Should().HaveCount(6);
        defaults.AssistsByPosition.Should().HaveCount(6);

        // PG should have higher assists
        defaults.AssistsByPosition[1].Should().BeGreaterThan(defaults.AssistsByPosition[5]);
    }

    [Fact]
    public void Calculate_InactivePlayers_Excluded()
    {
        var league = new League();
        var team = new Team { Name = "Test" };

        // Add one active player
        team.Roster.Add(new Player
        {
            Name = "Active",
            Position = "PG",
            Active = true,
            SeasonStats = new PlayerStatLine
            {
                Games = 40,
                Minutes = 40 * 30 * 60,
                FieldGoalsMade = 200,
                FieldGoalsAttempted = 440,
                Steals = 60,
                Turnovers = 40,
                Blocks = 20,
                PersonalFouls = 80,
                FreeThrowsAttempted = 100,
                OffensiveRebounds = 40,
                Rebounds = 200,
                Assists = 200,
                ThreePointersAttempted = 100,
                ThreePointersMade = 35,
                FreeThrowsMade = 75
            }
        });

        // Add one inactive player with huge stats (should be excluded)
        team.Roster.Add(new Player
        {
            Name = "Inactive",
            Position = "C",
            Active = false,
            SeasonStats = new PlayerStatLine
            {
                Games = 40,
                Minutes = 40 * 40 * 60,
                FieldGoalsMade = 1000,
                FieldGoalsAttempted = 2000,
                Steals = 500,
                Blocks = 500,
                PersonalFouls = 500,
                Turnovers = 500,
                FreeThrowsAttempted = 500,
                OffensiveRebounds = 500,
                Rebounds = 1000,
                Assists = 500,
                ThreePointersAttempted = 500,
                ThreePointersMade = 200,
                FreeThrowsMade = 400
            }
        });

        league.Teams.Add(team);
        var avg = LeagueAveragesCalculator.Calculate(league);

        // If inactive player was included, steals per 48 would be much higher
        avg.Steals.Should().BeLessThan(10, "Inactive players should not affect averages");
    }
}
