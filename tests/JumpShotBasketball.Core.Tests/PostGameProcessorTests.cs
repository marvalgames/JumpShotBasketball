using FluentAssertions;
using JumpShotBasketball.Core.Enums;
using JumpShotBasketball.Core.Models.Game;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Staff;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class PostGameProcessorTests
{
    private static Player CreatePlayer(string name, string position = "SF")
    {
        return new Player
        {
            Name = name,
            Position = position,
            Active = true,
            Health = 100,
            SeasonHighs = new PlayerSeasonHighs()
        };
    }

    private static PlayerGameState CreateGameState(string name, int minutes = 1800,
        int fgm = 5, int fga = 12, int ftm = 3, int fta = 4,
        int tpm = 2, int tpa = 5, int oreb = 1, int dreb = 4,
        int ast = 3, int stl = 1, int to = 2, int blk = 1, int pf = 2)
    {
        return new PlayerGameState
        {
            GameName = name,
            Minutes = minutes,
            FieldGoalsMade = fgm,
            FieldGoalsAttempted = fga,
            FreeThrowsMade = ftm,
            FreeThrowsAttempted = fta,
            ThreePointersMade = tpm,
            ThreePointersAttempted = tpa,
            OffensiveRebounds = oreb,
            DefensiveRebounds = dreb,
            Assists = ast,
            Steals = stl,
            Turnovers = to,
            Blocks = blk,
            PersonalFouls = pf
        };
    }

    // ── AccumulateStats ────────────────────────────────────────────

    [Fact]
    public void AccumulateStats_LeagueGame_AddsToSimulatedStats()
    {
        var player = CreatePlayer("John Doe");
        var gs = CreateGameState("John Doe", minutes: 1800, fgm: 8, fga: 15);

        PostGameProcessor.AccumulateStats(player, gs, GameType.League);

        player.SimulatedStats.Games.Should().Be(1);
        player.SimulatedStats.Minutes.Should().Be(30); // 1800/60
        player.SimulatedStats.FieldGoalsMade.Should().Be(8);
        player.SimulatedStats.FieldGoalsAttempted.Should().Be(15);
    }

    [Fact]
    public void AccumulateStats_PlayoffGame_AddsToPlayoffStats()
    {
        var player = CreatePlayer("John Doe");
        var gs = CreateGameState("John Doe", minutes: 2400, fgm: 10, fga: 20);

        PostGameProcessor.AccumulateStats(player, gs, GameType.Playoff);

        player.PlayoffStats.Games.Should().Be(1);
        player.PlayoffStats.Minutes.Should().Be(40); // 2400/60
        player.PlayoffStats.FieldGoalsMade.Should().Be(10);
        player.SimulatedStats.Games.Should().Be(0); // not touched
    }

    [Fact]
    public void AccumulateStats_ExhibitionGame_DoesNotAccumulate()
    {
        var player = CreatePlayer("John Doe");
        var gs = CreateGameState("John Doe");

        PostGameProcessor.AccumulateStats(player, gs, GameType.Exhibition);

        player.SimulatedStats.Games.Should().Be(0);
        player.PlayoffStats.Games.Should().Be(0);
    }

    [Fact]
    public void AccumulateStats_AllStarGame_DoesNotAccumulate()
    {
        var player = CreatePlayer("John Doe");
        var gs = CreateGameState("John Doe");

        PostGameProcessor.AccumulateStats(player, gs, GameType.AllStar);

        player.SimulatedStats.Games.Should().Be(0);
    }

    [Fact]
    public void AccumulateStats_ZeroMinutes_GamesCountIsZero()
    {
        var player = CreatePlayer("John Doe");
        var gs = CreateGameState("John Doe", minutes: 0);

        PostGameProcessor.AccumulateStats(player, gs, GameType.League);

        player.SimulatedStats.Games.Should().Be(0);
        player.SimulatedStats.Minutes.Should().Be(0);
    }

    [Fact]
    public void AccumulateStats_MinimalMinutes_RoundsUpToOne()
    {
        var player = CreatePlayer("John Doe");
        var gs = CreateGameState("John Doe", minutes: 20); // 20 seconds

        PostGameProcessor.AccumulateStats(player, gs, GameType.League);

        player.SimulatedStats.Games.Should().Be(1);
        player.SimulatedStats.Minutes.Should().Be(1); // min 1 if played
    }

    [Fact]
    public void AccumulateStats_MultipleGames_Accumulates()
    {
        var player = CreatePlayer("John Doe");
        var gs1 = CreateGameState("John Doe", fgm: 5, fga: 10);
        var gs2 = CreateGameState("John Doe", fgm: 7, fga: 14);

        PostGameProcessor.AccumulateStats(player, gs1, GameType.League);
        PostGameProcessor.AccumulateStats(player, gs2, GameType.League);

        player.SimulatedStats.Games.Should().Be(2);
        player.SimulatedStats.FieldGoalsMade.Should().Be(12);
        player.SimulatedStats.FieldGoalsAttempted.Should().Be(24);
    }

    [Fact]
    public void AccumulateStats_Rebounds_CombinesOrebAndDreb()
    {
        var player = CreatePlayer("John Doe");
        var gs = CreateGameState("John Doe", oreb: 3, dreb: 7);

        PostGameProcessor.AccumulateStats(player, gs, GameType.League);

        player.SimulatedStats.OffensiveRebounds.Should().Be(3);
        player.SimulatedStats.Rebounds.Should().Be(10); // 3 + 7
    }

    // ── UpdateSeasonHighs ──────────────────────────────────────────

    [Fact]
    public void UpdateSeasonHighs_LeagueGame_UpdatesSeasonAndCareerHighs()
    {
        var player = CreatePlayer("John Doe");
        var gs = CreateGameState("John Doe", fgm: 10, fga: 20, ftm: 5, fta: 6,
            tpm: 3, tpa: 6, oreb: 4, dreb: 8, ast: 7, stl: 2, blk: 3);

        PostGameProcessor.UpdateSeasonHighs(player, gs, GameType.League);

        // Points: fgm*2 + tpm + ftm = 20+3+5 = 28
        player.SeasonHighs.SeasonPoints.Should().Be(28);
        player.SeasonHighs.CareerPoints.Should().Be(28);
        player.SeasonHighs.SeasonRebounds.Should().Be(12); // 4+8
        player.SeasonHighs.SeasonAssists.Should().Be(7);
    }

    [Fact]
    public void UpdateSeasonHighs_DoubleDouble_IncrementsBoth()
    {
        var player = CreatePlayer("John Doe");
        // 25 pts (fgm*2+tpm+ftm = 8*2+3+4=23), 12 reb (3+9) -> 2 cats >= 10
        var gs = CreateGameState("John Doe", fgm: 8, fga: 18, ftm: 4, fta: 5,
            tpm: 3, tpa: 6, oreb: 3, dreb: 9, ast: 4, stl: 1, blk: 0);

        PostGameProcessor.UpdateSeasonHighs(player, gs, GameType.League);

        player.SeasonHighs.SeasonDoubleDoubles.Should().Be(1);
        player.SeasonHighs.CareerDoubleDoubles.Should().Be(1);
        player.SeasonHighs.SeasonTripleDoubles.Should().Be(0);
    }

    [Fact]
    public void UpdateSeasonHighs_TripleDouble_IncrementsBothCounters()
    {
        var player = CreatePlayer("John Doe");
        // 25 pts, 12 reb, 11 ast -> 3 cats >= 10
        var gs = CreateGameState("John Doe", fgm: 9, fga: 18, ftm: 4, fta: 5,
            tpm: 3, tpa: 6, oreb: 4, dreb: 8, ast: 11, stl: 1, blk: 0);

        PostGameProcessor.UpdateSeasonHighs(player, gs, GameType.League);

        player.SeasonHighs.SeasonDoubleDoubles.Should().Be(1);
        player.SeasonHighs.SeasonTripleDoubles.Should().Be(1);
    }

    [Fact]
    public void UpdateSeasonHighs_PlayoffGame_UpdatesPlayoffHighsOnly()
    {
        var player = CreatePlayer("John Doe");
        var gs = CreateGameState("John Doe", fgm: 12, fga: 22, ftm: 6, fta: 7,
            tpm: 4, tpa: 8, oreb: 3, dreb: 7, ast: 5, stl: 3, blk: 2);

        PostGameProcessor.UpdateSeasonHighs(player, gs, GameType.Playoff);

        // Points: 12*2 + 4 + 6 = 34
        player.SeasonHighs.PlayoffPoints.Should().Be(34);
        player.SeasonHighs.CareerPlayoffPoints.Should().Be(34);
        player.SeasonHighs.SeasonPoints.Should().Be(0); // not touched
    }

    [Fact]
    public void UpdateSeasonHighs_OnlyUpdatesIfHigher()
    {
        var player = CreatePlayer("John Doe");
        player.SeasonHighs.SeasonPoints = 50;

        var gs = CreateGameState("John Doe", fgm: 5, fga: 10, ftm: 2, fta: 3,
            tpm: 1, tpa: 3, oreb: 1, dreb: 3, ast: 2, stl: 1, blk: 0);

        PostGameProcessor.UpdateSeasonHighs(player, gs, GameType.League);

        player.SeasonHighs.SeasonPoints.Should().Be(50); // unchanged
    }

    // ── RecordStarters ─────────────────────────────────────────────

    [Fact]
    public void RecordStarters_IncrementsStarterCount()
    {
        var p1 = CreatePlayer("A");
        var p2 = CreatePlayer("B");
        p1.Starter = 5;
        p2.Starter = 10;

        PostGameProcessor.RecordStarters(new List<Player> { p1, p2 });

        p1.Starter.Should().Be(6);
        p2.Starter.Should().Be(11);
    }

    // ── ProcessGame ────────────────────────────────────────────────

    [Fact]
    public void ProcessGame_MarksScheduleGameAsPlayed()
    {
        var league = CreateMinimalLeague();
        var game = league.Schedule.Games[0];
        var result = CreateMinimalGameResult(league, game);

        PostGameProcessor.ProcessGame(league, game, result);

        game.Played.Should().BeTrue();
        game.HomeScore.Should().Be(result.HomeScore);
        game.VisitorScore.Should().Be(result.VisitorScore);
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static League CreateMinimalLeague()
    {
        var league = new League();
        var team1 = new Team { Id = 1, Name = "Team1", Record = new TeamRecord { TeamName = "Team1" } };
        var team2 = new Team { Id = 2, Name = "Team2", Record = new TeamRecord { TeamName = "Team2" } };

        for (int i = 0; i < 5; i++)
        {
            string[] positions = { "PG", "SG", "SF", "PF", "C" };
            team1.Roster.Add(CreatePlayer($"T1P{i}", positions[i]));
            team2.Roster.Add(CreatePlayer($"T2P{i}", positions[i]));
        }

        league.Teams.Add(team1);
        league.Teams.Add(team2);

        league.Schedule.Games.Add(new ScheduledGame
        {
            GameNumber = 1,
            Day = 1,
            HomeTeamIndex = 1,
            VisitorTeamIndex = 0,
            Type = GameType.League
        });

        return league;
    }

    private static GameResult CreateMinimalGameResult(League league, ScheduledGame game)
    {
        var visitorBox = league.Teams[game.VisitorTeamIndex].Roster
            .Select(p => CreateGameState(p.Name)).ToList();
        var homeBox = league.Teams[game.HomeTeamIndex].Roster
            .Select(p => CreateGameState(p.Name)).ToList();

        return new GameResult
        {
            VisitorScore = 95,
            HomeScore = 102,
            VisitorTeamIndex = game.VisitorTeamIndex,
            HomeTeamIndex = game.HomeTeamIndex,
            VisitorBoxScore = visitorBox,
            HomeBoxScore = homeBox,
            QuartersPlayed = 4
        };
    }
}
