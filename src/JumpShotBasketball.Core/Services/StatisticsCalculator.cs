using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Game;
using JumpShotBasketball.Core.Models.League;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Pure-function rating and standings calculations.
/// Ported from C++ CPlayer::Calculate(), SetOutside()..SetTransitionDefense(),
/// SetTrueRating(), SetTradeTrueRating(), SetTradeValue(), SetMvp(),
/// and CAverage::UpdateStandings().
/// Each ODPT method takes a single PlayerStatLine and returns a double,
/// eliminating the C++ triple-repetition for season/sim/playoff.
/// </summary>
public static class StatisticsCalculator
{
    // ───────────────────────────────────────────────────────────────
    // Offensive ODPT ratings (4 methods)
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Outside (perimeter shooting) rating.
    /// Ported from CPlayer::SetOutside() — Player.h:1926-2020.
    /// </summary>
    public static double CalculateOutsideRating(PlayerStatLine stats)
    {
        if (stats.Minutes <= 0) return 0;

        double fgm = stats.FieldGoalsMade;
        double fga = stats.FieldGoalsAttempted;
        double tfgm = stats.ThreePointersMade;
        double tfga = stats.ThreePointersAttempted;
        double ftm = stats.FreeThrowsMade;
        double fta = stats.FreeThrowsAttempted;
        double oreb = stats.OffensiveRebounds;
        double min = stats.Minutes;

        double ftPct = fta > 0 ? ftm / fta : 0;

        double result = fgm * 2 + tfgm * 3 - fta - oreb
                      - (fga - fgm) - (tfga - tfgm);
        result = result / min * 48 + ftPct;
        result = (result + 10) / 2;

        return result;
    }

    /// <summary>
    /// Driving (penetration) rating.
    /// Ported from CPlayer::SetDriving() — Player.h:2021-2098.
    /// </summary>
    public static double CalculateDrivingRating(PlayerStatLine stats)
    {
        if (stats.Minutes <= 0) return 0;

        double fgm = stats.FieldGoalsMade;
        double fta = stats.FreeThrowsAttempted;
        double ast = stats.Assists;
        double oreb = stats.OffensiveRebounds;
        double reb = (double)stats.Rebounds / 3.0;
        double min = stats.Minutes;

        double rebRatio = reb > 0 ? oreb / (reb * 3) : 0;

        double result = (fgm + fta) / 6.0 + ast / 2.0;
        result = result / min * 48 + rebRatio * 2;

        return result;
    }

    /// <summary>
    /// Post (inside) rating.
    /// Ported from CPlayer::SetPost() — Player.h:2100-2219.
    /// </summary>
    public static double CalculatePostRating(PlayerStatLine stats)
    {
        if (stats.Minutes <= 0) return 0;

        double fgm = stats.FieldGoalsMade;
        double fga = stats.FieldGoalsAttempted;
        double fta = stats.FreeThrowsAttempted;
        double oreb = stats.OffensiveRebounds;
        double reb = (double)stats.Rebounds / 3.0;
        double to = stats.Turnovers;
        double min = stats.Minutes;

        double fgPct = fga > 0 ? fgm / fga : 0;
        double rebRatio = reb > 0 ? oreb / (reb * 3) : 0;

        double result = (oreb * 4.0 / 5.0 + reb) * fgPct
                      + fta * 3.0 / 5.0 - to * 4.0 / 5.0;
        result = result / min * 48 + (1 - rebRatio);
        result = (result + 1) * 9.0 / 8.0;

        return result;
    }

    /// <summary>
    /// Transition (fastbreak) rating.
    /// Ported from CPlayer::SetTransition() — Player.h:2221-2329.
    /// </summary>
    public static double CalculateTransitionRating(PlayerStatLine stats)
    {
        if (stats.Minutes <= 0) return 0;

        double fta = stats.FreeThrowsAttempted;
        double oreb = stats.OffensiveRebounds;
        double ast = stats.Assists;
        double reb = (double)stats.Rebounds / 3.0;
        double min = stats.Minutes;

        double result = fta / 2.0 + oreb / 2.0 + ast / 2.0 - reb;
        result = result / min * 48;

        return result;
    }

