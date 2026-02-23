using JumpShotBasketball.Core.Models.Awards;
using JumpShotBasketball.Core.Models.Player;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Computes end-of-season awards: MVP, DPOY, ROY, 6th Man, Playoff MVP,
/// stat leaders, and positional All-Teams.
/// Ported from C++ CAwards (~2,664 lines) replaced with LINQ queries.
/// </summary>
public static class AwardsService
{
    // Eligibility constants (from C++ Awards.cpp)
    private const double MinMpgStandard = 24.0;
    private const double MinMpgSixthMan = 16.0;
    private const double MinGamesFraction = 0.625;
    private const double MaxStarterFractionSixthMan = 0.375;
    private const int MaxRookieYearsOfService = 1;
    private const double MinGamesFractionAlternate = 70.0 / 82.0;

    // Stat leader volume thresholds (per 82-game season)
    private const double PointsThreshold = 1400.0 / 82.0;
    private const double ReboundsThreshold = 800.0 / 82.0;
    private const double AssistsThreshold = 400.0 / 82.0;
    private const double StealsThreshold = 125.0 / 82.0;
    private const double BlocksThreshold = 100.0 / 82.0;

    private static readonly string[] Positions = { "PG", "SG", "SF", "PF", "C" };

    // ───────────────────────────────────────────────────────────────
    // Orchestrator
    // ───────────────────────────────────────────────────────────────

    public static SeasonAwards ComputeAllAwards(Models.League.League league)
    {
        var awards = new SeasonAwards();

        awards.Mvp = new AwardResult
        {
            AwardName = "MVP",
            Recipients = ComputeMvp(league)
        };
        awards.DefensivePlayerOfYear = new AwardResult
        {
            AwardName = "DPOY",
            Recipients = ComputeDefensivePlayer(league)
        };
        awards.RookieOfYear = new AwardResult
        {
            AwardName = "ROY",
            Recipients = ComputeRookieOfYear(league)
        };
        awards.SixthMan = new AwardResult
        {
            AwardName = "6th Man",
            Recipients = ComputeSixthMan(league)
        };

        var playoffMvp = ComputePlayoffMvp(league);
        awards.PlayoffMvp = new AwardResult
        {
            AwardName = "Playoff MVP",
            Recipients = playoffMvp != null ? new List<AwardRecipient> { playoffMvp } : new()
        };

        awards.ScoringLeader = new AwardResult
        {
            AwardName = "Scoring",
            Recipients = ComputeScoringLeaders(league)
        };
        awards.ReboundingLeader = new AwardResult
        {
            AwardName = "Rebounding",
            Recipients = ComputeReboundingLeaders(league)
        };
        awards.AssistsLeader = new AwardResult
        {
            AwardName = "Assists",
            Recipients = ComputeAssistsLeaders(league)
        };
        awards.StealsLeader = new AwardResult
        {
            AwardName = "Steals",
            Recipients = ComputeStealsLeaders(league)
        };
        awards.BlocksLeader = new AwardResult
        {
            AwardName = "Blocks",
            Recipients = ComputeBlocksLeaders(league)
        };

        awards.AllLeagueTeams = ComputeAllLeagueTeams(league);
        awards.AllDefenseTeams = ComputeAllDefenseTeams(league);
        awards.AllRookieTeams = ComputeAllRookieTeams(league);

        // Championship
        if (league.Bracket?.ChampionTeamIndex is int champIdx && champIdx >= 0 && champIdx < league.Teams.Count)
        {
            awards.ChampionTeamIndex = champIdx;
            awards.RingRecipientPlayerIds = league.Teams[champIdx].Roster
                .Select(p => p.Id)
                .ToList();
        }

        return awards;
    }

    // ───────────────────────────────────────────────────────────────
    // Individual awards
    // ───────────────────────────────────────────────────────────────

