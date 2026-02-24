using JumpShotBasketball.Core.Enums;
using JumpShotBasketball.Core.Models.History;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Record book management: single-game, season, and career records.
/// Port of C++ CHigh (High.cpp ~1,694 lines): SetGameHighs, SetAllTimeSeasonHighs, ClearSeasonHighs.
/// </summary>
public static class RecordBookService
{
    // ── Constants (from C++ High.cpp) ────────────────────────────────────────

    public const int MaxSingleGameEntries = 10;
    public const int MaxSeasonEntries = 50;
    public const int MaxCareerEntries = 50;

    // ── Stat Categories ──────────────────────────────────────────────────────

    public static readonly string[] SingleGameStats =
    {
        "Points", "Rebounds", "Assists", "Steals", "Blocks",
        "FieldGoalsMade", "ThreePointersMade", "FreeThrowsMade"
    };

    public static readonly string[] SeasonStats =
    {
        "PointsPerGame", "ReboundsPerGame", "AssistsPerGame", "StealsPerGame", "BlocksPerGame",
        "FieldGoalPct", "FreeThrowPct", "ThreePointPct"
    };

    // ── Volume Thresholds (from C++ High.cpp) ────────────────────────────────

    // Season totals required to qualify for per-game/percentage records
    private const int SeasonPointsMin = 1400;
    private const int SeasonReboundsMin = 800;
    private const int SeasonAssistsMin = 400;
    private const int SeasonStealsMin = 125;
    private const int SeasonBlocksMin = 100;
    private const int SeasonFgaMin = 300;
    private const int SeasonFtmMin = 55;
    private const int SeasonThreePaMin = 125;

    // Career percentage minimums
    private const int CareerFgmMin = 100;
    private const int CareerThreePmMin = 25;
    private const int CareerFtmMin = 50;

    // ── Scope Keys ───────────────────────────────────────────────────────────

    private const string LeagueScope = "League";
    private const string PlayoffScope = "Playoff";

    private static string TeamScope(int teamIndex) => $"Team_{teamIndex}";
    private static string TeamPlayoffScope(int teamIndex) => $"TeamPlayoff_{teamIndex}";

    // ── EnsureInitialized ────────────────────────────────────────────────────

    /// <summary>
    /// Ensures RecordBook is initialized with empty lists for all scopes and stats.
    /// </summary>
    public static void EnsureInitialized(League league)
    {
        var rb = league.RecordBook;

        // Single-game scopes: League, Playoff, per-team, per-team-playoff
        EnsureSingleGameScope(rb, LeagueScope);
        EnsureSingleGameScope(rb, PlayoffScope);
        for (int i = 0; i < league.Teams.Count; i++)
        {
            EnsureSingleGameScope(rb, TeamScope(i));
            EnsureSingleGameScope(rb, TeamPlayoffScope(i));
        }

        // Season scopes: League, per-team
        EnsureSeasonScope(rb, LeagueScope);
        for (int i = 0; i < league.Teams.Count; i++)
            EnsureSeasonScope(rb, TeamScope(i));

        // Career: league-wide only
        foreach (var stat in SeasonStats)
        {
            if (!rb.CareerRecords.ContainsKey(stat))
                rb.CareerRecords[stat] = new StatRecordList { StatName = stat, MaxEntries = MaxCareerEntries };
        }
    }

    // ── UpdateSingleGameRecords ──────────────────────────────────────────────