    // ───────────────────────────────────────────────────────────────
    // Defensive ODPT ratings (4 methods)
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Outside defense rating.
    /// Ported from CPlayer::SetOutsideDefense() — Player.h:2331-2488.
    /// </summary>
    public static double CalculateOutsideDefenseRating(PlayerStatLine stats)
    {
        if (stats.Minutes <= 0) return 0;

        double fta = stats.FreeThrowsAttempted;
        double fga = stats.FieldGoalsAttempted;
        double oreb = stats.OffensiveRebounds;
        double reb = (double)stats.Rebounds / 3.0;
        double stl = stats.Steals;
        double blk = stats.Blocks;
        double pf = stats.PersonalFouls;
        double min = stats.Minutes;

        double rebRatio = reb > 0 ? oreb / (reb * 3) : 0;
        double stlblkRatio = (stl + blk) > 0 ? stl / (stl + blk) : 0;
        double blkpfRatio = pf > 0 ? blk / pf : 0;
        double ftRatio = fga > 0 ? fta / fga : 0;
        double orebFactor = (oreb + reb * 3) / min * 10;

        double baseVal = stl + blk / 4.0 - pf / 4.0;
        double result = baseVal / min * 48 + ftRatio * 2
                      + stlblkRatio + (1 - rebRatio) * 2 + blkpfRatio + orebFactor;

        return result;
    }

    /// <summary>
    /// Driving defense rating.
    /// Ported from CPlayer::SetDrivingDefense() — Player.h:2490-2623.
    /// </summary>
    public static double CalculateDrivingDefenseRating(PlayerStatLine stats)
    {
        if (stats.Minutes <= 0) return 0;

        double fta = stats.FreeThrowsAttempted;
        double fga = stats.FieldGoalsAttempted;
        double oreb = stats.OffensiveRebounds;
        double reb = (double)stats.Rebounds / 3.0;
        double stl = stats.Steals;
        double blk = stats.Blocks;
        double pf = stats.PersonalFouls;
        double to = stats.Turnovers;
        double ast = stats.Assists;
        double min = stats.Minutes;

        double stlpfRatio = pf > 0 ? stl / pf : 0;
        double stlblkRatio = (stl + blk) > 0 ? stl / (stl + blk) : 0;
        double rebfgaRatio = fga > 0 ? reb * 3 / fga : 0;
        double ftRatio = fga > 0 ? fta / fga : 0;
        double ratio = (ast + reb + to) > 0
            ? (ast + reb * 3) / (ast + reb * 3 + to)
            : 0;

        double baseVal = stl - pf / 4.0;
        double result = baseVal / min * 48 + ftRatio
                      + stlblkRatio + rebfgaRatio + stlpfRatio + ratio;
        result = result * 4.0 / 3.0;

        return result;
    }

    /// <summary>
    /// Post defense rating.
    /// Ported from CPlayer::SetPostDefense() — Player.h:2625-2745.
    /// </summary>
    public static double CalculatePostDefenseRating(PlayerStatLine stats)
    {
        if (stats.Minutes <= 0) return 0;

        double fta = stats.FreeThrowsAttempted;
        double fga = stats.FieldGoalsAttempted;
        double oreb = stats.OffensiveRebounds;
        double reb = (double)stats.Rebounds / 3.0;
        double blk = stats.Blocks;
        double pf = stats.PersonalFouls;
        double min = stats.Minutes;

        double rebRatio = reb > 0 ? oreb / (reb * 3) : 0;
        double ftRatio = fga > 0 ? fta / fga : 0;

        double baseVal = oreb * 2.0 / 3.0 + reb + blk - pf / 4.0;
        double result = baseVal / min * 48 + ftRatio * 2 - rebRatio;

        return result;
    }

    /// <summary>
    /// Transition defense rating.
    /// Ported from CPlayer::SetTransitionDefense() — Player.h:2747-2870.
    /// </summary>
    public static double CalculateTransitionDefenseRating(PlayerStatLine stats)
    {
        if (stats.Minutes <= 0) return 0;

        double fta = stats.FreeThrowsAttempted;
        double fga = stats.FieldGoalsAttempted;
        double oreb = stats.OffensiveRebounds;
        double reb = (double)stats.Rebounds / 3.0;
        double stl = stats.Steals;
        double blk = stats.Blocks;
        double pf = stats.PersonalFouls;
        double min = stats.Minutes;

        double rebRatio = reb > 0 ? oreb / (reb * 3) : 0;
        double ftRatio = fga > 0 ? fta / fga : 0;
        double stlblkRatio = (stl + blk) > 0 ? stl / (stl + blk) : 0;

        double baseVal = stl - pf / 4.0;
        double result = baseVal / min * 48 + ftRatio + (1 - rebRatio) * 2 + stlblkRatio;
        result = (result + 0.5) * 1.5;

        return result;
    }