    public static List<AwardRecipient> ComputeMvp(Models.League.League league, int count = 5)
    {
        int gamesInSeason = league.Schedule.GamesInSeason;
        double leagueFactor = ComputeLeagueFactor(league);

        return FlattenRosters(league)
            .Where(t => IsStandardEligible(t.Player.SimulatedStats, gamesInSeason))
            .Select(t => new
            {
                t.Player,
                t.TeamIndex,
                Rating = StatisticsCalculator.CalculateTrueRating(t.Player.SimulatedStats, leagueFactor)
            })
            .OrderByDescending(x => x.Rating)
            .Take(count)
            .Select((x, i) => ToRecipient(x.Player, x.TeamIndex, x.Rating, i + 1))
            .ToList();
    }

    public static AwardRecipient? ComputePlayoffMvp(Models.League.League league)
    {
        if (league.Bracket?.ChampionTeamIndex is not int champIdx)
            return null;
        if (champIdx < 0 || champIdx >= league.Teams.Count)
            return null;

        double leagueFactor = ComputeLeagueFactor(league);
        var champTeam = league.Teams[champIdx];

        return champTeam.Roster
            .Where(p => p.PlayoffStats.Games > 0 && p.PlayoffStats.MinutesPerGame >= MinMpgStandard)
            .Select(p => new
            {
                Player = p,
                Rating = StatisticsCalculator.CalculateTrueRating(p.PlayoffStats, leagueFactor)
            })
            .OrderByDescending(x => x.Rating)
            .Select(x => ToRecipient(x.Player, champIdx, x.Rating, 1))
            .FirstOrDefault();
    }

    public static List<AwardRecipient> ComputeDefensivePlayer(Models.League.League league, int count = 5)
    {
        int gamesInSeason = league.Schedule.GamesInSeason;

        return FlattenRosters(league)
            .Where(t => IsStandardEligible(t.Player.SimulatedStats, gamesInSeason))
            .Select(t => new
            {
                t.Player,
                t.TeamIndex,
                Rating = StatisticsCalculator.CalculateDefenseRating(t.Player.SimulatedStats)
            })
            .OrderByDescending(x => x.Rating)
            .Take(count)
            .Select((x, i) => ToRecipient(x.Player, x.TeamIndex, x.Rating, i + 1))
            .ToList();
    }

    public static List<AwardRecipient> ComputeRookieOfYear(Models.League.League league, int count = 5)
    {
        double leagueFactor = ComputeLeagueFactor(league);

        return FlattenRosters(league)
            .Where(t => IsRookie(t.Player) && !string.IsNullOrEmpty(t.Player.Name))
            .Select(t => new
            {
                t.Player,
                t.TeamIndex,
                Rating = StatisticsCalculator.CalculateTrueRating(t.Player.SimulatedStats, leagueFactor)
            })
            .OrderByDescending(x => x.Rating)
            .Take(count)
            .Select((x, i) => ToRecipient(x.Player, x.TeamIndex, x.Rating, i + 1))
            .ToList();
    }

    public static List<AwardRecipient> ComputeSixthMan(Models.League.League league, int count = 5)
    {
        int gamesInSeason = league.Schedule.GamesInSeason;
        double leagueFactor = ComputeLeagueFactor(league);

        return FlattenRosters(league)
            .Where(t =>
            {
                var stats = t.Player.SimulatedStats;
                if (string.IsNullOrEmpty(t.Player.Name)) return false;
                if (stats.Games <= 0) return false;
                double starterRatio = (double)t.Player.Starter / stats.Games;
                if (starterRatio > MaxStarterFractionSixthMan) return false;
                if (stats.MinutesPerGame < MinMpgSixthMan) return false;
                double gamesFraction = (double)stats.Games / gamesInSeason;
                if (gamesFraction < MinGamesFraction) return false;
                return true;
            })
            .Select(t => new
            {
                t.Player,
                t.TeamIndex,
                Rating = StatisticsCalculator.CalculateTrueRating(t.Player.SimulatedStats, leagueFactor)
            })
            .OrderByDescending(x => x.Rating)
            .Take(count)
            .Select((x, i) => ToRecipient(x.Player, x.TeamIndex, x.Rating, i + 1))
            .ToList();
    }

    // ───────────────────────────────────────────────────────────────
    // Statistical leaders
    // ───────────────────────────────────────────────────────────────