    /// <summary>
    /// Checks box score against single-game records after every game.
    /// Port of CHigh::SetGameHighs().
    /// </summary>
    public static void UpdateSingleGameRecords(
        RecordBook recordBook,
        List<PlayerGameState> boxScore,
        Models.Team.Team team,
        int teamIndex,
        GameType gameType,
        int currentYear)
    {
        bool isPlayoff = gameType == GameType.Playoff;
        string leagueScope = isPlayoff ? PlayoffScope : LeagueScope;
        string teamScopeKey = isPlayoff ? TeamPlayoffScope(teamIndex) : TeamScope(teamIndex);

        foreach (var gs in boxScore)
        {
            if (gs.Minutes == 0) continue;

            int points = gs.Points;
            int rebounds = gs.TotalRebounds;

            var statValues = new (string stat, double value)[]
            {
                ("Points", points),
                ("Rebounds", rebounds),
                ("Assists", gs.Assists),
                ("Steals", gs.Steals),
                ("Blocks", gs.Blocks),
                ("FieldGoalsMade", gs.FieldGoalsMade),
                ("ThreePointersMade", gs.ThreePointersMade),
                ("FreeThrowsMade", gs.FreeThrowsMade)
            };

            foreach (var (stat, value) in statValues)
            {
                if (value <= 0) continue;

                var entry = new RecordBookEntry
                {
                    PlayerName = gs.GameName,
                    PlayerId = gs.PlayerPointer,
                    TeamIndex = teamIndex,
                    TeamName = team.Name,
                    Year = currentYear,
                    Value = value
                };

                // League/playoff scope
                TryInsert(recordBook.SingleGameRecords, leagueScope, stat, entry, MaxSingleGameEntries);

                // Team scope
                TryInsert(recordBook.SingleGameRecords, teamScopeKey, stat, entry, MaxSingleGameEntries);
            }
        }
    }

    // ── UpdateSeasonRecords ──────────────────────────────────────────────────

    /// <summary>
    /// Checks all player season averages against records at end of season.
    /// Port of CHigh::SetAllTimeSeasonHighs() (season portion).
    /// </summary>
    public static void UpdateSeasonRecords(RecordBook recordBook, League league, int currentYear)
    {
        for (int t = 0; t < league.Teams.Count; t++)
        {
            var team = league.Teams[t];
            foreach (var player in team.Roster)
            {
                if (string.IsNullOrEmpty(player.Name)) continue;

                var stats = player.SimulatedStats;
                if (stats.Games == 0) continue;

                var entry = CreateBaseEntry(player, t, team.Name, currentYear);

                // Points per game (min 1400 total points)
                if (stats.Points >= SeasonPointsMin)
                    TryInsertSeason(recordBook, LeagueScope, TeamScope(t), "PointsPerGame",
                        CloneEntry(entry, stats.PointsPerGame));

                // Rebounds per game (min 800 total rebounds)
                if (stats.Rebounds >= SeasonReboundsMin)
                    TryInsertSeason(recordBook, LeagueScope, TeamScope(t), "ReboundsPerGame",
                        CloneEntry(entry, stats.ReboundsPerGame));

                // Assists per game (min 400 total assists)
                if (stats.Assists >= SeasonAssistsMin)
                    TryInsertSeason(recordBook, LeagueScope, TeamScope(t), "AssistsPerGame",
                        CloneEntry(entry, stats.AssistsPerGame));

                // Steals per game (min 125 total steals)
                if (stats.Steals >= SeasonStealsMin)
                    TryInsertSeason(recordBook, LeagueScope, TeamScope(t), "StealsPerGame",
                        CloneEntry(entry, stats.StealsPerGame));

                // Blocks per game (min 100 total blocks)
                if (stats.Blocks >= SeasonBlocksMin)
                    TryInsertSeason(recordBook, LeagueScope, TeamScope(t), "BlocksPerGame",
                        CloneEntry(entry, stats.BlocksPerGame));

                // FG% (min 300 FGA)
                if (stats.FieldGoalsAttempted >= SeasonFgaMin)
                    TryInsertSeason(recordBook, LeagueScope, TeamScope(t), "FieldGoalPct",
                        CloneEntry(entry, stats.FieldGoalPercentage));

                // FT% (min 55 FTM)
                if (stats.FreeThrowsMade >= SeasonFtmMin)
                    TryInsertSeason(recordBook, LeagueScope, TeamScope(t), "FreeThrowPct",
                        CloneEntry(entry, stats.FreeThrowPercentage));

                // 3P% (min 125 3PA)
                if (stats.ThreePointersAttempted >= SeasonThreePaMin)
                    TryInsertSeason(recordBook, LeagueScope, TeamScope(t), "ThreePointPct",
                        CloneEntry(entry, stats.ThreePointPercentage));
            }
        }
    }