    // ───────────────────────────────────────────────────────────────
    // Defense rating (award-specific)
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Aggregate defense rating for DPOY / All-Defense selection.
    /// Ported from CPlayer::SetDefense() — Player.h:1828-1891.
    /// </summary>
    public static double CalculateDefenseRating(PlayerStatLine stats)
    {
        if (stats.Minutes <= 0 || stats.Games <= 0) return 0;

        double games = stats.Games;
        double oreb = stats.OffensiveRebounds;
        double dreb = (stats.Rebounds - stats.OffensiveRebounds) / 3.0;
        double fga = stats.FieldGoalsAttempted - stats.ThreePointersAttempted;
        double fta = stats.FreeThrowsAttempted;

        double rebRatio = dreb > 0 ? oreb / (dreb * 3) : 0;
        double ftRatio = fga > 0 ? fta / fga : 0;

        double defense = (stats.Steals + stats.Blocks + dreb - stats.PersonalFouls) / games;
        defense = defense + ftRatio - rebRatio;

        return defense;
    }

    // ───────────────────────────────────────────────────────────────
    // Composite ratings
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Simple true rating — raw gun+skill accumulator (not normalized).
    /// Ported from CPlayer::SetTrueRatingSimple(2) — Player.h:1522-1553.
    /// </summary>
    public static double CalculateTrueRatingSimple(PlayerStatLine stats)
    {
        double fgm = stats.FieldGoalsMade;
        double tfgm = stats.ThreePointersMade;
        double fga = stats.FieldGoalsAttempted;
        double ftm = stats.FreeThrowsMade;
        double fta = stats.FreeThrowsAttempted;
        double oreb = stats.OffensiveRebounds;
        double reb = stats.Rebounds;
        double ast = stats.Assists;
        double stl = stats.Steals;
        double to = stats.Turnovers;
        double blk = stats.Blocks;

        double gun = fgm + tfgm - ((fga - fgm) * 2.0 / 3.0);
        gun += ftm - (fta / 2.0);
        gun += (fta - ftm) / 6.0;
        gun *= 3.0 / 2.0;

        double skill = oreb * 2.0 / 3.0 + (reb - oreb) / 3.0;
        skill += stl - to + blk;
        skill += ast * 4.0 / 5.0;
        skill *= 3.0 / 4.0;

        return gun + skill;
    }

    /// <summary>
    /// Coach-adjusted true rating simple — same gun+skill formula but with coach factors per category.
    /// Ported from CPlayer::SetAdjTrueRatingSimple() — Player.h:1494-1520.
    /// Each stat component is multiplied by the corresponding coach skill factor.
    /// </summary>
    public static double CalculateCoachAdjustedTrueRatingSimple(
        PlayerStatLine stats, double shootFactor, double scoreFactor,
        double reboundFactor, double passFactor, double defendFactor)
    {
        double fgm = stats.FieldGoalsMade;
        double tfgm = stats.ThreePointersMade;
        double fga = stats.FieldGoalsAttempted;
        double ftm = stats.FreeThrowsMade;
        double fta = stats.FreeThrowsAttempted;
        double oreb = stats.OffensiveRebounds;
        double reb = stats.Rebounds;
        double ast = stats.Assists;
        double stl = stats.Steals;
        double to = stats.Turnovers;
        double blk = stats.Blocks;

        double gun = fgm * shootFactor + tfgm * shootFactor - ((fga * scoreFactor - fgm * shootFactor) * 2.0 / 3.0);
        gun += ftm * shootFactor - (fta * scoreFactor / 2.0);
        gun += (fta * scoreFactor - ftm * shootFactor) / 6.0;
        gun *= 3.0 / 2.0;

        double skill = oreb * reboundFactor * 2.0 / 3.0 + (reb * reboundFactor - oreb * reboundFactor) / 3.0;
        skill += stl * defendFactor - to * passFactor + blk * defendFactor;
        skill += ast * passFactor * 4.0 / 5.0;
        skill *= 3.0 / 4.0;

        return gun + skill;
    }

