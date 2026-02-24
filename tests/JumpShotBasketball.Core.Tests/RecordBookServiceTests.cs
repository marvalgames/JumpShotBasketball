using FluentAssertions;
using JumpShotBasketball.Core.Enums;
using JumpShotBasketball.Core.Models.History;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class RecordBookServiceTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static League CreateTestLeague(int teamCount = 2)
    {
        var league = new League
        {
            Settings = new LeagueSettings
            {
                CurrentYear = 2024,
                NumberOfTeams = teamCount,
                LeagueName = "TestLeague"
            }
        };

        for (int i = 0; i < teamCount; i++)
        {
            var team = new Team
            {
                Id = i,
                Name = $"Team{i}",
                Record = new TeamRecord { TeamName = $"Team{i}" }
            };
            league.Teams.Add(team);
        }

        return league;
    }

    private static Player CreateTestPlayer(string name, int id, int teamIndex, string teamName)
    {
        return new Player
        {
            Name = name,
            Id = id,
            TeamIndex = teamIndex,
            Team = teamName,
            Position = "PG",
            Ratings = new PlayerRatings { TradeTrueRating = 5.0 }
        };
    }

    private static PlayerGameState CreateGameState(string name, int playerId, int points, int rebounds = 5,
        int assists = 5, int steals = 2, int blocks = 1, int fgm = 10, int threes = 3, int ftm = 5)
    {
        return new PlayerGameState
        {
            GameName = name,
            PlayerPointer = playerId,
            Minutes = 2400, // 40 minutes in seconds
            FieldGoalsMade = fgm,
            FieldGoalsAttempted = fgm + 5,
            FreeThrowsMade = ftm,
            FreeThrowsAttempted = ftm + 2,
            ThreePointersMade = threes,
            ThreePointersAttempted = threes + 2,
            OffensiveRebounds = rebounds / 3,
            DefensiveRebounds = rebounds - rebounds / 3,
            Assists = assists,
            Steals = steals,
            Turnovers = 3,
            Blocks = blocks,
            PersonalFouls = 2
        };
    }

    // ── EnsureInitialized Tests ─────────────────────────────────────────────

    [Fact]
    public void EnsureInitialized_CreatesLeagueSingleGameScope()
    {
        var league = CreateTestLeague();
        RecordBookService.EnsureInitialized(league);

        league.RecordBook.SingleGameRecords.Should().ContainKey("League");
        league.RecordBook.SingleGameRecords["League"].Should().HaveCount(RecordBookService.SingleGameStats.Length);
    }

    [Fact]
    public void EnsureInitialized_CreatesPlayoffSingleGameScope()
    {
        var league = CreateTestLeague();
        RecordBookService.EnsureInitialized(league);

        league.RecordBook.SingleGameRecords.Should().ContainKey("Playoff");
    }

    [Fact]
    public void EnsureInitialized_CreatesTeamScopesForEachTeam()
    {
        var league = CreateTestLeague(3);
        RecordBookService.EnsureInitialized(league);

        league.RecordBook.SingleGameRecords.Should().ContainKey("Team_0");
        league.RecordBook.SingleGameRecords.Should().ContainKey("Team_1");
        league.RecordBook.SingleGameRecords.Should().ContainKey("Team_2");
        league.RecordBook.SingleGameRecords.Should().ContainKey("TeamPlayoff_0");
        league.RecordBook.SeasonRecords.Should().ContainKey("Team_0");
        league.RecordBook.CareerRecords.Should().HaveCount(RecordBookService.SeasonStats.Length);
    }

    // ── UpdateSingleGameRecords Tests ───────────────────────────────────────

    [Fact]
    public void UpdateSingleGameRecords_InsertsHighScoringGame()
    {
        var league = CreateTestLeague();
        RecordBookService.EnsureInitialized(league);
        var rb = league.RecordBook;

        // 50-point game: fgm=20, 3pm=5, ftm=5 → 20*2+5+5=50
        var boxScore = new List<PlayerGameState>
        {
            CreateGameState("Star Player", 1, 50, fgm: 20, threes: 5, ftm: 5)
        };

        RecordBookService.UpdateSingleGameRecords(rb, boxScore, league.Teams[0], 0, GameType.League, 2024);

        rb.SingleGameRecords["League"]["Points"].Entries.Should().HaveCount(1);
        rb.SingleGameRecords["League"]["Points"].Entries[0].Value.Should().Be(50);
        rb.SingleGameRecords["League"]["Points"].Entries[0].PlayerName.Should().Be("Star Player");
    }

    [Fact]
    public void UpdateSingleGameRecords_InsertsIntoTeamScope()
    {
        var league = CreateTestLeague();
        RecordBookService.EnsureInitialized(league);
        var rb = league.RecordBook;

        var boxScore = new List<PlayerGameState>
        {
            CreateGameState("Player A", 1, 30, fgm: 12, threes: 3, ftm: 3)
        };

        RecordBookService.UpdateSingleGameRecords(rb, boxScore, league.Teams[0], 0, GameType.League, 2024);

        rb.SingleGameRecords["Team_0"]["Points"].Entries.Should().HaveCount(1);
    }

    [Fact]
    public void UpdateSingleGameRecords_PlayoffGoesToPlayoffScope()
    {
        var league = CreateTestLeague();
        RecordBookService.EnsureInitialized(league);
        var rb = league.RecordBook;

        var boxScore = new List<PlayerGameState>
        {
            CreateGameState("Playoff Star", 2, 40, fgm: 16, threes: 4, ftm: 4)
        };

        RecordBookService.UpdateSingleGameRecords(rb, boxScore, league.Teams[0], 0, GameType.Playoff, 2024);

        rb.SingleGameRecords["Playoff"]["Points"].Entries.Should().HaveCount(1);
        rb.SingleGameRecords["TeamPlayoff_0"]["Points"].Entries.Should().HaveCount(1);
        // Regular season scope should be empty
        rb.SingleGameRecords["League"]["Points"].Entries.Should().BeEmpty();
    }

    [Fact]
    public void UpdateSingleGameRecords_SortsDescending()
    {
        var league = CreateTestLeague();
        RecordBookService.EnsureInitialized(league);
        var rb = league.RecordBook;

        var box1 = new List<PlayerGameState> { CreateGameState("Low Scorer", 1, 20, fgm: 8, threes: 2, ftm: 2) };
        var box2 = new List<PlayerGameState> { CreateGameState("High Scorer", 2, 40, fgm: 16, threes: 4, ftm: 4) };

        RecordBookService.UpdateSingleGameRecords(rb, box1, league.Teams[0], 0, GameType.League, 2024);
        RecordBookService.UpdateSingleGameRecords(rb, box2, league.Teams[0], 0, GameType.League, 2024);

        var entries = rb.SingleGameRecords["League"]["Points"].Entries;
        entries.Should().HaveCount(2);
        entries[0].PlayerName.Should().Be("High Scorer");
        entries[1].PlayerName.Should().Be("Low Scorer");
    }

    [Fact]
    public void UpdateSingleGameRecords_EnforcesMaxEntries()
    {
        var league = CreateTestLeague();
        RecordBookService.EnsureInitialized(league);
        var rb = league.RecordBook;

        // Insert 12 games, max is 10
        for (int i = 0; i < 12; i++)
        {
            var box = new List<PlayerGameState>
            {
                CreateGameState($"Player{i}", i, 20 + i, fgm: 8 + i / 2, threes: 2, ftm: 2)
            };
            RecordBookService.UpdateSingleGameRecords(rb, box, league.Teams[0], 0, GameType.League, 2024);
        }

        rb.SingleGameRecords["League"]["Points"].Entries.Should().HaveCount(RecordBookService.MaxSingleGameEntries);
        // Lowest 2 should have been trimmed
        rb.SingleGameRecords["League"]["Points"].Entries.Last().Value.Should().BeGreaterThanOrEqualTo(22);
    }

    [Fact]
    public void UpdateSingleGameRecords_SkipsZeroMinutePlayers()
    {
        var league = CreateTestLeague();
        RecordBookService.EnsureInitialized(league);
        var rb = league.RecordBook;

        var gs = CreateGameState("Benched Player", 1, 0);
        gs.Minutes = 0;
        var boxScore = new List<PlayerGameState> { gs };

        RecordBookService.UpdateSingleGameRecords(rb, boxScore, league.Teams[0], 0, GameType.League, 2024);

        rb.SingleGameRecords["League"]["Points"].Entries.Should().BeEmpty();
    }

    [Fact]
    public void UpdateSingleGameRecords_TracksAllStatCategories()
    {
        var league = CreateTestLeague();
        RecordBookService.EnsureInitialized(league);
        var rb = league.RecordBook;

        var boxScore = new List<PlayerGameState>
        {
            CreateGameState("AllAround", 1, 30, rebounds: 10, assists: 8, steals: 3, blocks: 2, fgm: 12, threes: 3, ftm: 3)
        };

        RecordBookService.UpdateSingleGameRecords(rb, boxScore, league.Teams[0], 0, GameType.League, 2024);

        var leagueRecords = rb.SingleGameRecords["League"];
        leagueRecords["Points"].Entries.Should().HaveCount(1);
        leagueRecords["Rebounds"].Entries.Should().HaveCount(1);
        leagueRecords["Assists"].Entries.Should().HaveCount(1);
        leagueRecords["Steals"].Entries.Should().HaveCount(1);
        leagueRecords["Blocks"].Entries.Should().HaveCount(1);
        leagueRecords["FieldGoalsMade"].Entries.Should().HaveCount(1);
        leagueRecords["ThreePointersMade"].Entries.Should().HaveCount(1);
        leagueRecords["FreeThrowsMade"].Entries.Should().HaveCount(1);
    }

    [Fact]
    public void UpdateSingleGameRecords_MultiplePlayersInSameGame()
    {
        var league = CreateTestLeague();
        RecordBookService.EnsureInitialized(league);
        var rb = league.RecordBook;

        var boxScore = new List<PlayerGameState>
        {
            CreateGameState("Player1", 1, 30, fgm: 12, threes: 3, ftm: 3),
            CreateGameState("Player2", 2, 25, fgm: 10, threes: 2, ftm: 3)
        };

        RecordBookService.UpdateSingleGameRecords(rb, boxScore, league.Teams[0], 0, GameType.League, 2024);

        rb.SingleGameRecords["League"]["Points"].Entries.Should().HaveCount(2);
    }

    [Fact]
    public void UpdateSingleGameRecords_RecordsCorrectYear()
    {
        var league = CreateTestLeague();
        RecordBookService.EnsureInitialized(league);
        var rb = league.RecordBook;

        var boxScore = new List<PlayerGameState>
        {
            CreateGameState("Star", 1, 50, fgm: 20, threes: 5, ftm: 5)
        };

        RecordBookService.UpdateSingleGameRecords(rb, boxScore, league.Teams[0], 0, GameType.League, 2025);

        rb.SingleGameRecords["League"]["Points"].Entries[0].Year.Should().Be(2025);
    }

    // ── UpdateSeasonRecords Tests ───────────────────────────────────────────

    [Fact]
    public void UpdateSeasonRecords_InsertsQualifyingPlayerPpg()
    {
        var league = CreateTestLeague(1);
        RecordBookService.EnsureInitialized(league);

        var player = CreateTestPlayer("Scorer", 1, 0, "Team0");
        player.SimulatedStats = new PlayerStatLine
        {
            Games = 82,
            Minutes = 2800,
            FieldGoalsMade = 600,
            FieldGoalsAttempted = 1200,
            FreeThrowsMade = 400,
            FreeThrowsAttempted = 500,
            ThreePointersMade = 100,
            ThreePointersAttempted = 300,
            Rebounds = 300,
            Assists = 200,
            Steals = 80,
            Blocks = 20
        };
        // Points = 600*2 + 400 + 100 = 1700 (> 1400 threshold)
        league.Teams[0].Roster.Add(player);

        RecordBookService.UpdateSeasonRecords(league.RecordBook, league, 2024);

        league.RecordBook.SeasonRecords["League"]["PointsPerGame"].Entries.Should().HaveCount(1);
        league.RecordBook.SeasonRecords["League"]["PointsPerGame"].Entries[0].Value
            .Should().BeApproximately(1700.0 / 82, 0.01);
    }

    [Fact]
    public void UpdateSeasonRecords_RejectsBelowVolumeThreshold()
    {
        var league = CreateTestLeague(1);
        RecordBookService.EnsureInitialized(league);

        var player = CreateTestPlayer("LowVolume", 1, 0, "Team0");
        player.SimulatedStats = new PlayerStatLine
        {
            Games = 40,
            Minutes = 1000,
            FieldGoalsMade = 200,
            FieldGoalsAttempted = 500,
            FreeThrowsMade = 100,
            FreeThrowsAttempted = 120,
            ThreePointersMade = 30,
            ThreePointersAttempted = 80,
            Rebounds = 100,
            Assists = 50,
            Steals = 20,
            Blocks = 5
        };
        // Points = 200*2 + 100 + 30 = 530 (< 1400 threshold)
        league.Teams[0].Roster.Add(player);

        RecordBookService.UpdateSeasonRecords(league.RecordBook, league, 2024);

        league.RecordBook.SeasonRecords["League"]["PointsPerGame"].Entries.Should().BeEmpty();
    }

    [Fact]
    public void UpdateSeasonRecords_FgPctRequiresMinimumFga()
    {
        var league = CreateTestLeague(1);
        RecordBookService.EnsureInitialized(league);

        var player = CreateTestPlayer("SharpShooter", 1, 0, "Team0");
        player.SimulatedStats = new PlayerStatLine
        {
            Games = 82,
            FieldGoalsMade = 250,
            FieldGoalsAttempted = 299, // Below 300 FGA minimum
            Rebounds = 100,
            Steals = 20,
            Blocks = 5
        };
        league.Teams[0].Roster.Add(player);

        RecordBookService.UpdateSeasonRecords(league.RecordBook, league, 2024);

        league.RecordBook.SeasonRecords["League"]["FieldGoalPct"].Entries.Should().BeEmpty();
    }

    [Fact]
    public void UpdateSeasonRecords_InsertsIntoTeamScope()
    {
        var league = CreateTestLeague(1);
        RecordBookService.EnsureInitialized(league);

        var player = CreateTestPlayer("Rebounder", 1, 0, "Team0");
        player.SimulatedStats = new PlayerStatLine
        {
            Games = 82,
            Minutes = 2800,
            Rebounds = 900, // > 800 threshold
            FieldGoalsMade = 300,
            FieldGoalsAttempted = 600,
            Assists = 100
        };
        league.Teams[0].Roster.Add(player);

        RecordBookService.UpdateSeasonRecords(league.RecordBook, league, 2024);

        league.RecordBook.SeasonRecords["Team_0"]["ReboundsPerGame"].Entries.Should().HaveCount(1);
    }

    [Fact]
    public void UpdateSeasonRecords_SkipsEmptyNamePlayers()
    {
        var league = CreateTestLeague(1);
        RecordBookService.EnsureInitialized(league);

        var player = new Player { Name = "", SimulatedStats = new PlayerStatLine { Games = 82 } };
        league.Teams[0].Roster.Add(player);

        RecordBookService.UpdateSeasonRecords(league.RecordBook, league, 2024);

        league.RecordBook.SeasonRecords["League"]["PointsPerGame"].Entries.Should().BeEmpty();
    }

    [Fact]
    public void UpdateSeasonRecords_ThreePointPctRequiresMinAttempts()
    {
        var league = CreateTestLeague(1);
        RecordBookService.EnsureInitialized(league);

        var player = CreateTestPlayer("Shooter", 1, 0, "Team0");
        player.SimulatedStats = new PlayerStatLine
        {
            Games = 82,
            ThreePointersMade = 60,
            ThreePointersAttempted = 130, // >= 125 threshold
            FieldGoalsMade = 200,
            FieldGoalsAttempted = 500
        };
        league.Teams[0].Roster.Add(player);

        RecordBookService.UpdateSeasonRecords(league.RecordBook, league, 2024);

        league.RecordBook.SeasonRecords["League"]["ThreePointPct"].Entries.Should().HaveCount(1);
    }

    [Fact]
    public void UpdateSeasonRecords_EnforcesMaxSeasonEntries()
    {
        var league = CreateTestLeague(1);
        RecordBookService.EnsureInitialized(league);

        // Add 55 qualifying players
        for (int i = 0; i < 55; i++)
        {
            var player = CreateTestPlayer($"Player{i}", i, 0, "Team0");
            player.SimulatedStats = new PlayerStatLine
            {
                Games = 82,
                FieldGoalsMade = 600 + i,
                FieldGoalsAttempted = 1200,
                FreeThrowsMade = 400,
                FreeThrowsAttempted = 500,
                ThreePointersMade = 100,
                ThreePointersAttempted = 300
            };
            league.Teams[0].Roster.Add(player);
        }

        RecordBookService.UpdateSeasonRecords(league.RecordBook, league, 2024);

        league.RecordBook.SeasonRecords["League"]["PointsPerGame"].Entries
            .Should().HaveCount(RecordBookService.MaxSeasonEntries);
    }

    // ── UpdateCareerRecords Tests ───────────────────────────────────────────

    [Fact]
    public void UpdateCareerRecords_InsertsQualifyingCareerPpg()
    {
        var league = CreateTestLeague(1);
        RecordBookService.EnsureInitialized(league);

        var player = CreateTestPlayer("Veteran", 1, 0, "Team0");
        player.CareerStats = new PlayerStatLine
        {
            Games = 500,
            FieldGoalsMade = 3000,
            FieldGoalsAttempted = 6000,
            FreeThrowsMade = 2000,
            FreeThrowsAttempted = 2500,
            ThreePointersMade = 500,
            ThreePointersAttempted = 1500,
            Rebounds = 2000,
            Assists = 1500,
            Steals = 500,
            Blocks = 200
        };
        league.Teams[0].Roster.Add(player);

        RecordBookService.UpdateCareerRecords(league.RecordBook, league, 2024);

        // Points = 3000*2+2000+500 = 8500, > 1400
        league.RecordBook.CareerRecords["PointsPerGame"].Entries.Should().HaveCount(1);
        league.RecordBook.CareerRecords["PointsPerGame"].Entries[0].Value
            .Should().BeApproximately(8500.0 / 500, 0.01);
    }

    [Fact]
    public void UpdateCareerRecords_DeduplicatesByPlayerId()
    {
        var league = CreateTestLeague(1);
        RecordBookService.EnsureInitialized(league);

        var player = CreateTestPlayer("Veteran", 1, 0, "Team0");
        player.CareerStats = new PlayerStatLine
        {
            Games = 500,
            FieldGoalsMade = 3000,
            FieldGoalsAttempted = 6000,
            FreeThrowsMade = 2000,
            FreeThrowsAttempted = 2500,
            ThreePointersMade = 500,
            ThreePointersAttempted = 1500,
            Rebounds = 2000,
            Assists = 1500,
            Steals = 500,
            Blocks = 200
        };
        league.Teams[0].Roster.Add(player);

        // Insert twice (simulating two consecutive off-seasons)
        RecordBookService.UpdateCareerRecords(league.RecordBook, league, 2024);
        RecordBookService.UpdateCareerRecords(league.RecordBook, league, 2025);

        // Should only have 1 entry per player (deduplicated)
        league.RecordBook.CareerRecords["PointsPerGame"].Entries.Should().HaveCount(1);
    }

    [Fact]
    public void UpdateCareerRecords_FgPctUsesCareerMinimums()
    {
        var league = CreateTestLeague(1);
        RecordBookService.EnsureInitialized(league);

        var player = CreateTestPlayer("FewShots", 1, 0, "Team0");
        player.CareerStats = new PlayerStatLine
        {
            Games = 50,
            FieldGoalsMade = 99, // Below 100 FGM career minimum
            FieldGoalsAttempted = 150
        };
        league.Teams[0].Roster.Add(player);

        RecordBookService.UpdateCareerRecords(league.RecordBook, league, 2024);

        league.RecordBook.CareerRecords["FieldGoalPct"].Entries.Should().BeEmpty();
    }

    [Fact]
    public void UpdateCareerRecords_ThreePointPctUsesCareerMinimums()
    {
        var league = CreateTestLeague(1);
        RecordBookService.EnsureInitialized(league);

        var player = CreateTestPlayer("Shooter", 1, 0, "Team0");
        player.CareerStats = new PlayerStatLine
        {
            Games = 200,
            ThreePointersMade = 30, // > 25 career minimum
            ThreePointersAttempted = 80,
            FieldGoalsMade = 200,
            FieldGoalsAttempted = 500
        };
        league.Teams[0].Roster.Add(player);

        RecordBookService.UpdateCareerRecords(league.RecordBook, league, 2024);

        league.RecordBook.CareerRecords["ThreePointPct"].Entries.Should().HaveCount(1);
    }

    [Fact]
    public void UpdateCareerRecords_SkipsZeroGamesPlayers()
    {
        var league = CreateTestLeague(1);
        RecordBookService.EnsureInitialized(league);

        var player = CreateTestPlayer("Rookie", 1, 0, "Team0");
        player.CareerStats = new PlayerStatLine { Games = 0 };
        league.Teams[0].Roster.Add(player);

        RecordBookService.UpdateCareerRecords(league.RecordBook, league, 2024);

        league.RecordBook.CareerRecords["PointsPerGame"].Entries.Should().BeEmpty();
    }

    // ── ClearSeasonGameHighs Tests ──────────────────────────────────────────

    [Fact]
    public void ClearSeasonGameHighs_ClearsLeagueAndTeamScopes()
    {
        var league = CreateTestLeague();
        RecordBookService.EnsureInitialized(league);
        var rb = league.RecordBook;

        // Add some records
        var boxScore = new List<PlayerGameState>
        {
            CreateGameState("Star", 1, 50, fgm: 20, threes: 5, ftm: 5)
        };
        RecordBookService.UpdateSingleGameRecords(rb, boxScore, league.Teams[0], 0, GameType.League, 2024);
        RecordBookService.UpdateSingleGameRecords(rb, boxScore, league.Teams[0], 0, GameType.Playoff, 2024);

        rb.SingleGameRecords["League"]["Points"].Entries.Should().HaveCount(1);
        rb.SingleGameRecords["Playoff"]["Points"].Entries.Should().HaveCount(1);

        RecordBookService.ClearSeasonGameHighs(rb);

        // Both league and playoff single-game cleared
        rb.SingleGameRecords["League"]["Points"].Entries.Should().BeEmpty();
        rb.SingleGameRecords["Playoff"]["Points"].Entries.Should().BeEmpty();
        rb.SingleGameRecords["Team_0"]["Points"].Entries.Should().BeEmpty();
    }

    [Fact]
    public void ClearSeasonGameHighs_PreservesSeasonAndCareerRecords()
    {
        var league = CreateTestLeague(1);
        RecordBookService.EnsureInitialized(league);

        // Add a qualifying player for season records
        var player = CreateTestPlayer("Scorer", 1, 0, "Team0");
        player.SimulatedStats = new PlayerStatLine
        {
            Games = 82,
            FieldGoalsMade = 600,
            FieldGoalsAttempted = 1200,
            FreeThrowsMade = 400,
            FreeThrowsAttempted = 500,
            ThreePointersMade = 100,
            ThreePointersAttempted = 300
        };
        player.CareerStats = new PlayerStatLine
        {
            Games = 500,
            FieldGoalsMade = 3000,
            FieldGoalsAttempted = 6000,
            FreeThrowsMade = 2000,
            FreeThrowsAttempted = 2500,
            ThreePointersMade = 500,
            ThreePointersAttempted = 1500
        };
        league.Teams[0].Roster.Add(player);

        RecordBookService.UpdateSeasonRecords(league.RecordBook, league, 2024);
        RecordBookService.UpdateCareerRecords(league.RecordBook, league, 2024);

        RecordBookService.ClearSeasonGameHighs(league.RecordBook);

        // Season and career records should be preserved
        league.RecordBook.SeasonRecords["League"]["PointsPerGame"].Entries.Should().HaveCount(1);
        league.RecordBook.CareerRecords["PointsPerGame"].Entries.Should().HaveCount(1);
    }

    // ── TryInsert Edge Cases ────────────────────────────────────────────────

    [Fact]
    public void TryInsert_WorseThanAllExistingDoesNotInsertWhenFull()
    {
        var league = CreateTestLeague();
        RecordBookService.EnsureInitialized(league);
        var rb = league.RecordBook;

        // Fill with 10 high-scoring games
        for (int i = 0; i < 10; i++)
        {
            var box = new List<PlayerGameState>
            {
                CreateGameState($"HighScorer{i}", i, 50 + i, fgm: 20 + i / 2, threes: 5, ftm: 5)
            };
            RecordBookService.UpdateSingleGameRecords(rb, box, league.Teams[0], 0, GameType.League, 2024);
        }

        // Try inserting a worse score
        var lowBox = new List<PlayerGameState>
        {
            CreateGameState("LowScorer", 99, 10, fgm: 4, threes: 1, ftm: 1)
        };
        RecordBookService.UpdateSingleGameRecords(rb, lowBox, league.Teams[0], 0, GameType.League, 2024);

        rb.SingleGameRecords["League"]["Points"].Entries.Should().HaveCount(10);
        rb.SingleGameRecords["League"]["Points"].Entries.Should().NotContain(e => e.PlayerName == "LowScorer");
    }

    [Fact]
    public void TryInsert_EqualValueInsertsAfterExisting()
    {
        var league = CreateTestLeague();
        RecordBookService.EnsureInitialized(league);
        var rb = league.RecordBook;

        // fgm=12, threes=3, ftm=3 → 12*2+3+3=30
        var box1 = new List<PlayerGameState> { CreateGameState("First", 1, 30, fgm: 12, threes: 3, ftm: 3) };
        var box2 = new List<PlayerGameState> { CreateGameState("Second", 2, 30, fgm: 12, threes: 3, ftm: 3) };

        RecordBookService.UpdateSingleGameRecords(rb, box1, league.Teams[0], 0, GameType.League, 2024);
        RecordBookService.UpdateSingleGameRecords(rb, box2, league.Teams[0], 0, GameType.League, 2024);

        var entries = rb.SingleGameRecords["League"]["Points"].Entries;
        entries.Should().HaveCount(2);
        entries[0].PlayerName.Should().Be("First");
        entries[1].PlayerName.Should().Be("Second");
    }
}
