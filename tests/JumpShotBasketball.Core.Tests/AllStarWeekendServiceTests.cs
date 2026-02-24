using FluentAssertions;
using JumpShotBasketball.Core.Enums;
using JumpShotBasketball.Core.Models.Game;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class AllStarWeekendServiceTests
{
    // ───────────────────────────────────────────────────────────────
    // Test helpers
    // ───────────────────────────────────────────────────────────────

    private static Models.League.League CreateTestLeague(
        int teamCount = 4, int playersPerTeam = 15, bool withRookies = false)
    {
        var league = new Models.League.League();
        league.Settings.ConferenceName1 = "East";
        league.Settings.ConferenceName2 = "West";
        league.Schedule.GamesInSeason = 82;

        for (int t = 0; t < teamCount; t++)
        {
            string conf = t < teamCount / 2 ? "East" : "West";
            var team = new Models.Team.Team
            {
                Id = t,
                Name = $"Team {t}",
                Record = new Models.Team.TeamRecord
                {
                    Conference = conf,
                    Division = $"Div{t % 2}"
                }
            };

            for (int p = 0; p < playersPerTeam; p++)
            {
                int id = t * 100 + p;
                var player = CreatePlayer(id, t, p, withRookies);
                team.Roster.Add(player);
            }

            league.Teams.Add(team);
        }

        return league;
    }

    private static Player CreatePlayer(int id, int teamIndex, int slot, bool withRookies = false)
    {
        string pos = slot switch
        {
            0 => "PG",
            1 => "SG",
            2 => "SF",
            3 => "PF",
            4 => "C",
            _ => new[] { "PG", "SG", "SF", "PF", "C" }[slot % 5]
        };

        double factor = 1.0 - (slot * 0.04);
        if (factor < 0.2) factor = 0.2;

        // For rookies: alternate YOS=1 and YOS=2 across positions for good distribution
        int yrs = withRookies && slot < 10 ? (slot % 2 == 0 ? 1 : 2) : 5;

        return new Player
        {
            Id = id,
            Name = $"Player {id}",
            Position = pos,
            TeamIndex = teamIndex,
            Age = 25,
            Active = true,
            Health = 100,
            Injury = 0,
            Contract = new PlayerContract { YearsOfService = yrs },
            SimulatedStats = new PlayerStatLine
            {
                Games = 70,
                Minutes = (int)(2800 * factor),
                FieldGoalsMade = (int)(400 * factor),
                FieldGoalsAttempted = (int)(900 * factor),
                FreeThrowsMade = (int)(200 * factor),
                FreeThrowsAttempted = (int)(250 * factor),
                ThreePointersMade = (int)(150 * factor),
                ThreePointersAttempted = (int)(400 * factor),
                OffensiveRebounds = (int)(80 * factor),
                Rebounds = (int)(400 * factor),
                Assists = (int)(250 * factor),
                Steals = (int)(80 * factor),
                Turnovers = (int)(50 * factor),
                Blocks = (int)(40 * factor),
                PersonalFouls = (int)(120 * factor)
            },
            SeasonStats = new PlayerStatLine
            {
                Games = 70,
                Minutes = (int)(2800 * factor)
            },
            Ratings = new PlayerRatings
            {
                ProjectionThreePointersAttempted = 50 + slot * 2,
                ProjectionThreePointPercentage = 35 + slot,
                ProjectionFieldGoalsAttempted = 100 + slot * 5,
                ProjectionFreeThrowsAttempted = 50 + slot * 3,
                ProjectionOffensiveRebounds = 20 + slot,
                ProjectionDefensiveRebounds = 40 + slot * 2,
                TransitionOffenseRaw = 10 + slot,
                TransitionDefenseRaw = 8 + slot,
                PenetrationDefenseRaw = 12 + slot,
                MovementDefenseRaw = 10 + slot,
                PostDefenseRaw = 8 + slot,
                FieldGoalPercentage = 45,
                FreeThrowPercentage = 80,
                ThreePointPercentage = 35,
                Stamina = 85,
                Consistency = 50,
                FieldGoalsAttemptedPer48Min = 12.0,
                AdjustedFieldGoalsAttemptedPer48Min = 12.0,
                ThreePointersAttemptedPer48Min = 4.0,
                AdjustedThreePointersAttemptedPer48Min = 4.0,
                FoulsDrawnPer48Min = 3.0,
                AdjustedFoulsDrawnPer48Min = 3.0,
                OffensiveReboundsPer48Min = 1.5,
                DefensiveReboundsPer48Min = 4.0,
                AssistsPer48Min = 3.0,
                StealsPer48Min = 1.0,
                TurnoversPer48Min = 2.0,
                AdjustedTurnoversPer48Min = 2.0,
                BlocksPer48Min = 0.5,
                PersonalFoulsPer48Min = 2.5,
                MinutesPerGame = 30.0
            }
        };
    }

    private static LeagueAverages CreateTestAverages()
    {
        return new LeagueAverages
        {
            FieldGoalsAttempted = 12.0,
            ThreePointersAttempted = 4.0,
            FieldGoalPercentageByPosition = new double[] { 450, 440, 445, 460, 470, 480 },
            OffensiveRebounds = 1.5,
            DefensiveRebounds = 4.0,
            AssistsByPosition = new double[] { 3.0, 5.0, 3.5, 2.5, 2.0, 1.5 },
            Steals = 1.0,
            Turnovers = 2.0,
            Blocks = 0.5,
            PersonalFouls = 2.5,
            FreeThrowsAttempted = 3.0
        };
    }

    // ───────────────────────────────────────────────────────────────
    // All-Star Selection Tests
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void SelectAllStarTeams_ReturnsPlayersFromBothConferences()
    {
        var league = CreateTestLeague(teamCount: 4, playersPerTeam: 15);

        var (conf1, conf2) = AllStarWeekendService.SelectAllStarTeams(league);

        conf1.Should().NotBeEmpty();
        conf2.Should().NotBeEmpty();
    }

    [Fact]
    public void SelectAllStarTeams_MaxTwelvePerConference()
    {
        var league = CreateTestLeague(teamCount: 8, playersPerTeam: 15);

        var (conf1, conf2) = AllStarWeekendService.SelectAllStarTeams(league);

        conf1.Count.Should().BeLessThanOrEqualTo(12);
        conf2.Count.Should().BeLessThanOrEqualTo(12);
    }

    [Fact]
    public void SelectAllStarTeams_HasPositionCoverage()
    {
        var league = CreateTestLeague(teamCount: 8, playersPerTeam: 15);

        var (conf1, conf2) = AllStarWeekendService.SelectAllStarTeams(league);

        // Each conference should have at least one player at each position
        var positions = new[] { "PG", "SG", "SF", "PF", "C" };
        foreach (var pos in positions)
        {
            conf1.Should().Contain(p => p.Position == pos,
                $"Conference 1 should have a {pos}");
            conf2.Should().Contain(p => p.Position == pos,
                $"Conference 2 should have a {pos}");
        }
    }

    [Fact]
    public void SelectAllStarTeams_CorrectConferenceAssignment()
    {
        var league = CreateTestLeague(teamCount: 4, playersPerTeam: 15);

        var (conf1, conf2) = AllStarWeekendService.SelectAllStarTeams(league);

        // Conference 1 = "East" (teams 0,1), Conference 2 = "West" (teams 2,3)
        foreach (var p in conf1)
        {
            var team = league.Teams[p.TeamIndex];
            team.Record.Conference.Should().Be("East");
        }
        foreach (var p in conf2)
        {
            var team = league.Teams[p.TeamIndex];
            team.Record.Conference.Should().Be("West");
        }
    }

    [Fact]
    public void SelectAllStarTeams_ExcludesInjuredPlayers()
    {
        var league = CreateTestLeague(teamCount: 4, playersPerTeam: 15);

        // Injure top players
        foreach (var team in league.Teams)
        {
            team.Roster[0].Injury = 10;
            team.Roster[1].Injury = 5;
        }

        var (conf1, conf2) = AllStarWeekendService.SelectAllStarTeams(league);

        var allSelected = conf1.Concat(conf2).ToList();
        allSelected.Should().NotContain(p => p.Injury > 0);
    }

    [Fact]
    public void SelectAllStarTeams_EmptyLeague_ReturnsEmpty()
    {
        var league = new Models.League.League();
        league.Settings.ConferenceName1 = "East";
        league.Settings.ConferenceName2 = "West";

        var (conf1, conf2) = AllStarWeekendService.SelectAllStarTeams(league);

        conf1.Should().BeEmpty();
        conf2.Should().BeEmpty();
    }

    [Fact]
    public void SelectAllStarTeams_SelectsActivePlayersWithStats()
    {
        var league = CreateTestLeague(teamCount: 4, playersPerTeam: 15);

        var (conf1, conf2) = AllStarWeekendService.SelectAllStarTeams(league);

        var allSelected = conf1.Concat(conf2).ToList();
        foreach (var p in allSelected)
        {
            p.Active.Should().BeTrue();
            p.SimulatedStats.Games.Should().BeGreaterThan(0);
            p.Injury.Should().Be(0);
        }
    }

    // ───────────────────────────────────────────────────────────────
    // Rookie Selection Tests
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void SelectRookieTeams_NormalMode_WhenEnoughRookiesAndSophs()
    {
        // Need 9+ rookies and 9+ sophomores
        var league = CreateTestLeague(teamCount: 8, playersPerTeam: 15, withRookies: true);

        var (team1, team2, isAllDefense) = AllStarWeekendService.SelectRookieTeams(league);

        isAllDefense.Should().BeFalse();
        team1.Should().NotBeEmpty();
        team2.Should().NotBeEmpty();
    }

    [Fact]
    public void SelectRookieTeams_AllDefenseFallback_WhenNotEnoughRookies()
    {
        // Default league has no rookies (all YOS=5), should trigger fallback
        var league = CreateTestLeague(teamCount: 4, playersPerTeam: 15, withRookies: false);

        var (team1, team2, isAllDefense) = AllStarWeekendService.SelectRookieTeams(league);

        isAllDefense.Should().BeTrue();
    }

    [Fact]
    public void SelectRookieTeams_NormalMode_HasPositionCoverage()
    {
        var league = CreateTestLeague(teamCount: 8, playersPerTeam: 15, withRookies: true);

        var (rookies, sophs, isAllDefense) = AllStarWeekendService.SelectRookieTeams(league);

        if (!isAllDefense && rookies.Count >= 5 && sophs.Count >= 5)
        {
            var positions = new[] { "PG", "SG", "SF", "PF", "C" };
            foreach (var pos in positions)
            {
                rookies.Should().Contain(p => p.Position == pos,
                    $"Rookies should have a {pos}");
                sophs.Should().Contain(p => p.Position == pos,
                    $"Sophomores should have a {pos}");
            }
        }
    }

    [Fact]
    public void SelectRookieTeams_NormalMode_RookiesAreYear1_SophsAreYear2()
    {
        var league = CreateTestLeague(teamCount: 8, playersPerTeam: 15, withRookies: true);

        var (rookies, sophs, isAllDefense) = AllStarWeekendService.SelectRookieTeams(league);

        if (!isAllDefense)
        {
            foreach (var p in rookies)
                p.Contract.YearsOfService.Should().Be(1);
            foreach (var p in sophs)
                p.Contract.YearsOfService.Should().Be(2);
        }
    }

    [Fact]
    public void SelectRookieTeams_AllDefenseFallback_UseConferenceBasedSplit()
    {
        var league = CreateTestLeague(teamCount: 4, playersPerTeam: 15, withRookies: false);

        var (team1, team2, isAllDefense) = AllStarWeekendService.SelectRookieTeams(league);

        if (isAllDefense && team1.Count > 0)
        {
            foreach (var p in team1)
            {
                var team = league.Teams[p.TeamIndex];
                team.Record.Conference.Should().Be("East");
            }
        }
    }

    // ───────────────────────────────────────────────────────────────
    // 3-Point / Dunk Score Tests
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void CalculateThreePointScore_ReturnsPositive_ForEligiblePlayer()
    {
        var player = CreatePlayer(1, 0, 1); // SG with stats

        int score = AllStarWeekendService.CalculateThreePointScore(player);

        score.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateThreePointScore_ReturnsNegative_ForInjuredPlayer()
    {
        var player = CreatePlayer(1, 0, 1);
        player.Injury = 5;

        int score = AllStarWeekendService.CalculateThreePointScore(player);

        score.Should().Be(-1);
    }

    [Fact]
    public void CalculateThreePointScore_ReturnsNegative_ForInactivePlayer()
    {
        var player = CreatePlayer(1, 0, 1);
        player.Active = false;

        int score = AllStarWeekendService.CalculateThreePointScore(player);

        score.Should().Be(-1);
    }

    [Fact]
    public void CalculateDunkScore_ReturnsPositive_ForEligiblePlayer()
    {
        var player = CreatePlayer(1, 0, 2); // SF

        int score = AllStarWeekendService.CalculateDunkScore(player);

        score.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateDunkScore_PositionBonus_SGandSFGetHigher()
    {
        var sg = CreatePlayer(1, 0, 1);
        sg.Position = "SG";
        var pf = CreatePlayer(2, 0, 3);
        pf.Position = "PF";
        // Give them identical ratings
        pf.Ratings = CloneRatings(sg.Ratings);
        pf.SimulatedStats = CloneStats(sg.SimulatedStats);
        pf.SeasonStats = CloneStats(sg.SeasonStats);

        int sgScore = AllStarWeekendService.CalculateDunkScore(sg);
        int pfScore = AllStarWeekendService.CalculateDunkScore(pf);

        sgScore.Should().BeGreaterThan(pfScore, "SG gets 1.2x bonus vs PF's 0.8x");
    }

    [Fact]
    public void SelectThreePointContestants_ReturnsUpTo8()
    {
        var league = CreateTestLeague(teamCount: 4, playersPerTeam: 15);

        var contestants = AllStarWeekendService.SelectThreePointContestants(league);

        contestants.Count.Should().BeLessThanOrEqualTo(8);
        contestants.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SelectDunkContestants_ReturnsUpTo8()
    {
        var league = CreateTestLeague(teamCount: 4, playersPerTeam: 15);

        var contestants = AllStarWeekendService.SelectDunkContestants(league);

        contestants.Count.Should().BeLessThanOrEqualTo(8);
        contestants.Count.Should().BeGreaterThan(0);
    }

    // ───────────────────────────────────────────────────────────────
    // 3-Point Contest Simulation Tests
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void SimulateThreePointContest_Returns8Participants()
    {
        var league = CreateTestLeague(teamCount: 4, playersPerTeam: 15);
        var contestants = AllStarWeekendService.SelectThreePointContestants(league);
        var random = new Random(42);

        var results = AllStarWeekendService.SimulateThreePointContest(contestants, random);

        results.Count.Should().Be(8);
    }

    [Fact]
    public void SimulateThreePointContest_WinnerReachedRound3()
    {
        var league = CreateTestLeague(teamCount: 4, playersPerTeam: 15);
        var contestants = AllStarWeekendService.SelectThreePointContestants(league);
        var random = new Random(42);

        var results = AllStarWeekendService.SimulateThreePointContest(contestants, random);

        results[0].HighestRoundReached.Should().Be(3);
        results[1].HighestRoundReached.Should().Be(3);
    }

    [Fact]
    public void SimulateThreePointContest_EliminationCounts_4Advance_2Advance()
    {
        var league = CreateTestLeague(teamCount: 4, playersPerTeam: 15);
        var contestants = AllStarWeekendService.SelectThreePointContestants(league);
        var random = new Random(42);

        var results = AllStarWeekendService.SimulateThreePointContest(contestants, random);

        // 2 reached R3 (finalists)
        results.Count(p => p.HighestRoundReached == 3).Should().Be(2);
        // 2 more reached R2 but not R3
        results.Count(p => p.HighestRoundReached == 2).Should().Be(2);
        // 4 eliminated in R1
        results.Count(p => p.HighestRoundReached == 1).Should().Be(4);
    }

    [Fact]
    public void SimulateThreePointContest_MoneyBallScoring()
    {
        var league = CreateTestLeague(teamCount: 4, playersPerTeam: 15);
        var contestants = AllStarWeekendService.SelectThreePointContestants(league);
        var random = new Random(42);

        var results = AllStarWeekendService.SimulateThreePointContest(contestants, random);

        // Max possible score per round: 20 regular + 5 money balls * 2 = 30
        foreach (var p in results)
        {
            p.RoundScores[1].Should().BeLessThanOrEqualTo(30);
            p.RoundScores[1].Should().BeGreaterThanOrEqualTo(0);
        }
    }

    [Fact]
    public void SimulateThreePointContest_Deterministic_SameSeed()
    {
        var league = CreateTestLeague(teamCount: 4, playersPerTeam: 15);
        var contestants = AllStarWeekendService.SelectThreePointContestants(league);

        var results1 = AllStarWeekendService.SimulateThreePointContest(contestants, new Random(123));
        var results2 = AllStarWeekendService.SimulateThreePointContest(contestants, new Random(123));

        results1[0].PlayerId.Should().Be(results2[0].PlayerId);
        results1[0].RoundScores[1].Should().Be(results2[0].RoundScores[1]);
    }

    // ───────────────────────────────────────────────────────────────
    // Dunk Contest Simulation Tests
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void SimulateDunkContest_Returns8Participants()
    {
        var league = CreateTestLeague(teamCount: 4, playersPerTeam: 15);
        var contestants = AllStarWeekendService.SelectDunkContestants(league);
        var random = new Random(42);

        var results = AllStarWeekendService.SimulateDunkContest(contestants, random);

        results.Count.Should().Be(8);
    }

    [Fact]
    public void SimulateDunkContest_EliminationCounts_3Advance()
    {
        var league = CreateTestLeague(teamCount: 4, playersPerTeam: 15);
        var contestants = AllStarWeekendService.SelectDunkContestants(league);
        var random = new Random(42);

        var results = AllStarWeekendService.SimulateDunkContest(contestants, random);

        // 3 reached R2
        results.Count(p => p.HighestRoundReached == 2).Should().Be(3);
        // 5 eliminated in R1
        results.Count(p => p.HighestRoundReached == 1).Should().Be(5);
    }

    [Fact]
    public void SimulateDunkContest_ScoresCappedAt1000()
    {
        var league = CreateTestLeague(teamCount: 4, playersPerTeam: 15);
        var contestants = AllStarWeekendService.SelectDunkContestants(league);
        var random = new Random(42);

        var results = AllStarWeekendService.SimulateDunkContest(contestants, random);

        foreach (var p in results)
        {
            if (p.RoundScores[1] > 0)
                p.RoundScores[1].Should().BeLessThanOrEqualTo(1000);
            if (p.RoundScores[2] > 0)
                p.RoundScores[2].Should().BeLessThanOrEqualTo(1000);
        }
    }

    [Fact]
    public void SimulateDunkContest_Deterministic_SameSeed()
    {
        var league = CreateTestLeague(teamCount: 4, playersPerTeam: 15);
        var contestants = AllStarWeekendService.SelectDunkContestants(league);

        var results1 = AllStarWeekendService.SimulateDunkContest(contestants, new Random(99));
        var results2 = AllStarWeekendService.SimulateDunkContest(contestants, new Random(99));

        results1[0].PlayerId.Should().Be(results2[0].PlayerId);
        results1[0].RoundScores[1].Should().Be(results2[0].RoundScores[1]);
    }

    // ───────────────────────────────────────────────────────────────
    // Game Simulation Tests
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void SimulateAllStarGame_CompletesWithValidScore()
    {
        var league = CreateTestLeague(teamCount: 8, playersPerTeam: 15);
        var (conf1, conf2) = AllStarWeekendService.SelectAllStarTeams(league);
        var random = new Random(42);
        var averages = CreateTestAverages();

        var result = AllStarWeekendService.SimulateAllStarGame(league, conf1, conf2, random, averages);

        result.Should().NotBeNull();
        result.VisitorScore.Should().BeGreaterThan(0);
        result.HomeScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SimulateRookieGame_CompletesWithValidScore()
    {
        var league = CreateTestLeague(teamCount: 4, playersPerTeam: 15, withRookies: false);
        var (team1, team2, isAllDefense) = AllStarWeekendService.SelectRookieTeams(league);
        var random = new Random(42);
        var averages = CreateTestAverages();

        if (team1.Count > 0 && team2.Count > 0)
        {
            var result = AllStarWeekendService.SimulateRookieGame(
                league, team1, team2, isAllDefense, random, averages);

            result.Should().NotBeNull();
            result.VisitorScore.Should().BeGreaterThan(0);
            result.HomeScore.Should().BeGreaterThan(0);
        }
    }

    // ───────────────────────────────────────────────────────────────
    // Orchestrator Tests
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void RunAllStarWeekend_CompletesFullWeekend()
    {
        var league = CreateTestLeague(teamCount: 8, playersPerTeam: 15);
        var random = new Random(42);
        var averages = CreateTestAverages();

        var result = AllStarWeekendService.RunAllStarWeekend(league, random, averages);

        result.Should().NotBeNull();
        result.Conference1Roster.Should().NotBeEmpty();
        result.Conference2Roster.Should().NotBeEmpty();
        result.AllStarGameResult.Should().NotBeNull();
        result.Conference1Name.Should().Be("East");
        result.Conference2Name.Should().Be("West");
    }

    [Fact]
    public void RunAllStarWeekend_ContestsHaveResults()
    {
        var league = CreateTestLeague(teamCount: 8, playersPerTeam: 15);
        var random = new Random(42);
        var averages = CreateTestAverages();

        var result = AllStarWeekendService.RunAllStarWeekend(league, random, averages);

        result.ThreePointContestants.Should().NotBeEmpty();
        result.ThreePointContestants.Count.Should().BeGreaterThanOrEqualTo(2);
        result.DunkContestants.Should().NotBeEmpty();
        result.DunkContestants.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void RunAllStarWeekend_PlayerFlagsAreSet()
    {
        var league = CreateTestLeague(teamCount: 8, playersPerTeam: 15);
        var random = new Random(42);
        var averages = CreateTestAverages();

        var result = AllStarWeekendService.RunAllStarWeekend(league, random, averages);

        // Find all-star players in the league and verify flags
        var allPlayers = league.Teams.SelectMany(t => t.Roster).ToDictionary(p => p.Id);

        foreach (int id in result.Conference1Roster.Concat(result.Conference2Roster))
        {
            if (allPlayers.TryGetValue(id, out var player))
                player.AllStar.Should().Be(1);
        }

        foreach (var cp in result.ThreePointContestants)
        {
            if (allPlayers.TryGetValue(cp.PlayerId, out var player))
                player.ThreePointContest.Should().Be(1);
        }

        foreach (var cp in result.DunkContestants)
        {
            if (allPlayers.TryGetValue(cp.PlayerId, out var player))
                player.DunkContest.Should().Be(1);
        }
    }

    [Fact]
    public void RunAllStarWeekend_ContestScoresStoredOnPlayers()
    {
        var league = CreateTestLeague(teamCount: 8, playersPerTeam: 15);
        var random = new Random(42);
        var averages = CreateTestAverages();

        var result = AllStarWeekendService.RunAllStarWeekend(league, random, averages);

        var allPlayers = league.Teams.SelectMany(t => t.Roster).ToDictionary(p => p.Id);

        // At least the winner should have non-zero scores stored
        if (allPlayers.TryGetValue(result.ThreePointWinnerId, out var tpWinner))
        {
            tpWinner.ThreePointScores[1].Should().BeGreaterThan(0, "3pt winner should have R1 score");
        }

        if (allPlayers.TryGetValue(result.DunkWinnerId, out var dkWinner))
        {
            dkWinner.DunkScores[1].Should().BeGreaterThan(0, "dunk winner should have R1 score");
        }
    }

    [Fact]
    public void RunAllStarWeekend_Deterministic_SameSeed()
    {
        var league1 = CreateTestLeague(teamCount: 8, playersPerTeam: 15);
        var league2 = CreateTestLeague(teamCount: 8, playersPerTeam: 15);
        var averages = CreateTestAverages();

        var result1 = AllStarWeekendService.RunAllStarWeekend(league1, new Random(777), averages);
        var result2 = AllStarWeekendService.RunAllStarWeekend(league2, new Random(777), averages);

        result1.ThreePointWinnerId.Should().Be(result2.ThreePointWinnerId);
        result1.DunkWinnerId.Should().Be(result2.DunkWinnerId);
        result1.AllStarGameResult!.VisitorScore.Should().Be(result2.AllStarGameResult!.VisitorScore);
        result1.AllStarGameResult.HomeScore.Should().Be(result2.AllStarGameResult.HomeScore);
    }

    [Fact]
    public void RunAllStarWeekend_LeagueRostersNotMutated()
    {
        var league = CreateTestLeague(teamCount: 8, playersPerTeam: 15);
        var averages = CreateTestAverages();

        // Record original roster counts
        var originalCounts = league.Teams.Select(t => t.Roster.Count).ToList();

        AllStarWeekendService.RunAllStarWeekend(league, new Random(42), averages);

        // Roster counts should be unchanged
        for (int i = 0; i < league.Teams.Count; i++)
        {
            league.Teams[i].Roster.Count.Should().Be(originalCounts[i]);
        }
    }

    // ───────────────────────────────────────────────────────────────
    // Helpers
    // ───────────────────────────────────────────────────────────────

    private static PlayerRatings CloneRatings(PlayerRatings r)
    {
        return new PlayerRatings
        {
            ProjectionThreePointersAttempted = r.ProjectionThreePointersAttempted,
            ProjectionThreePointPercentage = r.ProjectionThreePointPercentage,
            ProjectionFieldGoalsAttempted = r.ProjectionFieldGoalsAttempted,
            ProjectionFreeThrowsAttempted = r.ProjectionFreeThrowsAttempted,
            ProjectionOffensiveRebounds = r.ProjectionOffensiveRebounds,
            ProjectionDefensiveRebounds = r.ProjectionDefensiveRebounds,
            TransitionOffenseRaw = r.TransitionOffenseRaw,
            TransitionDefenseRaw = r.TransitionDefenseRaw,
            PenetrationDefenseRaw = r.PenetrationDefenseRaw,
            MovementDefenseRaw = r.MovementDefenseRaw,
            PostDefenseRaw = r.PostDefenseRaw,
            FieldGoalPercentage = r.FieldGoalPercentage,
            FreeThrowPercentage = r.FreeThrowPercentage,
            ThreePointPercentage = r.ThreePointPercentage,
            Stamina = r.Stamina,
            Consistency = r.Consistency,
            FieldGoalsAttemptedPer48Min = r.FieldGoalsAttemptedPer48Min,
            AdjustedFieldGoalsAttemptedPer48Min = r.AdjustedFieldGoalsAttemptedPer48Min,
            ThreePointersAttemptedPer48Min = r.ThreePointersAttemptedPer48Min,
            AdjustedThreePointersAttemptedPer48Min = r.AdjustedThreePointersAttemptedPer48Min,
            FoulsDrawnPer48Min = r.FoulsDrawnPer48Min,
            AdjustedFoulsDrawnPer48Min = r.AdjustedFoulsDrawnPer48Min,
            OffensiveReboundsPer48Min = r.OffensiveReboundsPer48Min,
            DefensiveReboundsPer48Min = r.DefensiveReboundsPer48Min,
            AssistsPer48Min = r.AssistsPer48Min,
            StealsPer48Min = r.StealsPer48Min,
            TurnoversPer48Min = r.TurnoversPer48Min,
            AdjustedTurnoversPer48Min = r.AdjustedTurnoversPer48Min,
            BlocksPer48Min = r.BlocksPer48Min,
            PersonalFoulsPer48Min = r.PersonalFoulsPer48Min,
            MinutesPerGame = r.MinutesPerGame
        };
    }

    private static PlayerStatLine CloneStats(PlayerStatLine s)
    {
        return new PlayerStatLine
        {
            Games = s.Games,
            Minutes = s.Minutes,
            FieldGoalsMade = s.FieldGoalsMade,
            FieldGoalsAttempted = s.FieldGoalsAttempted,
            FreeThrowsMade = s.FreeThrowsMade,
            FreeThrowsAttempted = s.FreeThrowsAttempted,
            ThreePointersMade = s.ThreePointersMade,
            ThreePointersAttempted = s.ThreePointersAttempted,
            OffensiveRebounds = s.OffensiveRebounds,
            Rebounds = s.Rebounds,
            Assists = s.Assists,
            Steals = s.Steals,
            Turnovers = s.Turnovers,
            Blocks = s.Blocks,
            PersonalFouls = s.PersonalFouls
        };
    }
}