    /// <summary>
    /// Full true rating with per-game normalization and logistic transform.
    /// Ported from CPlayer::SetTrueRating() — Player.h:1379-1520.
    /// </summary>
    public static double CalculateTrueRating(PlayerStatLine stats, double leagueFactor)
    {
        if (stats.Minutes <= 0) return 0;
        if (stats.Games <= 0) return 0;

        double fgm = stats.FieldGoalsMade;
        double tfgm = stats.ThreePointersMade;
        double fga = stats.FieldGoalsAttempted;
        double ftm = stats.FreeThrowsMade;
        double fta = stats.FreeThrowsAttempted;
        double oreb = stats.OffensiveRebounds;
        double reb = stats.Rebounds;
        double ast = stats.Assists;
        double stl = stats.Steals;
        double to = stats.Turnovers;
        double blk = stats.Blocks;

        double gun = fgm + tfgm - ((fga - fgm) * 2.0 / 3.0);
        gun += ftm - (fta / 2.0);
        gun += (fta - ftm) / 6.0;

        double skill = oreb * 2.0 / 3.0 + (reb - oreb) / 3.0;
        skill += stl - to + blk;
        skill += ast * 4.0 / 5.0;

        double tru = gun * 3.0 / 2.0 + skill * 3.0 / 4.0;

        double games = stats.Games;
        double min = (double)stats.Minutes / games;

        tru = tru / games;
        tru = tru - leagueFactor / 48.0 * min;
        tru = Math.Pow(100 + tru, 13.0) / (Math.Pow(100, 13.0) + Math.Pow(100 + tru, 13.0));
        tru = tru * games - games / 2.0;

        return tru;
    }

    /// <summary>
    /// Trade true rating — simple rating + defensive adjustment, per game.
    /// Ported from CPlayer::SetTradeTrueRating() — Player.h:1603-1612.
    /// </summary>
    public static double CalculateTradeTrueRating(
        double trueRatingSimple,
        int movementDefenseRaw,
        int postDefenseRaw,
        int penetrationDefenseRaw,
        int transitionDefenseRaw,
        int minutes,
        int games,
        int injury)
    {
        if (injury >= 5 || games == 0) return 0;

        double defense = (movementDefenseRaw + postDefenseRaw
                       + penetrationDefenseRaw + transitionDefenseRaw - 20) * 0.25;
        defense = defense / 48.0 * minutes;

        return (trueRatingSimple + defense) / games;
    }

    /// <summary>
    /// Trade value — market value factoring age, potential, effort.
    /// Ported from CPlayer::SetTradeValue() — Player.h:2972-2986.
    /// </summary>
    public static double CalculateTradeValue(
        double tradeTrueRating,
        int age,
        int pot1,
        int pot2,
        int effort)
    {
        double overTheHill = 1.0 - (age - 28.0) / 100.0;
        if (overTheHill > 1) overTheHill = 1;

        double pot = ((pot1 + 1.0) / 4.0 + (pot2 + 1.0) / 4.0) / 2.0;

        int ageForFactor = age > 28 ? 28 : age;
        double factor = 1.0 + (((28.0 - ageForFactor) * 3.0) / 100.0) * pot;

        double misc = (100.0 + effort + pot1 + pot2 - 9.0) / 100.0;

        return tradeTrueRating * factor * misc * overTheHill;
    }

