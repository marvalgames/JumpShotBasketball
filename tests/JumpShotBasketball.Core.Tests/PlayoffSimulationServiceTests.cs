using FluentAssertions;
using JumpShotBasketball.Core.Enums;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Staff;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class PlayoffSimulationServiceTests
{
    private static Team CreateFullTeam(string name, int teamIndex, string conference, string division, int wins)
    {
        int losses = 82 - wins;
        var team = new Team
        {
            Id = teamIndex + 1,
            Name = name,
            CityName = name,
            Record = new TeamRecord
            {
                TeamName = name,
                Control = "Computer",
                Conference = conference,
                Division = division,
                InitialNumber = teamIndex,
                LeagueRecord = wins * 100 + losses,
                Wins = wins,
                Losses = losses,
                LeaguePercentage = (double)wins / 82,
                DivisionRecord = wins * 100 + losses,
                DivisionPercentage = (double)wins / 82,
                ConferenceRecord = wins * 100 + losses,
                ConferencePercentage = (double)wins / 82
            },
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
                    Games = 82,
                    Minutes = i < 5 ? 82 * 30 : 82 * 15,
                    FieldGoalsMade = 400 + i * 10,
                    FieldGoalsAttempted = 900 + i * 20,
                    FreeThrowsMade = 160 + i * 5,
                    FreeThrowsAttempted = 200 + i * 5,
                    ThreePointersMade = 80 + i * 5,
                    ThreePointersAttempted = 240 + i * 10,
                    OffensiveRebounds = 80 + i * 5,
                    Rebounds = 400 + i * 15,
                    Assists = 300 + (i < 2 ? 100 : 0),
                    Steals = 80 + i * 3,
                    Turnovers = 120 + i * 3,
                    Blocks = 40 + i * 5,
                    PersonalFouls = 160 + i * 3
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

    private static League CreateSmallPlayoffLeague()
    {
        // 4 teams: 2 per conference, 1 per division
        // Minimizes sim time for tests: 2 teams per conference = 2 series, then finals
        var league = new League
        {
            Settings = new LeagueSettings
            {
                NumberOfTeams = 4,
                PlayoffFormat = "1 team per conference",
                Round1Format = "4 of 7",
                Round2Format = "None",
                Round3Format = "None",
                Round4Format = "None",
                ConferenceName1 = "East",
                ConferenceName2 = "West",
                DivisionName1 = "Atlantic",
                DivisionName2 = "Central",
                DivisionName3 = "Pacific",
                DivisionName4 = "Southwest"
            }
        };

        league.Teams.Add(CreateFullTeam("Hawks", 0, "East", "Atlantic", 55));
        league.Teams.Add(CreateFullTeam("Celtics", 1, "East", "Central", 45));
        league.Teams.Add(CreateFullTeam("Lakers", 2, "West", "Pacific", 50));
        league.Teams.Add(CreateFullTeam("Spurs", 3, "West", "Southwest", 40));

        // Set regular season as ended
        league.Schedule.RegularSeasonEnded = true;

        return league;
    }

    // ── StartPlayoffs ──────────────────────────────────────────────

    [Fact]
    public void StartPlayoffs_CreatesBracketAndAttachesToLeague()
    {
        var league = CreateSmallPlayoffLeague();

        var bracket = PlayoffSimulationService.StartPlayoffs(league);

        league.Bracket.Should().NotBeNull();
        league.Bracket.Should().BeSameAs(bracket);
        league.Schedule.PlayoffsStarted.Should().BeTrue();
        bracket.PlayoffsStarted.Should().BeTrue();
    }

    // ── SimulateGame ───────────────────────────────────────────────

    [Fact]
    public void SimulateGame_ReturnsValidResult()
    {
        var league = CreateSmallPlayoffLeague();
        PlayoffSimulationService.StartPlayoffs(league);

        var result = PlayoffSimulationService.SimulateGame(league, new Random(42));

        result.Should().NotBeNull();
        result!.HomeScore.Should().BeGreaterThan(0);
        result.VisitorScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SimulateGame_RecordsResultInBracket()
    {
        var league = CreateSmallPlayoffLeague();
        PlayoffSimulationService.StartPlayoffs(league);

        PlayoffSimulationService.SimulateGame(league, new Random(42));

        var series = league.Bracket!.Rounds[0].Series[0];
        series.Games[0].Played.Should().BeTrue();
        (series.HigherSeedWins + series.LowerSeedWins).Should().Be(1);
    }

    [Fact]
    public void SimulateGame_AccumulatesPlayoffStats()
    {
        var league = CreateSmallPlayoffLeague();
        PlayoffSimulationService.StartPlayoffs(league);

        PlayoffSimulationService.SimulateGame(league, new Random(42));

        // At least some players should have playoff stats
        var playersWithPlayoffStats = league.Teams.SelectMany(t => t.Roster)
            .Where(p => p.PlayoffStats.Games > 0)
            .ToList();

        playersWithPlayoffStats.Should().NotBeEmpty();
    }

    [Fact]
    public void SimulateGame_NoBracket_ReturnsNull()
    {
        var league = CreateSmallPlayoffLeague();
        // No bracket set

        var result = PlayoffSimulationService.SimulateGame(league, new Random(42));

        result.Should().BeNull();
    }

    // ── SimulateDay ────────────────────────────────────────────────

    [Fact]
    public void SimulateDay_SimulatesOneGamePerActiveSeries()
    {
        var league = CreateSmallPlayoffLeague();
        PlayoffSimulationService.StartPlayoffs(league);

        var result = PlayoffSimulationService.SimulateDay(league, new Random(42));

        // 1 series (1 per conference final) = 1 game per day
        result.GamesSimulated.Should().Be(1);
    }

    // ── SimulateAll ────────────────────────────────────────────────

    [Fact]
    public void SimulateAll_CompletesPlayoffs_CrownsChampion()
    {
        var league = CreateSmallPlayoffLeague();
        PlayoffSimulationService.StartPlayoffs(league);

        var result = PlayoffSimulationService.SimulateAll(league, new Random(42));

        result.PlayoffsComplete.Should().BeTrue();
        result.ChampionTeamIndex.Should().NotBeNull();
        result.GamesSimulated.Should().BeGreaterThanOrEqualTo(4); // at minimum a 4-0 sweep

        // Champion should have ring
        league.Teams[result.ChampionTeamIndex!.Value].Record.HasRing.Should().BeTrue();

        // Exactly one team should have a ring
        league.Teams.Count(t => t.Record.HasRing).Should().Be(1);
    }

    [Fact]
    public void SimulateAll_Deterministic_SameChampionWithSameSeed()
    {
        var league1 = CreateSmallPlayoffLeague();
        var league2 = CreateSmallPlayoffLeague();
        PlayoffSimulationService.StartPlayoffs(league1);
        PlayoffSimulationService.StartPlayoffs(league2);

        var result1 = PlayoffSimulationService.SimulateAll(league1, new Random(42));
        var result2 = PlayoffSimulationService.SimulateAll(league2, new Random(42));

        result1.ChampionTeamIndex.Should().Be(result2.ChampionTeamIndex);
        result1.GamesSimulated.Should().Be(result2.GamesSimulated);

        // All game scores should match
        for (int i = 0; i < result1.GameResults.Count; i++)
        {
            result1.GameResults[i].HomeScore.Should().Be(result2.GameResults[i].HomeScore);
            result1.GameResults[i].VisitorScore.Should().Be(result2.GameResults[i].VisitorScore);
        }
    }

    [Fact]
    public void SimulateAll_StatsAccumulateToPlayoffStats_NotSimulatedStats()
    {
        var league = CreateSmallPlayoffLeague();
        // Clear any simulated stats first
        foreach (var team in league.Teams)
            foreach (var player in team.Roster)
                player.SimulatedStats = new PlayerStatLine();

        PlayoffSimulationService.StartPlayoffs(league);
        PlayoffSimulationService.SimulateAll(league, new Random(42));

        // Playoff stats should be populated
        var anyPlayoffStats = league.Teams.SelectMany(t => t.Roster)
            .Any(p => p.PlayoffStats.Games > 0);
        anyPlayoffStats.Should().BeTrue();

        // SimulatedStats should remain zeroed (we cleared them, and playoff games don't add to SimulatedStats)
        var allSimStatsZero = league.Teams.SelectMany(t => t.Roster)
            .All(p => p.SimulatedStats.Games == 0);
        allSimStatsZero.Should().BeTrue();
    }

    [Fact]
    public void SimulateAll_AllPlayoffTeamsMarkedInPlayoffs()
    {
        var league = CreateSmallPlayoffLeague();
        PlayoffSimulationService.StartPlayoffs(league);
        PlayoffSimulationService.SimulateAll(league, new Random(42));

        // The two playoff teams should be marked InPlayoffs
        var inPlayoffs = league.Teams.Where(t => t.Record.InPlayoffs).ToList();
        inPlayoffs.Should().HaveCount(2); // 1 per conference

        // Non-playoff teams should not be marked
        var notInPlayoffs = league.Teams.Where(t => !t.Record.InPlayoffs).ToList();
        notInPlayoffs.Should().HaveCount(2);
    }
}