    public static List<AwardRecipient> ComputeScoringLeaders(Models.League.League league, int count = 5)
    {
        return ComputeStatLeaders(league, count,
            p => p.SimulatedStats.PointsPerGame,
            PointsThreshold);
    }

    public static List<AwardRecipient> ComputeReboundingLeaders(Models.League.League league, int count = 5)
    {
        return ComputeStatLeaders(league, count,
            p => p.SimulatedStats.ReboundsPerGame,
            ReboundsThreshold);
    }

    public static List<AwardRecipient> ComputeAssistsLeaders(Models.League.League league, int count = 5)
    {
        return ComputeStatLeaders(league, count,
            p => p.SimulatedStats.AssistsPerGame,
            AssistsThreshold);
    }

    public static List<AwardRecipient> ComputeStealsLeaders(Models.League.League league, int count = 5)
    {
        return ComputeStatLeaders(league, count,
            p => p.SimulatedStats.StealsPerGame,
            StealsThreshold);
    }

    public static List<AwardRecipient> ComputeBlocksLeaders(Models.League.League league, int count = 5)
    {
        return ComputeStatLeaders(league, count,
            p => p.SimulatedStats.BlocksPerGame,
            BlocksThreshold);
    }

    // ───────────────────────────────────────────────────────────────
    // Positional All-Teams
    // ───────────────────────────────────────────────────────────────

    public static List<AllTeamSelection> ComputeAllLeagueTeams(Models.League.League league, int teamCount = 3)
    {
        int gamesInSeason = league.Schedule.GamesInSeason;
        double leagueFactor = ComputeLeagueFactor(league);

        var eligible = FlattenRosters(league)
            .Where(t => IsStandardEligible(t.Player.SimulatedStats, gamesInSeason))
            .Select(t => new PlayerCandidate
            {
                Player = t.Player,
                TeamIndex = t.TeamIndex,
                Rating = StatisticsCalculator.CalculateTrueRating(t.Player.SimulatedStats, leagueFactor)
            })
            .ToList();

        return SelectPositionalTeams(eligible, teamCount, "All-League");
    }

    public static List<AllTeamSelection> ComputeAllDefenseTeams(Models.League.League league, int teamCount = 2)
    {
        int gamesInSeason = league.Schedule.GamesInSeason;

        var eligible = FlattenRosters(league)
            .Where(t => IsStandardEligible(t.Player.SimulatedStats, gamesInSeason))
            .Select(t => new PlayerCandidate
            {
                Player = t.Player,
                TeamIndex = t.TeamIndex,
                Rating = StatisticsCalculator.CalculateDefenseRating(t.Player.SimulatedStats)
            })
            .ToList();

        return SelectPositionalTeams(eligible, teamCount, "All-Defense");
    }

    public static List<AllTeamSelection> ComputeAllRookieTeams(Models.League.League league, int teamCount = 2)
    {
        double leagueFactor = ComputeLeagueFactor(league);

        var eligible = FlattenRosters(league)
            .Where(t => IsRookie(t.Player) && !string.IsNullOrEmpty(t.Player.Name))
            .Select(t => new PlayerCandidate
            {
                Player = t.Player,
                TeamIndex = t.TeamIndex,
                Rating = StatisticsCalculator.CalculateTrueRating(t.Player.SimulatedStats, leagueFactor)
            })
            .ToList();

        return SelectPositionalTeams(eligible, teamCount, "All-Rookie");
    }

    // ───────────────────────────────────────────────────────────────
    // Post-award application
    // ───────────────────────────────────────────────────────────────

    public static void ApplyAwardsToPlayers(Models.League.League league, SeasonAwards awards)
    {
        if (awards.ChampionTeamIndex >= 0 && awards.ChampionTeamIndex < league.Teams.Count)
        {
            foreach (var player in league.Teams[awards.ChampionTeamIndex].Roster)
            {
                player.Contract.YearsOnTeam += 0; // ring flag is on TeamRecord.HasRing
            }
        }
    }

    // ───────────────────────────────────────────────────────────────
    // Private helpers
    // ───────────────────────────────────────────────────────────────