    /// <summary>
    /// MVP rating — true rating with defensive per-48 contribution.
    /// Ported from CPlayer::SetMvp() — Player.h:2872-2970.
    /// </summary>
    public static double CalculateMvpRating(
        PlayerStatLine stats,
        double leagueFactor,
        double outsideDefPer48,
        double drivingDefPer48,
        double postDefPer48,
        double transitionDefPer48)
    {
        if (stats.Minutes <= 0) return 0;

        double fgm = stats.FieldGoalsMade;
        double tfgm = stats.ThreePointersMade;
        double fga = stats.FieldGoalsAttempted;
        double ftm = stats.FreeThrowsMade;
        double fta = stats.FreeThrowsAttempted;
        double oreb = stats.OffensiveRebounds;
        double reb = stats.Rebounds;
        double ast = stats.Assists;
        double stl = stats.Steals;
        double to = stats.Turnovers;
        double blk = stats.Blocks;

        double gun = fgm + tfgm - ((fga - fgm) * 2.0 / 3.0);
        gun += ftm - (fta / 2.0);
        gun += (fta - ftm) / 6.0;
        gun *= 3.0 / 2.0;

        double skill = oreb * 2.0 / 3.0 + (reb - oreb) / 3.0;
        skill += stl - to + blk;
        skill += ast * 4.0 / 5.0;
        skill *= 3.0 / 4.0;

        double tru = gun + skill;

        double games = stats.Games > 0 ? stats.Games : 1;
        double min = (double)stats.Minutes / games;

        tru = tru / games - leagueFactor / 48.0 * min;

        double def = outsideDefPer48 + drivingDefPer48 + postDefPer48 + transitionDefPer48;
        def = (def - min * 20.0 / 48.0) * 0.25;
        tru += def;

        tru = Math.Pow(100 + tru, 13.0) / (Math.Pow(100, 13.0) + Math.Pow(100 + tru, 13.0));
        tru = tru * games - games / 2.0;

        return tru;
    }

    // ───────────────────────────────────────────────────────────────
    // Per-48 production stats & shooting percentages
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Calculates the foul ratio from league averages.
    /// Used as input to CalculatePer48Stats for fouls-drawn computation.
    /// Ported from C++ Engine.cpp foul ratio calculation.
    /// </summary>
    public static double CalculateFoulRatio(LeagueAverages averages)
    {
        if (averages.FreeThrowsAttempted <= 0) return 0.44;
        return (averages.PersonalFouls - averages.Turnovers * 0.1) / averages.FreeThrowsAttempted;
    }

    /// <summary>
    /// Computes per-48-minute production rates and shooting percentages from an explicit stat line.
    /// Writes results to player.Ratings. Guards: skip if Minutes == 0 or Games == 0.
    /// Ported from C++ Engine.cpp per-48 calculations.
    /// Note: FieldGoalsAttemptedPer48Min stores 2-point FGA only (total FGA minus 3PA),
    /// and FieldGoalPercentage is 2-point FG% only. This matches C++ Engine.cpp behavior.
    /// </summary>
    public static void CalculatePer48Stats(Player player, PlayerStatLine stats, double foulRatio = 0.44)
    {
        var s = stats;
        var r = player.Ratings;

        if (s.Minutes <= 0 || s.Games <= 0) return;

        double min = s.Minutes;
        double games = s.Games;

        // 2-point only (matches C++ Engine.cpp behavior)
        int twoPtFga = s.FieldGoalsAttempted - s.ThreePointersAttempted;
        int twoPtFgm = s.FieldGoalsMade - s.ThreePointersMade;

        // Per-48 rates
        r.FieldGoalsAttemptedPer48Min = (double)twoPtFga / min * 48.0;
        r.ThreePointersAttemptedPer48Min = (double)s.ThreePointersAttempted / min * 48.0;
        r.OffensiveReboundsPer48Min = (double)s.OffensiveRebounds / min * 48.0;
        r.DefensiveReboundsPer48Min = (double)(s.Rebounds - s.OffensiveRebounds) / min * 48.0;
        r.AssistsPer48Min = (double)s.Assists / min * 48.0;
        r.StealsPer48Min = (double)s.Steals / min * 48.0 * 5.0 / 6.0 * 11.0 / 10.0;
        r.TurnoversPer48Min = (double)s.Turnovers / min * 48.0;
        r.BlocksPer48Min = (double)s.Blocks / min * 48.0;
        r.PersonalFoulsPer48Min = Math.Max(0, (double)(s.PersonalFouls - s.Turnovers / 10) / min * 48.0);
        r.FoulsDrawnPer48Min = (double)s.FreeThrowsAttempted * foulRatio / min * 48.0;
        r.MinutesPerGame = min / games;

        // Adjusted variants = base values initially (engine overwrites during games)
        r.AdjustedFieldGoalsAttemptedPer48Min = r.FieldGoalsAttemptedPer48Min;
        r.AdjustedThreePointersAttemptedPer48Min = r.ThreePointersAttemptedPer48Min;
        r.AdjustedFoulsDrawnPer48Min = r.FoulsDrawnPer48Min;
        r.AdjustedTurnoversPer48Min = r.TurnoversPer48Min;

        // Shooting percentages (integer × 1000)
        r.FieldGoalPercentage = twoPtFga > 0 ? (int)Math.Round((double)twoPtFgm / twoPtFga * 1000) : 0;
        r.FreeThrowPercentage = s.FreeThrowsAttempted > 0 ? (int)Math.Round((double)s.FreeThrowsMade / s.FreeThrowsAttempted * 1000) : 0;
        r.ThreePointPercentage = s.ThreePointersAttempted > 0 ? (int)Math.Round((double)s.ThreePointersMade / s.ThreePointersAttempted * 1000) : 0;
        r.AdjustedFieldGoalPercentage = r.FieldGoalPercentage;
        r.ProjectionFieldGoalPercentage = r.FieldGoalPercentage;

        // Assist-to-turnover ratio (C++ Player.h:1813-1825)
        r.AstToRatio = s.Turnovers > 0
            ? (double)s.Assists / s.Turnovers
            : (double)s.Assists;
    }

