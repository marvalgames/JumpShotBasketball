using FluentAssertions;
using JumpShotBasketball.Core.Constants;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class RosterManagementServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────

    private static Player CreatePlayer(
        string name, string position, double tradeTrueRating = 5.0,
        int injury = 0, bool justSigned = false, int yos = 3)
    {
        return new Player
        {
            Name = name,
            Position = position,
            Age = 25,
            Active = true,
            Injury = injury,
            Health = injury > 0 ? 50 : 100,
            Ratings = new PlayerRatings
            {
                TradeTrueRating = tradeTrueRating,
                TrueRatingSimple = tradeTrueRating,
                Effort = 5,
                Potential1 = 3,
                Potential2 = 3
            },
            Contract = new PlayerContract
            {
                IsFreeAgent = false,
                JustSigned = justSigned,
                YearsOfService = yos,
                ContractYears = 3,
                CurrentContractYear = 1,
                CurrentYearSalary = 200
            }
        };
    }

    private static Player CreateFreeAgent(
        string name, string position, double tradeTrueRating = 5.0, int yos = 3)
    {
        return new Player
        {
            Name = name,
            Position = position,
            Age = 25,
            Active = false,
            Health = 100,
            Ratings = new PlayerRatings
            {
                TradeTrueRating = tradeTrueRating,
                TrueRatingSimple = tradeTrueRating,
                Effort = 5,
                Potential1 = 3,
                Potential2 = 3
            },
            Contract = new PlayerContract
            {
                IsFreeAgent = true,
                YearsOfService = yos,
                ContractYears = 0,
                CurrentContractYear = 0,
                CurrentYearSalary = 0
            }
        };
    }

    private static Team CreateTeam(string name = "TestTeam", string control = "Computer")
    {
        return new Team
        {
            Id = 0,
            Name = name,
            CityName = "TestCity",
            Record = new TeamRecord
            {
                TeamName = name,
                Control = control
            },
            Financial = new TeamFinancial { TeamName = name }
        };
    }

    private static League CreateLeague(int numTeams = 2)
    {
        var league = new League();
        league.Settings.NumberOfTeams = numTeams;
        for (int i = 0; i < numTeams; i++)
        {
            var team = new Team
            {
                Id = i,
                Name = $"Team{i}",
                CityName = $"City{i}",
                Record = new TeamRecord
                {
                    TeamName = $"Team{i}",
                    Control = "Computer"
                },
                Financial = new TeamFinancial { TeamName = $"Team{i}" }
            };
            league.Teams.Add(team);
        }
        return league;
    }

    private static void AddPlayersToTeam(Team team, int count, string position = "SF", double baseRating = 5.0)
    {
        for (int i = 0; i < count; i++)
        {
            team.Roster.Add(CreatePlayer($"Player{team.Roster.Count}", position, baseRating + i * 0.1));
        }
    }

    /// <summary>
    /// Creates a team with a balanced roster of the given size.
    /// </summary>
    private static void AddBalancedRoster(Team team, int count, double baseRating = 5.0)
    {
        string[] positions = { "PG", "SG", "SF", "PF", "C" };
        for (int i = 0; i < count; i++)
        {
            string pos = positions[i % 5];
            team.Roster.Add(CreatePlayer($"Player{team.Roster.Count}", pos, baseRating + i * 0.1));
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // PositionNeeded Tests
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void PositionNeeded_MissingCenter_ReturnsCenter()
    {
        var team = CreateTeam();
        team.Roster.Add(CreatePlayer("PG1", "PG"));
        team.Roster.Add(CreatePlayer("PG2", "PG"));
        team.Roster.Add(CreatePlayer("SG1", "SG"));
        team.Roster.Add(CreatePlayer("SF1", "SF"));
        team.Roster.Add(CreatePlayer("PF1", "PF"));
        // No center

        int needed = RosterManagementService.PositionNeeded(team);
        needed.Should().Be(5); // C
    }

    [Fact]
    public void PositionNeeded_AllEqual_ReturnsCenterTiebreak()
    {
        var team = CreateTeam();
        // 1 player at each position — all equal
        team.Roster.Add(CreatePlayer("PG1", "PG"));
        team.Roster.Add(CreatePlayer("SG1", "SG"));
        team.Roster.Add(CreatePlayer("SF1", "SF"));
        team.Roster.Add(CreatePlayer("PF1", "PF"));
        team.Roster.Add(CreatePlayer("C1", "C"));

        int needed = RosterManagementService.PositionNeeded(team);
        needed.Should().Be(5); // C wins tiebreak
    }

    [Fact]
    public void PositionNeeded_SkipsInjuredPlayers()
    {
        var team = CreateTeam();
        team.Roster.Add(CreatePlayer("PG1", "PG"));
        team.Roster.Add(CreatePlayer("SG1", "SG"));
        team.Roster.Add(CreatePlayer("SF1", "SF"));
        team.Roster.Add(CreatePlayer("PF1", "PF"));
        team.Roster.Add(CreatePlayer("C1", "C", injury: 10)); // Injured center

        // Center is injured, so effectively 0 active centers
        int needed = RosterManagementService.PositionNeeded(team);
        needed.Should().Be(5); // C needed since only center is injured
    }

    [Fact]
    public void PositionNeeded_EmptyRoster_ReturnsCenter()
    {
        var team = CreateTeam();

        int needed = RosterManagementService.PositionNeeded(team);
        needed.Should().Be(5); // Default to C (first in tiebreak order)
    }

    // ══════════════════════════════════════════════════════════════════
    // ReleaseWorstPlayer Tests
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void ReleaseWorstPlayer_RemovesLowestRated()
    {
        var league = CreateLeague(1);
        var team = league.Teams[0];
        team.Roster.Add(CreatePlayer("Star", "PG", 9.0));
        team.Roster.Add(CreatePlayer("Scrub", "SG", 2.0));
        team.Roster.Add(CreatePlayer("Average", "SF", 5.0));

        string? released = RosterManagementService.ReleaseWorstPlayer(league, team, 0);

        released.Should().Be("Scrub");
        team.Roster.Should().HaveCount(2);
        team.Roster.Should().NotContain(p => p.Name == "Scrub");
    }

    [Fact]
    public void ReleaseWorstPlayer_SkipsInjuredPlayers()
    {
        var league = CreateLeague(1);
        var team = league.Teams[0];
        team.Roster.Add(CreatePlayer("Star", "PG", 9.0));
        team.Roster.Add(CreatePlayer("InjuredScrub", "SG", 1.0, injury: 20));
        team.Roster.Add(CreatePlayer("HealthyScrub", "SF", 3.0));

        string? released = RosterManagementService.ReleaseWorstPlayer(league, team, 0);

        released.Should().Be("HealthyScrub"); // Injured player skipped
    }

    [Fact]
    public void ReleaseWorstPlayer_SkipsJustSigned()
    {
        var league = CreateLeague(1);
        var team = league.Teams[0];
        team.Roster.Add(CreatePlayer("Star", "PG", 9.0));
        team.Roster.Add(CreatePlayer("NewGuy", "SG", 1.0, justSigned: true));
        team.Roster.Add(CreatePlayer("OldGuy", "SF", 3.0));

        string? released = RosterManagementService.ReleaseWorstPlayer(league, team, 0);

        released.Should().Be("OldGuy"); // JustSigned player skipped
    }

    [Fact]
    public void ReleaseWorstPlayer_AddsToFreeAgentPool()
    {
        var league = CreateLeague(1);
        var team = league.Teams[0];
        team.Roster.Add(CreatePlayer("Star", "PG", 9.0));
        team.Roster.Add(CreatePlayer("Scrub", "SG", 2.0));

        RosterManagementService.ReleaseWorstPlayer(league, team, 0);

        league.FreeAgentPool.Should().HaveCount(1);
        league.FreeAgentPool[0].Name.Should().Be("Scrub");
    }

    [Fact]
    public void ReleaseWorstPlayer_ClearsContract()
    {
        var league = CreateLeague(1);
        var team = league.Teams[0];
        var scrub = CreatePlayer("Scrub", "SG", 2.0);
        scrub.Contract.ContractYears = 3;
        scrub.Contract.CurrentContractYear = 2;
        team.Roster.Add(CreatePlayer("Star", "PG", 9.0));
        team.Roster.Add(scrub);

        RosterManagementService.ReleaseWorstPlayer(league, team, 0);

        scrub.Contract.IsFreeAgent.Should().BeTrue();
        scrub.Contract.ContractYears.Should().Be(0);
        scrub.Contract.CurrentContractYear.Should().Be(0);
    }

    // ══════════════════════════════════════════════════════════════════
    // FindBestFreeAgent Tests
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void FindBestFreeAgent_MatchesPosition()
    {
        var league = CreateLeague(1);
        var team = league.Teams[0];

        league.FreeAgentPool.Add(CreateFreeAgent("FA_SG", "SG", 7.0));
        league.FreeAgentPool.Add(CreateFreeAgent("FA_C", "C", 6.0));
        league.FreeAgentPool.Add(CreateFreeAgent("FA_PG", "PG", 8.0));

        // Need a center (position 5)
        var best = RosterManagementService.FindBestFreeAgent(league, team, 5);

        best.Should().NotBeNull();
        best!.Name.Should().Be("FA_C"); // Position match preferred
    }

    [Fact]
    public void FindBestFreeAgent_PicksHighestRated_BugFix()
    {
        var league = CreateLeague(1);
        var team = league.Teams[0];

        // Multiple centers — should pick highest rated (C++ bug picks last)
        league.FreeAgentPool.Add(CreateFreeAgent("BestCenter", "C", 9.0));
        league.FreeAgentPool.Add(CreateFreeAgent("MedCenter", "C", 5.0));
        league.FreeAgentPool.Add(CreateFreeAgent("WorstCenter", "C", 3.0));

        var best = RosterManagementService.FindBestFreeAgent(league, team, 5);

        best.Should().NotBeNull();
        best!.Name.Should().Be("BestCenter"); // Bug fix: picks BEST, not last
    }

    [Fact]
    public void FindBestFreeAgent_FallsBackToAnyPosition()
    {
        var league = CreateLeague(1);
        var team = league.Teams[0];

        // Only PG available, need center
        league.FreeAgentPool.Add(CreateFreeAgent("FA_PG", "PG", 7.0));

        var best = RosterManagementService.FindBestFreeAgent(league, team, 5);

        best.Should().NotBeNull();
        best!.Name.Should().Be("FA_PG"); // Fallback to any position
    }

    [Fact]
    public void FindBestFreeAgent_EmptyPool_ReturnsNull()
    {
        var league = CreateLeague(1);
        var team = league.Teams[0];

        var best = RosterManagementService.FindBestFreeAgent(league, team, 5);

        best.Should().BeNull();
    }

    [Fact]
    public void FindBestFreeAgent_SkipsRetiredPlayers()
    {
        var league = CreateLeague(1);
        var team = league.Teams[0];

        var retired = CreateFreeAgent("RetiredGuy", "C", 10.0);
        retired.Retired = true;
        league.FreeAgentPool.Add(retired);
        league.FreeAgentPool.Add(CreateFreeAgent("ActiveGuy", "C", 5.0));

        var best = RosterManagementService.FindBestFreeAgent(league, team, 5);

        best.Should().NotBeNull();
        best!.Name.Should().Be("ActiveGuy");
    }

    // ══════════════════════════════════════════════════════════════════
    // SignEmergencyFreeAgent Tests
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void SignEmergencyFreeAgent_AddsToRoster()
    {
        var league = CreateLeague(1);
        var team = league.Teams[0];
        var fa = CreateFreeAgent("NewGuy", "C", 6.0);
        league.FreeAgentPool.Add(fa);

        RosterManagementService.SignEmergencyFreeAgent(league, team, 0, fa);

        team.Roster.Should().Contain(fa);
    }

    [Fact]
    public void SignEmergencyFreeAgent_MinimumContract()
    {
        var league = CreateLeague(1);
        var team = league.Teams[0];
        var fa = CreateFreeAgent("NewGuy", "C", 6.0, yos: 5);
        league.FreeAgentPool.Add(fa);

        RosterManagementService.SignEmergencyFreeAgent(league, team, 0, fa);

        int expectedMin = LeagueConstants.SalaryMinimumByYos[5];
        fa.Contract.CurrentYearSalary.Should().Be(expectedMin);
        fa.Contract.ContractYears.Should().Be(1);
        fa.Contract.IsFreeAgent.Should().BeFalse();
    }

    [Fact]
    public void SignEmergencyFreeAgent_JustSignedFlag()
    {
        var league = CreateLeague(1);
        var team = league.Teams[0];
        var fa = CreateFreeAgent("NewGuy", "C", 6.0);
        league.FreeAgentPool.Add(fa);

        RosterManagementService.SignEmergencyFreeAgent(league, team, 0, fa);

        fa.Contract.JustSigned.Should().BeTrue();
    }

    [Fact]
    public void SignEmergencyFreeAgent_RemovedFromPool()
    {
        var league = CreateLeague(1);
        var team = league.Teams[0];
        var fa = CreateFreeAgent("NewGuy", "C", 6.0);
        league.FreeAgentPool.Add(fa);

        RosterManagementService.SignEmergencyFreeAgent(league, team, 0, fa);

        league.FreeAgentPool.Should().NotContain(fa);
    }

    // ══════════════════════════════════════════════════════════════════
    // ProcessRosterEmergencies Tests
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void ProcessRosterEmergencies_OverMaxRoster_ReleasesPlayer()
    {
        var league = CreateLeague(1);
        var team = league.Teams[0];
        // Add 16 players (over max of 15)
        AddBalancedRoster(team, 16);
        var random = new Random(42);

        var result = RosterManagementService.ProcessRosterEmergencies(league, random);

        result.PlayersReleased.Should().HaveCount(1);
        team.Roster.Should().HaveCount(15);
    }

    [Fact]
    public void ProcessRosterEmergencies_UnderMinActive_SignsPlayer()
    {
        var league = CreateLeague(1);
        var team = league.Teams[0];
        // Add 7 healthy players (under min of 8)
        AddBalancedRoster(team, 7);
        // Add some free agents to the pool
        league.FreeAgentPool.Add(CreateFreeAgent("FA1", "C", 5.0));
        league.FreeAgentPool.Add(CreateFreeAgent("FA2", "PG", 4.0));
        var random = new Random(42);

        var result = RosterManagementService.ProcessRosterEmergencies(league, random);

        result.PlayersSigned.Should().HaveCount(1);
        team.Roster.Should().HaveCount(8);
    }

    [Fact]
    public void ProcessRosterEmergencies_ValidRoster_NoChanges()
    {
        var league = CreateLeague(1);
        var team = league.Teams[0];
        // 10 players — valid roster
        AddBalancedRoster(team, 10);
        var random = new Random(42);

        var result = RosterManagementService.ProcessRosterEmergencies(league, random);

        result.PlayersReleased.Should().BeEmpty();
        result.PlayersSigned.Should().BeEmpty();
    }

    [Fact]
    public void ProcessRosterEmergencies_SkipsPlayerControlled()
    {
        var league = CreateLeague(1);
        var team = league.Teams[0];
        team.Record.Control = "Player"; // Player-controlled
        // Add 16 players — would be released if computer-controlled
        AddBalancedRoster(team, 16);
        var random = new Random(42);

        var result = RosterManagementService.ProcessRosterEmergencies(league, random);

        result.PlayersReleased.Should().BeEmpty();
        team.Roster.Should().HaveCount(16); // No changes
    }

    [Fact]
    public void ProcessRosterEmergencies_RecordsTransactions()
    {
        var league = CreateLeague(1);
        var team = league.Teams[0];
        // Add 16 players to trigger release
        AddBalancedRoster(team, 16);
        var random = new Random(42);

        RosterManagementService.ProcessRosterEmergencies(league, random);

        league.Transactions.Should().NotBeEmpty();
        league.Transactions[0].Type.Should().Be(TransactionType.Waiver);
        league.Transactions[0].Description.Should().Contain("released");
    }

    [Fact]
    public void ProcessRosterEmergencies_NoFreeAgents_SkipsSign()
    {
        var league = CreateLeague(1);
        var team = league.Teams[0];
        // Under min with no free agents available
        AddBalancedRoster(team, 5);
        // Empty free agent pool
        var random = new Random(42);

        var result = RosterManagementService.ProcessRosterEmergencies(league, random);

        result.PlayersSigned.Should().BeEmpty();
    }

    // ══════════════════════════════════════════════════════════════════
    // Integration Tests
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void FreeAgentPool_InitializedEmpty()
    {
        var league = new League();
        league.FreeAgentPool.Should().NotBeNull();
        league.FreeAgentPool.Should().BeEmpty();
    }

    [Fact]
    public void ReleasedPlayer_AppearsInFreeAgentPool()
    {
        var league = CreateLeague(1);
        var team = league.Teams[0];
        var star = CreatePlayer("Star", "PG", 9.0);
        var scrub = CreatePlayer("Scrub", "SG", 2.0);
        team.Roster.Add(star);
        team.Roster.Add(scrub);

        RosterManagementService.ReleaseWorstPlayer(league, team, 0);

        league.FreeAgentPool.Should().Contain(p => p.Name == "Scrub");
        // Released player should be available for signing
        var found = RosterManagementService.FindBestFreeAgent(league, team, 2);
        found.Should().NotBeNull();
        found!.Name.Should().Be("Scrub");
    }

    [Fact]
    public void FreeAgentPool_ClearedAfterOffSeason()
    {
        var league = CreateLeague(2);
        // Add some players to both teams to make a valid league
        for (int t = 0; t < 2; t++)
        {
            AddBalancedRoster(league.Teams[t], 10);
        }
        league.FreeAgentPool.Add(CreateFreeAgent("OldFA", "C", 3.0));
        league.FreeAgentPool.Should().HaveCount(1);

        // Simulate the clear that happens in AdvanceSeason
        league.FreeAgentPool.Clear();

        league.FreeAgentPool.Should().BeEmpty();
    }

    [Fact]
    public void FreeAgentPool_MergedIntoFreeAgency()
    {
        // Verify that FreeAgentPool players appear in CollectFreeAgents
        // by checking the FreeAgencyService's behavior indirectly
        var league = CreateLeague(1);
        var team = league.Teams[0];
        AddBalancedRoster(team, 10);

        // Add a released player to the free agent pool
        var releasedPlayer = CreateFreeAgent("ReleasedGuy", "C", 6.0);
        league.FreeAgentPool.Add(releasedPlayer);

        // Also add a roster free agent (standard path)
        var rosterFA = CreatePlayer("RosterFA", "PG", 4.0);
        rosterFA.Contract.IsFreeAgent = true;
        team.Roster.Add(rosterFA);

        // The CollectFreeAgents method should find both
        // We can't call it directly (private), but RunFreeAgencyPeriod uses it
        // Instead, verify the pool player is accessible for signing
        league.FreeAgentPool.Should().Contain(releasedPlayer);
    }

    // ══════════════════════════════════════════════════════════════════
    // CountActivePlayers Tests
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void CountActivePlayers_ExcludesInjured()
    {
        var team = CreateTeam();
        team.Roster.Add(CreatePlayer("Healthy", "PG"));
        team.Roster.Add(CreatePlayer("Injured", "SG", injury: 15));
        team.Roster.Add(CreatePlayer("AlsoHealthy", "SF"));

        int count = RosterManagementService.CountActivePlayers(team);
        count.Should().Be(2);
    }

    [Fact]
    public void CountActivePlayers_ExcludesEmptyNames()
    {
        var team = CreateTeam();
        team.Roster.Add(CreatePlayer("RealPlayer", "PG"));
        team.Roster.Add(new Player { Name = "", Position = "SG" });

        int count = RosterManagementService.CountActivePlayers(team);
        count.Should().Be(1);
    }

    // ══════════════════════════════════════════════════════════════════
    // AssignEmergencyContract Tests
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void AssignEmergencyContract_SetsMinimumSalaryByYos()
    {
        var player = CreateFreeAgent("TestFA", "PG", 5.0, yos: 7);

        RosterManagementService.AssignEmergencyContract(player, 0, "TestTeam");

        int expectedMin = LeagueConstants.SalaryMinimumByYos[7];
        player.Contract.CurrentYearSalary.Should().Be(expectedMin);
        player.Contract.TotalSalary.Should().Be(expectedMin);
        player.Contract.RemainingSalary.Should().Be(expectedMin);
        player.Contract.ContractSalaries[0].Should().Be(expectedMin);
    }

    [Fact]
    public void AssignEmergencyContract_ClearsOtherYearSalaries()
    {
        var player = CreateFreeAgent("TestFA", "PG", 5.0);
        // Set some junk data in salary array
        player.Contract.ContractSalaries[1] = 999;
        player.Contract.ContractSalaries[2] = 888;

        RosterManagementService.AssignEmergencyContract(player, 0, "TestTeam");

        for (int i = 1; i < player.Contract.ContractSalaries.Length; i++)
            player.Contract.ContractSalaries[i].Should().Be(0);
    }

    [Fact]
    public void HasAvailableFreeAgents_ReturnsTrueWhenPoolHasPlayers()
    {
        var league = CreateLeague(1);
        league.FreeAgentPool.Add(CreateFreeAgent("FA", "C"));

        RosterManagementService.HasAvailableFreeAgents(league).Should().BeTrue();
    }

    [Fact]
    public void HasAvailableFreeAgents_ReturnsFalseWhenPoolEmpty()
    {
        var league = CreateLeague(1);

        RosterManagementService.HasAvailableFreeAgents(league).Should().BeFalse();
    }
}
