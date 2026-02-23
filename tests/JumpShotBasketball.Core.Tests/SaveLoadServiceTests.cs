using FluentAssertions;
using JumpShotBasketball.Core.Constants;
using JumpShotBasketball.Core.Enums;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Staff;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class SaveLoadServiceTests : IDisposable
{
    private readonly string _tempDir;

    public SaveLoadServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "jsb_tests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    // ───────────────────────────────────────────────────────────────
    // Round-trip JSON (in-memory)
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_EmptyLeague_PreservesStructure()
    {
        var league = new League();

        string json = SaveLoadService.SerializeToJson(league);
        var loaded = SaveLoadService.DeserializeFromJson(json);

        loaded.Teams.Should().BeEmpty();
        loaded.Transactions.Should().BeEmpty();
        loaded.StaffPool.Should().BeEmpty();
    }

    [Fact]
    public void RoundTrip_LeagueWithTeams_PreservesTeamData()
    {
        var league = CreatePopulatedLeague();

        string json = SaveLoadService.SerializeToJson(league);
        var loaded = SaveLoadService.DeserializeFromJson(json);

        loaded.Teams.Should().HaveCount(2);
        loaded.Teams[0].Name.Should().Be("Warriors");
        loaded.Teams[0].CityName.Should().Be("Golden State");
        loaded.Teams[1].Name.Should().Be("Lakers");
    }

    [Fact]
    public void RoundTrip_PlayerStats_PreservesValues()
    {
        var league = CreatePopulatedLeague();

        string json = SaveLoadService.SerializeToJson(league);
        var loaded = SaveLoadService.DeserializeFromJson(json);

        var player = loaded.Teams[0].Roster[0];
        player.Name.Should().Be("Test Player");
        player.SeasonStats.Games.Should().Be(82);
        player.SeasonStats.FieldGoalsMade.Should().Be(500);
        player.SeasonStats.ThreePointersMade.Should().Be(200);
        player.Ratings.Potential1.Should().Be(7);
    }

    [Fact]
    public void RoundTrip_PlayerContract_PreservesArrays()
    {
        var league = CreatePopulatedLeague();
        league.Teams[0].Roster[0].Contract.ContractSalaries = new int[] { 1000, 1200, 1400, 0, 0, 0, 0 };

        string json = SaveLoadService.SerializeToJson(league);
        var loaded = SaveLoadService.DeserializeFromJson(json);

        loaded.Teams[0].Roster[0].Contract.ContractSalaries.Should().StartWith(new int[] { 1000, 1200, 1400 });
    }

    [Fact]
    public void RoundTrip_TeamRecord_PreservesHeadToHead()
    {
        var league = CreatePopulatedLeague();
        league.Teams[0].Record.VsOpponent[1] = 300; // 3-0
        league.Teams[0].Record.VsOpponentPercentage[1] = 1.0;

        string json = SaveLoadService.SerializeToJson(league);
        var loaded = SaveLoadService.DeserializeFromJson(json);

        loaded.Teams[0].Record.VsOpponent[1].Should().Be(300);
        loaded.Teams[0].Record.VsOpponentPercentage[1].Should().Be(1.0);
    }

    [Fact]
    public void RoundTrip_TeamFinancial_PreservesDecimals()
    {
        var league = CreatePopulatedLeague();
        league.Teams[0].Financial.TicketPrice = 450.50m;
        league.Teams[0].Financial.SeasonTotalRevenue = 1_500_000.75m;

        string json = SaveLoadService.SerializeToJson(league);
        var loaded = SaveLoadService.DeserializeFromJson(json);

        loaded.Teams[0].Financial.TicketPrice.Should().Be(450.50m);
        loaded.Teams[0].Financial.SeasonTotalRevenue.Should().Be(1_500_000.75m);
    }

    [Fact]
    public void RoundTrip_DraftBoard3DArray_PreservesValues()
    {
        var league = CreatePopulatedLeague();
        var board = league.Teams[0].DraftBoard;

        // Set some non-default values
        board.DraftChart[0, 0, 5] = 10;  // Team 5's 1st round pick owned by team 10
        board.DraftChart[2, 1, 15] = 22; // Team 15's 2nd round pick in year 3 owned by 22

        string json = SaveLoadService.SerializeToJson(league);
        var loaded = SaveLoadService.DeserializeFromJson(json);

        var loadedBoard = loaded.Teams[0].DraftBoard;
        loadedBoard.DraftChart[0, 0, 5].Should().Be(10);
        loadedBoard.DraftChart[2, 1, 15].Should().Be(22);

        // Default: team owns its own pick
        loadedBoard.DraftChart[0, 0, 1].Should().Be(1);
    }

    [Fact]
    public void RoundTrip_Schedule_PreservesGames()
    {
        var league = CreatePopulatedLeague();
        league.Schedule.Games.Add(new ScheduledGame
        {
            GameNumber = 1,
            Week = 1,
            HomeTeamIndex = 0,
            VisitorTeamIndex = 1,
            Type = GameType.League,
            Played = true,
            HomeScore = 110,
            VisitorScore = 105
        });

        string json = SaveLoadService.SerializeToJson(league);
        var loaded = SaveLoadService.DeserializeFromJson(json);

        loaded.Schedule.Games.Should().HaveCount(1);
        loaded.Schedule.Games[0].HomeScore.Should().Be(110);
        loaded.Schedule.Games[0].Type.Should().Be(GameType.League);
    }

    [Fact]
    public void RoundTrip_Transactions_PreservesData()
    {
        var league = CreatePopulatedLeague();
        league.Transactions.Add(new Transaction
        {
            Id = 1,
            Type = TransactionType.Trade,
            Description = "Big trade",
            TeamIndex1 = 0,
            TeamIndex2 = 1,
            PlayersInvolved = new List<int> { 1, 2 }
        });

        string json = SaveLoadService.SerializeToJson(league);
        var loaded = SaveLoadService.DeserializeFromJson(json);

        loaded.Transactions.Should().HaveCount(1);
        loaded.Transactions[0].Type.Should().Be(TransactionType.Trade);
        loaded.Transactions[0].PlayersInvolved.Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public void RoundTrip_StaffPool_PreservesStaff()
    {
        var league = CreatePopulatedLeague();
        league.StaffPool.Add(new StaffMember
        {
            Id = 1,
            Name = "Coach Smith",
            CoachJob = 0,
            CoachPot1 = 7,
            Pot1History = new[] { 5, 6, 7, 7, 0, 0 }
        });

        string json = SaveLoadService.SerializeToJson(league);
        var loaded = SaveLoadService.DeserializeFromJson(json);

        loaded.StaffPool.Should().HaveCount(1);
        loaded.StaffPool[0].Name.Should().Be("Coach Smith");
        loaded.StaffPool[0].CoachPot1.Should().Be(7);
    }

    [Fact]
    public void RoundTrip_LeagueSettings_PreservesConfig()
    {
        var league = CreatePopulatedLeague();
        league.Settings.NumberOfTeams = 32;
        league.Settings.CurrentYear = 2026;
        league.Settings.LeagueName = "JSNBA";
        league.Settings.SalaryCap = 4000.0;

        string json = SaveLoadService.SerializeToJson(league);
        var loaded = SaveLoadService.DeserializeFromJson(json);

        loaded.Settings.NumberOfTeams.Should().Be(32);
        loaded.Settings.CurrentYear.Should().Be(2026);
        loaded.Settings.LeagueName.Should().Be("JSNBA");
        loaded.Settings.SalaryCap.Should().Be(4000.0);
    }

    // ───────────────────────────────────────────────────────────────
    // File I/O
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void SaveAndLoad_FileRoundTrip()
    {
        var league = CreatePopulatedLeague();
        string path = Path.Combine(_tempDir, "test" + SaveLoadService.FileExtension);

        SaveLoadService.Save(league, path);
        File.Exists(path).Should().BeTrue();

        var loaded = SaveLoadService.Load(path);
        loaded.Teams.Should().HaveCount(2);
        loaded.Teams[0].Name.Should().Be("Warriors");
    }

    [Fact]
    public async Task SaveAndLoadAsync_FileRoundTrip()
    {
        var league = CreatePopulatedLeague();
        string path = Path.Combine(_tempDir, "test_async" + SaveLoadService.FileExtension);

        await SaveLoadService.SaveAsync(league, path);
        File.Exists(path).Should().BeTrue();

        var loaded = await SaveLoadService.LoadAsync(path);
        loaded.Teams.Should().HaveCount(2);
    }

    // ───────────────────────────────────────────────────────────────
    // Version validation
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void Deserialize_ThrowsOnInvalidVersion()
    {
        string json = """
        {
            "version": 999,
            "savedAt": "2026-01-01T00:00:00Z",
            "data": {}
        }
        """;

        var act = () => SaveLoadService.DeserializeFromJson(json);
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*version*999*");
    }

    [Fact]
    public void Deserialize_ThrowsOnZeroVersion()
    {
        string json = """
        {
            "version": 0,
            "savedAt": "2026-01-01T00:00:00Z",
            "data": {}
        }
        """;

        var act = () => SaveLoadService.DeserializeFromJson(json);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Serialize_IncludesVersion()
    {
        var league = new League();
        string json = SaveLoadService.SerializeToJson(league);
        json.Should().Contain("\"version\"");
        json.Should().Contain("\"savedAt\"");
    }

    // ───────────────────────────────────────────────────────────────
    // Edge cases
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_PlayerWithAllSubObjects()
    {
        var player = new Player
        {
            Id = 42,
            Name = "Full Player",
            LastName = "Test",
            Position = "PG",
            Team = "Warriors",
            Age = 27,
            Height = 75,
            Weight = 195,
            SeasonStats = new PlayerStatLine { Games = 82, Minutes = 2800, FieldGoalsMade = 500, FieldGoalsAttempted = 1100 },
            SimulatedStats = new PlayerStatLine { Games = 82, Minutes = 2700 },
            PlayoffStats = new PlayerStatLine { Games = 20, Minutes = 700 },
            Ratings = new PlayerRatings
            {
                TrueRating = 15.5,
                Outside = 7.2,
                MovementOffenseRaw = 7,
                Potential1 = 8,
                Potential2 = 6
            },
            Contract = new PlayerContract { CurrentYearSalary = 3500, ContractYears = 4 },
            ThreePointScores = new[] { 18, 22, 20, 19 },
            DunkScores = new[] { 45, 48, 50, 47 },
            SeasonHighs = new PlayerSeasonHighs { SeasonPoints = 55, CareerPoints = 60 }
        };

        var league = new League();
        league.Teams.Add(new Team { Id = 0, Name = "Warriors", Roster = { player } });

        string json = SaveLoadService.SerializeToJson(league);
        var loaded = SaveLoadService.DeserializeFromJson(json);
        var p = loaded.Teams[0].Roster[0];

        p.Name.Should().Be("Full Player");
        p.Ratings.TrueRating.Should().Be(15.5);
        p.ThreePointScores.Should().BeEquivalentTo(new[] { 18, 22, 20, 19 });
        p.SeasonHighs.SeasonPoints.Should().Be(55);
        p.Contract.CurrentYearSalary.Should().Be(3500);
    }

    [Fact]
    public void DraftBoard_DefaultInitialization_SurvivesRoundTrip()
    {
        var board = new DraftBoard();
        var league = new League();
        league.Teams.Add(new Team { Id = 0, DraftBoard = board });

        string json = SaveLoadService.SerializeToJson(league);
        var loaded = SaveLoadService.DeserializeFromJson(json);

        var loadedBoard = loaded.Teams[0].DraftBoard;
        // Verify default: each team owns its own picks
        for (int y = 0; y < LeagueConstants.MaxDraftYears; y++)
            for (int r = 0; r < LeagueConstants.MaxDraftRounds; r++)
                for (int p = 0; p <= LeagueConstants.MaxTeams; p++)
                    loadedBoard.DraftChart[y, r, p].Should().Be(p);
    }

    // ───────────────────────────────────────────────────────────────
    // Helpers
    // ───────────────────────────────────────────────────────────────

    private static League CreatePopulatedLeague()
    {
        var league = new League();

        var player = new Player
        {
            Id = 1,
            Name = "Test Player",
            Position = "SG",
            Age = 25,
            SeasonStats = new PlayerStatLine
            {
                Games = 82,
                Minutes = 2800,
                FieldGoalsMade = 500,
                FieldGoalsAttempted = 1100,
                ThreePointersMade = 200,
                ThreePointersAttempted = 500,
                FreeThrowsMade = 300,
                FreeThrowsAttempted = 350,
                OffensiveRebounds = 100,
                Rebounds = 500,
                Assists = 300,
                Steals = 100,
                Turnovers = 50,
                Blocks = 50,
                PersonalFouls = 150
            },
            Ratings = new PlayerRatings { Potential1 = 7, Potential2 = 6, Effort = 5 }
        };

        league.Teams.Add(new Team
        {
            Id = 0,
            Name = "Warriors",
            CityName = "Golden State",
            Roster = { player },
            Record = new TeamRecord { TeamName = "Warriors", Conference = "West", Division = "Pacific" }
        });

        league.Teams.Add(new Team
        {
            Id = 1,
            Name = "Lakers",
            CityName = "Los Angeles",
            Record = new TeamRecord { TeamName = "Lakers", Conference = "West", Division = "Pacific" }
        });

        return league;
    }
}
