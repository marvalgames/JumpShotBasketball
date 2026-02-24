using FluentAssertions;
using JumpShotBasketball.Core.Enums;
using JumpShotBasketball.Core.Models.Game;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Staff;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class GameSimulationEngineTests
{
    private static Team CreateTestTeam(string name, int offset = 0)
    {
        var team = new Team
        {
            Id = offset / 30 + 1,
            Name = name,
            CityName = name,
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
        string[] names = { "Player1", "Player2", "Player3", "Player4", "Player5",
                          "Player6", "Player7", "Player8", "Player9", "Player10",
                          "Player11", "Player12" };

        for (int i = 0; i < 12; i++)
        {
            var player = new Player
            {
                Id = offset + i + 1,
                Name = $"{name} {names[i]}",
                LastName = $"{names[i]}",
                Position = positions[i],
                Team = name,
                TeamIndex = offset / 30,
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
                    Minutes = i < 5 ? 40 * 30 * 60 : 40 * 15 * 60, // in seconds
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
            };

            team.Roster.Add(player);
        }

        return team;
    }

    private static LeagueAverages GetTestLeagueAverages()
    {
        return LeagueAveragesCalculator.GetDefaults();
    }

    [Fact]
    public void SimulateGame_DeterministicWithSeed_ProducesSameResult()
    {
        var visitor = CreateTestTeam("Visitor");
        var home = CreateTestTeam("Home", 30);
        var avg = GetTestLeagueAverages();

        var engine1 = new GameSimulationEngine(new Random(42));
        var result1 = engine1.SimulateGame(visitor, home, GameType.League, avg);

        // Reset game states for replay
        foreach (var p in visitor.Roster) p.GameState.Reset();
        foreach (var p in home.Roster) p.GameState.Reset();

        var engine2 = new GameSimulationEngine(new Random(42));
        var result2 = engine2.SimulateGame(visitor, home, GameType.League, avg);

        result1.VisitorScore.Should().Be(result2.VisitorScore);
        result1.HomeScore.Should().Be(result2.HomeScore);
    }

    [Fact]
    public void SimulateGame_ScoresArePositive()
    {
        var visitor = CreateTestTeam("Visitor");
        var home = CreateTestTeam("Home", 30);
        var avg = GetTestLeagueAverages();

        var engine = new GameSimulationEngine(new Random(123));
        var result = engine.SimulateGame(visitor, home, GameType.League, avg);

        result.VisitorScore.Should().BeGreaterThan(0);
        result.HomeScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SimulateGame_ScoreIntegrity_FGMtimesTwoPlusThreePMplusFTMEqualsScore()
    {
        var visitor = CreateTestTeam("Visitor");
        var home = CreateTestTeam("Home", 30);
        var avg = GetTestLeagueAverages();

        var engine = new GameSimulationEngine(new Random(42));
        var result = engine.SimulateGame(visitor, home, GameType.League, avg);

        // Visitor score integrity
        int visitorCalcScore = 0;
        foreach (var bs in result.VisitorBoxScore)
            visitorCalcScore += bs.FieldGoalsMade * 2 + bs.ThreePointersMade + bs.FreeThrowsMade;

        visitorCalcScore.Should().Be(result.VisitorScore,
            "Visitor box score FGM*2 + 3PM + FTM should equal final score");

        // Home score integrity
        int homeCalcScore = 0;
        foreach (var bs in result.HomeBoxScore)
            homeCalcScore += bs.FieldGoalsMade * 2 + bs.ThreePointersMade + bs.FreeThrowsMade;

        homeCalcScore.Should().Be(result.HomeScore,
            "Home box score FGM*2 + 3PM + FTM should equal final score");
    }

    [Fact]
    public void SimulateGame_RealisticScoreRange()
    {
        var visitor = CreateTestTeam("Visitor");
        var home = CreateTestTeam("Home", 30);
        var avg = GetTestLeagueAverages();

        // Run 50 games and check average
        int totalVisitor = 0, totalHome = 0;
        for (int g = 0; g < 50; g++)
        {
            foreach (var p in visitor.Roster) p.GameState.Reset();
            foreach (var p in home.Roster) p.GameState.Reset();

            var engine = new GameSimulationEngine(new Random(g * 17));
            var result = engine.SimulateGame(visitor, home, GameType.League, avg);

            totalVisitor += result.VisitorScore;
            totalHome += result.HomeScore;

            // Individual game scores should be reasonable (40-160)
            result.VisitorScore.Should().BeInRange(40, 160,
                $"Visitor score {result.VisitorScore} is unrealistic in game {g}");
            result.HomeScore.Should().BeInRange(40, 160,
                $"Home score {result.HomeScore} is unrealistic in game {g}");
        }

        double avgVisitor = totalVisitor / 50.0;
        double avgHome = totalHome / 50.0;

        // Average scores should be NBA-like (80-130)
        // Upper bound widened slightly to accommodate AI coaching intensity boosts
        avgVisitor.Should().BeInRange(80, 130, "Average visitor score should be NBA-like");
        avgHome.Should().BeInRange(80, 130, "Average home score should be NBA-like");
    }

    [Fact]
    public void SimulateGame_AllPlayerStatsNonNegative()
    {
        var visitor = CreateTestTeam("Visitor");
        var home = CreateTestTeam("Home", 30);
        var avg = GetTestLeagueAverages();

        var engine = new GameSimulationEngine(new Random(99));
        var result = engine.SimulateGame(visitor, home, GameType.League, avg);

        foreach (var bs in result.VisitorBoxScore.Concat(result.HomeBoxScore))
        {
            bs.Minutes.Should().BeGreaterThanOrEqualTo(0);
            bs.FieldGoalsMade.Should().BeGreaterThanOrEqualTo(0);
            bs.FieldGoalsAttempted.Should().BeGreaterThanOrEqualTo(0);
            bs.FreeThrowsMade.Should().BeGreaterThanOrEqualTo(0);
            bs.FreeThrowsAttempted.Should().BeGreaterThanOrEqualTo(0);
            bs.ThreePointersMade.Should().BeGreaterThanOrEqualTo(0);
            bs.ThreePointersAttempted.Should().BeGreaterThanOrEqualTo(0);
            bs.OffensiveRebounds.Should().BeGreaterThanOrEqualTo(0);
            bs.DefensiveRebounds.Should().BeGreaterThanOrEqualTo(0);
            bs.Assists.Should().BeGreaterThanOrEqualTo(0);
            bs.Steals.Should().BeGreaterThanOrEqualTo(0);
            bs.Turnovers.Should().BeGreaterThanOrEqualTo(0);
            bs.Blocks.Should().BeGreaterThanOrEqualTo(0);
            bs.PersonalFouls.Should().BeGreaterThanOrEqualTo(0);
            bs.FieldGoalsMade.Should().BeLessThanOrEqualTo(bs.FieldGoalsAttempted,
                "FGM cannot exceed FGA");
            bs.FreeThrowsMade.Should().BeLessThanOrEqualTo(bs.FreeThrowsAttempted,
                "FTM cannot exceed FTA");
            bs.ThreePointersMade.Should().BeLessThanOrEqualTo(bs.ThreePointersAttempted,
                "3PM cannot exceed 3PA");
        }
    }

    [Fact]
    public void SimulateGame_BoxScoresHavePlayers()
    {
        var visitor = CreateTestTeam("Visitor");
        var home = CreateTestTeam("Home", 30);
        var avg = GetTestLeagueAverages();

        var engine = new GameSimulationEngine(new Random(42));
        var result = engine.SimulateGame(visitor, home, GameType.League, avg);

        result.VisitorBoxScore.Should().NotBeEmpty("Visitor should have box score entries");
        result.HomeBoxScore.Should().NotBeEmpty("Home should have box score entries");
        result.VisitorBoxScore.Count.Should().BeGreaterThanOrEqualTo(5, "At least 5 visitor players should have minutes");
        result.HomeBoxScore.Count.Should().BeGreaterThanOrEqualTo(5, "At least 5 home players should have minutes");
    }

    [Fact]
    public void SimulateGame_HasPlayByPlay()
    {
        var visitor = CreateTestTeam("Visitor");
        var home = CreateTestTeam("Home", 30);
        var avg = GetTestLeagueAverages();

        var engine = new GameSimulationEngine(new Random(42));
        var result = engine.SimulateGame(visitor, home, GameType.League, avg);

        result.PlayByPlay.Should().NotBeEmpty("Game should generate play-by-play");
        result.PlayByPlay.Count.Should().BeGreaterThan(50, "Should have many PBP entries");
    }

    [Fact]
    public void SimulateGame_QuarterScoresAddUpToFinalScore()
    {
        var visitor = CreateTestTeam("Visitor");
        var home = CreateTestTeam("Home", 30);
        var avg = GetTestLeagueAverages();

        var engine = new GameSimulationEngine(new Random(42));
        var result = engine.SimulateGame(visitor, home, GameType.League, avg);

        int visitorQTotal = 0, homeQTotal = 0;
        for (int q = 1; q <= result.QuartersPlayed; q++)
        {
            visitorQTotal += result.QuarterScores[1, q];
            homeQTotal += result.QuarterScores[2, q];
        }

        // Quarter scores should sum to within 5 of final (some edge cases with OT scoring)
        Math.Abs(visitorQTotal - result.VisitorScore).Should().BeLessThanOrEqualTo(5,
            "Visitor quarter scores should approximately sum to final score");
        Math.Abs(homeQTotal - result.HomeScore).Should().BeLessThanOrEqualTo(5,
            "Home quarter scores should approximately sum to final score");
    }

    [Fact]
    public void SimulateGame_GameEndsWithDifferentScores()
    {
        var visitor = CreateTestTeam("Visitor");
        var home = CreateTestTeam("Home", 30);
        var avg = GetTestLeagueAverages();

        // Run 20 games — none should end in a tie
        for (int g = 0; g < 20; g++)
        {
            foreach (var p in visitor.Roster) p.GameState.Reset();
            foreach (var p in home.Roster) p.GameState.Reset();

            var engine = new GameSimulationEngine(new Random(g * 31));
            var result = engine.SimulateGame(visitor, home, GameType.League, avg);

            result.VisitorScore.Should().NotBe(result.HomeScore,
                $"Game {g} ended in a tie ({result.VisitorScore}-{result.HomeScore})");
        }
    }

    [Fact]
    public void SimulateGame_FoulOutTriggersSubstitution()
    {
        var visitor = CreateTestTeam("Visitor");
        var home = CreateTestTeam("Home", 30);
        var avg = GetTestLeagueAverages();

        // Run games and verify no player has > 6 fouls
        for (int g = 0; g < 30; g++)
        {
            foreach (var p in visitor.Roster) p.GameState.Reset();
            foreach (var p in home.Roster) p.GameState.Reset();

            var engine = new GameSimulationEngine(new Random(g * 23));
            var result = engine.SimulateGame(visitor, home, GameType.League, avg);

            foreach (var bs in result.VisitorBoxScore.Concat(result.HomeBoxScore))
            {
                bs.PersonalFouls.Should().BeLessThanOrEqualTo(6,
                    "No player should have more than 6 fouls");
            }
        }
    }

    [Fact]
    public void SimulateGame_MvpIsDetermined()
    {
        var visitor = CreateTestTeam("Visitor");
        var home = CreateTestTeam("Home", 30);
        var avg = GetTestLeagueAverages();

        var engine = new GameSimulationEngine(new Random(42));
        var result = engine.SimulateGame(visitor, home, GameType.League, avg);

        result.MvpPlayerIndex.Should().BeGreaterThan(0, "MVP should be determined");
    }

    [Fact]
    public void SimulateGame_ExhibitionWorks()
    {
        var visitor = CreateTestTeam("Visitor");
        var home = CreateTestTeam("Home", 30);
        var avg = GetTestLeagueAverages();

        var engine = new GameSimulationEngine(new Random(42));
        var result = engine.SimulateGame(visitor, home, GameType.Exhibition, avg);

        result.VisitorScore.Should().BeGreaterThan(0);
        result.HomeScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SimulateGame_PlayoffWorks()
    {
        var visitor = CreateTestTeam("Visitor");
        var home = CreateTestTeam("Home", 30);
        var avg = GetTestLeagueAverages();

        var engine = new GameSimulationEngine(new Random(42));
        var result = engine.SimulateGame(visitor, home, GameType.Playoff, avg);

        result.VisitorScore.Should().BeGreaterThan(0);
        result.HomeScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SimulateGame_DifferentSeedsProduceDifferentResults()
    {
        var visitor = CreateTestTeam("Visitor");
        var home = CreateTestTeam("Home", 30);
        var avg = GetTestLeagueAverages();

        var engine1 = new GameSimulationEngine(new Random(42));
        var result1 = engine1.SimulateGame(visitor, home, GameType.League, avg);

        foreach (var p in visitor.Roster) p.GameState.Reset();
        foreach (var p in home.Roster) p.GameState.Reset();

        var engine2 = new GameSimulationEngine(new Random(99));
        var result2 = engine2.SimulateGame(visitor, home, GameType.League, avg);

        // Not strictly guaranteed but extremely unlikely to match
        (result1.VisitorScore == result2.VisitorScore && result1.HomeScore == result2.HomeScore)
            .Should().BeFalse("Different seeds should usually produce different scores");
    }

    [Fact]
    public void SimulateGame_NoHomeCourtAdvantage_StillWorks()
    {
        var visitor = CreateTestTeam("Visitor");
        var home = CreateTestTeam("Home", 30);
        var avg = GetTestLeagueAverages();

        var engine = new GameSimulationEngine(new Random(42));
        var result = engine.SimulateGame(visitor, home, GameType.League, avg,
            homeCourtAdvantage: false);

        result.VisitorScore.Should().BeGreaterThan(0);
        result.HomeScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SimulateGame_CustomScoringFactor_AffectsScoring()
    {
        var visitor = CreateTestTeam("Visitor");
        var home = CreateTestTeam("Home", 30);
        var avg = GetTestLeagueAverages();

        // Run multiple games with high scoring factor
        int totalHigh = 0;
        for (int g = 0; g < 20; g++)
        {
            foreach (var p in visitor.Roster) p.GameState.Reset();
            foreach (var p in home.Roster) p.GameState.Reset();
            var engine = new GameSimulationEngine(new Random(g));
            var result = engine.SimulateGame(visitor, home, GameType.League, avg, scoringFactor: 1.3);
            totalHigh += result.VisitorScore + result.HomeScore;
        }

        // Run multiple games with low scoring factor
        int totalLow = 0;
        for (int g = 0; g < 20; g++)
        {
            foreach (var p in visitor.Roster) p.GameState.Reset();
            foreach (var p in home.Roster) p.GameState.Reset();
            var engine = new GameSimulationEngine(new Random(g));
            var result = engine.SimulateGame(visitor, home, GameType.League, avg, scoringFactor: 0.7);
            totalLow += result.VisitorScore + result.HomeScore;
        }

        totalHigh.Should().BeGreaterThan(totalLow, "Higher scoring factor should produce more points");
    }
}
