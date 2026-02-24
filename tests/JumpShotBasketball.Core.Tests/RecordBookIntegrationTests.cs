using FluentAssertions;
using JumpShotBasketball.Core.Enums;
using JumpShotBasketball.Core.Models.Awards;
using JumpShotBasketball.Core.Models.Game;
using JumpShotBasketball.Core.Models.History;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Staff;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

/// <summary>
/// Integration tests for record book, franchise history, and awards archival
/// hooks in PostGameProcessor, OffSeasonService, and LeagueCreationService.
/// </summary>
public class RecordBookIntegrationTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static League CreateIntegrationLeague()
    {
        var league = new League
        {
            Settings = new LeagueSettings
            {
                CurrentYear = 2024,
                NumberOfTeams = 2,
                LeagueName = "TestLeague"
            },
            Schedule = new Schedule
            {
                SeasonStarted = true,
                RegularSeasonEnded = true,
                PlayoffsStarted = true,
                GamesInSeason = 82
            }
        };

        for (int t = 0; t < 2; t++)
        {
            var team = new Team
            {
                Id = t,
                Name = $"Team{t}",
                Record = new TeamRecord
                {
                    TeamName = $"Team{t}",
                    Wins = 40 + t * 10,
                    Losses = 42 - t * 10,
                    IsPlayoffTeam = t == 1,
                    HasRing = t == 1
                },
                Coach = new StaffMember
                {
                    Name = $"Coach{t}",
                    CoachPot1 = 3, CoachPot2 = 3, CoachEffort = 3,
                    CoachScoring = 3, CoachShooting = 3,
                    CoachRebounding = 3, CoachPassing = 3, CoachDefense = 3
                }
            };

            for (int p = 0; p < 5; p++)
            {
                var player = new Player
                {
                    Id = t * 100 + p,
                    Name = $"Player{t}_{p}",
                    LastName = $"P{t}_{p}",
                    Position = new[] { "PG", "SG", "SF", "PF", " C" }[p],
                    Age = 25,
                    Health = 100,
                    Team = $"Team{t}",
                    TeamIndex = t,
                    Active = true,
                    Ratings = new PlayerRatings
                    {
                        Prime = 28,
                        Potential1 = 3, Potential2 = 3, Effort = 3,
                        TradeTrueRating = 5.0 + p,
                        MovementOffenseRaw = 5, PenetrationOffenseRaw = 5,
                        PostOffenseRaw = 5, TransitionOffenseRaw = 5,
                        MovementDefenseRaw = 5, PenetrationDefenseRaw = 5,
                        PostDefenseRaw = 5, TransitionDefenseRaw = 5,
                        ProjectionFieldGoalsAttempted = 50,
                        ProjectionFreeThrowsAttempted = 50,
                        ProjectionFieldGoalPercentage = 50
                    },
                    SimulatedStats = new PlayerStatLine
                    {
                        Games = 70, Minutes = 2100,
                        FieldGoalsMade = 350, FieldGoalsAttempted = 800,
                        FreeThrowsMade = 150, FreeThrowsAttempted = 200,
                        ThreePointersMade = 60, ThreePointersAttempted = 180,
                        OffensiveRebounds = 40, Rebounds = 250,
                        Assists = 300, Steals = 80, Turnovers = 150,
                        Blocks = 15, PersonalFouls = 130
                    },
                    Contract = new PlayerContract
                    {
                        YearsOfService = 5, YearsOnTeam = 3,
                        ContractYears = 4, CurrentContractYear = 2,
                        LoyaltyFactor = 3, CurrentYearSalary = 500
                    }
                };
                team.Roster.Add(player);
            }

            league.Teams.Add(team);
        }

        league.Schedule.Games.Add(new ScheduledGame { GameNumber = 1, Played = true });
        league.Transactions.Add(new Transaction());

        RecordBookService.EnsureInitialized(league);
        league.FranchiseHistories = league.Teams.Select((t, i) => new FranchiseHistory
            { TeamIndex = i, TeamName = t.Name }).ToList();

        return league;
    }

    private static PlayerGameState CreateGameState(string name, int playerId, int fgm = 5,
        int threes = 2, int ftm = 3, int rebounds = 5, int assists = 3)
    {
        return new PlayerGameState
        {
            GameName = name,
            PlayerPointer = playerId,
            Minutes = 2400,
            FieldGoalsMade = fgm,
            FieldGoalsAttempted = fgm + 5,
            FreeThrowsMade = ftm,
            FreeThrowsAttempted = ftm + 2,
            ThreePointersMade = threes,
            ThreePointersAttempted = threes + 2,
            OffensiveRebounds = rebounds / 3,
            DefensiveRebounds = rebounds - rebounds / 3,
            Assists = assists,
            Steals = 1,
            Turnovers = 2,
            Blocks = 1,
            PersonalFouls = 2
        };
    }

    // ── PostGameProcessor Integration ───────────────────────────────────────

    [Fact]
    public void ProcessGame_UpdatesRecordBookForLeagueGame()
    {
        var league = CreateIntegrationLeague();
        var game = new ScheduledGame
        {
            GameNumber = 99,
            HomeTeamIndex = 0,
            VisitorTeamIndex = 1,
            Type = GameType.League
        };
        league.Schedule.Games.Add(game);

        var result = new GameResult
        {
            HomeScore = 100,
            VisitorScore = 95,
            HomeTeamIndex = 0,
            VisitorTeamIndex = 1,
            HomeBoxScore = league.Teams[0].Roster.Select(p =>
                CreateGameState(p.Name, p.Id, fgm: 10, threes: 3, ftm: 4)).ToList(),
            VisitorBoxScore = league.Teams[1].Roster.Select(p =>
                CreateGameState(p.Name, p.Id, fgm: 8, threes: 2, ftm: 3)).ToList(),
            QuartersPlayed = 4
        };

        PostGameProcessor.ProcessGame(league, game, result);

        // Records should be populated for both teams
        league.RecordBook.SingleGameRecords["League"]["Points"].Entries.Should().NotBeEmpty();
        league.RecordBook.SingleGameRecords["Team_0"]["Points"].Entries.Should().NotBeEmpty();
        league.RecordBook.SingleGameRecords["Team_1"]["Points"].Entries.Should().NotBeEmpty();
    }

    [Fact]
    public void ProcessGame_DoesNotUpdateRecordBookForExhibition()
    {
        var league = CreateIntegrationLeague();
        var game = new ScheduledGame
        {
            GameNumber = 99,
            HomeTeamIndex = 0,
            VisitorTeamIndex = 1,
            Type = GameType.Exhibition
        };
        league.Schedule.Games.Add(game);

        var result = new GameResult
        {
            HomeScore = 100,
            VisitorScore = 95,
            HomeTeamIndex = 0,
            VisitorTeamIndex = 1,
            HomeBoxScore = league.Teams[0].Roster.Select(p =>
                CreateGameState(p.Name, p.Id, fgm: 20, threes: 5, ftm: 10)).ToList(),
            VisitorBoxScore = league.Teams[1].Roster.Select(p =>
                CreateGameState(p.Name, p.Id)).ToList(),
            QuartersPlayed = 4
        };

        PostGameProcessor.ProcessGame(league, game, result);

        league.RecordBook.SingleGameRecords["League"]["Points"].Entries.Should().BeEmpty();
    }

    [Fact]
    public void ProcessGame_PlayoffGameGoesToPlayoffScope()
    {
        var league = CreateIntegrationLeague();
        var game = new ScheduledGame
        {
            GameNumber = 99,
            HomeTeamIndex = 0,
            VisitorTeamIndex = 1,
            Type = GameType.Playoff
        };
        league.Schedule.Games.Add(game);

        var result = new GameResult
        {
            HomeScore = 110,
            VisitorScore = 105,
            HomeTeamIndex = 0,
            VisitorTeamIndex = 1,
            HomeBoxScore = league.Teams[0].Roster.Select(p =>
                CreateGameState(p.Name, p.Id, fgm: 15, threes: 4, ftm: 6)).ToList(),
            VisitorBoxScore = league.Teams[1].Roster.Select(p =>
                CreateGameState(p.Name, p.Id, fgm: 12, threes: 3, ftm: 5)).ToList(),
            QuartersPlayed = 4
        };

        PostGameProcessor.ProcessGame(league, game, result);

        league.RecordBook.SingleGameRecords["Playoff"]["Points"].Entries.Should().NotBeEmpty();
        league.RecordBook.SingleGameRecords["League"]["Points"].Entries.Should().BeEmpty();
    }

    [Fact]
    public void ProcessGame_NullRecordBookDoesNotCrash()
    {
        var league = CreateIntegrationLeague();
        league.RecordBook = null!;
        var game = new ScheduledGame
        {
            GameNumber = 99,
            HomeTeamIndex = 0,
            VisitorTeamIndex = 1,
            Type = GameType.League
        };
        league.Schedule.Games.Add(game);

        var result = new GameResult
        {
            HomeScore = 100,
            VisitorScore = 95,
            HomeTeamIndex = 0,
            VisitorTeamIndex = 1,
            HomeBoxScore = league.Teams[0].Roster.Select(p =>
                CreateGameState(p.Name, p.Id)).ToList(),
            VisitorBoxScore = league.Teams[1].Roster.Select(p =>
                CreateGameState(p.Name, p.Id)).ToList(),
            QuartersPlayed = 4
        };

        var act = () => PostGameProcessor.ProcessGame(league, game, result);
        act.Should().NotThrow();
    }

    [Fact]
    public void ProcessGame_AllStarDoesNotUpdateRecordBook()
    {
        var league = CreateIntegrationLeague();
        var game = new ScheduledGame
        {
            GameNumber = 99,
            HomeTeamIndex = 0,
            VisitorTeamIndex = 1,
            Type = GameType.AllStar
        };
        league.Schedule.Games.Add(game);

        var result = new GameResult
        {
            HomeScore = 150,
            VisitorScore = 140,
            HomeTeamIndex = 0,
            VisitorTeamIndex = 1,
            HomeBoxScore = league.Teams[0].Roster.Select(p =>
                CreateGameState(p.Name, p.Id, fgm: 20)).ToList(),
            VisitorBoxScore = league.Teams[1].Roster.Select(p =>
                CreateGameState(p.Name, p.Id, fgm: 20)).ToList(),
            QuartersPlayed = 4
        };

        PostGameProcessor.ProcessGame(league, game, result);

        league.RecordBook.SingleGameRecords["League"]["Points"].Entries.Should().BeEmpty();
    }

    // ── OffSeasonService Awards Archival ─────────────────────────────────────

    [Fact]
    public void ResetSeasonState_ArchivesAwardsBeforeClearing()
    {
        var league = CreateIntegrationLeague();
        league.Awards = new SeasonAwards { Year = 2024 };

        OffSeasonService.ResetSeasonState(league);

        league.Awards.Should().BeNull();
        league.AwardsHistory.Should().HaveCount(1);
        league.AwardsHistory[0].Year.Should().Be(2024);
    }

    [Fact]
    public void ResetSeasonState_NullAwardsDoesNotArchive()
    {
        var league = CreateIntegrationLeague();
        league.Awards = null;

        OffSeasonService.ResetSeasonState(league);

        league.AwardsHistory.Should().BeEmpty();
    }

    [Fact]
    public void ResetSeasonState_AccumulatesAwardsOverMultipleSeasons()
    {
        var league = CreateIntegrationLeague();

        league.Awards = new SeasonAwards { Year = 2024 };
        OffSeasonService.ResetSeasonState(league);

        // Reset records cleared by previous reset for next test
        foreach (var team in league.Teams)
        {
            team.Record.Wins = 50;
            team.Record.Losses = 32;
        }
        league.Schedule.Games.Add(new ScheduledGame { Played = true });

        league.Awards = new SeasonAwards { Year = 2025 };
        OffSeasonService.ResetSeasonState(league);

        league.AwardsHistory.Should().HaveCount(2);
    }

    // ── OffSeasonService Record Book Integration ─────────────────────────────

    [Fact]
    public void ResetSeasonState_ClearsSeasonGameHighs()
    {
        var league = CreateIntegrationLeague();

        // Add some single-game records
        var boxScore = league.Teams[0].Roster.Select(p =>
            CreateGameState(p.Name, p.Id, fgm: 15, threes: 5, ftm: 5)).ToList();
        RecordBookService.UpdateSingleGameRecords(league.RecordBook, boxScore,
            league.Teams[0], 0, GameType.League, 2024);

        league.RecordBook.SingleGameRecords["League"]["Points"].Entries.Should().NotBeEmpty();

        OffSeasonService.ResetSeasonState(league);

        league.RecordBook.SingleGameRecords["League"]["Points"].Entries.Should().BeEmpty();
    }

    [Fact]
    public void AdvanceSeason_UpdatesSeasonRecords()
    {
        var league = CreateIntegrationLeague();

        // Give players enough stats to qualify (Points > 1400)
        foreach (var team in league.Teams)
        {
            foreach (var player in team.Roster)
            {
                player.SimulatedStats.Games = 82;
                player.SimulatedStats.FieldGoalsMade = 600;
                player.SimulatedStats.FieldGoalsAttempted = 1200;
                player.SimulatedStats.FreeThrowsMade = 400;
                player.SimulatedStats.FreeThrowsAttempted = 500;
                player.SimulatedStats.ThreePointersMade = 100;
                player.SimulatedStats.ThreePointersAttempted = 300;
                player.SimulatedStats.Rebounds = 900;
                player.SimulatedStats.Assists = 500;
                player.SimulatedStats.Steals = 150;
                player.SimulatedStats.Blocks = 120;
                player.SimulatedStats.Minutes = 2800;
            }
        }

        OffSeasonService.AdvanceSeason(league, new Random(42));

        // Season records should have been populated before stats were reset
        league.RecordBook.SeasonRecords["League"]["PointsPerGame"].Entries.Should().NotBeEmpty();
    }

    [Fact]
    public void AdvanceSeason_ArchivesFranchiseHistory()
    {
        var league = CreateIntegrationLeague();

        OffSeasonService.AdvanceSeason(league, new Random(42));

        league.FranchiseHistories[0].Seasons.Should().HaveCount(1);
        league.FranchiseHistories[0].Seasons[0].Year.Should().Be(2024);
    }

    [Fact]
    public void AdvanceSeason_NullRecordBookDoesNotCrash()
    {
        var league = CreateIntegrationLeague();
        league.RecordBook = null!;

        var act = () => OffSeasonService.AdvanceSeason(league, new Random(42));
        act.Should().NotThrow();
    }

    // ── LeagueCreationService Integration ────────────────────────────────────

    [Fact]
    public void CreateNewLeague_InitializesRecordBook()
    {
        var league = LeagueCreationService.CreateNewLeague(random: new Random(42));

        league.RecordBook.Should().NotBeNull();
        league.RecordBook.SingleGameRecords.Should().ContainKey("League");
        league.RecordBook.SingleGameRecords.Should().ContainKey("Playoff");
        league.RecordBook.SeasonRecords.Should().ContainKey("League");
        league.RecordBook.CareerRecords.Should().NotBeEmpty();
    }

    [Fact]
    public void CreateNewLeague_InitializesFranchiseHistories()
    {
        var league = LeagueCreationService.CreateNewLeague(random: new Random(42));

        league.FranchiseHistories.Should().HaveCount(30);
        league.FranchiseHistories[0].TeamName.Should().Be("Celtics");
        league.FranchiseHistories[0].TeamIndex.Should().Be(0);
        league.FranchiseHistories[0].Seasons.Should().BeEmpty();
    }
}
