using JumpShotBasketball.Core.Enums;
using JumpShotBasketball.Core.Models.Game;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Playoff;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Top-level orchestrator for complete season lifecycle simulation.
/// Coordinates regular season (with All-Star break), playoffs, awards,
/// off-season, and multi-season cycles.
/// </summary>
public static class LeagueSimulationService
{
    /// <summary>
    /// Fraction of the season at which the All-Star break occurs.
    /// Matches C++ allstarbreak = int(45./82. * m_currentGame).
    /// </summary>
    internal const double AllStarBreakFraction = 45.0 / 82.0;

    /// <summary>
    /// Safety limit on maximum days to simulate in any half-season loop.
    /// </summary>
    private const int MaxDaysPerHalf = 300;

    // ── Public API ────────────────────────────────────────────────────

    /// <summary>
    /// Simulates a complete season: regular season (first half → All-Star weekend → second half),
    /// playoffs, and awards.
    /// </summary>
    public static SeasonResult SimulateFullSeason(League league, Random? random = null)
    {
        random ??= Random.Shared;
        ValidateLeagueForSimulation(league);

        // Verify rosters before season starts (C++ VerifyRosters pipeline)
        RosterManagementService.ProcessRosterEmergencies(league, random);

        var result = new SeasonResult
        {
            Year = league.Settings.CurrentYear
        };

        // Phase 1: First half of regular season (up to All-Star break)
        result.FirstHalfResult = SimulateFirstHalf(league, random);

        // Phase 2: All-Star Weekend
        if (league.AllStarWeekend == null)
        {
            var averages = LeagueAveragesCalculator.Calculate(league);
            var aswResult = AllStarWeekendService.RunAllStarWeekend(league, random, averages);
            league.AllStarWeekend = aswResult;
            result.AllStarWeekendResult = aswResult;
            result.AllStarWeekendPlayed = true;
        }

        // Phase 3: Second half of regular season
        result.SecondHalfResult = SimulateSecondHalf(league, random);
        league.Schedule.RegularSeasonEnded = true;

        // Accumulate totals
        result.TotalRegularSeasonGames =
            result.FirstHalfResult.GamesSimulated + result.SecondHalfResult.GamesSimulated;
        result.TotalRegularSeasonDays =
            result.FirstHalfResult.DaysSimulated + result.SecondHalfResult.DaysSimulated;

        // Phase 4: Playoffs
        PlayoffSimulationService.StartPlayoffs(league);
        var playoffResult = PlayoffSimulationService.SimulateAll(league, random);
        result.PlayoffResult = playoffResult;
        result.ChampionTeamIndex = playoffResult.ChampionTeamIndex;

        // Phase 5: Awards
        var awards = AwardsService.ComputeAllAwards(league);
        awards.Year = league.Settings.CurrentYear;
        league.Awards = awards;
        result.Awards = awards;

        return result;
    }

    /// <summary>
    /// Simulates a full season followed by the off-season transition,
    /// leaving the league ready for the next season.
    /// </summary>
    public static SeasonCycleResult SimulateFullCycle(League league, Random? random = null)
    {
        random ??= Random.Shared;

        var cycleResult = new SeasonCycleResult
        {
            SeasonResult = SimulateFullSeason(league, random)
        };

        // Run off-season pipeline (increments year, resets state)
        cycleResult.OffSeasonResult = OffSeasonService.AdvanceSeason(league, random);

        // AdvanceSeason → ResetSeasonState clears Schedule.Games, so regenerate
        ScheduleGenerationService.GenerateSchedule(
            league, league.Schedule.GamesInSeason, random: random);

        return cycleResult;
    }

