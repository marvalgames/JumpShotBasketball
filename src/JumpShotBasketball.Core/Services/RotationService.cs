using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Team;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Computer rotation setting and roster validation.
/// Port of CAverage::ComputerSetsRotation() Average.cpp L833
/// and CAverage::VerifyRosters() Average.cpp L1764.
/// </summary>
public static class RotationService
{
    /// <summary>
    /// Sets rotation flags for all computer-managed teams.
    /// Best player per position -> Starter (Rot=1). Above-average players -> rotation (Rot=2).
    /// Includes position flexibility swaps for secondary positions.
    /// </summary>
    public static void SetComputerRotations(League league)
    {
        // Calculate league-wide average trade true rating
        double avgTru = CalculateLeagueAverageTradeTrueRating(league);
        double threshold = avgTru * 3.0 / 4.0;

        for (int t = 0; t < league.Teams.Count; t++)
        {
            var team = league.Teams[t];

            // Skip player-controlled teams
            if (team.Record.Control == "Player") continue;

            var roster = team.Roster;

            // Phase 1: Clear all rotations and game plans
            foreach (var p in roster)
            {
                p.PgRotation = false;
                p.SgRotation = false;
                p.SfRotation = false;
                p.PfRotation = false;
                p.CRotation = false;
                p.GameMinutes = 0;
                p.OffensiveFocus = 0;
                p.DefensiveFocus = 0;
                p.OffensiveIntensity = 0;
                p.DefensiveIntensity = 0;
                p.PlayMaker = 0;
            }

            // Phase 2: Assign bench rotation (value=true) for above-threshold players
            foreach (var p in roster)
            {
                if (string.IsNullOrEmpty(p.Name) || p.Injury > 0) continue;
                double rating = p.Ratings.TradeTrueRating;
                if (rating <= threshold) continue;

                switch (NormalizePosition(p.Position))
                {
                    case "PG": p.PgRotation = true; break;
                    case "SG": p.SgRotation = true; break;
                    case "SF": p.SfRotation = true; break;
                    case "PF": p.PfRotation = true; break;
                    case "C": p.CRotation = true; break;
                }
            }

            // Phase 3: Find best player per position -> Starter
            // We track starters separately since our bool can't distinguish 1 vs 2
            var starters = FindBestPerPosition(roster);

            // Apply starters: For the "best" player, they get both rotation=true and Starter incremented
            // The engine uses rotation flags to determine who plays; Starter is a counter.
            // For our purposes, we ensure the best player at each position is in the rotation.
            foreach (var p in roster)
            {
                if (string.IsNullOrEmpty(p.Name)) continue;
                string pos = NormalizePosition(p.Position);
                switch (pos)
                {
                    case "PG": p.PgRotation = true; break;
                    case "SG": p.SgRotation = true; break;
                    case "SF": p.SfRotation = true; break;
                    case "PF": p.PfRotation = true; break;
                    case "C": p.CRotation = true; break;
                }
            }

            // Phase 4: Position flexibility — better players can fill adjacent positions
            ApplyPositionFlexibility(roster);
        }
    }

    /// <summary>
    /// Verifies a team has enough eligible (non-injured, active) players.
    /// Requires >= 5 eligible players with at least 1 per position group.
    /// </summary>
    public static bool VerifyRoster(Team team)
    {
        int pg = 0, sg = 0, sf = 0, pf = 0, c = 0;
        int eligiblePlayers = 0;

        foreach (var p in team.Roster)
        {
            if (string.IsNullOrEmpty(p.Name)) continue;
            if (p.Injury > 0) continue;

            eligiblePlayers++;

            if (!p.Active) continue;

            string pos = NormalizePosition(p.Position);
            if (pos == "PG" || p.PgRotation) pg++;
            if (pos == "SG" || p.SgRotation) sg++;
            if (pos == "SF" || p.SfRotation) sf++;
            if (pos == "PF" || p.PfRotation) pf++;
            if (pos == "C" || p.CRotation) c++;
        }

        bool isPlayerControlled = team.Record.Control == "Player";

        if (isPlayerControlled)
        {
            int activeRoster = team.Roster.Count(p => !string.IsNullOrEmpty(p.Name));
            return activeRoster >= 9;
        }

        return pg >= 1 && sg >= 1 && sf >= 1 && pf >= 1 && c >= 1 && eligiblePlayers >= 9;
    }

    /// <summary>
    /// Returns indices of teams with invalid rosters.
    /// </summary>
    public static List<int> VerifyAllRosters(League league)
    {
        var invalidTeams = new List<int>();
        for (int i = 0; i < league.Teams.Count; i++)
        {
            if (!VerifyRoster(league.Teams[i]))
                invalidTeams.Add(i);
        }
        return invalidTeams;
    }

    private static string NormalizePosition(string? position)
    {
        return position?.Trim().ToUpper() switch
        {
            "PG" => "PG",
            "SG" => "SG",
            "SF" => "SF",
            "PF" => "PF",
            "C" => "C",
            _ => "SF" // default fallback
        };
    }

