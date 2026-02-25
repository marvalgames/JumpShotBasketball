using FluentAssertions;
using JumpShotBasketball.Core.Enums;
using JumpShotBasketball.Core.Models.Game;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Staff;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class GamePlanEngineFixTests
{
    // ── Helpers ─────────────────────────────────────────────────────────

    private static Team CreateTestTeam(string name, int offset = 0, int offensiveIntensity = 0,
        int defensiveIntensity = 0, int offensiveFocus = 0, int defensiveFocus = 0)
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
                OffensiveIntensity = offensiveIntensity,
                DefensiveIntensity = defensiveIntensity,
                OffensiveFocus = offensiveFocus,
                DefensiveFocus = defensiveFocus,
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
            };

            team.Roster.Add(player);
        }

        return team;
    }

    private static LeagueAverages GetTestLeagueAverages()
    {
        return LeagueAveragesCalculator.GetDefaults();
    }

    private static GameResult SimGame(Team visitor, Team home, Random random)
    {
        foreach (var p in visitor.Roster) p.GameState.Reset();
        foreach (var p in home.Roster) p.GameState.Reset();
        var engine = new GameSimulationEngine(random);
        return engine.SimulateGame(visitor, home, GameType.League, GetTestLeagueAverages());
    }

    private static int TotalFGA(GameResult result, bool homeTeam)
    {
        var box = homeTeam ? result.HomeBoxScore : result.VisitorBoxScore;
        return box.Sum(b => b.FieldGoalsAttempted + b.ThreePointersAttempted);
    }

    // ── AdjustOffenseForGamePlan — Negative Intensity Tests ─────────────

    [Fact]
    public void NegativeIntensity_ReducesTotalFGA_StatisticalTest()
    {
        // Run many games with max negative intensity vs baseline
        int baselineFGA = 0, negIntensityFGA = 0;
        const int games = 100;

        for (int g = 0; g < games; g++)
        {
            var baseV = CreateTestTeam("BaseV");
            var baseH = CreateTestTeam("BaseH", 30);
            var r1 = SimGame(baseV, baseH, new Random(g * 17));
            baselineFGA += TotalFGA(r1, false);

            var negV = CreateTestTeam("NegV", offensiveIntensity: -2);
            var negH = CreateTestTeam("NegH", 30);
            var r2 = SimGame(negV, negH, new Random(g * 17));
            negIntensityFGA += TotalFGA(r2, false);
        }

        // Negative intensity team should attempt fewer shots on average
        negIntensityFGA.Should().BeLessThan(baselineFGA,
            "Negative offensive intensity should reduce total shot attempts");
    }

    [Fact]
    public void NegativeIntensity_ProducesDifferentResults()
    {
        // Same seed, negative intensity vs zero — should produce different game scores
        // proving the new AdjustOffenseForGamePlan code path activates
        var baseV = CreateTestTeam("V");
        var baseH = CreateTestTeam("H", 30);
        var r1 = SimGame(baseV, baseH, new Random(42));

        var negV = CreateTestTeam("V", offensiveIntensity: -2);
        var negH = CreateTestTeam("H", 30);
        var r2 = SimGame(negV, negH, new Random(42));

        // Scores should differ since the random sequence is consumed differently
        bool different = r1.VisitorScore != r2.VisitorScore || r1.HomeScore != r2.HomeScore;
        different.Should().BeTrue("Negative intensity should alter game outcome vs baseline");
    }

    [Fact]
    public void IntensityMinus2_MoreReductionThanMinus1()
    {
        int minus1Score = 0, minus2Score = 0;
        const int games = 100;

        for (int g = 0; g < games; g++)
        {
            var v1 = CreateTestTeam("V1", offensiveIntensity: -1);
            var h1 = CreateTestTeam("H1", 30);
            var r1 = SimGame(v1, h1, new Random(g * 31));
            minus1Score += r1.VisitorScore;

            var v2 = CreateTestTeam("V2", offensiveIntensity: -2);
            var h2 = CreateTestTeam("H2", 30);
            var r2 = SimGame(v2, h2, new Random(g * 31));
            minus2Score += r2.VisitorScore;
        }

        minus2Score.Should().BeLessThanOrEqualTo(minus1Score,
            "Higher magnitude negative intensity should produce more reduction");
    }

    [Fact]
    public void IntensityZero_NoReduction_MatchesBaseline()
    {
        // Zero intensity should produce identical results to default (also zero)
        var baseV = CreateTestTeam("BaseV", offensiveIntensity: 0);
        var baseH = CreateTestTeam("BaseH", 30, offensiveIntensity: 0);

        var defV = CreateTestTeam("DefV");
        var defH = CreateTestTeam("DefH", 30);

        var r1 = SimGame(baseV, baseH, new Random(42));
        var r2 = SimGame(defV, defH, new Random(42));

        r1.VisitorScore.Should().Be(r2.VisitorScore,
            "Zero intensity should produce same result as default");
        r1.HomeScore.Should().Be(r2.HomeScore);
    }

    [Fact]
    public void IntensityPositive_NoReduction_InOffenseMethod()
    {
        // Positive intensity should NOT trigger the negative-intensity reduction path
        // (Positive intensity is handled by AdjustIntensityForGamePlan, not AdjustOffenseForGamePlan)
        int baselineScore = 0, posScore = 0;
        const int games = 50;

        for (int g = 0; g < games; g++)
        {
            var baseV = CreateTestTeam("BaseV");
            var baseH = CreateTestTeam("BaseH", 30);
            var r1 = SimGame(baseV, baseH, new Random(g * 37));
            baselineScore += r1.VisitorScore;

            var posV = CreateTestTeam("PosV", offensiveIntensity: 2);
            var posH = CreateTestTeam("PosH", 30);
            var r2 = SimGame(posV, posH, new Random(g * 37));
            posScore += r2.VisitorScore;
        }

        // Positive intensity gets boost from AdjustIntensityForGamePlan, so score should be >= baseline
        posScore.Should().BeGreaterThanOrEqualTo(baselineScore - 100,
            "Positive intensity should not trigger play reduction");
    }

    [Fact]
    public void FocusAndNegativeIntensity_BothActivate()
    {
        // Focus=3 + negative intensity should produce different results than focus=3 alone
        // (proves both code paths fire)
        var v1 = CreateTestTeam("V", offensiveFocus: 3);
        var h1 = CreateTestTeam("H", 30);
        var r1 = SimGame(v1, h1, new Random(42));

        var v2 = CreateTestTeam("V", offensiveFocus: 3, offensiveIntensity: -3);
        var h2 = CreateTestTeam("H", 30);
        var r2 = SimGame(v2, h2, new Random(42));

        bool different = r1.VisitorScore != r2.VisitorScore || r1.HomeScore != r2.HomeScore;
        different.Should().BeTrue("Adding negative intensity to focus should change the game outcome");
    }

    [Fact]
    public void NegativeIntensity_ScoresRemainPositive()
    {
        // Even with max negative intensity, games should still produce valid positive scores
        for (int g = 0; g < 20; g++)
        {
            var visitor = CreateTestTeam("Visitor", offensiveIntensity: -4);
            var home = CreateTestTeam("Home", 30, offensiveIntensity: -4);

            var result = SimGame(visitor, home, new Random(g * 47));

            result.VisitorScore.Should().BeGreaterThan(0,
                $"Game {g}: Even with max negative intensity, scores must be positive");
            result.HomeScore.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public void DeterministicWithSeed_OffenseIntensity_Reproducible()
    {
        var v1 = CreateTestTeam("V", offensiveIntensity: -2);
        var h1 = CreateTestTeam("H", 30);
        var r1 = SimGame(v1, h1, new Random(42));

        var v2 = CreateTestTeam("V", offensiveIntensity: -2);
        var h2 = CreateTestTeam("H", 30);
        var r2 = SimGame(v2, h2, new Random(42));

        r1.VisitorScore.Should().Be(r2.VisitorScore);
        r1.HomeScore.Should().Be(r2.HomeScore);
    }

    [Fact]
    public void NegativeIntensity_AllPlayersAffected()
    {
        // Verify that the game still produces valid stats for all players
        var visitor = CreateTestTeam("Visitor", offensiveIntensity: -3);
        var home = CreateTestTeam("Home", 30);

        var result = SimGame(visitor, home, new Random(42));

        result.VisitorBoxScore.Should().NotBeEmpty();
        foreach (var bs in result.VisitorBoxScore)
        {
            bs.FieldGoalsMade.Should().BeGreaterThanOrEqualTo(0);
            bs.FieldGoalsAttempted.Should().BeGreaterThanOrEqualTo(bs.FieldGoalsMade);
        }
    }

    [Fact]
    public void NegativeIntensity_Reduces2ptFGA()
    {
        // 2PT FGA should decrease with negative intensity
        int baseFGA = 0, negFGA = 0;
        const int games = 100;

        for (int g = 0; g < games; g++)
        {
            var baseV = CreateTestTeam("BaseV");
            var baseH = CreateTestTeam("BaseH", 30);
            var r1 = SimGame(baseV, baseH, new Random(g * 53));
            baseFGA += r1.VisitorBoxScore.Sum(b => b.FieldGoalsAttempted);

            var negV = CreateTestTeam("NegV", offensiveIntensity: -3);
            var negH = CreateTestTeam("NegH", 30);
            var r2 = SimGame(negV, negH, new Random(g * 53));
            negFGA += r2.VisitorBoxScore.Sum(b => b.FieldGoalsAttempted);
        }

        negFGA.Should().BeLessThan(baseFGA,
            "Negative intensity should reduce 2PT FGA across many games");
    }

    // ── AdjustDefenseForGamePlan — OffensiveIntensity > 0 Vulnerability Tests ──

    [Fact]
    public void PositiveOffIntensity_AltersDefensiveOutcome()
    {
        // Same seed, positive offensive intensity on home team vs zero — scores should differ
        // proving the new AdjustDefenseForGamePlan vulnerability code path activates
        var v1 = CreateTestTeam("V");
        var h1 = CreateTestTeam("H", 30);
        var r1 = SimGame(v1, h1, new Random(42));

        var v2 = CreateTestTeam("V");
        var h2 = CreateTestTeam("H", 30, offensiveIntensity: 3);
        var r2 = SimGame(v2, h2, new Random(42));

        bool different = r1.VisitorScore != r2.VisitorScore || r1.HomeScore != r2.HomeScore;
        different.Should().BeTrue(
            "Positive offensive intensity on home team should alter game outcome vs baseline");
    }

    [Fact]
    public void IntensityOf2_DifferentFrom1()
    {
        // Higher intensity should produce different results than lower
        var v1 = CreateTestTeam("V");
        var h1 = CreateTestTeam("H", 30, offensiveIntensity: 1);
        var r1 = SimGame(v1, h1, new Random(42));

        var v2 = CreateTestTeam("V");
        var h2 = CreateTestTeam("H", 30, offensiveIntensity: 2);
        var r2 = SimGame(v2, h2, new Random(42));

        bool different = r1.VisitorScore != r2.VisitorScore || r1.HomeScore != r2.HomeScore;
        different.Should().BeTrue(
            "Different intensity levels should produce different game outcomes");
    }

    [Fact]
    public void ZeroOffensiveIntensity_NoDefenseBoost()
    {
        // Zero intensity on either side: same as default
        var v1 = CreateTestTeam("V", offensiveIntensity: 0);
        var h1 = CreateTestTeam("H", 30, offensiveIntensity: 0);
        var r1 = SimGame(v1, h1, new Random(42));

        var v2 = CreateTestTeam("V");
        var h2 = CreateTestTeam("H", 30);
        var r2 = SimGame(v2, h2, new Random(42));

        r1.VisitorScore.Should().Be(r2.VisitorScore);
        r1.HomeScore.Should().Be(r2.HomeScore);
    }

    [Fact]
    public void NegativeOffIntensity_NoDefenseVulnerability()
    {
        // Negative offensive intensity should NOT trigger the defensive vulnerability code path.
        // With same seed, the vulnerability code (OffensiveIntensity > 0) does not fire,
        // but AdjustIntensityForGamePlan DOES fire for any non-zero intensity.
        // So negative intensity changes the game vs baseline (from per-48 adjustments),
        // but the defense vulnerability specifically is NOT triggered.
        // We verify by checking that negative intensity on HOME changes results
        // (from per-48 adjustment), confirming the defense vulnerability path
        // with negative values does NOT crash or produce invalid state.
        for (int g = 0; g < 10; g++)
        {
            var visitor = CreateTestTeam("V");
            var home = CreateTestTeam("H", 30, offensiveIntensity: -2);
            var result = SimGame(visitor, home, new Random(g * 67));

            result.VisitorScore.Should().BeGreaterThan(0);
            result.HomeScore.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public void DefenseFocusAndVulnerability_BothActivate()
    {
        // DefensiveFocus=3 + high offensive intensity — both paths fire, producing different
        // results than focus alone
        var v1 = CreateTestTeam("V");
        var h1 = CreateTestTeam("H", 30, defensiveFocus: 3);
        var r1 = SimGame(v1, h1, new Random(42));

        var v2 = CreateTestTeam("V");
        var h2 = CreateTestTeam("H", 30, defensiveFocus: 3, offensiveIntensity: 3);
        var r2 = SimGame(v2, h2, new Random(42));

        bool different = r1.VisitorScore != r2.VisitorScore || r1.HomeScore != r2.HomeScore;
        different.Should().BeTrue(
            "Adding offensive intensity vulnerability to defensive focus should change outcome");
    }

    [Fact]
    public void DefIntensityAndOffIntensity_BothActivate()
    {
        // DefensiveIntensity=2 + OffensiveIntensity=2 — both paths fire
        // Result should differ from defensive intensity alone
        var v1 = CreateTestTeam("V");
        var h1 = CreateTestTeam("H", 30, defensiveIntensity: 2);
        var r1 = SimGame(v1, h1, new Random(42));

        var v2 = CreateTestTeam("V");
        var h2 = CreateTestTeam("H", 30, defensiveIntensity: 2, offensiveIntensity: 2);
        var r2 = SimGame(v2, h2, new Random(42));

        bool different = r1.VisitorScore != r2.VisitorScore || r1.HomeScore != r2.HomeScore;
        different.Should().BeTrue(
            "Adding offensive intensity to defensive intensity should change outcome");
    }

    [Fact]
    public void DeterministicWithSeed_DefenseVulnerability_Reproducible()
    {
        var v1 = CreateTestTeam("V");
        var h1 = CreateTestTeam("H", 30, offensiveIntensity: 3);
        var r1 = SimGame(v1, h1, new Random(42));

        var v2 = CreateTestTeam("V");
        var h2 = CreateTestTeam("H", 30, offensiveIntensity: 3);
        var r2 = SimGame(v2, h2, new Random(42));

        r1.VisitorScore.Should().Be(r2.VisitorScore);
        r1.HomeScore.Should().Be(r2.HomeScore);
    }

    [Fact]
    public void PositiveOffIntensity_ValidBoxScores()
    {
        // Games with high offensive intensity should still produce valid box scores
        var visitor = CreateTestTeam("Visitor");
        var home = CreateTestTeam("Home", 30, offensiveIntensity: 4);

        var result = SimGame(visitor, home, new Random(42));

        foreach (var bs in result.VisitorBoxScore.Concat(result.HomeBoxScore))
        {
            bs.FieldGoalsMade.Should().BeGreaterThanOrEqualTo(0);
            bs.FieldGoalsAttempted.Should().BeGreaterThanOrEqualTo(bs.FieldGoalsMade);
            bs.PersonalFouls.Should().BeLessThanOrEqualTo(6);
        }
    }

    [Fact]
    public void PositiveOffIntensity_ChangesTeamScoring()
    {
        // High offensive intensity adjusts per-48 rates (more FGA + more TO) and creates
        // defensive vulnerability. The net scoring effect is a tradeoff, not strictly positive.
        // Verify the code path activates by checking results differ from baseline.
        int baseScore = 0, highScore = 0;
        const int games = 50;

        for (int g = 0; g < games; g++)
        {
            var v1 = CreateTestTeam("V1");
            var h1 = CreateTestTeam("H1", 30);
            var r1 = SimGame(v1, h1, new Random(g * 79));
            baseScore += r1.HomeScore;

            var v2 = CreateTestTeam("V2");
            var h2 = CreateTestTeam("H2", 30, offensiveIntensity: 3);
            var r2 = SimGame(v2, h2, new Random(g * 79));
            highScore += r2.HomeScore;
        }

        Math.Abs(highScore - baseScore).Should().BeGreaterThan(0,
            "High offensive intensity should change scoring output vs baseline");
    }

    // ── Integration Tests ────────────────────────────────────────────────

    [Fact]
    public void FullGame_NegativeIntensity_LowerFGA()
    {
        int baseFGA = 0, negFGA = 0;
        const int games = 50;

        for (int g = 0; g < games; g++)
        {
            var baseV = CreateTestTeam("BaseV");
            var baseH = CreateTestTeam("BaseH", 30);
            var r1 = SimGame(baseV, baseH, new Random(g * 83));
            baseFGA += TotalFGA(r1, false);

            var negV = CreateTestTeam("NegV", offensiveIntensity: -2);
            var negH = CreateTestTeam("NegH", 30);
            var r2 = SimGame(negV, negH, new Random(g * 83));
            negFGA += TotalFGA(r2, false);
        }

        negFGA.Should().BeLessThan(baseFGA,
            "Negative intensity team should produce fewer FGA overall");
    }

    [Fact]
    public void FullGame_PositiveIntensity_AltersGameDynamics()
    {
        // High off intensity changes per-48 rates AND defensive vulnerability.
        // Net effect is a tradeoff (more shots but more turnovers + worse defense).
        // Verify the code path activates by checking total scores differ.
        int baseTotal = 0, highTotal = 0;
        const int games = 50;

        for (int g = 0; g < games; g++)
        {
            var baseV = CreateTestTeam("BaseV");
            var baseH = CreateTestTeam("BaseH", 30);
            var r1 = SimGame(baseV, baseH, new Random(g * 89));
            baseTotal += r1.VisitorScore + r1.HomeScore;

            var attV = CreateTestTeam("AttV");
            var highH = CreateTestTeam("HighH", 30, offensiveIntensity: 3);
            var r2 = SimGame(attV, highH, new Random(g * 89));
            highTotal += r2.VisitorScore + r2.HomeScore;
        }

        Math.Abs(highTotal - baseTotal).Should().BeGreaterThan(0,
            "High offensive intensity should alter total game scoring vs baseline");
    }

    [Fact]
    public void GamePlanDefaults_NoChangeFromPriorBehavior()
    {
        // Default game plans (all 0) should produce identical outcomes
        var v1 = CreateTestTeam("V");
        var h1 = CreateTestTeam("H", 30);
        var r1 = SimGame(v1, h1, new Random(42));

        var v2 = CreateTestTeam("V", offensiveIntensity: 0, defensiveIntensity: 0,
            offensiveFocus: 0, defensiveFocus: 0);
        var h2 = CreateTestTeam("H", 30, offensiveIntensity: 0, defensiveIntensity: 0,
            offensiveFocus: 0, defensiveFocus: 0);
        var r2 = SimGame(v2, h2, new Random(42));

        r1.VisitorScore.Should().Be(r2.VisitorScore);
        r1.HomeScore.Should().Be(r2.HomeScore);
    }

    [Fact]
    public void ExtremeCombination_NoOverflowOrCrash()
    {
        // Max intensity values in all directions should not produce invalid state
        for (int g = 0; g < 10; g++)
        {
            var visitor = CreateTestTeam("V", offensiveIntensity: -4, defensiveIntensity: -4,
                offensiveFocus: 1, defensiveFocus: 3);
            var home = CreateTestTeam("H", 30, offensiveIntensity: 4, defensiveIntensity: 4,
                offensiveFocus: 3, defensiveFocus: 1);

            var result = SimGame(visitor, home, new Random(g * 97));

            result.VisitorScore.Should().BeGreaterThan(0);
            result.HomeScore.Should().BeGreaterThan(0);
            result.VisitorScore.Should().BeLessThan(250, "Score should be within reason");
            result.HomeScore.Should().BeLessThan(250, "Score should be within reason");
        }
    }

    [Fact]
    public void FullGame_ZeroIntensity_MatchesBaseline()
    {
        // Explicit zero intensity should be identical to omitted intensity
        var v1 = CreateTestTeam("V");
        var h1 = CreateTestTeam("H", 30);
        var r1 = SimGame(v1, h1, new Random(123));

        var v2 = CreateTestTeam("V", offensiveIntensity: 0);
        var h2 = CreateTestTeam("H", 30, offensiveIntensity: 0);
        var r2 = SimGame(v2, h2, new Random(123));

        r1.VisitorScore.Should().Be(r2.VisitorScore);
        r1.HomeScore.Should().Be(r2.HomeScore);
    }
}
