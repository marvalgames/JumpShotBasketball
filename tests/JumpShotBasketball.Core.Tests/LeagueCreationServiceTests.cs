using FluentAssertions;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class LeagueCreationServiceTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────

    private static League CreateDefaultLeague(int seed = 42)
    {
        return LeagueCreationService.CreateNewLeague(random: new Random(seed));
    }

    private static League CreateSmallLeague(int teams = 4, int seed = 42)
    {
        var options = new LeagueCreationOptions { NumberOfTeams = teams };
        return LeagueCreationService.CreateNewLeague(options, new Random(seed));
    }

    // ── CreateNewLeague Integration Tests ───────────────────────────────────

    [Fact]
    public void CreateNewLeague_CreatesCorrectNumberOfTeams()
    {
        var league = CreateDefaultLeague();
        league.Teams.Should().HaveCount(30);
    }

    [Fact]
    public void CreateNewLeague_15PlayersPerTeam()
    {
        var league = CreateDefaultLeague();
        league.Teams.Should().AllSatisfy(t => t.Roster.Should().HaveCount(15));
    }

    [Fact]
    public void CreateNewLeague_AllPlayersHaveNames()
    {
        var league = CreateDefaultLeague();
        var allPlayers = league.Teams.SelectMany(t => t.Roster);
        allPlayers.Should().AllSatisfy(p => p.Name.Should().NotBeNullOrEmpty());
    }

    [Fact]
    public void CreateNewLeague_AllPlayersHavePositions()
    {
        var league = CreateDefaultLeague();
        var validPositions = new[] { "PG", "SG", "SF", "PF", " C" };
        var allPlayers = league.Teams.SelectMany(t => t.Roster);
        allPlayers.Should().AllSatisfy(p => validPositions.Should().Contain(p.Position));
    }

    [Fact]
    public void CreateNewLeague_NoDuplicateNamesAcrossLeague()
    {
        var league = CreateDefaultLeague();
        var allNames = league.Teams.SelectMany(t => t.Roster).Select(p => p.Name).ToList();
        allNames.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void CreateNewLeague_PlayersHaveOdptRatings1to9()
    {
        var league = CreateDefaultLeague();
        var allPlayers = league.Teams.SelectMany(t => t.Roster);
        allPlayers.Should().AllSatisfy(p =>
        {
            // ODPT raw ratings should be computed (integers 1-9 clamped by CalculateAllRatings)
            p.Ratings.MovementOffenseRaw.Should().BeInRange(1, 9);
            p.Ratings.PenetrationOffenseRaw.Should().BeInRange(1, 9);
            p.Ratings.PostOffenseRaw.Should().BeInRange(1, 9);
            p.Ratings.TransitionOffenseRaw.Should().BeInRange(1, 9);
        });
    }

    [Fact]
    public void CreateNewLeague_PlayersHaveTradeTrueRatingGreaterThanZero()
    {
        var league = CreateDefaultLeague();
        var allPlayers = league.Teams.SelectMany(t => t.Roster);
        allPlayers.Should().AllSatisfy(p => p.Ratings.TradeTrueRating.Should().BeGreaterThan(0));
    }

    [Fact]
    public void CreateNewLeague_PlayersHaveContracts()
    {
        var league = CreateDefaultLeague();
        var allPlayers = league.Teams.SelectMany(t => t.Roster);
        allPlayers.Should().AllSatisfy(p => p.Contract.ContractYears.Should().BeGreaterThan(0));
    }

    [Fact]
    public void CreateNewLeague_PlayersHaveHeightAndWeight()
    {
        var league = CreateDefaultLeague();
        var allPlayers = league.Teams.SelectMany(t => t.Roster);
        allPlayers.Should().AllSatisfy(p =>
        {
            p.Height.Should().BeGreaterThan(0);
            p.Weight.Should().BeGreaterThan(0);
        });
    }

    [Fact]
    public void CreateNewLeague_ScheduleGenerated()
    {
        var league = CreateDefaultLeague();
        league.Schedule.Games.Should().NotBeEmpty();
    }

    [Fact]
    public void CreateNewLeague_DraftBoardInitialized()
    {
        var league = CreateDefaultLeague();
        league.DraftBoard.Should().NotBeNull();
    }

    [Fact]
    public void CreateNewLeague_FinancialsInitializedWhenEnabled()
    {
        var league = CreateDefaultLeague();
        league.Teams.Should().AllSatisfy(t =>
        {
            t.Financial.Capacity.Should().BeGreaterThan(0);
        });
    }

    [Fact]
    public void CreateNewLeague_StaffAssigned3PerTeam()
    {
        var league = CreateDefaultLeague();
        league.Teams.Should().AllSatisfy(t =>
        {
            t.Scout.Should().NotBeNull();
            t.Coach.Should().NotBeNull();
            t.GeneralManager.Should().NotBeNull();
        });
        league.StaffPool.Should().HaveCount(90); // 30 teams × 3 staff
    }

    [Fact]
    public void CreateNewLeague_RotationsSet()
    {
        var league = CreateDefaultLeague();
        // After SetComputerRotations, at least some players should have rotation flags set
        var allPlayers = league.Teams.SelectMany(t => t.Roster);
        var withRotation = allPlayers.Where(p =>
            p.PgRotation || p.SgRotation || p.SfRotation || p.PfRotation || p.CRotation);
        withRotation.Should().NotBeEmpty();
    }

    [Fact]
    public void CreateNewLeague_DeterministicWithSeed()
    {
        var league1 = CreateDefaultLeague(seed: 123);
        var league2 = CreateDefaultLeague(seed: 123);

        // Same seed → same team names, player names, ratings
        for (int i = 0; i < league1.Teams.Count; i++)
        {
            league1.Teams[i].Name.Should().Be(league2.Teams[i].Name);
            for (int j = 0; j < league1.Teams[i].Roster.Count; j++)
            {
                league1.Teams[i].Roster[j].Name.Should().Be(league2.Teams[i].Roster[j].Name);
                league1.Teams[i].Roster[j].Age.Should().Be(league2.Teams[i].Roster[j].Age);
            }
        }
    }

    [Fact]
    public void CreateNewLeague_CustomOptionsApplied()
    {
        var options = new LeagueCreationOptions
        {
            NumberOfTeams = 8,
            StartingYear = 2030,
            LeagueName = "Test League"
        };
        var league = LeagueCreationService.CreateNewLeague(options, new Random(42));

        league.Teams.Should().HaveCount(8);
        league.Settings.CurrentYear.Should().Be(2030);
        league.Settings.LeagueName.Should().Be("Test League");
    }

    [Fact]
    public void CreateNewLeague_SettingsFromOptions()
    {
        var options = new LeagueCreationOptions
        {
            FinancialEnabled = false,
            FreeAgencyEnabled = true,
            ComputerTradesEnabled = true
        };
        var league = LeagueCreationService.CreateNewLeague(options, new Random(42));

        league.Settings.FinancialEnabled.Should().BeFalse();
        league.Settings.FreeAgencyEnabled.Should().BeTrue();
        league.Settings.ComputerTradesEnabled.Should().BeTrue();
    }

    // ── GenerateTeams Tests ─────────────────────────────────────────────────

    [Fact]
    public void GenerateTeams_30TeamsWithCorrectNames()
    {
        var league = new League();
        LeagueCreationService.GenerateTeams(league, 30, new Random(42));

        league.Teams.Should().HaveCount(30);
        league.Teams[0].Name.Should().Be("Celtics");
        league.Teams[0].CityName.Should().Be("Boston");
        league.Teams[29].Name.Should().Be("Kings");
        league.Teams[29].CityName.Should().Be("Sacramento");
    }

    [Fact]
    public void GenerateTeams_ConferencesAndDivisionsAssigned()
    {
        var league = new League();
        LeagueCreationService.GenerateTeams(league, 30, new Random(42));

        var eastern = league.Teams.Where(t => t.Record.Conference == "Eastern").ToList();
        var western = league.Teams.Where(t => t.Record.Conference == "Western").ToList();

        eastern.Should().HaveCount(15);
        western.Should().HaveCount(15);

        // Each team should have a division
        league.Teams.Should().AllSatisfy(t => t.Record.Division.Should().NotBeNullOrEmpty());
    }

    [Fact]
    public void GenerateTeams_CustomTeamCount()
    {
        var league = new League();
        LeagueCreationService.GenerateTeams(league, 8, new Random(42));

        league.Teams.Should().HaveCount(8);
    }

    [Fact]
    public void GenerateTeams_TeamRecordInitialized()
    {
        var league = new League();
        LeagueCreationService.GenerateTeams(league, 30, new Random(42));

        league.Teams.Should().AllSatisfy(t =>
        {
            t.Record.TeamName.Should().NotBeNullOrEmpty();
            t.Record.Control.Should().Be("Computer");
        });
    }

    // ── GeneratePlayerName Tests ────────────────────────────────────────────

    [Fact]
    public void GeneratePlayerName_UniqueNames()
    {
        var random = new Random(42);
        var usedNames = new HashSet<string>();
        var names = new List<string>();

        for (int i = 0; i < 100; i++)
            names.Add(LeagueCreationService.GeneratePlayerName(random, usedNames));

        names.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void GeneratePlayerName_HandlesCollision()
    {
        var random = new Random(42);
        var usedNames = new HashSet<string>();

        // Pre-populate with a name we know will be generated
        string first = LeagueCreationService.GeneratePlayerName(random, usedNames);
        first.Should().NotBeNullOrEmpty();

        // Generate many more — should all be unique
        for (int i = 0; i < 200; i++)
            LeagueCreationService.GeneratePlayerName(random, usedNames);

        usedNames.Should().HaveCount(201);
    }

    // ── GenerateStatLine Tests ──────────────────────────────────────────────

    [Fact]
    public void GenerateStatLine_PG_HighAssists()
    {
        var random = new Random(42);
        var stats = LeagueCreationService.GenerateStatLine("PG", 34, random);

        // PGs should have relatively high assists
        stats.AssistsPerGame.Should().BeGreaterThan(3.0);
        stats.Games.Should().Be(82);
    }

    [Fact]
    public void GenerateStatLine_SG_ReasonableStats()
    {
        var random = new Random(42);
        var stats = LeagueCreationService.GenerateStatLine("SG", 30, random);

        stats.FieldGoalsAttempted.Should().BeGreaterThan(0);
        stats.Games.Should().Be(82);
    }

    [Fact]
    public void GenerateStatLine_SF_ReasonableStats()
    {
        var random = new Random(42);
        var stats = LeagueCreationService.GenerateStatLine("SF", 28, random);

        stats.Rebounds.Should().BeGreaterThan(0);
        stats.Games.Should().Be(82);
    }

    [Fact]
    public void GenerateStatLine_PF_HighRebounds()
    {
        var random = new Random(42);
        var stats = LeagueCreationService.GenerateStatLine("PF", 30, random);

        // PFs should have solid rebounds
        stats.ReboundsPerGame.Should().BeGreaterThan(4.0);
        stats.Games.Should().Be(82);
    }

    [Fact]
    public void GenerateStatLine_C_HighBlocksAndRebounds()
    {
        var random = new Random(42);
        var stats = LeagueCreationService.GenerateStatLine(" C", 32, random);

        stats.ReboundsPerGame.Should().BeGreaterThan(5.0);
        stats.Games.Should().Be(82);
    }

    [Fact]
    public void GenerateStatLine_MinutesScaling()
    {
        var random1 = new Random(42);
        var random2 = new Random(42);

        var highMinStats = LeagueCreationService.GenerateStatLine("PG", 36, random1);
        var lowMinStats = LeagueCreationService.GenerateStatLine("PG", 18, random2);

        // Higher minutes should generally produce more total stats
        // Use same seed, so the only difference is the minutes scaling
        highMinStats.Minutes.Should().BeGreaterThan(lowMinStats.Minutes);
    }

    [Fact]
    public void GenerateStatLine_RandomVariance()
    {
        var stats1 = LeagueCreationService.GenerateStatLine("PG", 30, new Random(1));
        var stats2 = LeagueCreationService.GenerateStatLine("PG", 30, new Random(2));

        // Different seeds should produce different stats
        (stats1.FieldGoalsMade == stats2.FieldGoalsMade &&
         stats1.Assists == stats2.Assists &&
         stats1.Rebounds == stats2.Rebounds).Should().BeFalse();
    }

    [Fact]
    public void GenerateStatLine_GamesSetTo82()
    {
        var stats = LeagueCreationService.GenerateStatLine("SG", 25, new Random(42));
        stats.Games.Should().Be(82);
    }

    // ── GeneratePlayer Tests ────────────────────────────────────────────────

    [Fact]
    public void GeneratePlayer_StarTierAgeRange()
    {
        var usedNames = new HashSet<string>();
        // Generate multiple to check range
        for (int i = 0; i < 50; i++)
        {
            var player = LeagueCreationService.GeneratePlayer("PG", 0, new Random(i), usedNames);
            player.Age.Should().BeInRange(25, 30);
        }
    }

    [Fact]
    public void GeneratePlayer_StarterTierAgeRange()
    {
        var usedNames = new HashSet<string>();
        for (int i = 0; i < 50; i++)
        {
            var player = LeagueCreationService.GeneratePlayer("SG", 1, new Random(i), usedNames);
            player.Age.Should().BeInRange(24, 29);
        }
    }

    [Fact]
    public void GeneratePlayer_RotationTierAgeRange()
    {
        var usedNames = new HashSet<string>();
        for (int i = 0; i < 50; i++)
        {
            var player = LeagueCreationService.GeneratePlayer("SF", 2, new Random(i), usedNames);
            player.Age.Should().BeInRange(22, 28);
        }
    }

    [Fact]
    public void GeneratePlayer_BenchTierAgeRange()
    {
        var usedNames = new HashSet<string>();
        for (int i = 0; i < 50; i++)
        {
            var player = LeagueCreationService.GeneratePlayer("PF", 3, new Random(i), usedNames);
            player.Age.Should().BeInRange(20, 26);
        }
    }

    [Fact]
    public void GeneratePlayer_PotentialsSet1to9()
    {
        var usedNames = new HashSet<string>();
        for (int i = 0; i < 50; i++)
        {
            var player = LeagueCreationService.GeneratePlayer("PG", 0, new Random(i), usedNames);
            player.Ratings.Potential1.Should().BeInRange(1, 9);
            player.Ratings.Potential2.Should().BeInRange(1, 9);
        }
    }

    [Fact]
    public void GeneratePlayer_EffortSet3to7()
    {
        var usedNames = new HashSet<string>();
        for (int i = 0; i < 50; i++)
        {
            var player = LeagueCreationService.GeneratePlayer(" C", 1, new Random(i), usedNames);
            player.Ratings.Effort.Should().BeInRange(3, 7);
        }
    }

    // ── PopulateRosters Tests ───────────────────────────────────────────────

    [Fact]
    public void PopulateRosters_PositionDistribution()
    {
        var league = new League();
        LeagueCreationService.GenerateTeams(league, 4, new Random(42));
        LeagueCreationService.PopulateRosters(league, 15, new Random(42));

        foreach (var team in league.Teams)
        {
            var positions = team.Roster.Select(p => p.Position.Trim()).ToList();
            positions.Count(p => p == "PG").Should().Be(3);
            positions.Count(p => p == "SG").Should().Be(3);
            positions.Count(p => p == "SF").Should().Be(3);
            positions.Count(p => p == "PF").Should().Be(3);
            positions.Count(p => p == "C").Should().Be(3);
        }
    }

    [Fact]
    public void PopulateRosters_MinutesDistribution()
    {
        var league = new League();
        LeagueCreationService.GenerateTeams(league, 2, new Random(42));
        LeagueCreationService.PopulateRosters(league, 15, new Random(42));

        foreach (var team in league.Teams)
        {
            // Star tier (first 5): highest minutes
            var stars = team.Roster.Take(5).ToList();
            // Rotation tier (last 5): lowest minutes
            var rotation = team.Roster.Skip(10).Take(5).ToList();

            // Stars should generally have more minutes than rotation players
            double avgStarMinutes = stars.Average(p => p.SeasonStats.MinutesPerGame);
            double avgRotMinutes = rotation.Average(p => p.SeasonStats.MinutesPerGame);
            avgStarMinutes.Should().BeGreaterThan(avgRotMinutes);
        }
    }

    [Fact]
    public void PopulateRosters_AllTiersRepresented()
    {
        var league = new League();
        LeagueCreationService.GenerateTeams(league, 2, new Random(42));
        LeagueCreationService.PopulateRosters(league, 15, new Random(42));

        foreach (var team in league.Teams)
        {
            // 3 tiers × 5 positions = 15 players
            team.Roster.Should().HaveCount(15);

            // Check we have a mix of minutes ranges
            var minutesPerGame = team.Roster.Select(p => p.SeasonStats.MinutesPerGame).ToList();
            minutesPerGame.Max().Should().BeGreaterThan(30);
            minutesPerGame.Min().Should().BeLessThan(27);
        }
    }

    // ── InitializeAllPlayerRatings Tests ─────────────────────────────────────

    [Fact]
    public void InitializeAllPlayerRatings_OdptComputed()
    {
        var league = new League();
        LeagueCreationService.GenerateTeams(league, 2, new Random(42));
        LeagueCreationService.PopulateRosters(league, 15, new Random(42));
        LeagueCreationService.InitializeAllPlayerRatings(league);

        var allPlayers = league.Teams.SelectMany(t => t.Roster);
        allPlayers.Should().AllSatisfy(p =>
        {
            // ODPT double ratings should be computed (non-zero)
            (p.Ratings.Outside + p.Ratings.Driving + p.Ratings.Post + p.Ratings.Transition)
                .Should().BeGreaterThan(0);
        });
    }

    [Fact]
    public void InitializeAllPlayerRatings_SeasonStatsClearedAfter()
    {
        var league = new League();
        LeagueCreationService.GenerateTeams(league, 2, new Random(42));
        LeagueCreationService.PopulateRosters(league, 15, new Random(42));
        LeagueCreationService.InitializeAllPlayerRatings(league);

        var allPlayers = league.Teams.SelectMany(t => t.Roster);
        allPlayers.Should().AllSatisfy(p =>
        {
            p.SeasonStats.Games.Should().Be(0);
            p.SeasonStats.Minutes.Should().Be(0);
        });
    }

    [Fact]
    public void InitializeAllPlayerRatings_TrueRatingComputed()
    {
        var league = new League();
        LeagueCreationService.GenerateTeams(league, 2, new Random(42));
        LeagueCreationService.PopulateRosters(league, 15, new Random(42));
        LeagueCreationService.InitializeAllPlayerRatings(league);

        var allPlayers = league.Teams.SelectMany(t => t.Roster);
        allPlayers.Should().AllSatisfy(p =>
        {
            p.Ratings.TrueRatingSimple.Should().BeGreaterThan(0);
        });
    }

    // ── InitializeFinancials Tests ──────────────────────────────────────────

    [Fact]
    public void InitializeFinancials_ArenaGenerated()
    {
        var league = new League();
        LeagueCreationService.GenerateTeams(league, 4, new Random(42));
        LeagueCreationService.InitializeFinancials(league, new Random(42));

        league.Teams.Should().AllSatisfy(t =>
        {
            t.Financial.Capacity.Should().BeGreaterThan(0);
        });
    }

    [Fact]
    public void InitializeFinancials_CityGenerated()
    {
        var league = new League();
        LeagueCreationService.GenerateTeams(league, 4, new Random(42));
        LeagueCreationService.InitializeFinancials(league, new Random(42));

        league.Teams.Should().AllSatisfy(t =>
        {
            // City metrics should be set (GenerateRandomCity sets them)
            t.Financial.FanSupport.Should().BeInRange(1, 7);
        });
    }

    // ── InitializeStaff Tests ───────────────────────────────────────────────

    [Fact]
    public void InitializeStaff_3StaffPerTeam()
    {
        var league = new League();
        LeagueCreationService.GenerateTeams(league, 4, new Random(42));
        LeagueCreationService.InitializeStaff(league, new Random(42));

        league.StaffPool.Should().HaveCount(12); // 4 teams × 3 staff
    }

    [Fact]
    public void InitializeStaff_CorrectRoles()
    {
        var league = new League();
        LeagueCreationService.GenerateTeams(league, 4, new Random(42));
        LeagueCreationService.InitializeStaff(league, new Random(42));

        foreach (var team in league.Teams)
        {
            team.Scout.Should().NotBeNull();
            team.Scout!.CurrentScout.Should().Be(team.Id + 1);

            team.Coach.Should().NotBeNull();
            team.Coach!.CurrentCoach.Should().Be(team.Id + 1);

            team.GeneralManager.Should().NotBeNull();
            team.GeneralManager!.CurrentGM.Should().Be(team.Id + 1);
        }
    }

    // ── InitializeDraftBoard Tests ──────────────────────────────────────────

    [Fact]
    public void InitializeDraftBoard_ChartCreated()
    {
        var league = new League { Settings = { NumberOfTeams = 30 } };
        LeagueCreationService.InitializeDraftBoard(league);

        league.DraftBoard.Should().NotBeNull();
        // Each team owns its own pick by default
        league.DraftBoard!.DraftChart[0, 0, 0].Should().Be(0);
        league.DraftBoard.DraftChart[0, 0, 5].Should().Be(5);
    }

    // ── Edge Case Tests ────────────────────────────────────────────────────

    [Fact]
    public void CreateNewLeague_FinancialsDisabledSkipsGeneration()
    {
        var options = new LeagueCreationOptions
        {
            NumberOfTeams = 4,
            FinancialEnabled = false
        };
        var league = LeagueCreationService.CreateNewLeague(options, new Random(42));

        league.Settings.FinancialEnabled.Should().BeFalse();
        // Teams still exist but financials not initialized via GenerateRandom*
        league.Teams.Should().HaveCount(4);
    }

    [Fact]
    public void CreateNewLeague_SmallLeague4TeamsWorks()
    {
        var league = CreateSmallLeague(teams: 4, seed: 42);

        league.Teams.Should().HaveCount(4);
        league.Teams.Should().AllSatisfy(t => t.Roster.Should().HaveCount(15));
        league.Schedule.Games.Should().NotBeEmpty();
        league.DraftBoard.Should().NotBeNull();
    }
}
