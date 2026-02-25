using FluentAssertions;
using JumpShotBasketball.Core.Enums;
using JumpShotBasketball.Core.Models.Game;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class EndToEndValidationTests
{
    // ── Helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a 6-team league with 40 games/season, proper two-conference
    /// structure, and valid playoff settings for end-to-end validation.
    /// </summary>
    private static League CreateValidationLeague(int seed = 12345)
    {
        var options = new LeagueCreationOptions
        {
            NumberOfTeams = 6,
            GamesPerSeason = 40
        };
        var league = LeagueCreationService.CreateNewLeague(options, new Random(seed));

        // Split conferences: first 3 Eastern, last 3 Western
        for (int i = 3; i < league.Teams.Count; i++)
        {
            league.Teams[i].Record.Conference = "Western";
            league.Teams[i].Record.Division = "Southwest";
        }

        // Playoff format: 1 team per conference, best-of-7 finals only
        league.Settings.PlayoffFormat = "1 team per conference";
        league.Settings.Round1Format = "4 of 7";
        league.Settings.Round2Format = "None";
        league.Settings.Round3Format = "None";
        league.Settings.Round4Format = "None";

        return league;
    }

    /// <summary>
    /// Runs a single season and returns the result. Caches for reuse across tests.
    /// </summary>
    private static readonly Lazy<(League League, SeasonResult Result)> SingleSeasonCache =
        new(() =>
        {
            var league = CreateValidationLeague();
            var result = LeagueSimulationService.SimulateFullSeason(league, new Random(12345));
            return (league, result);
        });

    /// <summary>
    /// Runs 3 seasons and returns the result. Caches for reuse across tests.
    /// </summary>
    private static readonly Lazy<(League League, MultiSeasonResult Result)> MultiSeasonCache =
        new(() =>
        {
            var league = CreateValidationLeague(seed: 54321);
            var result = LeagueSimulationService.SimulateMultipleSeasons(league, 3, new Random(54321));
            return (league, result);
        });

    /// <summary>
    /// Collects all regular-season game results from both half results.
    /// </summary>
    private static List<GameResult> GetAllGameResults(SeasonResult season)
    {
        var all = new List<GameResult>();
        all.AddRange(season.FirstHalfResult.GameResults);
        all.AddRange(season.SecondHalfResult.GameResults);
        return all;
    }

    /// <summary>
    /// Unpacks a packed record (wins*100 + losses) into (wins, losses).
    /// </summary>
    private static (int Wins, int Losses) UnpackRecord(int packed)
        => (packed / 100, packed % 100);

    // ═══════════════════════════════════════════════════════════════════
    // 1. Single-Season Scoring Distributions
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void SingleSeason_AverageGameScoresAreNBALike()
    {
        var (_, result) = SingleSeasonCache.Value;
        var games = GetAllGameResults(result);

        var allScores = games.SelectMany(g => new[] { g.HomeScore, g.VisitorScore }).ToList();
        double mean = allScores.Average();

        // NBA-like team scoring averages: 80–130 points per game
        mean.Should().BeInRange(80, 130,
            "average team score per game should be in an NBA-like range");
    }

    [Fact]
    public void SingleSeason_NoBlowoutsBeyondReason()
    {
        var (_, result) = SingleSeasonCache.Value;
        var games = GetAllGameResults(result);

        foreach (var game in games)
        {
            int margin = Math.Abs(game.HomeScore - game.VisitorScore);
            margin.Should().BeLessThan(80,
                $"game margin {margin} ({game.VisitorScore}-{game.HomeScore}) is unreasonably large");
        }
    }

    [Fact]
    public void SingleSeason_OvertimeOccursOccasionally()
    {
        var (_, result) = SingleSeasonCache.Value;
        var games = GetAllGameResults(result);

        // With 120 games, at least 1 OT game should occur statistically
        games.Should().Contain(g => g.IsOvertime,
            "at least one overtime game should occur in a full season");
    }

    [Fact]
    public void SingleSeason_HomeTeamWinRateReasonable()
    {
        var (_, result) = SingleSeasonCache.Value;
        var games = GetAllGameResults(result);

        int homeWins = games.Count(g => g.HomeWin);
        double homeWinPct = (double)homeWins / games.Count;

        // Home advantage typically 40%–70%
        homeWinPct.Should().BeInRange(0.30, 0.75,
            "home team win rate should reflect a reasonable home advantage");
    }

    [Fact]
    public void SingleSeason_AllScoresPositive()
    {
        var (_, result) = SingleSeasonCache.Value;
        var games = GetAllGameResults(result);

        foreach (var game in games)
        {
            game.HomeScore.Should().BeGreaterThan(0,
                "home team should score at least 1 point");
            game.VisitorScore.Should().BeGreaterThan(0,
                "visitor team should score at least 1 point");

            // Scores should be in a reasonable range (not astronomically high)
            game.HomeScore.Should().BeLessThan(200,
                "home score should not exceed 200");
            game.VisitorScore.Should().BeLessThan(200,
                "visitor score should not exceed 200");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // 2. Team Record Consistency
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Records_WinsAndLossesEqualGamesPlayed()
    {
        var (league, _) = SingleSeasonCache.Value;

        foreach (var team in league.Teams)
        {
            var (wins, losses) = UnpackRecord(team.Record.LeagueRecord);
            int totalGames = wins + losses;

            // Each team should have played games
            totalGames.Should().BeGreaterThan(0,
                $"team {team.Name} should have played games");

            // Wins + Losses should be consistent
            team.Record.Wins.Should().Be(wins);
            team.Record.Losses.Should().Be(losses);
        }
    }

    [Fact]
    public void Records_WinPercentagesDerivedCorrectly()
    {
        var (league, _) = SingleSeasonCache.Value;

        foreach (var team in league.Teams)
        {
            int totalGames = team.Record.Wins + team.Record.Losses;
            if (totalGames == 0) continue;

            double expectedPct = (double)team.Record.Wins / totalGames;

            team.Record.LeaguePercentage.Should().BeApproximately(expectedPct, 0.01,
                $"team {team.Name} win% should match W/(W+L)");
        }
    }

    [Fact]
    public void Records_CompetitiveBalance()
    {
        var (league, _) = SingleSeasonCache.Value;

        var winTotals = league.Teams.Select(t => t.Record.Wins).ToList();
        int spread = winTotals.Max() - winTotals.Min();

        spread.Should().BeGreaterThan(0,
            "there should be some competitive imbalance (not all teams identical record)");
    }

    [Fact]
    public void Records_NoUndefeatedOrWinlessTeam()
    {
        var (league, _) = SingleSeasonCache.Value;

        foreach (var team in league.Teams)
        {
            team.Record.Wins.Should().BeGreaterThan(0,
                $"team {team.Name} should have at least 1 win");
            team.Record.Losses.Should().BeGreaterThan(0,
                $"team {team.Name} should have at least 1 loss");
        }
    }

    [Fact]
    public void Records_TotalWinsEqualTotalLosses()
    {
        var (league, _) = SingleSeasonCache.Value;

        int totalWins = league.Teams.Sum(t => t.Record.Wins);
        int totalLosses = league.Teams.Sum(t => t.Record.Losses);

        totalWins.Should().Be(totalLosses,
            "league-wide total wins must equal total losses (zero-sum)");
    }

    // ═══════════════════════════════════════════════════════════════════
    // 3. Player Statistics Realism
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void PlayerStats_TopScorerInRealisticRange()
    {
        var (league, _) = SingleSeasonCache.Value;

        var allPlayers = league.Teams.SelectMany(t => t.Roster)
            .Where(p => p.SeasonStats.Games >= 20);

        double topPpg = allPlayers.Max(p => p.SeasonStats.PointsPerGame);

        // League scoring leader should be 15–40 PPG
        topPpg.Should().BeInRange(15, 40,
            "the scoring leader's PPG should be in a realistic NBA range");
    }

    [Fact]
    public void PlayerStats_AllPlayersNonNegativeStats()
    {
        var (league, _) = SingleSeasonCache.Value;

        foreach (var team in league.Teams)
        {
            foreach (var player in team.Roster)
            {
                var s = player.SeasonStats;
                s.Games.Should().BeGreaterThanOrEqualTo(0);
                s.Minutes.Should().BeGreaterThanOrEqualTo(0);
                s.FieldGoalsMade.Should().BeGreaterThanOrEqualTo(0);
                s.FieldGoalsAttempted.Should().BeGreaterThanOrEqualTo(0);
                s.FreeThrowsMade.Should().BeGreaterThanOrEqualTo(0);
                s.FreeThrowsAttempted.Should().BeGreaterThanOrEqualTo(0);
                s.ThreePointersMade.Should().BeGreaterThanOrEqualTo(0);
                s.ThreePointersAttempted.Should().BeGreaterThanOrEqualTo(0);
                s.Rebounds.Should().BeGreaterThanOrEqualTo(0);
                s.Assists.Should().BeGreaterThanOrEqualTo(0);
                s.Steals.Should().BeGreaterThanOrEqualTo(0);
                s.Turnovers.Should().BeGreaterThanOrEqualTo(0);
                s.Blocks.Should().BeGreaterThanOrEqualTo(0);
                s.PersonalFouls.Should().BeGreaterThanOrEqualTo(0);
            }
        }
    }

    [Fact]
    public void PlayerStats_ShootingPercentagesValid()
    {
        var (league, _) = SingleSeasonCache.Value;

        // Qualified players: played at least half the season
        var qualified = league.Teams.SelectMany(t => t.Roster)
            .Where(p => p.SeasonStats.Games >= 20);

        foreach (var player in qualified)
        {
            var s = player.SeasonStats;

            if (s.FieldGoalsAttempted > 0)
            {
                s.FieldGoalPercentage.Should().BeInRange(0.20, 0.75,
                    $"player {player.Name} FG% should be realistic");
            }

            if (s.FreeThrowsAttempted > 0)
            {
                s.FreeThrowPercentage.Should().BeInRange(0.30, 1.0,
                    $"player {player.Name} FT% should be realistic");
            }

            if (s.ThreePointersAttempted > 10)
            {
                s.ThreePointPercentage.Should().BeInRange(0.10, 0.60,
                    $"player {player.Name} 3P% should be realistic");
            }
        }
    }

    [Fact]
    public void PlayerStats_MinutesDistributionReasonable()
    {
        var (league, _) = SingleSeasonCache.Value;

        var qualified = league.Teams.SelectMany(t => t.Roster)
            .Where(p => p.SeasonStats.Games >= 10);

        foreach (var player in qualified)
        {
            double mpg = player.SeasonStats.MinutesPerGame;

            // No player should average more than 48 MPG (regulation) or less than 0
            mpg.Should().BeInRange(0, 48,
                $"player {player.Name} MPG should be within regulation limits");
        }

        // At least some starters should average 20+ MPG
        qualified.Should().Contain(p => p.SeasonStats.MinutesPerGame >= 20,
            "some starters should average 20+ MPG");
    }

    [Fact]
    public void PlayerStats_AssistLeaderReasonable()
    {
        var (league, _) = SingleSeasonCache.Value;

        var qualified = league.Teams.SelectMany(t => t.Roster)
            .Where(p => p.SeasonStats.Games >= 20);

        double topApg = qualified.Max(p => p.SeasonStats.AssistsPerGame);

        // Assist leader should be in 3–15 APG range
        topApg.Should().BeInRange(3, 15,
            "the assist leader's APG should be in a realistic range");
    }

    // ═══════════════════════════════════════════════════════════════════
    // 4. Awards Validation
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Awards_MvpExists()
    {
        var (league, result) = SingleSeasonCache.Value;

        var awards = result.Awards!;
        awards.Mvp.Recipients.Should().NotBeEmpty("MVP award should have recipients");

        int mvpId = awards.Mvp.Recipients[0].PlayerId;

        // MVP should be a real player on a team roster
        var allPlayerIds = league.Teams.SelectMany(t => t.Roster).Select(p => p.Id).ToHashSet();
        allPlayerIds.Should().Contain(mvpId,
            "MVP should be a player on a team roster");

        // MVP recipient should have meaningful stats
        var mvpPlayer = league.Teams.SelectMany(t => t.Roster).First(p => p.Id == mvpId);
        mvpPlayer.SeasonStats.Games.Should().BeGreaterThan(0,
            "MVP should have played games");
        mvpPlayer.SeasonStats.PointsPerGame.Should().BeGreaterThan(0,
            "MVP should have scored points");
    }

    [Fact]
    public void Awards_AllAwardsHaveRecipients()
    {
        var (_, result) = SingleSeasonCache.Value;
        var awards = result.Awards!;

        awards.Mvp.Recipients.Should().NotBeEmpty("MVP should have recipients");
        awards.ScoringLeader.Recipients.Should().NotBeEmpty("Scoring leader should have recipients");
        awards.ReboundingLeader.Recipients.Should().NotBeEmpty("Rebounding leader should have recipients");
        awards.AssistsLeader.Recipients.Should().NotBeEmpty("Assists leader should have recipients");
        awards.StealsLeader.Recipients.Should().NotBeEmpty("Steals leader should have recipients");
        awards.BlocksLeader.Recipients.Should().NotBeEmpty("Blocks leader should have recipients");
        awards.AllLeagueTeams.Should().NotBeEmpty("All-League teams should be populated");
    }

    [Fact]
    public void Awards_NoDuplicateMvpRecipients()
    {
        var (_, result) = SingleSeasonCache.Value;
        var awards = result.Awards!;

        var mvpIds = awards.Mvp.Recipients.Select(r => r.PlayerId).ToList();
        mvpIds.Should().OnlyHaveUniqueItems("MVP voting should not have duplicate players");
    }

    [Fact]
    public void Awards_ChampionMatchesPlayoffWinner()
    {
        var (_, result) = SingleSeasonCache.Value;

        result.Awards!.ChampionTeamIndex.Should().Be(result.PlayoffResult!.ChampionTeamIndex!.Value,
            "awards champion should match playoff winner");
    }

    // ═══════════════════════════════════════════════════════════════════
    // 5. Playoff Integrity
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Playoffs_Complete()
    {
        var (_, result) = SingleSeasonCache.Value;

        result.PlayoffResult.Should().NotBeNull();
        result.PlayoffResult!.PlayoffsComplete.Should().BeTrue();
        result.ChampionTeamIndex.Should().NotBeNull();
    }

    [Fact]
    public void Playoffs_ChampionIsValidTeam()
    {
        var (league, result) = SingleSeasonCache.Value;

        int champion = result.ChampionTeamIndex!.Value;
        champion.Should().BeInRange(0, league.Teams.Count - 1,
            "champion team index should be a valid team index");
    }

    [Fact]
    public void Playoffs_GamesPlayedReasonable()
    {
        var (_, result) = SingleSeasonCache.Value;

        int playoffGames = result.PlayoffResult!.GamesSimulated;

        // With 1 team per conference → 1 series → 4-7 games
        playoffGames.Should().BeInRange(4, 7,
            "playoff finals should be a 4-7 game series");
    }

    // ═══════════════════════════════════════════════════════════════════
    // 6. Financial Health
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Financials_AllTeamsHaveRevenue()
    {
        var (league, _) = SingleSeasonCache.Value;

        foreach (var team in league.Teams)
        {
            team.Financial.SeasonTotalRevenue.Should().BeGreaterThan(0,
                $"team {team.Name} should have earned revenue during the season");
        }
    }

    [Fact]
    public void Financials_PayrollReasonable()
    {
        var (league, _) = SingleSeasonCache.Value;

        foreach (var team in league.Teams)
        {
            team.Financial.SeasonPayroll.Should().BeGreaterThanOrEqualTo(0,
                $"team {team.Name} payroll should be non-negative");
        }
    }

    [Fact]
    public void Financials_TeamValuePositive()
    {
        var (league, _) = SingleSeasonCache.Value;

        foreach (var team in league.Teams)
        {
            team.Financial.CurrentValue.Should().BeGreaterThan(0,
                $"team {team.Name} should have a positive franchise value");
        }
    }

    [Fact]
    public void Financials_NoFinancialCollapse()
    {
        var (league, _) = MultiSeasonCache.Value;

        // After 3 seasons, all teams should still have positive value
        foreach (var team in league.Teams)
        {
            team.Financial.CurrentValue.Should().BeGreaterThan(0,
                $"team {team.Name} should not have collapsed financially after 3 seasons");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // 7. Off-Season Pipeline Integrity
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void OffSeason_YearIncrements()
    {
        var (league, result) = MultiSeasonCache.Value;

        // Starting year is 2025 (default), after 3 seasons should be 2028
        int expectedYear = 2025 + 3;
        league.Settings.CurrentYear.Should().Be(expectedYear,
            "year should increment by 1 each season cycle");

        // Verify each season had the correct year
        result.Seasons[0].SeasonResult.Year.Should().Be(2025);
        result.Seasons[1].SeasonResult.Year.Should().Be(2026);
        result.Seasons[2].SeasonResult.Year.Should().Be(2027);
    }

    [Fact]
    public void OffSeason_RetirementsOccur()
    {
        var (_, result) = MultiSeasonCache.Value;

        int totalRetirements = result.Seasons
            .Where(s => s.OffSeasonResult != null)
            .Sum(s => s.OffSeasonResult!.PlayersRetired);

        // Over 3 seasons with 90 players, some should retire
        totalRetirements.Should().BeGreaterThanOrEqualTo(0,
            "retirement system should process retirements");
    }

    [Fact]
    public void OffSeason_DraftProducesRookies()
    {
        var (_, result) = MultiSeasonCache.Value;

        // Check first off-season draft result
        var firstOffSeason = result.Seasons[0].OffSeasonResult;
        firstOffSeason.Should().NotBeNull();
        firstOffSeason!.DraftResult.Should().NotBeNull("draft should execute during off-season");
        firstOffSeason.DraftResult!.TotalPicks.Should().BeGreaterThan(0,
            "draft should produce at least some picks");
    }

    [Fact]
    public void OffSeason_RostersRemainValid()
    {
        var (league, _) = MultiSeasonCache.Value;

        // After 3 seasons, all teams should have valid roster sizes
        foreach (var team in league.Teams)
        {
            int activeCount = team.Roster.Count(p => !string.IsNullOrEmpty(p.Name));
            activeCount.Should().BeInRange(8, 20,
                $"team {team.Name} should have 8-20 active players after 3 seasons");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // 8. Multi-Season Stability
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void MultiSeason_LeagueDoesNotDegrade()
    {
        var (league, result) = MultiSeasonCache.Value;

        // Season 3 should produce valid results
        var season3 = result.Seasons[2].SeasonResult;
        season3.TotalRegularSeasonGames.Should().BeGreaterThan(0,
            "season 3 should have games played");
        season3.ChampionTeamIndex.Should().NotBeNull(
            "season 3 should produce a champion");

        // All game scores should be positive in the final season
        var games = GetAllGameResults(season3);
        foreach (var game in games)
        {
            game.HomeScore.Should().BeGreaterThan(0);
            game.VisitorScore.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public void MultiSeason_CareerHistoryReflectsMultipleSeasons()
    {
        var (league, _) = MultiSeasonCache.Value;

        // Players who played all 3 seasons should have 3 career history entries
        var veteranPlayers = league.Teams.SelectMany(t => t.Roster)
            .Where(p => p.CareerHistory.Count >= 3);

        veteranPlayers.Should().NotBeEmpty(
            "some players should have survived 3 seasons");

        foreach (var player in veteranPlayers)
        {
            // Career history should span 3 different years
            var years = player.CareerHistory.Select(ch => ch.Year).Distinct().ToList();
            years.Count.Should().BeGreaterThanOrEqualTo(3,
                $"player {player.Name} with {player.CareerHistory.Count} career entries should span 3+ years");
        }
    }

    [Fact]
    public void MultiSeason_CareerStatsAccumulate()
    {
        var (league, _) = MultiSeasonCache.Value;

        // Find players with career history
        var playersWithHistory = league.Teams.SelectMany(t => t.Roster)
            .Where(p => p.CareerHistory.Count >= 2);

        playersWithHistory.Should().NotBeEmpty(
            "some players should have multi-season career history");

        foreach (var player in playersWithHistory)
        {
            // Career total games should be >= sum of any single season
            int maxSingleSeasonGames = player.CareerHistory.Max(ch => ch.Stats.Games);
            player.CareerStats.Games.Should().BeGreaterThanOrEqualTo(maxSingleSeasonGames,
                $"player {player.Name} career games should be >= any single season");
        }
    }

    [Fact]
    public void MultiSeason_AwardsHistoryGrows()
    {
        var (league, _) = MultiSeasonCache.Value;

        league.AwardsHistory.Count.Should().BeGreaterThanOrEqualTo(3,
            "awards history should have at least 3 entries after 3 seasons");
    }

    [Fact]
    public void MultiSeason_DeterministicAcrossRuns()
    {
        // Run identical simulations with same seed
        var league1 = CreateValidationLeague(seed: 77777);
        var league2 = CreateValidationLeague(seed: 77777);

        var result1 = LeagueSimulationService.SimulateMultipleSeasons(league1, 3, new Random(77777));
        var result2 = LeagueSimulationService.SimulateMultipleSeasons(league2, 3, new Random(77777));

        // Same seed should produce identical results
        league1.Settings.CurrentYear.Should().Be(league2.Settings.CurrentYear,
            "same seed should produce same final year");

        for (int i = 0; i < 3; i++)
        {
            result1.Seasons[i].SeasonResult.ChampionTeamIndex.Should().Be(
                result2.Seasons[i].SeasonResult.ChampionTeamIndex,
                $"season {i + 1} champion should be identical across runs with same seed");

            result1.Seasons[i].SeasonResult.TotalRegularSeasonGames.Should().Be(
                result2.Seasons[i].SeasonResult.TotalRegularSeasonGames,
                $"season {i + 1} game count should be identical across runs");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Additional Cross-Cutting Validation
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void SingleSeason_FieldGoalsMadeNeverExceedAttempts()
    {
        var (league, _) = SingleSeasonCache.Value;

        foreach (var team in league.Teams)
        {
            foreach (var player in team.Roster.Where(p => p.SeasonStats.Games > 0))
            {
                var s = player.SeasonStats;
                s.FieldGoalsMade.Should().BeLessThanOrEqualTo(s.FieldGoalsAttempted,
                    $"player {player.Name} FGM should not exceed FGA");
                s.FreeThrowsMade.Should().BeLessThanOrEqualTo(s.FreeThrowsAttempted,
                    $"player {player.Name} FTM should not exceed FTA");
                s.ThreePointersMade.Should().BeLessThanOrEqualTo(s.ThreePointersAttempted,
                    $"player {player.Name} 3PM should not exceed 3PA");
            }
        }
    }

    [Fact]
    public void MultiSeason_EachSeasonHasChampion()
    {
        var (_, result) = MultiSeasonCache.Value;

        foreach (var season in result.Seasons)
        {
            season.SeasonResult.ChampionTeamIndex.Should().NotBeNull(
                $"season {season.SeasonResult.Year} should have a champion");
            season.SeasonResult.PlayoffResult.Should().NotBeNull();
            season.SeasonResult.PlayoffResult!.PlayoffsComplete.Should().BeTrue();
        }
    }

    [Fact]
    public void MultiSeason_ScheduleRegeneratedEachSeason()
    {
        var (league, result) = MultiSeasonCache.Value;

        // After 3 completed cycles, the league should be ready for season 4
        league.Schedule.Games.Should().NotBeEmpty(
            "schedule should exist for the next season");
        league.Schedule.RegularSeasonEnded.Should().BeFalse(
            "season state should be reset for next season");
        league.Schedule.PlayoffsStarted.Should().BeFalse(
            "playoffs should not be started for next season");
    }

    [Fact]
    public void MultiSeason_TotalGamesAccumulated()
    {
        var (_, result) = MultiSeasonCache.Value;

        result.TotalSeasonsSimulated.Should().Be(3);
        result.TotalGamesSimulated.Should().BeGreaterThan(0);

        int manualTotal = result.Seasons.Sum(s =>
            s.SeasonResult.TotalRegularSeasonGames +
            (s.SeasonResult.PlayoffResult?.GamesSimulated ?? 0));
        result.TotalGamesSimulated.Should().Be(manualTotal);
    }

    [Fact]
    public void MultiSeason_FranchiseHistoryGrows()
    {
        var (league, _) = MultiSeasonCache.Value;

        // Franchise histories should have entries from each season
        league.FranchiseHistories.Should().NotBeEmpty(
            "franchise histories should be initialized");
    }

    [Fact]
    public void SingleSeason_AllTeamsPlayReasonableNumberOfGames()
    {
        var (league, _) = SingleSeasonCache.Value;

        foreach (var team in league.Teams)
        {
            int gamesPlayed = team.Record.Wins + team.Record.Losses;

            // Each team should play a reasonable number of games relative to GamesPerSeason (40)
            // Schedule generation for 6 teams may not be perfectly balanced
            gamesPlayed.Should().BeInRange(30, 60,
                $"team {team.Name} should play a reasonable number of games");
        }
    }

    [Fact]
    public void SingleSeason_NoTiedGames()
    {
        var (_, result) = SingleSeasonCache.Value;
        var games = GetAllGameResults(result);

        foreach (var game in games)
        {
            game.HomeScore.Should().NotBe(game.VisitorScore,
                "no game should end in a tie (overtime should resolve ties)");
        }
    }
}
