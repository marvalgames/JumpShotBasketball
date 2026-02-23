using FluentAssertions;
using JumpShotBasketball.Core.Enums;
using JumpShotBasketball.Core.Models.Game;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Staff;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class SeasonSimulationServiceTests
{
    private static Team CreateFullTeam(string name, int teamIndex)
    {
        var team = new Team
        {
            Id = teamIndex + 1,
            Name = name,
            CityName = name,
            Record = new TeamRecord { TeamName = name, Control = "Computer" },
            Coach = new StaffMember
            {
                Name = $"{name} Coach",
                CoachOutside = 3,
                CoachPenetration = 3,
                CoachInside = 3,
                CoachFastbreak = 3,
                CoachOutsideDefense = 3,
                CoachPenetrationDefense = 3,
                CoachInsideDefense = 3,
                CoachFastbreakDefense = 3
            }
        };

        string[] positions = { "PG", "SG", "SF", "PF", " C", "PG", "SG", "SF", "PF", " C", "SF", "PF" };

        for (int i = 0; i < 12; i++)
        {
            team.Roster.Add(new Player
            {
                Id = teamIndex * 30 + i + 1,
                Name = $"{name} Player{i + 1}",
                LastName = $"Player{i + 1}",
                Position = positions[i],
                Team = name,
                TeamIndex = teamIndex,
                Active = true,
                Health = 100,
                Starter = i < 5 ? 1 : 0,
                GameMinutes = i < 5 ? 30 : 15,
                PgRotation = positions[i] == "PG",
                SgRotation = positions[i] == "SG",
                SfRotation = positions[i] == "SF",
                PfRotation = positions[i] == "PF",
                CRotation = positions[i] is " C" or "C",
                SeasonStats = new PlayerStatLine
                {
                    Games = 40,
                    Minutes = i < 5 ? 40 * 30 * 60 : 40 * 15 * 60,
                    FieldGoalsMade = 200 + i * 10,
                    FieldGoalsAttempted = 450 + i * 20,
                    FreeThrowsMade = 80 + i * 5,
                    FreeThrowsAttempted = 100 + i * 5,
                    ThreePointersMade = 40 + i * 5,
                    ThreePointersAttempted = 120 + i * 10,
                    OffensiveRebounds = 40 + i * 5,
                    Rebounds = 200 + i * 15,
                    Assists = 150 + (i < 2 ? 100 : 0),
                    Steals = 40 + i * 3,
                    Turnovers = 60 + i * 3,
                    Blocks = 20 + i * 5,
                    PersonalFouls = 80 + i * 3
                },
                Ratings = new PlayerRatings
                {
                    TrueRating = 50 + i * 3,
                    TradeTrueRating = 50 + i * 3,
                    TeammatesBetterRating = 1.0 + i * 0.2,
                    FieldGoalPercentage = 450 + i * 5,
                    AdjustedFieldGoalPercentage = 450 + i * 5,
                    FreeThrowPercentage = 750 + i * 10,
                    ThreePointPercentage = 350 + i * 5,
                    FieldGoalsAttemptedPer48Min = 15 + i * 0.5,
                    AdjustedFieldGoalsAttemptedPer48Min = 15 + i * 0.5,
                    ThreePointersAttemptedPer48Min = 5 + i * 0.3,
                    AdjustedThreePointersAttemptedPer48Min = 5 + i * 0.3,
                    FoulsDrawnPer48Min = 4 + i * 0.2,
                    AdjustedFoulsDrawnPer48Min = 4 + i * 0.2,
                    OffensiveReboundsPer48Min = 2 + i * 0.3,
                    DefensiveReboundsPer48Min = 5 + i * 0.5,
                    AssistsPer48Min = i < 2 ? 8 : 3,
                    StealsPer48Min = 1.5 + i * 0.1,
                    TurnoversPer48Min = 2.5 + i * 0.1,
                    AdjustedTurnoversPer48Min = 2.5 + i * 0.1,
                    BlocksPer48Min = 0.5 + i * 0.3,
                    PersonalFoulsPer48Min = 3.5 + i * 0.1,
                    Stamina = 80 + i * 2,
                    Consistency = 3,
                    Clutch = 50 + i * 5,
                    InjuryRating = 5 + i,
                    MovementOffenseRaw = 5 + (i < 3 ? 2 : 0),
                    MovementDefenseRaw = 5,
                    PenetrationOffenseRaw = 5 + (i < 2 ? 3 : 0),
                    PenetrationDefenseRaw = 5,
                    PostOffenseRaw = 5 + (i >= 3 ? 2 : 0),
                    PostDefenseRaw = 5,
                    TransitionOffenseRaw = 5,
                    TransitionDefenseRaw = 5,
                    ProjectionFieldGoalPercentage = 450 + i * 5,
                    MinutesPerGame = i < 5 ? 30 : 15
                },
                Better = 50
            });
        }

        return team;
    }

    private static League CreateTwoTeamLeague(int gamesPerTeam = 4)
    {
        var league = new League();
        league.Teams.Add(CreateFullTeam("Alpha", 0));
        league.Teams.Add(CreateFullTeam("Beta", 1));

        // Create schedule: alternating home/away
        int gameNum = 0;
        for (int g = 0; g < gamesPerTeam; g++)
        {
            gameNum++;
            league.Schedule.Games.Add(new ScheduledGame
            {
                GameNumber = gameNum,
                Day = gameNum,
                HomeTeamIndex = g % 2 == 0 ? 0 : 1,
                VisitorTeamIndex = g % 2 == 0 ? 1 : 0,
                Type = GameType.League
            });
        }

        league.Schedule.GamesInSeason = gamesPerTeam;
        return league;
    }

    // ── SimulateGame ───────────────────────────────────────────────

    [Fact]
    public void SimulateGame_ReturnsValidResult()
    {
        var league = CreateTwoTeamLeague(1);
        var game = league.Schedule.Games[0];

        var result = SeasonSimulationService.SimulateGame(league, game, new Random(42));

        result.HomeScore.Should().BeGreaterThan(0);
        result.VisitorScore.Should().BeGreaterThan(0);
        game.Played.Should().BeTrue();
    }

    [Fact]
    public void SimulateGame_UpdatesStandings()
    {
        var league = CreateTwoTeamLeague(1);
        var game = league.Schedule.Games[0];

        SeasonSimulationService.SimulateGame(league, game, new Random(42));

        var rec0 = league.Teams[0].Record;
        var rec1 = league.Teams[1].Record;
        // One team should have a win, other a loss
        (rec0.LeagueRecord + rec1.LeagueRecord).Should().Be(101); // 100 (win) + 1 (loss)
    }

    [Fact]
    public void SimulateGame_AccumulatesPlayerStats()
    {
        var league = CreateTwoTeamLeague(1);
        var game = league.Schedule.Games[0];

        SeasonSimulationService.SimulateGame(league, game, new Random(42));

        // At least some players should have accumulated simulated stats
        var playersWithStats = league.Teams.SelectMany(t => t.Roster)
            .Where(p => p.SimulatedStats.Games > 0)
            .ToList();

        playersWithStats.Should().NotBeEmpty();
    }

    // ── SimulateDay ────────────────────────────────────────────────

    [Fact]
    public void SimulateDay_SimulatesAllGamesOnSameDay()
    {
        var league = CreateTwoTeamLeague(4);
        // Put first 2 games on same day
        league.Schedule.Games[0].Day = 1;
        league.Schedule.Games[1].Day = 1;
        league.Schedule.Games[2].Day = 2;
        league.Schedule.Games[3].Day = 3;

        var result = SeasonSimulationService.SimulateDay(league, new Random(42));

        result.GamesSimulated.Should().Be(2);
        result.DaysSimulated.Should().Be(1);
        result.SeasonComplete.Should().BeFalse();
        league.Schedule.SeasonStarted.Should().BeTrue();
    }

    [Fact]
    public void SimulateDay_NoGamesLeft_ReturnsSeasonComplete()
    {
        var league = CreateTwoTeamLeague(1);
        league.Schedule.Games[0].Played = true; // already played

        var result = SeasonSimulationService.SimulateDay(league, new Random(42));

        result.SeasonComplete.Should().BeTrue();
        result.GamesSimulated.Should().Be(0);
        league.Schedule.RegularSeasonEnded.Should().BeTrue();
    }

    // ── SimulateWeek ───────────────────────────────────────────────

    [Fact]
    public void SimulateWeek_SimulatesUpTo7Days()
    {
        var league = CreateTwoTeamLeague(10);
        // Assign each game a different day
        for (int i = 0; i < league.Schedule.Games.Count; i++)
            league.Schedule.Games[i].Day = i + 1;

        var result = SeasonSimulationService.SimulateWeek(league, new Random(42));

        result.DaysSimulated.Should().BeLessThanOrEqualTo(7);
        result.GamesSimulated.Should().BeLessThanOrEqualTo(10);
    }

    // ── SimulateMonth ──────────────────────────────────────────────

    [Fact]
    public void SimulateMonth_SimulatesUpTo30Days()
    {
        var league = CreateTwoTeamLeague(10);
        for (int i = 0; i < league.Schedule.Games.Count; i++)
            league.Schedule.Games[i].Day = i + 1;

        var result = SeasonSimulationService.SimulateMonth(league, new Random(42));

        result.DaysSimulated.Should().BeLessThanOrEqualTo(30);
        result.GamesSimulated.Should().Be(10); // all games fit in 30 days
        result.SeasonComplete.Should().BeTrue();
    }

    // ── SimulateSeason ─────────────────────────────────────────────

    [Fact]
    public void SimulateSeason_CompletesAllGames()
    {
        var league = CreateTwoTeamLeague(4);
        for (int i = 0; i < league.Schedule.Games.Count; i++)
            league.Schedule.Games[i].Day = i + 1;

        var result = SeasonSimulationService.SimulateSeason(league, new Random(42));

        result.SeasonComplete.Should().BeTrue();
        result.GamesSimulated.Should().Be(4);
        league.Schedule.RegularSeasonEnded.Should().BeTrue();
        league.Schedule.Games.All(g => g.Played).Should().BeTrue();
    }

    [Fact]
    public void SimulateSeason_Deterministic_SameResultWithSameSeed()
    {
        var league1 = CreateTwoTeamLeague(4);
        var league2 = CreateTwoTeamLeague(4);
        for (int i = 0; i < 4; i++)
        {
            league1.Schedule.Games[i].Day = i + 1;
            league2.Schedule.Games[i].Day = i + 1;
        }

        var result1 = SeasonSimulationService.SimulateSeason(league1, new Random(42));
        var result2 = SeasonSimulationService.SimulateSeason(league2, new Random(42));

        result1.GamesSimulated.Should().Be(result2.GamesSimulated);
        for (int i = 0; i < result1.GameResults.Count; i++)
        {
            result1.GameResults[i].HomeScore.Should().Be(result2.GameResults[i].HomeScore);
            result1.GameResults[i].VisitorScore.Should().Be(result2.GameResults[i].VisitorScore);
        }
    }

    [Fact]
    public void SimulateSeason_RecordsMatch_WinsAndLossesConsistent()
    {
        var league = CreateTwoTeamLeague(4);
        for (int i = 0; i < league.Schedule.Games.Count; i++)
            league.Schedule.Games[i].Day = i + 1;

        SeasonSimulationService.SimulateSeason(league, new Random(42));

        var rec0 = league.Teams[0].Record;
        var rec1 = league.Teams[1].Record;

        // Total wins should equal total losses (2-team league)
        int totalWins = rec0.LeagueRecord / 100 + rec1.LeagueRecord / 100;
        int totalLosses = rec0.LeagueRecord % 100 + rec1.LeagueRecord % 100;
        totalWins.Should().Be(totalLosses);
        totalWins.Should().Be(4); // 4 games total
    }

    [Fact]
    public void SimulateSeason_PlayersHaveAccumulatedStats()
    {
        var league = CreateTwoTeamLeague(4);
        for (int i = 0; i < league.Schedule.Games.Count; i++)
            league.Schedule.Games[i].Day = i + 1;

        SeasonSimulationService.SimulateSeason(league, new Random(42));

        // Starters should have simulated stats
        var starters = league.Teams.SelectMany(t => t.Roster.Take(5))
            .Where(p => p.SimulatedStats.Games > 0)
            .ToList();

        starters.Should().NotBeEmpty();
        starters.Should().OnlyContain(p => p.SimulatedStats.Minutes > 0);
    }
}