    /// <summary>
    /// Computes per-48-minute production rates and shooting percentages from SeasonStats.
    /// Convenience wrapper that delegates to the explicit stat line overload.
    /// </summary>
    public static void CalculatePer48Stats(Player player, double foulRatio = 0.44)
    {
        CalculatePer48Stats(player, player.SeasonStats, foulRatio);
    }

    // ───────────────────────────────────────────────────────────────
    // Stamina
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Calculates per-game stamina from stats and coach endurance rating.
    /// Ported from CPlayer::SetGameStamina() — Player.h:5595-5601.
    /// </summary>
    public static int CalculateStamina(int games, int minutes, int coachEndurance)
    {
        if (games <= 0) return 480;
        double stamina = (double)minutes / games / 3.0 * 60.0;
        double factor = 1.0 + coachEndurance / 50.0;
        stamina *= factor;
        if (stamina < 480) stamina = 480;
        return (int)stamina;
    }

    // ───────────────────────────────────────────────────────────────
    // Orchestrator
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Computes all ratings for a player from their season stats.
    /// Ported from CPlayer::Calculate(bool rookie_import) — Player.cpp:22-83.
    /// Writes results to player.Ratings.
    /// </summary>
    public static void CalculateAllRatings(Player player, bool rookieImport = false)
    {
        var stats = player.SeasonStats;
        var ratings = player.Ratings;

        // Compute raw ODPT doubles
        double outside = CalculateOutsideRating(stats);
        double driving = CalculateDrivingRating(stats);
        double post = CalculatePostRating(stats);
        double transition = CalculateTransitionRating(stats);
        double outsideDef = CalculateOutsideDefenseRating(stats);
        double drivingDef = CalculateDrivingDefenseRating(stats);
        double postDef = CalculatePostDefenseRating(stats);
        double transitionDef = CalculateTransitionDefenseRating(stats);

        // Store the double ratings
        ratings.Outside = outside;
        ratings.Driving = driving;
        ratings.Post = post;
        ratings.Transition = transition;
        ratings.OutsideDefense = outsideDef;
        ratings.DrivingDefense = drivingDef;
        ratings.PostDefense = postDef;
        ratings.TransitionDefense = transitionDef;

        // Round to integers with optional rookie adjustment
        int oo = (int)Math.Round(outside);
        int od = (int)Math.Round(outsideDef);
        int po = (int)Math.Round(driving);
        int pd = (int)Math.Round(drivingDef);
        int io = (int)Math.Round(post);
        int id = (int)Math.Round(postDef);
        int fo = (int)Math.Round(transition);
        int fd = (int)Math.Round(transitionDef);

        if (rookieImport)
        {
            oo = (int)Math.Round(outside + 1);
            od = (int)Math.Round(outsideDef - 2.0);
            po = (int)Math.Round(driving + 1);
            pd = (int)Math.Round(drivingDef - 2.0);
            io = (int)Math.Round(post + 1);
            id = (int)Math.Round(postDef - 2.0);
            fo = (int)Math.Round(transition + 1);
            fd = (int)Math.Round(transitionDef - 2.0);
        }

        // Clamp to 1-9
        ratings.MovementOffenseRaw = Clamp(oo, 1, 9);
        ratings.MovementDefenseRaw = Clamp(od, 1, 9);
        ratings.PenetrationOffenseRaw = Clamp(po, 1, 9);
        ratings.PenetrationDefenseRaw = Clamp(pd, 1, 9);
        ratings.PostOffenseRaw = Clamp(io, 1, 9);
        ratings.PostDefenseRaw = Clamp(id, 1, 9);
        ratings.TransitionOffenseRaw = Clamp(fo, 1, 9);
        ratings.TransitionDefenseRaw = Clamp(fd, 1, 9);

        // Composites
        ratings.TrueRatingSimple = CalculateTrueRatingSimple(stats);
        ratings.TradeTrueRating = CalculateTradeTrueRating(
            ratings.TrueRatingSimple,
            ratings.MovementDefenseRaw,
            ratings.PostDefenseRaw,
            ratings.PenetrationDefenseRaw,
            ratings.TransitionDefenseRaw,
            stats.Minutes,
            stats.Games,
            player.Injury);
        ratings.TradeValue = CalculateTradeValue(
            ratings.TradeTrueRating,
            player.Age,
            ratings.Potential1,
            ratings.Potential2,
            ratings.Effort);
    }