    private static Dictionary<string, Player?> FindBestPerPosition(List<Player> roster)
    {
        var best = new Dictionary<string, Player?>
        {
            ["PG"] = null, ["SG"] = null, ["SF"] = null, ["PF"] = null, ["C"] = null
        };
        var bestRating = new Dictionary<string, double>
        {
            ["PG"] = 0, ["SG"] = 0, ["SF"] = 0, ["PF"] = 0, ["C"] = 0
        };

        foreach (var p in roster)
        {
            if (string.IsNullOrEmpty(p.Name) || p.Injury > 0) continue;
            string pos = NormalizePosition(p.Position);
            double rating = p.Ratings.TradeTrueRating;
            if (rating > bestRating[pos])
            {
                bestRating[pos] = rating;
                best[pos] = p;
            }
        }

        return best;
    }

    private static void ApplyPositionFlexibility(List<Player> roster)
    {
        // Adjacent position map: what secondary positions each position can fill
        // PG <-> SG, SG <-> SF, SF <-> PF, PF <-> C
        foreach (var p in roster)
        {
            if (string.IsNullOrEmpty(p.Name) || p.Injury > 0) continue;
            string pos = NormalizePosition(p.Position);
            double rating = p.Ratings.TradeTrueRating;

            // Only apply flex if player doesn't have a rotation at their primary position
            bool hasPrimaryRotation = pos switch
            {
                "PG" => p.PgRotation,
                "SG" => p.SgRotation,
                "SF" => p.SfRotation,
                "PF" => p.PfRotation,
                "C" => p.CRotation,
                _ => false
            };

            if (hasPrimaryRotation) continue;

            // Try adjacent positions
            switch (pos)
            {
                case "PG":
                    if (!p.SgRotation) TryFlexAssign(p, roster, rating, "SG",
                        (a, b) => a.SeasonStats.Rebounds > b.SeasonStats.Rebounds);
                    break;
                case "SG":
                    if (!p.PgRotation) TryFlexAssign(p, roster, rating, "PG",
                        (a, b) => a.SeasonStats.Assists > b.SeasonStats.Assists);
                    if (!p.SfRotation) TryFlexAssign(p, roster, rating, "SF", null);
                    break;
                case "SF":
                    if (!p.SgRotation) TryFlexAssign(p, roster, rating, "SG",
                        (a, b) => (a.SeasonStats.Rebounds + a.SeasonStats.Assists) >
                                  (b.SeasonStats.Rebounds + b.SeasonStats.Assists));
                    if (!p.PfRotation) TryFlexAssign(p, roster, rating, "PF",
                        (a, b) => a.SeasonStats.Rebounds > b.SeasonStats.Rebounds);
                    break;
                case "PF":
                    if (!p.SfRotation) TryFlexAssign(p, roster, rating, "SF",
                        (a, b) => a.SeasonStats.Assists > b.SeasonStats.Assists);
                    if (!p.CRotation) TryFlexAssign(p, roster, rating, "C",
                        (a, b) => (a.SeasonStats.Rebounds + a.SeasonStats.Blocks) >
                                  (b.SeasonStats.Rebounds + b.SeasonStats.Blocks));
                    break;
                case "C":
                    if (!p.PfRotation) TryFlexAssign(p, roster, rating, "PF",
                        (a, b) => (a.SeasonStats.Rebounds + a.SeasonStats.Assists) >
                                  (b.SeasonStats.Rebounds + b.SeasonStats.Assists));
                    break;
            }
        }
    }

    private static void TryFlexAssign(Player player, List<Player> roster, double rating,
        string targetPos, Func<Player, Player, bool>? statComparison)
    {
        foreach (var other in roster)
        {
            if (other == player || string.IsNullOrEmpty(other.Name)) continue;

            bool otherHasRotation = targetPos switch
            {
                "PG" => other.PgRotation,
                "SG" => other.SgRotation,
                "SF" => other.SfRotation,
                "PF" => other.PfRotation,
                "C" => other.CRotation,
                _ => false
            };

            if (!otherHasRotation) continue;

            bool betterRating = rating > other.Ratings.TradeTrueRating;
            bool meetsStat = statComparison == null || statComparison(player, other);

            if (betterRating && meetsStat)
            {
                // Swap: player takes the rotation spot, other loses it
                SetRotation(player, targetPos, true);
                SetRotation(other, targetPos, false);
                break;
            }
        }
    }

    private static void SetRotation(Player player, string position, bool value)
    {
        switch (position)
        {
            case "PG": player.PgRotation = value; break;
            case "SG": player.SgRotation = value; break;
            case "SF": player.SfRotation = value; break;
            case "PF": player.PfRotation = value; break;
            case "C": player.CRotation = value; break;
        }
    }

    private static double CalculateLeagueAverageTradeTrueRating(League league)
    {
        var allPlayers = league.Teams
            .SelectMany(t => t.Roster)
            .Where(p => !string.IsNullOrEmpty(p.Name) && p.Active)
            .ToList();

        if (allPlayers.Count == 0) return 5.0;

        return allPlayers.Average(p => p.Ratings.TradeTrueRating);
    }
}