    // ── UpdateCareerRecords ──────────────────────────────────────────────────

    /// <summary>
    /// Checks all player career totals against records at end of season.
    /// Port of CHigh::SetAllTimeSeasonHighs() (career portion, deduplicated by PlayerId).
    /// </summary>
    public static void UpdateCareerRecords(RecordBook recordBook, League league, int currentYear)
    {
        for (int t = 0; t < league.Teams.Count; t++)
        {
            var team = league.Teams[t];
            foreach (var player in team.Roster)
            {
                if (string.IsNullOrEmpty(player.Name)) continue;

                var career = player.CareerStats;
                if (career.Games == 0) continue;

                var entry = CreateBaseEntry(player, t, team.Name, currentYear);

                // Career per-game averages (same volume thresholds as season, but on career totals)
                if (career.Points >= SeasonPointsMin)
                    TryInsertCareer(recordBook, "PointsPerGame",
                        CloneEntry(entry, career.PointsPerGame));

                if (career.Rebounds >= SeasonReboundsMin)
                    TryInsertCareer(recordBook, "ReboundsPerGame",
                        CloneEntry(entry, career.ReboundsPerGame));

                if (career.Assists >= SeasonAssistsMin)
                    TryInsertCareer(recordBook, "AssistsPerGame",
                        CloneEntry(entry, career.AssistsPerGame));

                if (career.Steals >= SeasonStealsMin)
                    TryInsertCareer(recordBook, "StealsPerGame",
                        CloneEntry(entry, career.StealsPerGame));

                if (career.Blocks >= SeasonBlocksMin)
                    TryInsertCareer(recordBook, "BlocksPerGame",
                        CloneEntry(entry, career.BlocksPerGame));

                // Career percentages (different minimums)
                if (career.FieldGoalsMade >= CareerFgmMin)
                    TryInsertCareer(recordBook, "FieldGoalPct",
                        CloneEntry(entry, career.FieldGoalPercentage));

                if (career.FreeThrowsMade >= CareerFtmMin)
                    TryInsertCareer(recordBook, "FreeThrowPct",
                        CloneEntry(entry, career.FreeThrowPercentage));

                if (career.ThreePointersMade >= CareerThreePmMin)
                    TryInsertCareer(recordBook, "ThreePointPct",
                        CloneEntry(entry, career.ThreePointPercentage));
            }
        }
    }

    // ── ClearSeasonGameHighs ─────────────────────────────────────────────────

    /// <summary>
    /// Clears current-season single-game records (League + team scopes).
    /// Preserves all-time season/career records and playoff records.
    /// Port of CHigh::ClearSeasonHighs(0,0).
    /// </summary>
    public static void ClearSeasonGameHighs(RecordBook recordBook)
    {
        // Clear league regular-season single-game records
        if (recordBook.SingleGameRecords.TryGetValue(LeagueScope, out var leagueRecords))
        {
            foreach (var list in leagueRecords.Values)
                list.Entries.Clear();
        }

        // Clear team regular-season single-game records
        foreach (var key in recordBook.SingleGameRecords.Keys.ToList())
        {
            if (key.StartsWith("Team_") && !key.StartsWith("TeamPlayoff_"))
            {
                foreach (var list in recordBook.SingleGameRecords[key].Values)
                    list.Entries.Clear();
            }
        }

        // Also clear playoff single-game records for the new season
        if (recordBook.SingleGameRecords.TryGetValue(PlayoffScope, out var playoffRecords))
        {
            foreach (var list in playoffRecords.Values)
                list.Entries.Clear();
        }

        foreach (var key in recordBook.SingleGameRecords.Keys.ToList())
        {
            if (key.StartsWith("TeamPlayoff_"))
            {
                foreach (var list in recordBook.SingleGameRecords[key].Values)
                    list.Entries.Clear();
            }
        }
    }

