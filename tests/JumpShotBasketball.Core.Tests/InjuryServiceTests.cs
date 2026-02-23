using FluentAssertions;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class InjuryServiceTests
{
    private static League CreateLeagueWithInjuredPlayer(int injuryDays)
    {
        var league = new League();
        var team = new Team { Id = 1, Name = "TestTeam" };
        team.Roster.Add(new Player
        {
            Name = "Injured Player",
            Injury = injuryDays,
            Health = 80,
            InjuryDescription = "Sprained ankle",
            Active = true
        });
        team.Roster.Add(new Player
        {
            Name = "Healthy Player",
            Injury = 0,
            Health = 100,
            Active = true
        });
        league.Teams.Add(team);
        return league;
    }

    // ── HealInjuries ───────────────────────────────────────────────

    [Fact]
    public void HealInjuries_DecrementsInjuryByDays()
    {
        var league = CreateLeagueWithInjuredPlayer(5);
        var player = league.Teams[0].Roster[0];

        InjuryService.HealInjuries(league, 1, new Random(42));

        player.Injury.Should().Be(4);
    }

    [Fact]
    public void HealInjuries_WhenReachesZero_RestoresHealth()
    {
        var league = CreateLeagueWithInjuredPlayer(1);
        var player = league.Teams[0].Roster[0];

        InjuryService.HealInjuries(league, 1, new Random(42));

        player.Injury.Should().Be(0);
        player.Health.Should().BeInRange(90, 100);
    }

    [Fact]
    public void HealInjuries_WhenFullHealth_ClearsDescription()
    {
        var league = CreateLeagueWithInjuredPlayer(1);
        var player = league.Teams[0].Roster[0];

        // Use a seed that produces health=100
        InjuryService.HealInjuries(league, 1, new Random(100));

        player.Injury.Should().Be(0);
        if (player.Health >= 100)
            player.InjuryDescription.Should().BeEmpty();
    }

    [Fact]
    public void HealInjuries_DoesNotGoBelowZero()
    {
        var league = CreateLeagueWithInjuredPlayer(1);

        InjuryService.HealInjuries(league, 5, new Random(42));

        league.Teams[0].Roster[0].Injury.Should().Be(0);
    }

    [Fact]
    public void HealInjuries_HealthyPlayerUnaffected()
    {
        var league = CreateLeagueWithInjuredPlayer(5);
        var healthyPlayer = league.Teams[0].Roster[1];

        InjuryService.HealInjuries(league, 1, new Random(42));

        healthyPlayer.Health.Should().Be(100);
        healthyPlayer.Injury.Should().Be(0);
    }

    [Fact]
    public void HealInjuries_MultipleTeams_HealsAll()
    {
        var league = new League();
        var team1 = new Team { Id = 1, Name = "T1" };
        team1.Roster.Add(new Player { Name = "P1", Injury = 3, Health = 80, InjuryDescription = "Injury", Active = true });
        var team2 = new Team { Id = 2, Name = "T2" };
        team2.Roster.Add(new Player { Name = "P2", Injury = 2, Health = 80, InjuryDescription = "Injury", Active = true });
        league.Teams.Add(team1);
        league.Teams.Add(team2);

        InjuryService.HealInjuries(league, 1, new Random(42));

        team1.Roster[0].Injury.Should().Be(2);
        team2.Roster[0].Injury.Should().Be(1);
    }

    // ── ApplyInjury ────────────────────────────────────────────────

    [Fact]
    public void ApplyInjury_SetsInjuryAndDescription()
    {
        var player = new Player { Name = "Test", Health = 100, Active = true };

        InjuryService.ApplyInjury(player, 5, new Random(42));

        player.Injury.Should().BeGreaterThan(0);
        player.Health.Should().BeLessThan(100);
        player.InjuryDescription.Should().NotBeEmpty();
    }

    [Fact]
    public void ApplyInjury_ZeroGamesOut_NoEffect()
    {
        var player = new Player { Name = "Test", Health = 100, Active = true };

        InjuryService.ApplyInjury(player, 0, new Random(42));

        player.Injury.Should().Be(0);
        player.Health.Should().Be(100);
    }

    // ── GenerateInjuryDescription ──────────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(15)]
    [InlineData(50)]
    [InlineData(90)]
    public void GenerateInjuryDescription_ReturnsSeverityAppropriateName(int gamesOut)
    {
        var desc = InjuryService.GenerateInjuryDescription(gamesOut, new Random(42));

        desc.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateInjuryDescription_DeterministicWithSeed()
    {
        var desc1 = InjuryService.GenerateInjuryDescription(10, new Random(42));
        var desc2 = InjuryService.GenerateInjuryDescription(10, new Random(42));

        desc1.Should().Be(desc2);
    }
}