    // ───────────────────────────────────────────────────────────────
    // Standings
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Win percentage. Returns 0 if no games played.
    /// </summary>
    public static double CalculateWinPercentage(int wins, int losses)
    {
        int games = wins + losses;
        return games > 0 ? (double)wins / games : 0;
    }

    /// <summary>
    /// Games back from a reference team.
    /// </summary>
    public static double CalculateGamesBack(int refWins, int refLosses, int teamWins, int teamLosses)
    {
        return ((refWins - refLosses) - (teamWins - teamLosses)) / 2.0;
    }

    /// <summary>
    /// Extracts wins and losses from a packed record (wins * 100 + losses).
    /// Matches the C++ convention used in CRecord.
    /// </summary>
    public static (int Wins, int Losses) UnpackRecord(int packedRecord)
    {
        int wins = packedRecord / 100;
        int losses = packedRecord - wins * 100;
        return (wins, losses);
    }

    /// <summary>
    /// Records a game result and updates standings for both teams.
    /// Ported from CAverage::UpdateStandings() — Average.cpp:605-712.
    /// t1 = visitor, t2 = home (by C++ convention).
    /// </summary>
    public static void UpdateStandings(League league, int team1Index, int score1, int team2Index, int score2)
    {
        if (team1Index < 0 || team1Index >= league.Teams.Count) return;
        if (team2Index < 0 || team2Index >= league.Teams.Count) return;

        var rec1 = league.Teams[team1Index].Record;
        var rec2 = league.Teams[team2Index].Record;

        // Determine winner: t1 is visitor, t2 is home
        int winnerAdd = 100; // wins are stored as *100
        int loserAdd = 1;    // losses are stored as +1

        if (score1 > score2)
        {
            // Team 1 (visitor) wins
            rec1.LeagueRecord += winnerAdd;
            rec1.VisitorRecord += winnerAdd;
            rec2.LeagueRecord += loserAdd;
            rec2.HomeRecord += loserAdd;
        }
        else if (score2 > score1)
        {
            // Team 2 (home) wins
            rec2.LeagueRecord += winnerAdd;
            rec2.HomeRecord += winnerAdd;
            rec1.LeagueRecord += loserAdd;
            rec1.VisitorRecord += loserAdd;
        }
        else
        {
            return; // tie — no update
        }

        // Conference/division records
        if (score1 > score2)
        {
            if (rec1.Conference == rec2.Conference)
                rec1.ConferenceRecord += winnerAdd;
            if (rec1.Division == rec2.Division)
                rec1.DivisionRecord += winnerAdd;
            if (rec2.Conference == rec1.Conference)
                rec2.ConferenceRecord += loserAdd;
            if (rec2.Division == rec1.Division)
                rec2.DivisionRecord += loserAdd;
        }
        else
        {
            if (rec2.Conference == rec1.Conference)
                rec2.ConferenceRecord += winnerAdd;
            if (rec2.Division == rec1.Division)
                rec2.DivisionRecord += winnerAdd;
            if (rec1.Conference == rec2.Conference)
                rec1.ConferenceRecord += loserAdd;
            if (rec1.Division == rec2.Division)
                rec1.DivisionRecord += loserAdd;
        }

        // Head-to-head
        if (score1 > score2)
        {
            rec1.VsOpponent.TryGetValue(team2Index, out int val1);
            rec1.VsOpponent[team2Index] = val1 + winnerAdd;
            rec2.VsOpponent.TryGetValue(team1Index, out int val2);
            rec2.VsOpponent[team1Index] = val2 + loserAdd;
        }
        else
        {
            rec2.VsOpponent.TryGetValue(team1Index, out int val2);
            rec2.VsOpponent[team1Index] = val2 + winnerAdd;
            rec1.VsOpponent.TryGetValue(team2Index, out int val1);
            rec1.VsOpponent[team2Index] = val1 + loserAdd;
        }

        // Recalculate derived fields for both teams
        RecalculateTeamStandings(rec1);
        RecalculateTeamStandings(rec2);

        // Update head-to-head percentages
        UpdateVsPercentage(rec1, team2Index);
        UpdateVsPercentage(rec2, team1Index);
    }