    // ── Private Helpers ──────────────────────────────────────────────────────

    private static void EnsureSingleGameScope(RecordBook rb, string scope)
    {
        if (!rb.SingleGameRecords.ContainsKey(scope))
            rb.SingleGameRecords[scope] = new Dictionary<string, StatRecordList>();

        foreach (var stat in SingleGameStats)
        {
            if (!rb.SingleGameRecords[scope].ContainsKey(stat))
                rb.SingleGameRecords[scope][stat] = new StatRecordList { StatName = stat, MaxEntries = MaxSingleGameEntries };
        }
    }

    private static void EnsureSeasonScope(RecordBook rb, string scope)
    {
        if (!rb.SeasonRecords.ContainsKey(scope))
            rb.SeasonRecords[scope] = new Dictionary<string, StatRecordList>();

        foreach (var stat in SeasonStats)
        {
            if (!rb.SeasonRecords[scope].ContainsKey(stat))
                rb.SeasonRecords[scope][stat] = new StatRecordList { StatName = stat, MaxEntries = MaxSeasonEntries };
        }
    }

    /// <summary>
    /// Sorted insertion into a scoped record list with capacity enforcement.
    /// Entries are sorted descending by Value.
    /// </summary>
    private static void TryInsert(
        Dictionary<string, Dictionary<string, StatRecordList>> scopedRecords,
        string scope, string stat, RecordBookEntry entry, int maxEntries)
    {
        if (!scopedRecords.TryGetValue(scope, out var statRecords))
        {
            statRecords = new Dictionary<string, StatRecordList>();
            scopedRecords[scope] = statRecords;
        }

        if (!statRecords.TryGetValue(stat, out var list))
        {
            list = new StatRecordList { StatName = stat, MaxEntries = maxEntries };
            statRecords[stat] = list;
        }

        InsertSorted(list, entry);
    }

    private static void TryInsertSeason(
        RecordBook rb, string leagueScope, string teamScope, string stat, RecordBookEntry entry)
    {
        TryInsert(rb.SeasonRecords, leagueScope, stat, entry, MaxSeasonEntries);
        TryInsert(rb.SeasonRecords, teamScope, stat, entry, MaxSeasonEntries);
    }

    private static void TryInsertCareer(RecordBook rb, string stat, RecordBookEntry entry)
    {
        if (!rb.CareerRecords.TryGetValue(stat, out var list))
        {
            list = new StatRecordList { StatName = stat, MaxEntries = MaxCareerEntries };
            rb.CareerRecords[stat] = list;
        }

        // Remove existing entry for same player (deduplication)
        list.Entries.RemoveAll(e => e.PlayerId == entry.PlayerId);

        InsertSorted(list, entry);
    }

    private static void InsertSorted(StatRecordList list, RecordBookEntry entry)
    {
        // Find insertion point (descending order by value)
        int insertIdx = 0;
        while (insertIdx < list.Entries.Count && list.Entries[insertIdx].Value >= entry.Value)
            insertIdx++;

        // Only insert if within capacity or better than the worst
        if (insertIdx >= list.MaxEntries)
            return;

        list.Entries.Insert(insertIdx, entry);

        // Trim to max capacity
        while (list.Entries.Count > list.MaxEntries)
            list.Entries.RemoveAt(list.Entries.Count - 1);
    }

    private static RecordBookEntry CreateBaseEntry(Player player, int teamIndex, string teamName, int year)
    {
        return new RecordBookEntry
        {
            PlayerName = player.Name,
            PlayerId = player.Id,
            TeamIndex = teamIndex,
            TeamName = teamName,
            Year = year
        };
    }

    private static RecordBookEntry CloneEntry(RecordBookEntry template, double value)
    {
        return new RecordBookEntry
        {
            PlayerName = template.PlayerName,
            PlayerId = template.PlayerId,
            TeamIndex = template.TeamIndex,
            TeamName = template.TeamName,
            Year = template.Year,
            Value = value
        };
    }
}
