using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Staff;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Scout and coach rating adjustment service.
/// Ported from CPlayer::PrRating(), SetAdjustedRatings(), SetAdjTrueRatingSimple(), SetAdjTradeValue().
/// Scouts introduce uncertainty into projection ratings; coaches modify true ratings and trade values.
/// </summary>
public static class ScoutingService
{
    /// <summary>
    /// Core digit-splitting algorithm that maps a base rating through a scout/coach chart.
    /// Port of CPlayer::PrRating(bool shooting, int r, int m_chart[11]) — Player.h:1247-1280.
    /// shooting=true → 1/4 multiplier (less distortion for percentages); false → full factor.
    /// Special case: r > 200 → direct chart lookup chart[r-200] (for potentials 1-5).
    /// </summary>
    public static int CalculatePrRating(bool shooting, int rating, int[] chart)
    {
        if (chart == null || chart.Length < 6) return rating;

        // Special case: potentials (r > 200) → direct chart lookup, skip digit-splitting
        // In C++, the digit-split runs first (harmless buffer overrun) then gets overwritten.
        if (rating > 200 && rating <= 210)
        {
            int index = rating - 200;
            if (index >= 0 && index < chart.Length)
                return chart[index];
            return rating;
        }

        int r = rating;

        // Clamp values outside 0-99 to 99 (C++ clamps 100-200; we also clamp >200 non-potential)
        if (r > 99) r = 99;
        if (r < 0) r = 0;

        int d1 = (int)(r / 10.0);
        int d2 = r - d1 * 10;

        int tmpD1, tmpD2;

        if (d1 <= 4)
            tmpD1 = chart[d1 + 1] - 1;
        else
            tmpD1 = chart[d1 - 4] + 4;

        if (d2 <= 4)
            tmpD2 = chart[d2 + 1] - 1;
        else
            tmpD2 = chart[d2 - 4] + 4;

        int result = (int)(tmpD1 * 10.0 + tmpD2);
        double multiplier = shooting ? 1.0 / 4.0 : 1.0;
        double factor = 100.0 + (result - (double)r) * multiplier;
        result = (int)(r * factor / 100.0);

        return result;
    }

    /// <summary>
    /// Applies scout adjustments to a player's projection ratings.
    /// Port of CPlayer::SetAdjustedRatings(CStaff scout) — Player.h:3296-3359.
    /// Transforms 12 projection stats + 3 attributes through scout's 8 chart arrays.
    /// </summary>
    public static void ApplyScoutAdjustments(Player player, StaffMember? scout)
    {
        if (scout == null) return;

        var r = player.Ratings;

        // Scoring chart → FGA, FTA, 3PA (volume stats, shooting=false)
        r.AdjustedProjectionFieldGoalsAttempted = CalculatePrRating(false, r.ProjectionFieldGoalsAttempted, scout.ScoringHistory);
        r.AdjustedProjectionFreeThrowsAttempted = CalculatePrRating(false, r.ProjectionFreeThrowsAttempted, scout.ScoringHistory);
        r.AdjustedProjectionThreePointersAttempted = CalculatePrRating(false, r.ProjectionThreePointersAttempted, scout.ScoringHistory);

        // Shooting chart → FG%, FT%, 3P% (accuracy stats, shooting=true)
        r.AdjustedProjectionFieldGoalPercentage = CalculatePrRating(true, r.ProjectionFieldGoalPercentage, scout.ShootingHistory);
        r.AdjustedProjectionFreeThrowPercentage = CalculatePrRating(true, r.ProjectionFreeThrowPercentage, scout.ShootingHistory);
        r.AdjustedProjectionThreePointPercentage = CalculatePrRating(true, r.ProjectionThreePointPercentage, scout.ShootingHistory);

        // Rebounding chart → OREB, DREB
        r.AdjustedProjectionOffensiveRebounds = CalculatePrRating(false, r.ProjectionOffensiveRebounds, scout.ReboundingHistory);
        r.AdjustedProjectionDefensiveRebounds = CalculatePrRating(false, r.ProjectionDefensiveRebounds, scout.ReboundingHistory);

        // Passing chart → AST, TO
        r.AdjustedProjectionAssists = CalculatePrRating(false, r.ProjectionAssists, scout.PassingHistory);
        r.AdjustedProjectionTurnovers = CalculatePrRating(false, r.ProjectionTurnovers, scout.PassingHistory);

        // Defense chart → STL, BLK
        r.AdjustedProjectionSteals = CalculatePrRating(false, r.ProjectionSteals, scout.DefenseHistory);
        r.AdjustedProjectionBlocks = CalculatePrRating(false, r.ProjectionBlocks, scout.DefenseHistory);

        // Pot1/Pot2/Effort charts → +200 offset (direct chart lookup path)
        r.AdjustedPotential1 = CalculatePrRating(false, r.Potential1 + 200, scout.Pot1History);
        r.AdjustedPotential2 = CalculatePrRating(false, r.Potential2 + 200, scout.Pot2History);
        r.AdjustedEffort = CalculatePrRating(false, r.Effort + 200, scout.EffortHistory);
    }

    /// <summary>
    /// Applies scout adjustments to all players on a team.
    /// </summary>
    public static void ApplyScoutAdjustmentsToTeam(League league, int teamIndex)
    {
        if (teamIndex < 0 || teamIndex >= league.Teams.Count) return;

        var team = league.Teams[teamIndex];
        if (team.Scout == null) return;

        foreach (var player in team.Roster)
        {
            if (string.IsNullOrEmpty(player.Name)) continue;
            ApplyScoutAdjustments(player, team.Scout);
        }
    }

