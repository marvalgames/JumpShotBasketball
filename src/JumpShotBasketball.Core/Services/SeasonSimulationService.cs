using JumpShotBasketball.Core.Enums;
using JumpShotBasketball.Core.Models.Game;
using JumpShotBasketball.Core.Models.League;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Orchestrates season simulation: single game, day, week, month, full season.
/// Connects GameSimulationEngine with post-game processing, injury healing,
/// and rotation management.
/// </summary>
public static class SeasonSimulationService
{
    /// <summary>
    /// Simulates a single scheduled game and processes all side effects.
    /// </summary>
    public static GameResult SimulateGame(League league, ScheduledGame game, Random? random = null)
    {
        random ??= Random.Shared;

        var visitorTeam = league.Teams[game.VisitorTeamIndex];
        var homeTeam = league.Teams[game.HomeTeamIndex];

        // Calculate league averages (use defaults if no stats yet)
        var leagueAverages = LeagueAveragesCalculator.Calculate(league);

        var engine = new GameSimulationEngine(random);
        var result = engine.SimulateGame(
            visitorTeam, homeTeam, game.Type, leagueAverages,
            league.Settings.ScoringFactor > 0 ? league.Settings.ScoringFactor : 1.0);

        PostGameProcessor.ProcessGame(league, game, result);

        // Apply injuries from the game
        ApplyGameInjuries(league, game, result, random);

        // Process financials (home team gets full revenue, road teams get media only)
        if (league.Settings.FinancialEnabled)
        {
            FinancialSimulationService.ProcessHomeGameFinancials(
                league, game.HomeTeamIndex, game.VisitorTeamIndex, game.Type, random);
            FinancialSimulationService.ProcessRoadGameFinancials(league, game.VisitorTeamIndex);
        }

        return result;
    }

    /// <summary>
    /// Simulates all games on the next unplayed day.
    /// After all games: heal injuries (1 day), set computer rotations.
    /// </summary>
    public static SimulationResult SimulateDay(League league, Random? random = null)
    {
        random ??= Random.Shared;
        var result = new SimulationResult();

        var unplayedGames = league.Schedule.Games
            .Where(g => !g.Played && g.Type == GameType.League)
            .ToList();

        if (unplayedGames.Count == 0)
        {
            result.SeasonComplete = true;
            league.Schedule.RegularSeasonEnded = true;
            return result;
        }

        // Find the lowest Day value among unplayed games
        int nextDay = unplayedGames.Min(g => g.Day);

        // Get all games on that day
        var todaysGames = unplayedGames.Where(g => g.Day == nextDay).ToList();

        foreach (var game in todaysGames)
        {
            var gameResult = SimulateGame(league, game, random);
            result.GameResults.Add(gameResult);
        }

        result.GamesSimulated = todaysGames.Count;
        result.DaysSimulated = 1;

        // Mark season as started
        if (!league.Schedule.SeasonStarted)
            league.Schedule.SeasonStarted = true;

        // Day boundary processing
        ProcessDayBoundary(league, random);

        // Check if season is complete
        bool hasMoreGames = league.Schedule.Games
            .Any(g => !g.Played && g.Type == GameType.League);
        if (!hasMoreGames)
        {
            result.SeasonComplete = true;
            league.Schedule.RegularSeasonEnded = true;
        }

        result.InvalidRosterTeamIndices = RotationService.VerifyAllRosters(league);

        return result;
    }

    /// <summary>
    /// Simulates up to 7 game-days (skips off-days).
    /// </summary>
    public static SimulationResult SimulateWeek(League league, Random? random = null)
    {
        return SimulateMultipleDays(league, 7, random);
    }

    /// <summary>
    /// Simulates approximately 30 game-days (~1 calendar month).
    /// </summary>
    public static SimulationResult SimulateMonth(League league, Random? random = null)
    {
        return SimulateMultipleDays(league, 30, random);
    }

    /// <summary>
    /// Simulates all remaining regular-season games.
    /// Sets RegularSeasonEnded flag when complete.
    /// </summary>
    public static SimulationResult SimulateSeason(League league, Random? random = null)
    {
        random ??= Random.Shared;
        var result = new SimulationResult();

        int maxDays = 300; // safety limit
        while (maxDays-- > 0)
        {
            bool hasMoreGames = league.Schedule.Games
                .Any(g => !g.Played && g.Type == GameType.League);
            if (!hasMoreGames) break;

            var dayResult = SimulateDay(league, random);
            result.GameResults.AddRange(dayResult.GameResults);
            result.DaysSimulated += dayResult.DaysSimulated;
            result.GamesSimulated += dayResult.GamesSimulated;

            if (dayResult.SeasonComplete) break;
        }

        result.SeasonComplete = true;
        league.Schedule.RegularSeasonEnded = true;
        result.InvalidRosterTeamIndices = RotationService.VerifyAllRosters(league);

        return result;
    }

    private static SimulationResult SimulateMultipleDays(League league, int maxDays, Random? random)
    {
        random ??= Random.Shared;
        var result = new SimulationResult();

        for (int d = 0; d < maxDays; d++)
        {
            bool hasMoreGames = league.Schedule.Games
                .Any(g => !g.Played && g.Type == GameType.League);
            if (!hasMoreGames)
            {
                result.SeasonComplete = true;
                league.Schedule.RegularSeasonEnded = true;
                break;
            }

            var dayResult = SimulateDay(league, random);
            result.GameResults.AddRange(dayResult.GameResults);
            result.DaysSimulated += dayResult.DaysSimulated;
            result.GamesSimulated += dayResult.GamesSimulated;

            if (dayResult.SeasonComplete)
            {
                result.SeasonComplete = true;
                break;
            }
        }

        result.InvalidRosterTeamIndices = RotationService.VerifyAllRosters(league);
        return result;
    }

    private static void ProcessDayBoundary(League league, Random random)
    {
        // 1. Heal injuries (1 day)
        InjuryService.HealInjuries(league, 1, random);

        // 2. Run computer trades (if enabled)
        if (league.Settings.ComputerTradesEnabled)
        {
            TradeService.RunTradingPeriod(league, 10, 1, random);
        }

        // 3. Set computer rotations
        RotationService.SetComputerRotations(league);
    }

    private static void ApplyGameInjuries(League league, ScheduledGame game, GameResult result, Random random)
    {
        var visitorTeam = league.Teams[game.VisitorTeamIndex];
        var homeTeam = league.Teams[game.HomeTeamIndex];

        ApplyTeamInjuries(visitorTeam, result.VisitorBoxScore, random);
        ApplyTeamInjuries(homeTeam, result.HomeBoxScore, random);
    }

    private static void ApplyTeamInjuries(
        Models.Team.Team team, List<Models.Player.PlayerGameState> boxScore, Random random)
    {
        foreach (var gs in boxScore)
        {
            if (gs.GameInjury <= 0) continue;

            var player = team.Roster.FirstOrDefault(p => p.Name == gs.GameName);
            if (player != null)
            {
                InjuryService.ApplyInjury(player, gs.GameInjury, random);
            }
        }
    }
}
