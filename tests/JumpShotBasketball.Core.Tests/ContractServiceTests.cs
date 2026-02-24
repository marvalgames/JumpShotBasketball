using FluentAssertions;
using JumpShotBasketball.Core.Constants;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class ContractServiceTests
{
    private static Player CreatePlayerForContract(int age = 28, int yos = 5,
        double tradeTrueRating = 8.0, string pos = "PG")
    {
        return new Player
        {
            Name = "Test Player",
            Position = pos,
            Age = age,
            Ratings = new PlayerRatings
            {
                TradeTrueRating = tradeTrueRating,
                Potential1 = 3,
                Potential2 = 3,
                Effort = 3,
                Prime = 28
            },
            Contract = new PlayerContract
            {
                YearsOfService = yos
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

    // ── GeneratePlayerContract ──────────────────────────────────────

    [Fact]
    public void GeneratePlayerContract_SalaryScaledByRating()
    {
        var highRated = CreatePlayerForContract(tradeTrueRating: 15.0, yos: 8);
        var lowRated = CreatePlayerForContract(tradeTrueRating: 3.0, yos: 8);

        ContractService.GeneratePlayerContract(highRated, new Random(42));
        ContractService.GeneratePlayerContract(lowRated, new Random(42));

        highRated.Contract.CurrentYearSalary.Should().BeGreaterThan(lowRated.Contract.CurrentYearSalary);
    }

    [Fact]
    public void GeneratePlayerContract_MinimumSalaryEnforced()
    {
        // Very low rated player, YOS 0
        var player = CreatePlayerForContract(tradeTrueRating: 0.1, yos: 0);

        ContractService.GeneratePlayerContract(player, new Random(42));

        // Salary should be at least the YOS minimum
        player.Contract.CurrentYearSalary.Should().BeGreaterThanOrEqualTo(LeagueConstants.SalaryMinimumByYos[0]);
    }

    [Fact]
    public void GeneratePlayerContract_MaxSalaryCapped()
    {
        // Very high rated player
        var player = CreatePlayerForContract(tradeTrueRating: 50.0, yos: 12);

        ContractService.GeneratePlayerContract(player, new Random(42));

        // Current year salary should not exceed the max for 10+ YOS
        player.Contract.CurrentYearSalary.Should().BeLessThanOrEqualTo(LeagueConstants.SalaryMaximumByYos[10] + 200);
    }

    [Fact]
    public void GeneratePlayerContract_RookieScaleCapped()
    {
        // Rookie with YOS 1
        var player = CreatePlayerForContract(tradeTrueRating: 20.0, yos: 1);

        ContractService.GeneratePlayerContract(player, new Random(42));

        player.Contract.ContractYears.Should().Be(3);
        player.Contract.CurrentContractYear.Should().Be(1);
        player.Contract.CurrentYearSalary.Should().BeLessThanOrEqualTo(LeagueConstants.RookieSalaryCapYear1);
    }

    [Fact]
    public void GeneratePlayerContract_Year2RookieScaleCapped()
    {
        var player = CreatePlayerForContract(tradeTrueRating: 20.0, yos: 2);

        ContractService.GeneratePlayerContract(player, new Random(42));

        player.Contract.ContractYears.Should().Be(3);
        player.Contract.CurrentContractYear.Should().Be(2);
        player.Contract.CurrentYearSalary.Should().BeLessThanOrEqualTo(LeagueConstants.RookieSalaryCapYear2);
    }

    [Fact]
    public void GeneratePlayerContract_EndOfContract_MarksFreeAgent()
    {
        var player = CreatePlayerForContract(tradeTrueRating: 8.0, yos: 3);

        ContractService.GeneratePlayerContract(player, new Random(42));

        // YOS 3 → contractYears=3, currentContractYear=3 → free agent
        player.Contract.IsFreeAgent.Should().BeTrue();
    }

    [Fact]
    public void GeneratePlayerContract_ContractYearsMaxIs6()
    {
        var player = CreatePlayerForContract(age: 25, tradeTrueRating: 15.0, yos: 8);

        for (int seed = 0; seed < 50; seed++)
        {
            ContractService.GeneratePlayerContract(player, new Random(seed));
            player.Contract.ContractYears.Should().BeLessThanOrEqualTo(6);
        }
    }

    [Fact]
    public void GeneratePlayerContract_OldPlayerReducedYears()
    {
        var oldPlayer = CreatePlayerForContract(age: 33, tradeTrueRating: 10.0, yos: 10);

        ContractService.GeneratePlayerContract(oldPlayer, new Random(42));

        // Age 33: contractMax = 35-33 = 2
        oldPlayer.Contract.ContractYears.Should().BeLessThanOrEqualTo(6);
    }

    [Fact]
    public void GeneratePlayerContract_RemainingSalaryCalculated()
    {
        var player = CreatePlayerForContract(tradeTrueRating: 10.0, yos: 5);

        ContractService.GeneratePlayerContract(player, new Random(42));

        player.Contract.RemainingSalary.Should().BeGreaterThan(0);
    }

    // ── GeneratePreferenceFactors ──────────────────────────────────

    [Fact]
    public void GeneratePreferenceFactors_AllInRange1To5()
    {
        var player = CreatePlayerForContract();

        ContractService.GeneratePreferenceFactors(player, new Random(42));

        player.Contract.CoachFactor.Should().BeInRange(1, 5);
        player.Contract.SecurityFactor.Should().BeInRange(1, 5);
        player.Contract.LoyaltyFactor.Should().BeInRange(1, 5);
        player.Contract.WinningFactor.Should().BeInRange(1, 5);
        player.Contract.PlayingTimeFactor.Should().BeInRange(1, 5);
        player.Contract.TraditionFactor.Should().BeInRange(1, 5);
    }

    [Fact]
    public void GeneratePreferenceFactors_Deterministic()
    {
        var p1 = CreatePlayerForContract();
        var p2 = CreatePlayerForContract();

        ContractService.GeneratePreferenceFactors(p1, new Random(99));
        ContractService.GeneratePreferenceFactors(p2, new Random(99));

        p1.Contract.CoachFactor.Should().Be(p2.Contract.CoachFactor);
        p1.Contract.SecurityFactor.Should().Be(p2.Contract.SecurityFactor);
    }

    // ── GenerateContracts (full pipeline) ──────────────────────────

    [Fact]
    public void GenerateContracts_SetsContractsForAllPlayers()
    {
        var p1 = CreatePlayerForContract(age: 25, yos: 3, tradeTrueRating: 8.0);
        var p2 = CreatePlayerForContract(age: 30, yos: 8, tradeTrueRating: 12.0);
        var league = CreateLeagueWithPlayers(p1, p2);

        ContractService.GenerateContracts(league, skipAges: true, skipContracts: false,
            skipPotentials: true, new Random(42));

        p1.Contract.ContractYears.Should().BeGreaterThan(0);
        p2.Contract.ContractYears.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GenerateContracts_SkipPotentialsFalse_RandomizesPotentials()
    {
        var player = CreatePlayerForContract();
        player.Ratings.Potential1 = 3;
        player.Ratings.Potential2 = 3;
        player.Ratings.Effort = 3;
        var league = CreateLeagueWithPlayers(player);

        ContractService.GenerateContracts(league, skipAges: true, skipContracts: false,
            skipPotentials: false, new Random(42));

        // At least one should have changed from default 3
        bool changed = player.Ratings.Potential1 != 3
                     || player.Ratings.Potential2 != 3
                     || player.Ratings.Effort != 3;
        changed.Should().BeTrue("potentials should be randomized when skipPotentials=false");
    }
}