    /// <summary>
    /// Applies scout adjustments to all players on all teams.
    /// </summary>
    public static void ApplyAllScoutAdjustments(League league)
    {
        for (int i = 0; i < league.Teams.Count; i++)
            ApplyScoutAdjustmentsToTeam(league, i);
    }

    /// <summary>
    /// Calculates the coach skill factor for a given chart category.
    /// Port of deviation calculation from CPlayer::SetAdjTrueRatingSimple() — Player.h:1494-1520.
    /// deviation = sum(|chart[i] - i| for i=1..5) / 100.0
    /// If chart[1] &lt;= 3: return 1.0 - deviation (below avg); else 1.0 + deviation.
    /// </summary>
    public static double CalculateCoachSkillFactor(int[] chart)
    {
        if (chart == null || chart.Length < 6) return 1.0;

        double deviation = (Math.Abs(chart[1] - 1) + Math.Abs(chart[2] - 2) +
                           Math.Abs(chart[3] - 3) + Math.Abs(chart[4] - 4) +
                           Math.Abs(chart[5] - 5)) / 100.0;

        return chart[1] <= 3 ? 1.0 - deviation : 1.0 + deviation;
    }

    /// <summary>
    /// Calculates coach-adjusted true rating simple for a player.
    /// Port of CPlayer::SetAdjTrueRatingSimple() — Player.h:1494-1520.
    /// Same gun+skill formula as CalculateTrueRatingSimple but with coach factors per category.
    /// Uses SimulatedStats if available, otherwise SeasonStats.
    /// Stores result in player.Ratings.TrueRatingSimple.
    /// </summary>
    public static void CalculateCoachAdjustedTrueRating(Player player, StaffMember? coach)
    {
        if (coach == null) return;

        var stats = player.SimulatedStats.Minutes > 0 ? player.SimulatedStats : player.SeasonStats;
        if (stats.Minutes <= 0) return;

        double shootFactor = CalculateCoachSkillFactor(coach.ShootingHistory);
        double scoreFactor = CalculateCoachSkillFactor(coach.ScoringHistory);
        double reboundFactor = CalculateCoachSkillFactor(coach.ReboundingHistory);
        double passFactor = CalculateCoachSkillFactor(coach.PassingHistory);
        double defendFactor = CalculateCoachSkillFactor(coach.DefenseHistory);

        player.Ratings.TrueRatingSimple = StatisticsCalculator.CalculateCoachAdjustedTrueRatingSimple(
            stats, shootFactor, scoreFactor, reboundFactor, passFactor, defendFactor);
    }

    /// <summary>
    /// Calculates coach-adjusted trade value for a player.
    /// Port of CPlayer::SetAdjTradeValue(pot1[], pot2[], effort[]) — Player.h:3010-3026.
    /// Uses coach's history arrays to look up perceived potential/effort, then computes
    /// age curve, decline, and misc factors.
    /// Stores result in player.Ratings.TradeValue.
    /// </summary>
    public static void CalculateCoachAdjustedTradeValue(Player player, StaffMember? coach)
    {
        if (coach == null) return;

        // Look up coach's perceived ratings (bounds-checked)
        int pot1Idx = Math.Clamp(player.Ratings.Potential1, 0, coach.Pot1History.Length - 1);
        int pot2Idx = Math.Clamp(player.Ratings.Potential2, 0, coach.Pot2History.Length - 1);
        int effortIdx = Math.Clamp(player.Ratings.Effort, 0, coach.EffortHistory.Length - 1);

        int p1 = coach.Pot1History[pot1Idx];
        int p2 = coach.Pot2History[pot2Idx];
        int ef = coach.EffortHistory[effortIdx];

        double rating = player.Ratings.TradeTrueRating;
        int age = player.Age;

        double overTheHill = 1.0 - (age - 28.0) / 100.0;
        if (overTheHill > 1) overTheHill = 1;

        double pot = ((p1 + 1.0) / 4.0 + (p2 + 1.0) / 4.0) / 2.0;

        int ageForFactor = age > 28 ? 28 : age;
        double factor = 1.0 + (((28.0 - ageForFactor) * 3.0) / 100.0) * pot;

        double misc = (100.0 + ef + p1 + p2 - 9.0) / 100.0;

        player.Ratings.TradeValue = rating * factor * misc * overTheHill;
    }

    /// <summary>
    /// Applies coach adjustments (true rating + trade value) to all players on a team.
    /// </summary>
    public static void ApplyCoachAdjustments(League league, int teamIndex)
    {
        if (teamIndex < 0 || teamIndex >= league.Teams.Count) return;

        var team = league.Teams[teamIndex];
        if (team.Coach == null) return;

        foreach (var player in team.Roster)
        {
            if (string.IsNullOrEmpty(player.Name)) continue;
            CalculateCoachAdjustedTrueRating(player, team.Coach);
            CalculateCoachAdjustedTradeValue(player, team.Coach);
        }
    }

    /// <summary>
    /// Applies coach adjustments to all players on all teams.
    /// </summary>
    public static void ApplyAllCoachAdjustments(League league)
    {
        for (int i = 0; i < league.Teams.Count; i++)
            ApplyCoachAdjustments(league, i);
    }
}
