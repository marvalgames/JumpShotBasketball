using JumpShotBasketball.Core.Enums;
using JumpShotBasketball.Core.Models.Game;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Playoff;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Orchestrates playoff simulation: game, day, round, and full playoffs.
/// Uses GameSimulationEngine + PostGameProcessor directly (not SeasonSimulationService)
/// so playoff games live in the bracket, not the regular schedule.
/// </summary>
public static class PlayoffSimulationService
{
    /// <summary>
    /// Initializes playoffs: generates bracket and attaches it to the league.
    /// </summary>
    public static PlayoffBracket StartPlayoffs(League league)
    {
        var bracket = PlayoffService.GenerateBracket(league);
        league.Bracket = bracket;
        league.Schedule.PlayoffsStarted = true;
        return bracket;
    }

    /// <summary>
    /// Simulates a single playoff game and processes all side effects.
    /// Returns null if no games remain.
    /// </summary>
    public static GameResult? SimulateGame(League league, Random? random = null)
    {
        random ??= Random.Shared;
        var bracket = league.Bracket;
        if (bracket == null || bracket.PlayoffsComplete) return null;

        var nextGame = PlayoffService.GetNextPlayoffGame(bracket);
        if (nextGame == null) return null;

        var visitorTeam = league.Teams[nextGame.VisitorTeamIndex];
        var homeTeam = league.Teams[nextGame.HomeTeamIndex];

        // Calculate league averages
        var leagueAverages = LeagueAveragesCalculator.Calculate(league);

        // Simulate the game
        var engine = new GameSimulationEngine(random);
        var result = engine.SimulateGame(
            visitorTeam, homeTeam, GameType.Playoff, leagueAverages,
            league.Settings.ScoringFactor > 0 ? league.Settings.ScoringFactor : 1.0);

        // Create a ScheduledGame wrapper for PostGameProcessor
        var scheduledGame = new ScheduledGame
        {
            HomeTeamIndex = nextGame.HomeTeamIndex,
            VisitorTeamIndex = nextGame.VisitorTeamIndex,
            Type = GameType.Playoff
        };

        // Process post-game (stats to PlayoffStats, standings, season highs)
        PostGameProcessor.ProcessGame(league, scheduledGame, result);

        // Apply injuries
        ApplyGameInjuries(league, nextGame, result, random);

        // Record result in the bracket
        var currentRound = bracket.Rounds.Last();
        var series = currentRound.Series.FirstOrDefault(s =>
            s.Games.Contains(nextGame));

        if (series != null)
        {
            PlayoffService.RecordGameResult(series, nextGame,
                result.HomeScore, result.VisitorScore);
        }

        // Check for round advancement
        PlayoffService.TryAdvanceRound(bracket, league);

        // Finalize if complete
        if (bracket.PlayoffsComplete)
        {
            PlayoffService.FinalizePlayoffs(league);
        }

        return result;
    }

    /// <summary>
    /// Simulates all games in the next "day" of playoffs (one game per active series).
    /// After all games: heal injuries, set computer rotations.
    /// </summary>
    public static PlayoffSimulationResult SimulateDay(League league, Random? random = null)
    {
        random ??= Random.Shared;
        var result = new PlayoffSimulationResult();
        var bracket = league.Bracket;

        if (bracket == null || bracket.PlayoffsComplete)
        {
            result.PlayoffsComplete = bracket?.PlayoffsComplete ?? false;
            result.ChampionTeamIndex = bracket?.ChampionTeamIndex;
            return result;
        }

        // Simulate one game per active series in the current round
        var currentRound = bracket.Rounds.LastOrDefault();
        if (currentRound == null) return result;

        var seriesToPlay = currentRound.Series.Where(s => !s.IsComplete).ToList();

        foreach (var series in seriesToPlay)
        {
            var gameResult = SimulateGame(league, random);
            if (gameResult != null)
            {
                result.GameResults.Add(gameResult);
                result.GamesSimulated++;
            }
        }

        // Day boundary: heal injuries and set rotations
        InjuryService.HealInjuries(league, 1, random);
        RotationService.SetComputerRotations(league);

        // Check state after day
        result.RoundComplete = currentRound.IsComplete;
        result.PlayoffsComplete = bracket.PlayoffsComplete;
        result.ChampionTeamIndex = bracket.ChampionTeamIndex;

        return result;
    }

    /// <summary>
    /// Simulates all remaining games in the current round.
    /// </summary>
    public static PlayoffSimulationResult SimulateRound(League league, Random? random = null)
    {
        random ??= Random.Shared;
        var result = new PlayoffSimulationResult();
        var bracket = league.Bracket;

        if (bracket == null || bracket.PlayoffsComplete)
        {
            result.PlayoffsComplete = bracket?.PlayoffsComplete ?? false;
            result.ChampionTeamIndex = bracket?.ChampionTeamIndex;
            return result;
        }

        int maxGames = 100; // safety limit
        while (maxGames-- > 0)
        {
            var currentRound = bracket.Rounds.LastOrDefault();
            if (currentRound == null || currentRound.IsComplete) break;

            var dayResult = SimulateDay(league, random);
            result.GameResults.AddRange(dayResult.GameResults);
            result.GamesSimulated += dayResult.GamesSimulated;

            if (dayResult.GamesSimulated == 0) break;
        }

        result.RoundComplete = true;
        result.PlayoffsComplete = bracket.PlayoffsComplete;
        result.ChampionTeamIndex = bracket.ChampionTeamIndex;

        return result;
    }

    /// <summary>
    /// Simulates the entire playoffs from current state to champion.
    /// </summary>
    public static PlayoffSimulationResult SimulateAll(League league, Random? random = null)
    {
        random ??= Random.Shared;
        var result = new PlayoffSimulationResult();
        var bracket = league.Bracket;

        if (bracket == null)
        {
            bracket = StartPlayoffs(league);
        }

        int maxGames = 500; // safety limit
        while (maxGames-- > 0 && !bracket.PlayoffsComplete)
        {
            var gameResult = SimulateGame(league, random);
            if (gameResult == null) break;

            result.GameResults.Add(gameResult);
            result.GamesSimulated++;

            // Day boundary after each game (simplified — one game at a time)
            // Real implementation would batch by day, but for SimulateAll this is equivalent
        }

        // Final day boundary
        InjuryService.HealInjuries(league, 1, random);
        RotationService.SetComputerRotations(league);

        result.PlayoffsComplete = bracket.PlayoffsComplete;
        result.ChampionTeamIndex = bracket.ChampionTeamIndex;

        return result;
    }

    private static void ApplyGameInjuries(League league, PlayoffGame game, GameResult result, Random random)
    {
        var visitorTeam = league.Teams[game.VisitorTeamIndex];
        var homeTeam = league.Teams[game.HomeTeamIndex];

        ApplyTeamInjuries(visitorTeam, result.VisitorBoxScore, random);
        ApplyTeamInjuries(homeTeam, result.HomeBoxScore, random);
    }

    private static void ApplyTeamInjuries(
        Models.Team.Team team, List<PlayerGameState> boxScore, Random random)
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
