using JumpShotBasketball.Core.Enums;
using JumpShotBasketball.Core.Models.Game;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Post-game processing: stat accumulation, season highs, schedule marking, standings.
/// Port of Engine.cpp L7088-7262 and Player.cpp L379-439.
/// </summary>
public static class PostGameProcessor
{
    /// <summary>
    /// Full post-game pipeline: mark schedule, update standings, accumulate stats,
    /// update season highs, record starters.
    /// </summary>
    public static void ProcessGame(League league, ScheduledGame game, GameResult result)
    {
        // 1. Mark game as played with scores
        game.Played = true;
        game.HomeScore = result.HomeScore;
        game.VisitorScore = result.VisitorScore;

        // 2. Update standings (League/Playoff games only)
        if (game.Type == GameType.League || game.Type == GameType.SingleTeam || game.Type == GameType.Playoff)
        {
            StatisticsCalculator.UpdateStandings(
                league, game.VisitorTeamIndex, result.VisitorScore,
                game.HomeTeamIndex, result.HomeScore);
        }

        // 3. Accumulate stats for all players
        var visitorTeam = league.Teams[game.VisitorTeamIndex];
        var homeTeam = league.Teams[game.HomeTeamIndex];

        AccumulateTeamStats(visitorTeam, result.VisitorBoxScore, game.Type);
        AccumulateTeamStats(homeTeam, result.HomeBoxScore, game.Type);

        // 4. Update season highs (not for Exhibition/AllStar/Rookie)
        if (game.Type != GameType.Exhibition && game.Type != GameType.AllStar && game.Type != GameType.Rookie)
        {
            UpdateTeamSeasonHighs(visitorTeam, result.VisitorBoxScore, game.Type);
            UpdateTeamSeasonHighs(homeTeam, result.HomeBoxScore, game.Type);
        }

        // 4a. Update record book single-game records
        if (league.RecordBook != null && game.Type != GameType.Exhibition
            && game.Type != GameType.AllStar && game.Type != GameType.Rookie)
        {
            RecordBookService.UpdateSingleGameRecords(league.RecordBook,
                result.VisitorBoxScore, visitorTeam, game.VisitorTeamIndex, game.Type, league.Settings.CurrentYear);
            RecordBookService.UpdateSingleGameRecords(league.RecordBook,
                result.HomeBoxScore, homeTeam, game.HomeTeamIndex, game.Type, league.Settings.CurrentYear);
        }

        // 5. Record starters (League games only)
        if (game.Type == GameType.League)
        {
            var starters = new List<Player>();
            foreach (var gs in result.VisitorBoxScore.Take(5))
            {
                var player = FindPlayerByGameState(visitorTeam, gs);
                if (player != null) starters.Add(player);
            }
            foreach (var gs in result.HomeBoxScore.Take(5))
            {
                var player = FindPlayerByGameState(homeTeam, gs);
                if (player != null) starters.Add(player);
            }
            RecordStarters(starters);
        }
    }

    /// <summary>
    /// Accumulates PlayerGameState box score into season/playoff PlayerStatLine.
    /// Minutes converted from seconds to minutes (rounded, min 1 if played).
    /// League/SingleTeam games -> SimulatedStats; Playoff games -> PlayoffStats.
    /// Exhibition/AllStar/Rookie games do NOT accumulate stats.
    /// </summary>
    public static void AccumulateStats(Player player, PlayerGameState gameState, GameType gameType)
    {
        if (gameType == GameType.Exhibition || gameType == GameType.AllStar || gameType == GameType.Rookie)
            return;

        var statLine = gameType == GameType.Playoff ? player.PlayoffStats : player.SimulatedStats;

        // Only count as a game if player had minutes
        int games = gameState.Minutes != 0 ? 1 : 0;

        // Convert seconds to minutes, minimum 1 if played
        int min = (int)Math.Round(gameState.Minutes / 60.0);
        if (gameState.Minutes > 0 && min == 0) min = 1;

        statLine.Games += games;
        statLine.Minutes += min;
        statLine.FieldGoalsMade += gameState.FieldGoalsMade;
        statLine.FieldGoalsAttempted += gameState.FieldGoalsAttempted;
        statLine.FreeThrowsMade += gameState.FreeThrowsMade;
        statLine.FreeThrowsAttempted += gameState.FreeThrowsAttempted;
        statLine.ThreePointersMade += gameState.ThreePointersMade;
        statLine.ThreePointersAttempted += gameState.ThreePointersAttempted;
        statLine.OffensiveRebounds += gameState.OffensiveRebounds;
        statLine.Rebounds += gameState.OffensiveRebounds + gameState.DefensiveRebounds;
        statLine.Assists += gameState.Assists;
        statLine.Steals += gameState.Steals;
        statLine.Turnovers += gameState.Turnovers;
        statLine.Blocks += gameState.Blocks;
        statLine.PersonalFouls += gameState.PersonalFouls;
    }