    /// <summary>
    /// Recomputes derived standings fields (wins, losses, percentages, games back)
    /// for all teams. Call after loading or bulk updates.
    /// </summary>
    public static void RecalculateStandings(League league)
    {
        foreach (var team in league.Teams)
        {
            RecalculateTeamStandings(team.Record);
        }

        // Recalculate games back relative to the best record
        if (league.Teams.Count == 0) return;

        int bestDiff = int.MinValue;
        foreach (var team in league.Teams)
        {
            int diff = team.Record.Wins - team.Record.Losses;
            if (diff > bestDiff) bestDiff = diff;
        }

        foreach (var team in league.Teams)
        {
            team.Record.LeagueGamesBack = bestDiff - (team.Record.Wins - team.Record.Losses);
        }
    }

    /// <summary>
    /// Calculates the league-wide average production factor per 48 minutes.
    /// Used as the leagueFactor parameter for CalculateTrueRating.
    /// </summary>
    public static double CalculateLeagueAverageFactor(IEnumerable<PlayerStatLine> statLines)
    {
        double totalRaw = 0;
        int totalMinutes = 0;

        foreach (var stats in statLines)
        {
            if (stats.Minutes <= 0) continue;

            double fgm = stats.FieldGoalsMade;
            double tfgm = stats.ThreePointersMade;
            double fga = stats.FieldGoalsAttempted;
            double ftm = stats.FreeThrowsMade;
            double fta = stats.FreeThrowsAttempted;
            double oreb = stats.OffensiveRebounds;
            double reb = stats.Rebounds;
            double ast = stats.Assists;
            double stl = stats.Steals;
            double to = stats.Turnovers;
            double blk = stats.Blocks;

            double gun = fgm + tfgm - ((fga - fgm) * 2.0 / 3.0);
            gun += ftm - (fta / 2.0);
            gun += (fta - ftm) / 6.0;

            double skill = oreb * 2.0 / 3.0 + (reb - oreb) / 3.0;
            skill += stl - to + blk;
            skill += ast * 4.0 / 5.0;

            double raw = gun * 3.0 / 2.0 + skill * 3.0 / 4.0;
            totalRaw += raw;
            totalMinutes += stats.Minutes;
        }

        if (totalMinutes <= 0) return 0;
        return totalRaw / totalMinutes * 48;
    }

    // ───────────────────────────────────────────────────────────────
    // Helpers
    // ───────────────────────────────────────────────────────────────

    private static void RecalculateTeamStandings(Models.Team.TeamRecord rec)
    {
        var (w, l) = UnpackRecord(rec.LeagueRecord);
        rec.Wins = w;
        rec.Losses = l;
        rec.LeaguePercentage = CalculateWinPercentage(w, l);
        rec.LeagueGamesBack = w - l; // raw diff; games-back vs leader done in RecalculateStandings

        var (dw, dl) = UnpackRecord(rec.DivisionRecord);
        rec.DivisionPercentage = CalculateWinPercentage(dw, dl);

        var (cw, cl) = UnpackRecord(rec.ConferenceRecord);
        rec.ConferencePercentage = CalculateWinPercentage(cw, cl);
    }

    private static void UpdateVsPercentage(Models.Team.TeamRecord rec, int opponentIndex)
    {
        if (!rec.VsOpponent.TryGetValue(opponentIndex, out int packed)) return;
        var (w, l) = UnpackRecord(packed);
        rec.VsOpponentPercentage[opponentIndex] = CalculateWinPercentage(w, l);
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}
