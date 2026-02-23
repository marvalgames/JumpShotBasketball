using JumpShotBasketball.Core.Models.Game;
using JumpShotBasketball.Core.Models.League;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Computes league-wide per-48-minute averages from all active players.
/// These averages are used as baseline comparisons during game simulation.
/// Maps to C++ avg.m_avg_fga[0], avg.m_avg_blk[0], etc.
/// </summary>
public static class LeagueAveragesCalculator
{
    /// <summary>
    /// Computes LeagueAverages from all active players in the league.
    /// Values are per-player per-48-minute averages.
    /// </summary>
    public static LeagueAverages Calculate(League league)
    {
        var averages = new LeagueAverages();

        // Gather all active players with meaningful minutes
        var players = league.Teams
            .SelectMany(t => t.Roster)
            .Where(p => p.Active && p.SeasonStats.Games > 0 && p.SeasonStats.Minutes > 0)
            .ToList();

        if (players.Count == 0)
            return GetDefaults();

        double totalFga = 0, totalTfga = 0, totalOrb = 0, totalDrb = 0;
        double totalAst = 0, totalStl = 0, totalTo = 0, totalBlk = 0;
        double totalPf = 0, totalFta = 0;
        double totalMinutes = 0;

        // Per-position accumulators
        double[] posFgp = new double[6]; // [0]=overall, [1-5]=PG..C
        double[] posFga = new double[6];
        double[] posAst = new double[6];
        double[] posMin = new double[6];

        foreach (var p in players)
        {
            var s = p.SeasonStats;
            double min = s.Minutes;
            totalMinutes += min;

            totalFga += s.FieldGoalsAttempted;
            totalTfga += s.ThreePointersAttempted;
            totalOrb += s.OffensiveRebounds;
            totalDrb += s.Rebounds - s.OffensiveRebounds;
            totalAst += s.Assists;
            totalStl += s.Steals;
            totalTo += s.Turnovers;
            totalBlk += s.Blocks;
            totalPf += s.PersonalFouls;
            totalFta += s.FreeThrowsAttempted;

            int posIndex = GetPositionIndex(p.Position);
            posFga[posIndex] += s.FieldGoalsAttempted;
            posFga[0] += s.FieldGoalsAttempted;
            posFgp[posIndex] += s.FieldGoalsMade;
            posFgp[0] += s.FieldGoalsMade;
            posAst[posIndex] += s.Assists;
            posAst[0] += s.Assists;
            posMin[posIndex] += min;
            posMin[0] += min;
        }

        if (totalMinutes <= 0)
            return GetDefaults();

        double factor = 48.0 * 60.0; // 2880 seconds in 48 min

        averages.FieldGoalsAttempted = totalFga / totalMinutes * factor;
        averages.ThreePointersAttempted = totalTfga / totalMinutes * factor;
        averages.OffensiveRebounds = totalOrb / totalMinutes * factor;
        averages.DefensiveRebounds = totalDrb / totalMinutes * factor;
        averages.Steals = totalStl / totalMinutes * factor;
        averages.Turnovers = totalTo / totalMinutes * factor;
        averages.Blocks = totalBlk / totalMinutes * factor;
        averages.PersonalFouls = totalPf / totalMinutes * factor;
        averages.FreeThrowsAttempted = totalFta / totalMinutes * factor;

        for (int i = 0; i <= 5; i++)
        {
            averages.FieldGoalPercentageByPosition[i] =
                posFga[i] > 0 ? posFgp[i] / posFga[i] * 1000 : 450;
            averages.AssistsByPosition[i] =
                posMin[i] > 0 ? posAst[i] / posMin[i] * factor : 3.0;
        }

        return averages;
    }

    /// <summary>
    /// Returns default averages for when no player data is available.
    /// Values approximate a typical NBA season per-player per-48 min.
    /// </summary>
    public static LeagueAverages GetDefaults()
    {
        return new LeagueAverages
        {
            FieldGoalsAttempted = 18.0,
            ThreePointersAttempted = 6.0,
            OffensiveRebounds = 2.0,
            DefensiveRebounds = 7.0,
            Steals = 1.5,
            Turnovers = 2.5,
            Blocks = 1.0,
            PersonalFouls = 4.0,
            FreeThrowsAttempted = 5.0,
            FieldGoalPercentageByPosition = new double[] { 450, 440, 445, 450, 460, 500 },
            AssistsByPosition = new double[] { 5.0, 8.0, 4.5, 3.5, 3.0, 2.5 }
        };
    }

    private static int GetPositionIndex(string position)
    {
        return position?.Trim().ToUpper() switch
        {
            "PG" => 1,
            "SG" => 2,
            "SF" => 3,
            "PF" => 4,
            "C" => 5,
            _ => 1
        };
    }
}
