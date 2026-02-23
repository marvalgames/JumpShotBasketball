using FluentAssertions;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class RotationServiceTests
{
    private static Team CreateTeamWithPlayers(string control = "Computer")
    {
        var team = new Team
        {
            Id = 1,
            Name = "TestTeam",
            Record = new TeamRecord { TeamName = "TestTeam", Control = control }
        };

        string[] positions = { "PG", "SG", "SF", "PF", "C", "PG", "SG", "SF", "PF", "C", "SF", "PF" };
        for (int i = 0; i < 12; i++)
        {
            team.Roster.Add(new Player
            {
                Name = $"Player{i + 1}",
                Position = positions[i],
                Active = true,
                Health = 100,
                Ratings = new PlayerRatings
                {
                    TradeTrueRating = 50 + i * 3
                },
                SeasonStats = new PlayerStatLine
                {
                    Rebounds = 100 + i * 10,
                    Assists = 80 + i * 5,
                    Blocks = 20 + i * 3
                }
            });
        }

        return team;
    }

    private static League CreateLeagueWithTeam(string control = "Computer")
    {
        var league = new League();
        league.Teams.Add(CreateTeamWithPlayers(control));
        return league;
    }

    // ── SetComputerRotations ───────────────────────────────────────

    [Fact]
    public void SetComputerRotations_AssignsRotationsToComputerTeam()
    {
        var league = CreateLeagueWithTeam("Computer");

        RotationService.SetComputerRotations(league);

        var roster = league.Teams[0].Roster;
        // At least some players should have rotations
        roster.Any(p => p.PgRotation || p.SgRotation || p.SfRotation || p.PfRotation || p.CRotation)
            .Should().BeTrue();
    }

    [Fact]
    public void SetComputerRotations_SkipsPlayerControlledTeam()
    {
        var league = CreateLeagueWithTeam("Player");

        // Set some rotations beforehand
        league.Teams[0].Roster[0].PgRotation = true;
        league.Teams[0].Roster[1].SgRotation = true;

        RotationService.SetComputerRotations(league);

        // Player-controlled team should not have rotations cleared
        league.Teams[0].Roster[0].PgRotation.Should().BeTrue();
    }

    [Fact]
    public void SetComputerRotations_ClearsGamePlans()
    {
        var league = CreateLeagueWithTeam("Computer");
        league.Teams[0].Roster[0].GameMinutes = 30;
        league.Teams[0].Roster[0].OffensiveFocus = 5;

        RotationService.SetComputerRotations(league);

        league.Teams[0].Roster[0].GameMinutes.Should().Be(0);
        league.Teams[0].Roster[0].OffensiveFocus.Should().Be(0);
    }

    [Fact]
    public void SetComputerRotations_EachPositionHasAtLeastOneRotation()
    {
        var league = CreateLeagueWithTeam("Computer");

        RotationService.SetComputerRotations(league);

        var roster = league.Teams[0].Roster;
        roster.Any(p => p.PgRotation).Should().BeTrue("should have PG rotation");
        roster.Any(p => p.SgRotation).Should().BeTrue("should have SG rotation");
        roster.Any(p => p.SfRotation).Should().BeTrue("should have SF rotation");
        roster.Any(p => p.PfRotation).Should().BeTrue("should have PF rotation");
        roster.Any(p => p.CRotation).Should().BeTrue("should have C rotation");
    }

    // ── VerifyRoster ───────────────────────────────────────────────

    [Fact]
    public void VerifyRoster_ValidTeam_ReturnsTrue()
    {
        var team = CreateTeamWithPlayers();

        var result = RotationService.VerifyRoster(team);

        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyRoster_TooFewPlayers_ReturnsFalse()
    {
        var team = new Team
        {
            Id = 1,
            Name = "ShortTeam",
            Record = new TeamRecord { TeamName = "ShortTeam", Control = "Computer" }
        };
        // Only 4 players
        string[] positions = { "PG", "SG", "SF", "PF" };
        for (int i = 0; i < 4; i++)
        {
            team.Roster.Add(new Player
            {
                Name = $"Player{i}",
                Position = positions[i],
                Active = true,
                Health = 100
            });
        }

        RotationService.VerifyRoster(team).Should().BeFalse();
    }

    [Fact]
    public void VerifyRoster_MissingPosition_ReturnsFalse()
    {
        var team = new Team
        {
            Id = 1,
            Name = "NoCenter",
            Record = new TeamRecord { TeamName = "NoCenter", Control = "Computer" }
        };
        // 10 players but no Center
        for (int i = 0; i < 10; i++)
        {
            team.Roster.Add(new Player
            {
                Name = $"Player{i}",
                Position = i % 2 == 0 ? "PG" : "SF",
                Active = true,
                Health = 100
            });
        }

        RotationService.VerifyRoster(team).Should().BeFalse();
    }

    // ── VerifyAllRosters ───────────────────────────────────────────

    [Fact]
    public void VerifyAllRosters_ReturnsIndicesOfInvalidTeams()
    {
        var league = new League();
        league.Teams.Add(CreateTeamWithPlayers()); // valid
        var invalidTeam = new Team
        {
            Id = 2,
            Name = "Invalid",
            Record = new TeamRecord { TeamName = "Invalid", Control = "Computer" }
        };
        // Only 3 players
        invalidTeam.Roster.Add(new Player { Name = "P1", Position = "PG", Active = true });
        invalidTeam.Roster.Add(new Player { Name = "P2", Position = "SG", Active = true });
        invalidTeam.Roster.Add(new Player { Name = "P3", Position = "SF", Active = true });
        league.Teams.Add(invalidTeam);

        var result = RotationService.VerifyAllRosters(league);

        result.Should().Contain(1);
        result.Should().NotContain(0);
    }
}