    private class PlayerCandidate
    {
        public Player Player { get; set; } = null!;
        public int TeamIndex { get; set; }
        public double Rating { get; set; }
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

    private static bool IsStandardEligible(PlayerStatLine stats, int gamesInSeason)
    {
        if (stats.Games <= 0 || stats.Minutes <= 0) return false;
        if (stats.MinutesPerGame < MinMpgStandard) return false;
        double gamesFraction = gamesInSeason > 0 ? (double)stats.Games / gamesInSeason : 0;
        return gamesFraction >= MinGamesFraction;
    }

    private static bool IsRookie(Player player)
    {
        return player.Contract.YearsOfService <= MaxRookieYearsOfService;
    }

    private static bool IsStatLeaderEligible(Player player, double perGameAvg, double volumeThreshold, int gamesInSeason)
    {
        var stats = player.SimulatedStats;
        if (stats.Games <= 0 || string.IsNullOrEmpty(player.Name)) return false;

        double gamesFraction = gamesInSeason > 0 ? (double)stats.Games / gamesInSeason : 0;

        // Volume path: weighted stat >= threshold
        double weightedStat = perGameAvg * gamesFraction;
        if (weightedStat >= volumeThreshold) return true;

        // Games path: played 70+ games equivalent
        if (gamesFraction >= MinGamesFractionAlternate) return true;

        return false;
    }

    private static List<AwardRecipient> ComputeStatLeaders(
        Models.League.League league,
        int count,
        Func<Player, double> statSelector,
        double volumeThreshold)
    {
        int gamesInSeason = league.Schedule.GamesInSeason;

        return FlattenRosters(league)
            .Where(t =>
            {
                double perGame = statSelector(t.Player);
                return IsStatLeaderEligible(t.Player, perGame, volumeThreshold, gamesInSeason);
            })
            .Select(t => new
            {
                t.Player,
                t.TeamIndex,
                Stat = statSelector(t.Player)
            })
            .OrderByDescending(x => x.Stat)
            .Take(count)
            .Select((x, i) => ToRecipient(x.Player, x.TeamIndex, x.Stat, i + 1))
            .ToList();
    }

    private static List<AllTeamSelection> SelectPositionalTeams(
        List<PlayerCandidate> eligible,
        int teamCount,
        string labelPrefix)
    {
        var teams = new List<AllTeamSelection>();
        var chosen = new HashSet<int>();

        for (int teamNum = 1; teamNum <= teamCount; teamNum++)
        {
            var selection = new AllTeamSelection
            {
                TeamLabel = GetTeamOrdinal(teamNum) + " Team",
                TeamNumber = teamNum
            };

            foreach (var pos in Positions)
            {
                var best = eligible
                    .Where(c => !chosen.Contains(c.Player.Id) && MatchesPosition(c.Player.Position, pos))
                    .OrderByDescending(c => c.Rating)
                    .FirstOrDefault();

                if (best != null)
                {
                    chosen.Add(best.Player.Id);
                    selection.Players.Add(ToRecipient(best.Player, best.TeamIndex, best.Rating, teamNum));
                }
            }

            teams.Add(selection);
        }

        return teams;
    }

    private static bool MatchesPosition(string playerPosition, string targetPosition)
    {
        // Trim spaces (C++ uses " C" with leading space)
        return string.Equals(playerPosition?.Trim(), targetPosition?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string GetTeamOrdinal(int num) => num switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        _ => $"{num}th"
    };

    private static AwardRecipient ToRecipient(Player player, int teamIndex, double value, int rank)
    {
        return new AwardRecipient
        {
            PlayerId = player.Id,
            PlayerName = player.Name,
            TeamIndex = teamIndex,
            Position = player.Position,
            Value = value,
            Rank = rank
        };
    }

    private static double ComputeLeagueFactor(Models.League.League league)
    {
        var allSimStats = league.Teams
            .SelectMany(t => t.Roster)
            .Select(p => p.SimulatedStats);
        return StatisticsCalculator.CalculateLeagueAverageFactor(allSimStats);
    }
}
