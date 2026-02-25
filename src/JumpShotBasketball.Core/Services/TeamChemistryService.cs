using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Calculates teammate chemistry ratings (Better and TeammatesBetterRating).
/// Port of CAverage::CalculateBetter() — Average.cpp:310-430
/// and CPlayer::SetTeammatesBetterRating() — Player.h:1617-1679.
/// </summary>
public static class TeamChemistryService
{
    internal record ChemistryAverages(
        double AvgFga, double AvgFta, double AvgTfga, double AvgTru,
        double AvgOrb, double AvgDrb, double AvgAst, double AvgStl,
        double AvgTo, double AvgBlk);

    /// <summary>
    /// Computes league-wide per-48 averages needed for chemistry calculations.
    /// </summary>
    internal static ChemistryAverages CalculateChemistryAverages(League league)
    {
        long totalFga = 0, totalFta = 0, totalTfga = 0;
        long totalOrb = 0, totalDrb = 0, totalAst = 0;
        long totalStl = 0, totalTo = 0, totalBlk = 0;
        long totalMin = 0;
        double totalRaw = 0;

        foreach (var team in league.Teams)
        {
            foreach (var player in team.Roster)
            {
                var s = player.SeasonStats;
                if (s.Minutes <= 0 || s.Games <= 0) continue;

                totalFga += s.FieldGoalsAttempted;
                totalFta += s.FreeThrowsAttempted;
                totalTfga += s.ThreePointersAttempted;
                totalOrb += s.OffensiveRebounds;
                totalDrb += s.Rebounds - s.OffensiveRebounds;
                totalAst += s.Assists;
                totalStl += s.Steals;
                totalTo += s.Turnovers;
                totalBlk += s.Blocks;
                totalMin += s.Minutes;

                totalRaw += StatisticsCalculator.CalculateTrueRatingSimple(s);
            }
        }

        if (totalMin <= 0)
            return new ChemistryAverages(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        double m = totalMin;
        return new ChemistryAverages(
            AvgFga: totalFga / m * 48,
            AvgFta: totalFta / m * 48,
            AvgTfga: totalTfga / m * 48,
            AvgTru: totalRaw / m * 48,
            AvgOrb: totalOrb / m * 48,
            AvgDrb: totalDrb / m * 48,
            AvgAst: totalAst / m * 48,
            AvgStl: totalStl / m * 48,
            AvgTo: totalTo / m * 48,
            AvgBlk: totalBlk / m * 48);
    }

    /// <summary>
    /// Computes how much teammates perform better/worse when this player is on court vs league average.
    /// Port of CPlayer::SetTeammatesBetterRating() — Player.h:1617-1679.
    /// </summary>
    internal static double CalculateTeammatesBetterRating(PlayerStatLine s, ChemistryAverages a)
    {
        if (s.Minutes <= 0) return 0;

        double min = s.Minutes;

        // Player's TRU per 48 minus league average
        double tru = StatisticsCalculator.CalculateTrueRatingSimple(s);
        tru = tru / min * 48 - a.AvgTru;

        // Extract player season totals
        double fgm = s.FieldGoalsMade;
        double fga = s.FieldGoalsAttempted;
        double tfgm = s.ThreePointersMade;
        double tfga = s.ThreePointersAttempted;
        double ftm = s.FreeThrowsMade;
        double fta = s.FreeThrowsAttempted;
        double oreb = s.OffensiveRebounds;
        double dreb = s.Rebounds - s.OffensiveRebounds;
        double stl = s.Steals;
        double to = s.Turnovers;
        double blk = s.Blocks;

        // Normalize player's shooting % to league-average attempts
        double twoPtFga = fga - tfga;
        double fgm2 = twoPtFga > 0 ? (fgm - tfgm) / twoPtFga * a.AvgFga : 0;
        double tfgm3 = tfga > 0 ? tfgm / tfga * a.AvgTfga : 0;
        double ftm1 = fta > 0 ? ftm / fta * a.AvgFta : 0;

        // Gun from normalized stats
        double tmGun = fgm2 + tfgm3 - (a.AvgFga - fgm2) * 2.0 / 3.0
                     + ftm1 - a.AvgFta / 2.0 + (a.AvgFta - ftm1) / 6.0;

        // Player's skill stats per 48
        double tmSkill = oreb / min * 48 * 2.0 / 3.0
                       + dreb / min * 48 / 3.0
                       + stl / min * 48
                       - to / min * 48
                       + blk / min * 48;

        // League average skill
        double avgSkill = a.AvgOrb * 2.0 / 3.0
                        + a.AvgDrb / 3.0
                        + a.AvgAst * 4.0 / 5.0
                        + a.AvgStl
                        - a.AvgTo
                        + a.AvgBlk;

        double tm = tmGun + tmSkill - avgSkill;

        return tru - tm;
    }

    /// <summary>
    /// Calculates Better and TeammatesBetterRating for all players on a team.
    /// Port of CAverage::CalculateBetter() — Average.cpp:310-430.
    /// </summary>
    internal static void CalculateBetterForTeam(Models.Team.Team team, ChemistryAverages avg, bool seasonStarted)
    {
        var players = team.Roster
            .Where(p => !string.IsNullOrEmpty(p.Name))
            .OrderByDescending(p => p.SeasonStats.Minutes)
            .ToList();

        if (players.Count == 0) return;

        // Always update TBR regardless of seasonStarted
        foreach (var p in players)
        {
            p.Ratings.TeammatesBetterRating = CalculateTeammatesBetterRating(p.SeasonStats, avg);
        }

        if (seasonStarted) return;

        // Calculate total team minutes
        double totalMin = players.Sum(p => (double)p.SeasonStats.Minutes);
        if (totalMin <= 0) return;

        int n = players.Count;

        // Build court-time overlap matrix: Chart[i][j] = min_i * min_j / totalMin * 5
        var chart = new double[n, n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                chart[i, j] = (double)players[i].SeasonStats.Minutes
                            * players[j].SeasonStats.Minutes
                            / totalMin * 5;
            }
        }

        // For each player, compute Better from weighted teammate TBR
        for (int i = 0; i < n; i++)
        {
            double playerMin = players[i].SeasonStats.Minutes;
            if (playerMin <= 0) continue;

            double rating = 0;
            for (int j = 0; j < n; j++)
            {
                rating += chart[i, j] * players[j].Ratings.TeammatesBetterRating;
            }
            rating /= playerMin;

            // Clamp to [-2.0, 8.0]
            rating = Math.Clamp(rating, -2.0, 8.0);

            // Scale to int: r = (int)(((rating + 9.9) * 10) / 2)
            players[i].Better = (int)(((rating + 9.9) * 10) / 2);
        }
    }

    /// <summary>
    /// Calculates Better and TeammatesBetterRating for all players in the league.
    /// When seasonStarted is true, only TBR is updated (Better is preserved).
    /// </summary>
    public static void CalculateBetterForLeague(League league, bool seasonStarted = false)
    {
        var avg = CalculateChemistryAverages(league);

        foreach (var team in league.Teams)
        {
            CalculateBetterForTeam(team, avg, seasonStarted);
        }
    }
}
