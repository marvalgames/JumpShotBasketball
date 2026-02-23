using JumpShotBasketball.Core.Models.Awards;
using JumpShotBasketball.Core.Models.Player;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// League-wide statistical rankings (top N per category).
/// Replaces C++ CHigh (~2,169 lines of 4D arrays) with LINQ queries.
/// </summary>
public static class LeaderboardService
{
    // Same eligibility thresholds as AwardsService stat leaders
    private const double PointsThreshold = 1400.0 / 82.0;
    private const double ReboundsThreshold = 800.0 / 82.0;
    private const double AssistsThreshold = 400.0 / 82.0;
    private const double StealsThreshold = 125.0 / 82.0;
    private const double BlocksThreshold = 100.0 / 82.0;
    private const double MinGamesFractionAlternate = 70.0 / 82.0;

    // Percentage stat minimums (per 82-game season, scaled)
    private const double MinFgaForPct = 300.0 / 82.0;   // ~3.66 FGA/game
    private const double MinFtaForPct = 125.0 / 82.0;    // ~1.52 FTA/game
    private const double MinTpaForPct = 82.0 / 82.0;     // ~1.0 3PA/game

    public static LeagueLeaderboard ComputeLeaderboard(Models.League.League league, int count = 20)
    {
        return new LeagueLeaderboard
        {
            PointsLeaders = RankByPointsPerGame(league, count),
            ReboundsLeaders = RankByReboundsPerGame(league, count),
            AssistsLeaders = RankByAssistsPerGame(league, count),
            StealsLeaders = RankByStealsPerGame(league, count),
            BlocksLeaders = RankByBlocksPerGame(league, count),
            FieldGoalPctLeaders = RankByFieldGoalPct(league, count),
            FreeThrowPctLeaders = RankByFreeThrowPct(league, count),
            ThreePointPctLeaders = RankByThreePointPct(league, count)
        };
    }

    public static List<StatLeaderEntry> RankByPointsPerGame(Models.League.League league, int count = 20)
    {
        return RankByVolumeStat(league, count,
            p => p.SimulatedStats.PointsPerGame, PointsThreshold);
    }

    public static List<StatLeaderEntry> RankByReboundsPerGame(Models.League.League league, int count = 20)
    {
        return RankByVolumeStat(league, count,
            p => p.SimulatedStats.ReboundsPerGame, ReboundsThreshold);
    }

    public static List<StatLeaderEntry> RankByAssistsPerGame(Models.League.League league, int count = 20)
    {
        return RankByVolumeStat(league, count,
            p => p.SimulatedStats.AssistsPerGame, AssistsThreshold);
    }

    public static List<StatLeaderEntry> RankByStealsPerGame(Models.League.League league, int count = 20)
    {
        return RankByVolumeStat(league, count,
            p => p.SimulatedStats.StealsPerGame, StealsThreshold);
    }

    public static List<StatLeaderEntry> RankByBlocksPerGame(Models.League.League league, int count = 20)
    {
        return RankByVolumeStat(league, count,
            p => p.SimulatedStats.BlocksPerGame, BlocksThreshold);
    }

    public static List<StatLeaderEntry> RankByFieldGoalPct(Models.League.League league, int count = 20)
    {
        return RankByPercentageStat(league, count,
            p => p.SimulatedStats.FieldGoalPercentage,
            p => p.SimulatedStats.FieldGoalsAttempted,
            MinFgaForPct);
    }

    public static List<StatLeaderEntry> RankByFreeThrowPct(Models.League.League league, int count = 20)
    {
        return RankByPercentageStat(league, count,
            p => p.SimulatedStats.FreeThrowPercentage,
            p => p.SimulatedStats.FreeThrowsAttempted,
            MinFtaForPct);
    }

    public static List<StatLeaderEntry> RankByThreePointPct(Models.League.League league, int count = 20)
    {
        return RankByPercentageStat(league, count,
            p => p.SimulatedStats.ThreePointPercentage,
            p => p.SimulatedStats.ThreePointersAttempted,
            MinTpaForPct);
    }

    // ───────────────────────────────────────────────────────────────
    // Private helpers
    // ───────────────────────────────────────────────────────────────

    private static List<StatLeaderEntry> RankByVolumeStat(
        Models.League.League league,
        int count,
        Func<Player, double> statSelector,
        double volumeThreshold)
    {
        int gamesInSeason = league.Schedule.GamesInSeason;

        return FlattenRosters(league)
            .Where(t =>
            {
                var stats = t.Player.SimulatedStats;
                if (stats.Games <= 0 || string.IsNullOrEmpty(t.Player.Name)) return false;
                double gamesFraction = gamesInSeason > 0 ? (double)stats.Games / gamesInSeason : 0;
                double perGame = statSelector(t.Player);
                double weightedStat = perGame * gamesFraction;
                return weightedStat >= volumeThreshold || gamesFraction >= MinGamesFractionAlternate;
            })
            .Select(t => new
            {
                t.Player,
                t.TeamIndex,
                Stat = statSelector(t.Player)
            })
            .OrderByDescending(x => x.Stat)
            .Take(count)
            .Select(x => new StatLeaderEntry
            {
                PlayerId = x.Player.Id,
                PlayerName = x.Player.Name,
                TeamIndex = x.TeamIndex,
                Position = x.Player.Position,
                PerGameAverage = x.Stat,
                GamesPlayed = x.Player.SimulatedStats.Games
            })
            .ToList();
    }

    private static List<StatLeaderEntry> RankByPercentageStat(
        Models.League.League league,
        int count,
        Func<Player, double> pctSelector,
        Func<Player, int> attemptsSelector,
        double minAttemptsPerGame)
    {
        int gamesInSeason = league.Schedule.GamesInSeason;

        return FlattenRosters(league)
            .Where(t =>
            {
                var stats = t.Player.SimulatedStats;
                if (stats.Games <= 0 || string.IsNullOrEmpty(t.Player.Name)) return false;
                double gamesFraction = gamesInSeason > 0 ? (double)stats.Games / gamesInSeason : 0;
                if (gamesFraction < MinGamesFractionAlternate && gamesFraction < 0.625) return false;
                double attemptsPerGame = (double)attemptsSelector(t.Player) / stats.Games;
                return attemptsPerGame >= minAttemptsPerGame;
            })
            .Select(t => new
            {
                t.Player,
                t.TeamIndex,
                Pct = pctSelector(t.Player)
            })
            .OrderByDescending(x => x.Pct)
            .Take(count)
            .Select(x => new StatLeaderEntry
            {
                PlayerId = x.Player.Id,
                PlayerName = x.Player.Name,
                TeamIndex = x.TeamIndex,
                Position = x.Player.Position,
                PerGameAverage = x.Pct,
                GamesPlayed = x.Player.SimulatedStats.Games
            })
            .ToList();
    }

    private static IEnumerable<(Player Player, int TeamIndex)> FlattenRosters(Models.League.League league)
    {
        for (int i = 0; i < league.Teams.Count; i++)
        {
            foreach (var player in league.Teams[i].Roster)
            {
                yield return (player, i);
            }
        }
    }
}