    /// <summary>
    /// Updates season/career/playoff highs from game performance.
    /// Double-double: 2+ categories >= 10 (pts/reb/ast/stl/blk).
    /// Triple-double: 3+ categories >= 10.
    /// Port of CPlayer::SetGameHighs() Player.cpp L379-439.
    /// </summary>
    public static void UpdateSeasonHighs(Player player, PlayerGameState gameState, GameType gameType)
    {
        // Points: fgm*2 + tfgm (3PM extra point) + ftm
        // Note: 3-pointers count in both FGM and 3PM, so fgm*2 + tfgm + ftm
        int points = gameState.FieldGoalsMade * 2 + gameState.ThreePointersMade + gameState.FreeThrowsMade;
        int reb = gameState.OffensiveRebounds + gameState.DefensiveRebounds;
        int ast = gameState.Assists;
        int stl = gameState.Steals;
        int blk = gameState.Blocks;

        // Count categories >= 10
        int cats = 0;
        if (points >= 10) cats++;
        if (reb >= 10) cats++;
        if (ast >= 10) cats++;
        if (stl >= 10) cats++;
        if (blk >= 10) cats++;

        var highs = player.SeasonHighs;

        if (gameType == GameType.League || gameType == GameType.SingleTeam)
        {
            // Double-double: cats == 2 or cats == 3 (C++ logic)
            if (cats >= 2)
            {
                highs.SeasonDoubleDoubles++;
                highs.CareerDoubleDoubles++;
            }
            if (cats >= 3)
            {
                highs.SeasonTripleDoubles++;
                highs.CareerTripleDoubles++;
            }

            // Season highs
            if (points > highs.SeasonPoints) highs.SeasonPoints = points;
            if (reb > highs.SeasonRebounds) highs.SeasonRebounds = reb;
            if (ast > highs.SeasonAssists) highs.SeasonAssists = ast;
            if (stl > highs.SeasonSteals) highs.SeasonSteals = stl;
            if (blk > highs.SeasonBlocks) highs.SeasonBlocks = blk;

            // Career highs
            if (points > highs.CareerPoints) highs.CareerPoints = points;
            if (reb > highs.CareerRebounds) highs.CareerRebounds = reb;
            if (ast > highs.CareerAssists) highs.CareerAssists = ast;
            if (stl > highs.CareerSteals) highs.CareerSteals = stl;
            if (blk > highs.CareerBlocks) highs.CareerBlocks = blk;
        }
        else if (gameType == GameType.Playoff)
        {
            // Playoff season highs
            if (points > highs.PlayoffPoints) highs.PlayoffPoints = points;
            if (reb > highs.PlayoffRebounds) highs.PlayoffRebounds = reb;
            if (ast > highs.PlayoffAssists) highs.PlayoffAssists = ast;
            if (stl > highs.PlayoffSteals) highs.PlayoffSteals = stl;
            if (blk > highs.PlayoffBlocks) highs.PlayoffBlocks = blk;

            // Career playoff highs
            if (points > highs.CareerPlayoffPoints) highs.CareerPlayoffPoints = points;
            if (reb > highs.CareerPlayoffRebounds) highs.CareerPlayoffRebounds = reb;
            if (ast > highs.CareerPlayoffAssists) highs.CareerPlayoffAssists = ast;
            if (stl > highs.CareerPlayoffSteals) highs.CareerPlayoffSteals = stl;
            if (blk > highs.CareerPlayoffBlocks) highs.CareerPlayoffBlocks = blk;
        }
    }

    /// <summary>
    /// Increments Starter count for each player in the starters list.
    /// </summary>
    public static void RecordStarters(List<Player> starters)
    {
        foreach (var player in starters)
        {
            player.Starter++;
        }
    }

    private static void AccumulateTeamStats(
        Models.Team.Team team, List<PlayerGameState> boxScore, GameType gameType)
    {
        foreach (var gs in boxScore)
        {
            var player = FindPlayerByGameState(team, gs);
            if (player != null)
            {
                AccumulateStats(player, gs, gameType);
            }
        }
    }

    private static void UpdateTeamSeasonHighs(
        Models.Team.Team team, List<PlayerGameState> boxScore, GameType gameType)
    {
        foreach (var gs in boxScore)
        {
            var player = FindPlayerByGameState(team, gs);
            if (player != null)
            {
                UpdateSeasonHighs(player, gs, gameType);
            }
        }
    }

    private static Player? FindPlayerByGameState(Models.Team.Team team, PlayerGameState gs)
    {
        // Match by name (GameName set during engine initialization)
        if (!string.IsNullOrEmpty(gs.GameName))
        {
            return team.Roster.FirstOrDefault(p => p.Name == gs.GameName);
        }
        // Fallback: match by stat slot index
        if (gs.StatSlot > 0 && gs.StatSlot <= team.Roster.Count)
        {
            return team.Roster[gs.StatSlot - 1];
        }
        return null;
    }
}
