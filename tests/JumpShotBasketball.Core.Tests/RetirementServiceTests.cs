using FluentAssertions;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class RetirementServiceTests
{
    private static Player CreatePlayer(int age, int prime, double tradeTrueRating = 10.0,
        int prFga = 50, int prFta = 50, int prFgp = 50)
    {
        return new Player
        {
            Name = "Test Player",
            Position = "PG",
            Age = age,
            Ratings = new PlayerRatings
            {
                Prime = prime,
                TradeTrueRating = tradeTrueRating,
                ProjectionFieldGoalsAttempted = prFga,
                ProjectionFreeThrowsAttempted = prFta,
                ProjectionFieldGoalPercentage = prFgp
            }
        };
    }

    private static League CreateLeagueWithPlayers(params Player[] players)
    {
        var league = new League();
        var team = new Team { Id = 1, Name = "TestTeam" };
        foreach (var p in players)
            team.Roster.Add(p);
        league.Teams.Add(team);
        return league;
    }

    // ── ShouldRetire ────────────────────────────────────────────────

    [Fact]
    public void ShouldRetire_YoungPlayerWithGoodStats_ReturnsFalse()
    {
        var player = CreatePlayer(age: 25, prime: 28);

        // Young player with decent ratings should almost never retire
        // Base probability is 0.0005, so with seed 42 they should not retire
        var result = RetirementService.ShouldRetire(player, new Random(42));

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldRetire_OldPlayerPastPrime_MoreLikelyToRetire()
    {
        // Age 42, prime 28 → well past prime+5, age>40 multiplier
        // f = (42 - 28 + tradeTrueRating) / 250 * (42-39) = (14 + 5) / 250 * 3 = 0.228
        var player = CreatePlayer(age: 42, prime: 28, tradeTrueRating: 5.0);

        // With such high probability, test with many seeds to verify most retire
        int retireCount = 0;
        for (int seed = 0; seed < 100; seed++)
        {
            if (RetirementService.ShouldRetire(player, new Random(seed)))
                retireCount++;
        }

        retireCount.Should().BeGreaterThan(15, "a 42-year-old past-prime player should retire frequently");
    }

    [Fact]
    public void ShouldRetire_PerformanceCutoff_ForcesRetirement()
    {
        // Player with projection ratings below threshold → forced retire
        var player = CreatePlayer(age: 30, prime: 28, prFga: 2, prFta: 2, prFgp: 20);

        var result = RetirementService.ShouldRetire(player, new Random(42));

        result.Should().BeTrue("performance cutoff should force retirement");
    }

    [Fact]
    public void ShouldRetire_PerformanceCutoff_OnlyWhenAllBelowThreshold()
    {
        // prFga >= 3 means performance is OK
        var player = CreatePlayer(age: 30, prime: 28, prFga: 3, prFta: 3, prFgp: 25);

        var result = RetirementService.ShouldRetire(player, new Random(42));

        result.Should().BeFalse("performance is above cutoff");
    }

    [Fact]
    public void ShouldRetire_Age40_IncreasedProbability()
    {
        // Age 41, prime 28 → past prime+5 → f increased, then * (41-39) = *2
        var player = CreatePlayer(age: 41, prime: 28, tradeTrueRating: 8.0);

        int retireCount = 0;
        for (int seed = 0; seed < 100; seed++)
        {
            if (RetirementService.ShouldRetire(player, new Random(seed)))
                retireCount++;
        }

        retireCount.Should().BeGreaterThan(10, "age 41 with multiplier should increase retirements");
    }

    [Fact]
    public void ShouldRetire_NotPastPrimePlus5_UsesBaseProbability()
    {
        // Age 32, prime 28 → 28+5=33 > 32 → uses base f=0.0005
        var player = CreatePlayer(age: 32, prime: 28);

        int retireCount = 0;
        for (int seed = 0; seed < 1000; seed++)
        {
            if (RetirementService.ShouldRetire(player, new Random(seed)))
                retireCount++;
        }

        retireCount.Should().BeLessThan(10, "base probability 0.0005 should rarely trigger");
    }

    // ── ProcessRetirements ──────────────────────────────────────────

    [Fact]
    public void ProcessRetirements_ReturnsRetiredNames()
    {
        // Force retirement via performance cutoff
        var retiring = CreatePlayer(age: 30, prime: 28, prFga: 1, prFta: 1, prFgp: 10);
        retiring.Name = "Old Player";
        var staying = CreatePlayer(age: 25, prime: 28);
        staying.Name = "Young Player";

        var league = CreateLeagueWithPlayers(retiring, staying);
        var result = RetirementService.ProcessRetirements(league, new Random(42));

        result.Should().Contain("Old Player");
        result.Should().NotContain("Young Player");
    }

    [Fact]
    public void ProcessRetirements_SetsRetiredFlag()
    {
        var retiring = CreatePlayer(age: 30, prime: 28, prFga: 0, prFta: 0, prFgp: 0);
        retiring.Name = "Bad Player";
        var league = CreateLeagueWithPlayers(retiring);

        RetirementService.ProcessRetirements(league, new Random(42));

        retiring.Retired.Should().BeTrue();
    }

    [Fact]
    public void ProcessRetirements_SkipsEmptyNameOrPosition()
    {
        var noName = CreatePlayer(age: 42, prime: 28, prFga: 0, prFta: 0, prFgp: 0);
        noName.Name = "";
        var noPos = CreatePlayer(age: 42, prime: 28, prFga: 0, prFta: 0, prFgp: 0);
        noPos.Name = "No Pos";
        noPos.Position = "";

        var league = CreateLeagueWithPlayers(noName, noPos);
        var result = RetirementService.ProcessRetirements(league, new Random(42));

        result.Should().BeEmpty("players with empty name or position should be skipped");
    }

    [Fact]
    public void ProcessRetirements_Deterministic()
    {
        var player = CreatePlayer(age: 38, prime: 28, tradeTrueRating: 5.0);
        player.Name = "Vet Player";

        var league1 = CreateLeagueWithPlayers(player);
        var result1 = RetirementService.ProcessRetirements(league1, new Random(99));

        // Reset
        player.Retired = false;
        var league2 = CreateLeagueWithPlayers(player);
        var result2 = RetirementService.ProcessRetirements(league2, new Random(99));

        result1.Count.Should().Be(result2.Count, "same seed should produce same result");
    }
}