    /// <summary>
    /// Simulates multiple consecutive seasons (season + off-season each cycle).
    /// </summary>
    public static MultiSeasonResult SimulateMultipleSeasons(League league, int seasons, Random? random = null)
    {
        if (seasons <= 0)
            throw new ArgumentException("Number of seasons must be positive.", nameof(seasons));

        random ??= Random.Shared;

        var result = new MultiSeasonResult();

        for (int i = 0; i < seasons; i++)
        {
            var cycleResult = SimulateFullCycle(league, random);
            result.Seasons.Add(cycleResult);
            result.TotalGamesSimulated +=
                cycleResult.SeasonResult.TotalRegularSeasonGames +
                (cycleResult.SeasonResult.PlayoffResult?.GamesSimulated ?? 0);
        }

        result.TotalSeasonsSimulated = seasons;
        return result;
    }

    // ── Internal Methods ──────────────────────────────────────────────

    /// <summary>
    /// Simulates day-by-day until the All-Star break point is reached.
    /// </summary>
    internal static SimulationResult SimulateFirstHalf(League league, Random random)
    {
        var result = new SimulationResult();
        int daysLeft = MaxDaysPerHalf;

        while (daysLeft-- > 0)
        {
            if (IsAllStarBreakReached(league))
                break;

            bool hasMoreGames = league.Schedule.Games
                .Any(g => !g.Played && g.Type == GameType.League);
            if (!hasMoreGames)
                break;

            var dayResult = SeasonSimulationService.SimulateDay(league, random);
            result.GameResults.AddRange(dayResult.GameResults);
            result.DaysSimulated += dayResult.DaysSimulated;
            result.GamesSimulated += dayResult.GamesSimulated;

            if (dayResult.SeasonComplete)
            {
                result.SeasonComplete = true;
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// Simulates day-by-day from the current point until all regular-season games are played.
    /// </summary>
    internal static SimulationResult SimulateSecondHalf(League league, Random random)
    {
        var result = new SimulationResult();
        int daysLeft = MaxDaysPerHalf;

        while (daysLeft-- > 0)
        {
            bool hasMoreGames = league.Schedule.Games
                .Any(g => !g.Played && g.Type == GameType.League);
            if (!hasMoreGames)
                break;

            var dayResult = SeasonSimulationService.SimulateDay(league, random);
            result.GameResults.AddRange(dayResult.GameResults);
            result.DaysSimulated += dayResult.DaysSimulated;
            result.GamesSimulated += dayResult.GamesSimulated;

            if (dayResult.SeasonComplete)
            {
                result.SeasonComplete = true;
                break;
            }
        }

        result.SeasonComplete = true;
        return result;
    }

    /// <summary>
    /// Returns true when the maximum games played by any team has reached
    /// the All-Star break threshold (45/82 of total season games).
    /// </summary>
    internal static bool IsAllStarBreakReached(League league)
    {
        int gamesInSeason = league.Schedule.GamesInSeason;
        int threshold = (int)(AllStarBreakFraction * gamesInSeason);

        // Count played league games per team
        var playedGames = league.Schedule.Games
            .Where(g => g.Played && g.Type == GameType.League);

        int maxGamesPlayed = 0;
        foreach (var game in playedGames)
        {
            // Each played game counts for both teams — we track per-team via schedule
        }

        // Count per-team: iterate all played games and track home + visitor appearances
        var teamGameCounts = new int[league.Teams.Count];
        foreach (var game in league.Schedule.Games.Where(g => g.Played && g.Type == GameType.League))
        {
            if (game.HomeTeamIndex < teamGameCounts.Length)
                teamGameCounts[game.HomeTeamIndex]++;
            if (game.VisitorTeamIndex < teamGameCounts.Length)
                teamGameCounts[game.VisitorTeamIndex]++;
        }

        maxGamesPlayed = teamGameCounts.Length > 0 ? teamGameCounts.Max() : 0;

        return maxGamesPlayed >= threshold;
    }

    /// <summary>
    /// Validates that the league is in a valid state for simulation.
    /// </summary>
    internal static void ValidateLeagueForSimulation(League league)
    {
        if (league.Teams.Count < 2)
            throw new InvalidOperationException("League must have at least 2 teams to simulate.");

        if (league.Schedule.Games.Count == 0)
            throw new InvalidOperationException("League has no scheduled games.");

        if (league.Schedule.RegularSeasonEnded)
            throw new InvalidOperationException("Regular season has already ended.");
    }
}
