using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Sorts team rosters by position (2 per: PG, SG, SF, PF, C) then fills remaining
/// slots by trade value with prospect priority.
/// Port of CAverage::SortTeamRosters() — Average.cpp:1817-1954.
/// </summary>
public static class RosterSortingService
{
    private static readonly string[] PositionOrder = { "PG", "SG", "SF", "PF", "C" };

    /// <summary>
    /// Sorts all teams' rosters in the league.
    /// </summary>
    public static void SortAllTeamRosters(League league)
    {
        foreach (var team in league.Teams)
            SortTeamRoster(team);
    }

    /// <summary>
    /// Sorts a single team's roster: 2 players per position (PG, SG, SF, PF, C) by
    /// composite trade value, then remaining players by value with prospect priority.
    /// Port of CAverage::SortTeamRoster() — Average.cpp:1958-2100.
    /// </summary>
    public static void SortTeamRoster(Models.Team.Team team, bool okErase = false)
    {
        var eligible = team.Roster
            .Where(p => !string.IsNullOrEmpty(p.Name) && IsEligible(p, okErase))
            .ToList();

        if (eligible.Count == 0) return;

        var sorted = new List<Player>();
        var used = new HashSet<Player>();

        // Phase 1: Pick top 2 players per position (PG, SG, SF, PF, C)
        foreach (var pos in PositionOrder)
        {
            for (int slot = 0; slot < 2; slot++)
            {
                Player? best = null;
                double bestValue = double.MinValue;

                foreach (var player in eligible)
                {
                    if (used.Contains(player)) continue;
                    if (player.Position != pos) continue;

                    double value = CalculateCompositeTradeValue(player, team);
                    if (value > bestValue)
                    {
                        bestValue = value;
                        best = player;
                    }
                }

                if (best != null)
                {
                    sorted.Add(best);
                    used.Add(best);
                }
            }
        }

        // Phase 2: Pick top remaining players by composite value, prospects get priority
        int gmPower2 = team.GeneralManager?.Power2 ?? 3;

        var remaining = eligible
            .Where(p => !used.Contains(p))
            .Select(p => new
            {
                Player = p,
                IsProspect = IsProspect(p, gmPower2),
                Value = CalculateCompositeTradeValue(p, team)
            })
            .OrderByDescending(x => x.IsProspect) // prospects first
            .ThenByDescending(x => x.Value)
            .Take(10)
            .Select(x => x.Player)
            .ToList();

        sorted.AddRange(remaining);
        foreach (var p in remaining)
            used.Add(p);

        // Add any leftover players not picked
        var leftover = eligible.Where(p => !used.Contains(p)).ToList();
        sorted.AddRange(leftover);

        // Add empty-name players that were skipped
        var emptyPlayers = team.Roster.Where(p => string.IsNullOrEmpty(p.Name)).ToList();

        // Also add ineligible players (okErase filtered them out)
        var ineligible = team.Roster
            .Where(p => !string.IsNullOrEmpty(p.Name) && !IsEligible(p, okErase))
            .ToList();

        // Rebuild roster
        team.Roster.Clear();
        team.Roster.AddRange(sorted);
        team.Roster.AddRange(ineligible);
        team.Roster.AddRange(emptyPlayers);

        // Set OriginalNumber for each slot
        for (int i = 0; i < team.Roster.Count; i++)
            team.Roster[i].OriginalNumber = i;
    }

    /// <summary>
    /// Calculates composite trade value including contract bonus.
    /// Port of Average.cpp:1880-1900 contract value adjustment.
    /// </summary>
    internal static double CalculateCompositeTradeValue(Player player, Models.Team.Team team)
    {
        double tradeValue = player.Ratings.TradeValue;
        var contract = player.Contract;

        int yearsLeft = 1 + contract.ContractYears - contract.CurrentContractYear;
        if (yearsLeft < 0) yearsLeft = 0;

        double underpaid = Math.Abs(tradeValue * yearsLeft - contract.RemainingSalary / 100.0);

        double teamValue = (double)team.Financial.CurrentValue;
        if (teamValue <= 0) teamValue = 1.0;

        double contractBonus = underpaid * 400.0 / teamValue;

        return tradeValue + contractBonus;
    }

    private static bool IsEligible(Player player, bool okErase)
    {
        if (!okErase) return true;

        // okErase mode: filter out players with weak stats
        var ratings = player.Ratings;
        var stats = player.SeasonStats;

        return ratings.ProjectionFieldGoalsAttempted >= 5
            && ratings.ProjectionFreeThrowsAttempted >= 5
            && ratings.ProjectionFieldGoalPercentage > 0
            && stats.FieldGoalsMade > 4
            && stats.FreeThrowsMade > 2;
    }

    private static bool IsProspect(Player player, int gmPower2)
    {
        int yearsToPrime = player.Ratings.Prime - player.Age;
        if (yearsToPrime < 7) return false;

        int prospectPoints = player.Ratings.Potential1 + player.Ratings.Potential2 + player.Ratings.Effort;
        int threshold = (6 - gmPower2) + 8;

        return prospectPoints > threshold;
    }
}
