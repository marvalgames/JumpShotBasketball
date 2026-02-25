using FluentAssertions;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Staff;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class RosterSortingServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────

    private static Player CreatePlayer(string name, string position, double tradeValue,
        int contractYears = 3, int currentContractYear = 1, int remainingSalary = 300,
        int age = 25, int prime = 30, int pot1 = 3, int pot2 = 3, int effort = 3,
        int prFga = 10, int prFta = 10, int prFgp = 450, int fgm = 10, int ftm = 5)
    {
        return new Player
        {
            Name = name,
            Position = position,
            Age = age,
            Active = true,
            Health = 100,
            Ratings = new PlayerRatings
            {
                TradeValue = tradeValue,
                Prime = prime,
                Potential1 = pot1,
                Potential2 = pot2,
                Effort = effort,
                ProjectionFieldGoalsAttempted = prFga,
                ProjectionFreeThrowsAttempted = prFta,
                ProjectionFieldGoalPercentage = prFgp
            },
            Contract = new PlayerContract
            {
                ContractYears = contractYears,
                CurrentContractYear = currentContractYear,
                RemainingSalary = remainingSalary
            },
            SeasonStats = new PlayerStatLine
            {
                FieldGoalsMade = fgm,
                FreeThrowsMade = ftm,
                Games = 40,
                Minutes = 1200
            }
        };
    }

    private static Team CreateTeam(string name = "Test Team", int currentValue = 1750,
        int gmPower2 = 3)
    {
        return new Team
        {
            Id = 1,
            Name = name,
            Financial = new TeamFinancial { CurrentValue = currentValue },
            GeneralManager = new StaffMember { Power2 = gmPower2 }
        };
    }

    // ── SortTeamRoster Tests ─────────────────────────────────────────

    [Fact]
    public void EmptyRoster_NoError()
    {
        var team = CreateTeam();

        var act = () => RosterSortingService.SortTeamRoster(team);

        act.Should().NotThrow();
        team.Roster.Should().BeEmpty();
    }

    [Fact]
    public void SinglePlayer_RemainsInPlace()
    {
        var team = CreateTeam();
        var player = CreatePlayer("Solo", "PG", 50);
        team.Roster.Add(player);

        RosterSortingService.SortTeamRoster(team);

        team.Roster.Should().HaveCount(1);
        team.Roster[0].Name.Should().Be("Solo");
        team.Roster[0].OriginalNumber.Should().Be(0);
    }

    [Fact]
    public void TwoPGs_SortedByValue()
    {
        var team = CreateTeam();
        team.Roster.Add(CreatePlayer("Weak PG", "PG", 30));
        team.Roster.Add(CreatePlayer("Strong PG", "PG", 80));

        RosterSortingService.SortTeamRoster(team);

        team.Roster[0].Name.Should().Be("Strong PG");
        team.Roster[1].Name.Should().Be("Weak PG");
    }

    [Fact]
    public void AllPositions_TwoPerPosition()
    {
        var team = CreateTeam();
        // Add 2 players per position
        foreach (var pos in new[] { "PG", "SG", "SF", "PF", "C" })
        {
            team.Roster.Add(CreatePlayer($"{pos}1", pos, 50));
            team.Roster.Add(CreatePlayer($"{pos}2", pos, 40));
        }

        RosterSortingService.SortTeamRoster(team);

        // Verify position order: PG, PG, SG, SG, SF, SF, PF, PF, C, C
        team.Roster[0].Position.Should().Be("PG");
        team.Roster[1].Position.Should().Be("PG");
        team.Roster[2].Position.Should().Be("SG");
        team.Roster[3].Position.Should().Be("SG");
        team.Roster[4].Position.Should().Be("SF");
        team.Roster[5].Position.Should().Be("SF");
        team.Roster[6].Position.Should().Be("PF");
        team.Roster[7].Position.Should().Be("PF");
        team.Roster[8].Position.Should().Be("C");
        team.Roster[9].Position.Should().Be("C");
    }

    [Fact]
    public void RemainingPlayers_SortedByValue()
    {
        var team = CreateTeam();
        // Fill position slots
        foreach (var pos in new[] { "PG", "SG", "SF", "PF", "C" })
        {
            team.Roster.Add(CreatePlayer($"{pos}1", pos, 50));
            team.Roster.Add(CreatePlayer($"{pos}2", pos, 40));
        }
        // Add bench players (3 extra PGs with varying value)
        team.Roster.Add(CreatePlayer("Bench3", "PG", 10));
        team.Roster.Add(CreatePlayer("Bench1", "PG", 30));
        team.Roster.Add(CreatePlayer("Bench2", "PG", 20));

        RosterSortingService.SortTeamRoster(team);

        // First 10 = starters by position, then bench by value descending
        team.Roster[10].Name.Should().Be("Bench1"); // 30
        team.Roster[11].Name.Should().Be("Bench2"); // 20
        team.Roster[12].Name.Should().Be("Bench3"); // 10
    }

    [Fact]
    public void Prospects_SortFirst_InRemaining()
    {
        var team = CreateTeam();
        foreach (var pos in new[] { "PG", "SG", "SF", "PF", "C" })
        {
            team.Roster.Add(CreatePlayer($"{pos}1", pos, 50));
            team.Roster.Add(CreatePlayer($"{pos}2", pos, 40));
        }

        // High-value non-prospect bench player
        team.Roster.Add(CreatePlayer("Veteran", "PG", 35, age: 30, prime: 28, pot1: 1, pot2: 1, effort: 1));

        // Low-value prospect (young, high potential, 7+ years to prime)
        team.Roster.Add(CreatePlayer("Prospect", "PG", 10, age: 18, prime: 28, pot1: 5, pot2: 5, effort: 5));

        RosterSortingService.SortTeamRoster(team);

        // Prospect should sort before veteran in bench slots despite lower trade value
        team.Roster[10].Name.Should().Be("Prospect");
        team.Roster[11].Name.Should().Be("Veteran");
    }

    [Fact]
    public void ProspectDetection_PrimeAge_RequiresSeven()
    {
        var team = CreateTeam();
        foreach (var pos in new[] { "PG", "SG", "SF", "PF", "C" })
        {
            team.Roster.Add(CreatePlayer($"{pos}1", pos, 50));
            team.Roster.Add(CreatePlayer($"{pos}2", pos, 40));
        }

        // 6 years to prime = NOT a prospect
        team.Roster.Add(CreatePlayer("NotProspect", "PG", 10, age: 22, prime: 28, pot1: 5, pot2: 5, effort: 5));
        // 7 years to prime = IS a prospect
        team.Roster.Add(CreatePlayer("Prospect", "PG", 5, age: 21, prime: 28, pot1: 5, pot2: 5, effort: 5));

        RosterSortingService.SortTeamRoster(team);

        // Prospect sorts first despite lower value
        team.Roster[10].Name.Should().Be("Prospect");
        team.Roster[11].Name.Should().Be("NotProspect");
    }

    [Fact]
    public void ProspectDetection_GmPower2_AffectsThreshold()
    {
        // With gmPower2=5: threshold = (6-5)+8 = 9, so pot1+pot2+effort must be > 9
        var team = CreateTeam(gmPower2: 5);
        foreach (var pos in new[] { "PG", "SG", "SF", "PF", "C" })
        {
            team.Roster.Add(CreatePlayer($"{pos}1", pos, 50));
            team.Roster.Add(CreatePlayer($"{pos}2", pos, 40));
        }

        // pot1+pot2+effort = 9 → NOT prospect (must be > 9)
        team.Roster.Add(CreatePlayer("Borderline", "PG", 10, age: 18, prime: 28, pot1: 3, pot2: 3, effort: 3));
        // pot1+pot2+effort = 12 → IS prospect
        team.Roster.Add(CreatePlayer("ClearProspect", "PG", 5, age: 18, prime: 28, pot1: 4, pot2: 4, effort: 4));

        RosterSortingService.SortTeamRoster(team);

        team.Roster[10].Name.Should().Be("ClearProspect");
        team.Roster[11].Name.Should().Be("Borderline");
    }

    [Fact]
    public void ContractBonus_UnderpaidPlayer()
    {
        var team = CreateTeam(currentValue: 1750);

        // Player A: tradeValue=50, underpaid (low remaining salary relative to value)
        var playerA = CreatePlayer("Underpaid", "PG", 50, contractYears: 3, currentContractYear: 1, remainingSalary: 100);
        // Player B: tradeValue=50, no underpayment
        var playerB = CreatePlayer("FairPay", "PG", 50, contractYears: 3, currentContractYear: 1, remainingSalary: 15000);

        team.Roster.Add(playerB);
        team.Roster.Add(playerA);

        RosterSortingService.SortTeamRoster(team);

        // Both are PGs; the one with higher composite value should be first
        // Underpaid: |50*3 - 100/100| * 400/1750 = |150-1| * 0.2286 = 149*0.2286 = 34.06 → total 84.06
        // FairPay: |50*3 - 15000/100| * 400/1750 = |150-150| * 0.2286 = 0 → total 50.0
        team.Roster[0].Name.Should().Be("Underpaid");
    }

    [Fact]
    public void ContractBonus_OverpaidPlayer()
    {
        var team = CreateTeam(currentValue: 1750);

        // Overpaid player still gets absolute value bonus
        var overpaid = CreatePlayer("Overpaid", "PG", 20, contractYears: 3, currentContractYear: 1, remainingSalary: 50000);
        var normal = CreatePlayer("Normal", "PG", 20, contractYears: 3, currentContractYear: 1, remainingSalary: 6000);

        team.Roster.Add(normal);
        team.Roster.Add(overpaid);

        RosterSortingService.SortTeamRoster(team);

        // Overpaid: |20*3 - 50000/100| * 400/1750 = |60-500|*0.2286 = 440*0.2286 = 100.57 → 120.57
        // Normal: |20*3 - 6000/100| * 400/1750 = |60-60|*0.2286 = 0 → 20.0
        team.Roster[0].Name.Should().Be("Overpaid");
    }

    [Fact]
    public void OkErase_FiltersWeakStats()
    {
        var team = CreateTeam();
        var strong = CreatePlayer("Strong", "PG", 50, prFga: 10, prFta: 10, prFgp: 450, fgm: 10, ftm: 5);
        var weak = CreatePlayer("Weak", "PG", 60, prFga: 3, prFta: 3, prFgp: 0, fgm: 2, ftm: 1);

        team.Roster.Add(weak);
        team.Roster.Add(strong);

        RosterSortingService.SortTeamRoster(team, okErase: true);

        // Weak player is ineligible under okErase, Strong sorts first among eligible
        team.Roster[0].Name.Should().Be("Strong");
        // Weak player is added after eligible players
        team.Roster[1].Name.Should().Be("Weak");
    }

    [Fact]
    public void OkErase_False_AllPlayersEligible()
    {
        var team = CreateTeam();
        var weak = CreatePlayer("Weak", "PG", 60, prFga: 3, prFta: 3, prFgp: 0, fgm: 2, ftm: 1);
        var strong = CreatePlayer("Strong", "PG", 50);

        team.Roster.Add(strong);
        team.Roster.Add(weak);

        RosterSortingService.SortTeamRoster(team, okErase: false);

        // Both eligible (okErase=false); weak has higher TradeValue
        team.Roster[0].Name.Should().Be("Weak");
        team.Roster[1].Name.Should().Be("Strong");
    }

    [Fact]
    public void MissingPosition_SkipsGracefully()
    {
        var team = CreateTeam();
        // No PG on team — add 2 SGs and 1 SF
        team.Roster.Add(CreatePlayer("SG1", "SG", 50));
        team.Roster.Add(CreatePlayer("SG2", "SG", 40));
        team.Roster.Add(CreatePlayer("SF1", "SF", 30));

        var act = () => RosterSortingService.SortTeamRoster(team);
        act.Should().NotThrow();

        // SGs should be first (no PGs to fill slots 0-1), then SF
        team.Roster[0].Position.Should().Be("SG");
        team.Roster[1].Position.Should().Be("SG");
        team.Roster[2].Position.Should().Be("SF");
    }

    [Fact]
    public void OriginalNumber_SetCorrectly()
    {
        var team = CreateTeam();
        team.Roster.Add(CreatePlayer("PG1", "PG", 50));
        team.Roster.Add(CreatePlayer("SG1", "SG", 40));
        team.Roster.Add(CreatePlayer("SF1", "SF", 30));

        RosterSortingService.SortTeamRoster(team);

        for (int i = 0; i < team.Roster.Count; i++)
            team.Roster[i].OriginalNumber.Should().Be(i);
    }

    [Fact]
    public void NoCoachOrGM_UsesDefaults()
    {
        var team = new Team
        {
            Id = 1,
            Name = "No Staff",
            Financial = new TeamFinancial { CurrentValue = 1750 },
            GeneralManager = null
        };
        team.Roster.Add(CreatePlayer("PG1", "PG", 50));
        team.Roster.Add(CreatePlayer("Young", "PG", 10, age: 18, prime: 28, pot1: 5, pot2: 5, effort: 5));

        var act = () => RosterSortingService.SortTeamRoster(team);
        act.Should().NotThrow();
    }

    // ── SortAllTeamRosters Tests ─────────────────────────────────────

    [Fact]
    public void AllTeams_Sorted()
    {
        var league = new League();
        for (int t = 0; t < 3; t++)
        {
            var team = CreateTeam($"Team{t}");
            team.Roster.Add(CreatePlayer($"PG{t}Low", "PG", 20));
            team.Roster.Add(CreatePlayer($"PG{t}High", "PG", 80));
            league.Teams.Add(team);
        }

        RosterSortingService.SortAllTeamRosters(league);

        foreach (var team in league.Teams)
        {
            // Higher value PG should be first
            team.Roster[0].Ratings.TradeValue.Should().BeGreaterThan(team.Roster[1].Ratings.TradeValue);
        }
    }

    [Fact]
    public void EmptyLeague_NoError()
    {
        var league = new League();

        var act = () => RosterSortingService.SortAllTeamRosters(league);

        act.Should().NotThrow();
    }

    [Fact]
    public void MixedTeamSizes_Handled()
    {
        var league = new League();

        var team1 = CreateTeam("Big");
        for (int i = 0; i < 15; i++)
            team1.Roster.Add(CreatePlayer($"P{i}", "PG", 50 - i));
        league.Teams.Add(team1);

        var team2 = CreateTeam("Small");
        team2.Roster.Add(CreatePlayer("Lonely", "C", 40));
        league.Teams.Add(team2);

        var act = () => RosterSortingService.SortAllTeamRosters(league);
        act.Should().NotThrow();

        league.Teams[0].Roster.Should().HaveCount(15);
        league.Teams[1].Roster.Should().HaveCount(1);
    }

    // ── CalculateCompositeTradeValue Tests ────────────────────────────

    [Fact]
    public void BasicCalculation()
    {
        var team = CreateTeam(currentValue: 1750);
        var player = CreatePlayer("Test", "PG", 50, contractYears: 3, currentContractYear: 1, remainingSalary: 6000);

        double value = RosterSortingService.CalculateCompositeTradeValue(player, team);

        // yearsLeft = 1+3-1 = 3, underpaid = |50*3 - 6000/100| = |150-60| = 90
        // bonus = 90 * 400 / 1750 ≈ 20.57
        // total ≈ 70.57
        value.Should().BeApproximately(50 + 90.0 * 400.0 / 1750.0, 0.01);
    }

    [Fact]
    public void ZeroTeamValue_NoException()
    {
        var team = CreateTeam(currentValue: 0);
        var player = CreatePlayer("Test", "PG", 50);

        var act = () => RosterSortingService.CalculateCompositeTradeValue(player, team);
        act.Should().NotThrow();

        double value = RosterSortingService.CalculateCompositeTradeValue(player, team);
        value.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ExpiredContract_ZeroYearsLeft()
    {
        var team = CreateTeam(currentValue: 1750);
        var player = CreatePlayer("Test", "PG", 50, contractYears: 3, currentContractYear: 4, remainingSalary: 0);

        double value = RosterSortingService.CalculateCompositeTradeValue(player, team);

        // yearsLeft = 1+3-4 = 0, underpaid = |50*0 - 0/100| = 0, bonus = 0
        value.Should().Be(50);
    }

    [Fact]
    public void HighlyUnderpaid_LargeBonus()
    {
        var team = CreateTeam(currentValue: 500);
        var player = CreatePlayer("Star", "PG", 100, contractYears: 5, currentContractYear: 1, remainingSalary: 100);

        double value = RosterSortingService.CalculateCompositeTradeValue(player, team);

        // yearsLeft=5, underpaid = |100*5 - 100/100| = |500-1| = 499
        // bonus = 499 * 400/500 = 399.2
        // total = 100 + 399.2 = 499.2
        value.Should().BeGreaterThan(400);
    }

    // ── Integration Tests ────────────────────────────────────────────

    [Fact]
    public void RosterOrder_MatchesCppPattern()
    {
        var team = CreateTeam();

        // Add 3 players per position (20 total = 15 starters + bench)
        var positions = new[] { "PG", "SG", "SF", "PF", "C" };
        int idx = 0;
        foreach (var pos in positions)
        {
            team.Roster.Add(CreatePlayer($"{pos}Star", pos, 80 - idx));
            team.Roster.Add(CreatePlayer($"{pos}Starter", pos, 60 - idx));
            team.Roster.Add(CreatePlayer($"{pos}Bench", pos, 30 - idx));
            idx += 2;
        }

        RosterSortingService.SortTeamRoster(team);

        // Verify C++ pattern: PG,PG,SG,SG,SF,SF,PF,PF,C,C,bench...
        var expectedPositions = new[] { "PG", "PG", "SG", "SG", "SF", "SF", "PF", "PF", "C", "C" };
        for (int i = 0; i < 10; i++)
            team.Roster[i].Position.Should().Be(expectedPositions[i], $"slot {i} should be {expectedPositions[i]}");

        // Bench slots (10-14) should be filled from remaining
        team.Roster.Count.Should().Be(15);
    }

    [Fact]
    public void EmptyNamePlayers_PreservedAtEnd()
    {
        var team = CreateTeam();
        team.Roster.Add(CreatePlayer("PG1", "PG", 50));
        team.Roster.Add(new Player { Name = "", Position = "PG" }); // empty name
        team.Roster.Add(CreatePlayer("SG1", "SG", 40));

        RosterSortingService.SortTeamRoster(team);

        // Named players sorted first, empty at end
        team.Roster.Should().HaveCount(3);
        team.Roster[0].Name.Should().Be("PG1");
        team.Roster[1].Name.Should().Be("SG1");
        team.Roster[2].Name.Should().BeEmpty();
    }

    [Fact]
    public void ExcessPositionPlayers_GoToBench()
    {
        var team = CreateTeam();
        // 4 PGs — only top 2 go to PG slots, other 2 to bench
        team.Roster.Add(CreatePlayer("PG1", "PG", 80));
        team.Roster.Add(CreatePlayer("PG2", "PG", 70));
        team.Roster.Add(CreatePlayer("PG3", "PG", 60));
        team.Roster.Add(CreatePlayer("PG4", "PG", 50));
        team.Roster.Add(CreatePlayer("C1", "C", 90));

        RosterSortingService.SortTeamRoster(team);

        // PG1, PG2 in PG slots, C1 in C slot, PG3/PG4 in bench
        team.Roster[0].Name.Should().Be("PG1");
        team.Roster[1].Name.Should().Be("PG2");
        team.Roster[2].Position.Should().Be("C"); // first C slot
    }
}
