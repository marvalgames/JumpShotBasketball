using JumpShotBasketball.Core.Models.Game;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Player aging, improvement/decline, projection ratings, and career stat archival.
/// Port of Career.cpp (AdjustScoring, SetImprovementVariable) and
/// Player.h (SetRatings, SetGameInjuryRating, GenerateRandomPrime).
/// </summary>
public static class PlayerDevelopmentService
{
    // ── GenerateRandomPrime ─────────────────────────────────────────

    /// <summary>
    /// Generates a random prime age for a player.
    /// Port of CPlayer::GenerateRandomPrime() — Player.h:5604-5628.
    /// Bug fix: C++ line 5614 has IntRandom(1)&lt;=5 which always passes;
    /// we use proper probability checks per tier.
    /// </summary>
    public static int GenerateRandomPrime(Player player, Random? random = null)
    {
        random ??= Random.Shared;

        int prime = 26 + random.Next(1, 4); // 27-29

        if (random.Next(1, 6) <= 1) // 1/5 chance
        {
            prime = 25 + random.Next(1, 6); // 26-30

            if (random.Next(1, 6) <= 1) // 1/5 chance
            {
                prime = 24 + random.Next(1, 8); // 25-31

                // Bug fix: C++ has IntRandom(1)<=5 which always passes.
                // Use 1/5 check like the other tiers.
                if (random.Next(1, 6) <= 1)
                {
                    prime = 23 + random.Next(1, 10); // 24-32

                    if (random.Next(1, 6) <= 1) // 1/5 chance
                    {
                        prime = 22 + random.Next(1, 12); // 23-33
                    }
                }
            }
        }

        if (player.Age < 21) prime -= 1;
        if (player.Position == " C") prime += 1;

        player.Ratings.Prime = prime;
        return prime;
    }

    // ── CalculateInjuryRating ──────────────────────────────────────

    /// <summary>
    /// Calculates an injury proneness rating based on games missed relative to plays.
    /// Port of CPlayer::SetGameInjuryRating() — Player.h:5573-5593.
    /// </summary>
    public static int CalculateInjuryRating(int games, int minutes, int fga, int fta,
        int turnovers, int injurySetting, int gamesInSeason)
    {
        if (gamesInSeason > 82) gamesInSeason = 82;
        if (games <= 0 || minutes <= 0) return 1;

        int plays = fga + fta + turnovers;
        if (plays <= 0) return 1;

        double playsPerGame = (double)plays / games;
        double plays82 = playsPerGame * gamesInSeason;
        if (plays82 <= 0) return 1;

        int gamesMissed = gamesInSeason - games;
        double rating = (double)gamesMissed * 1000 / plays82;

        if (rating < 1) rating = 1;
        else if (rating > 27) rating = 27;

        int result = (int)rating;

        // Injury setting 2 = moderate: clamp to 2-5
        if (injurySetting == 2)
        {
            if (result > 5) result = 5;
            else if (result < 2) result = 2;
        }

        return result;
    }

    // ── CalculateLeagueHighs ────────────────────────────────────────

    /// <summary>
    /// Scans all players to compute per-48-minute stat highs for each category.
    /// These are used as denominators in projection rating calculation.
    /// </summary>
    public static LeagueHighs CalculateLeagueHighs(League league)
    {
        var highs = new LeagueHighs();

        foreach (var team in league.Teams)
        {
            foreach (var player in team.Roster)
            {
                var stats = player.SimulatedStats;
                if (string.IsNullOrEmpty(player.Name) || stats.Minutes <= 0 || stats.Games <= 0)
                    continue;

                double min = stats.Minutes;
                double fga2 = stats.FieldGoalsAttempted - stats.ThreePointersAttempted;
                double fta = stats.FreeThrowsAttempted;
                double tga = stats.ThreePointersAttempted;
                double oreb = stats.OffensiveRebounds;
                double dreb = stats.Rebounds - stats.OffensiveRebounds;
                double ast = stats.Assists;
                double stl = stats.Steals;
                double to = stats.Turnovers;
                double blk = stats.Blocks;

                // Per-48 rates (multiply by 480 = per-48 with 10x scale as C++ does)
                double fga48 = fga2 / min * 480;
                double fta48 = fta / min * 480;
                double tga48 = tga / min * 480;
                double oreb48 = oreb / min * 480;
                double dreb48 = dreb / min * 480;
                double ast48 = ast / min * 480;
                double stl48 = stl / min * 480;
                double to48 = to / min * 480;
                double blk48 = blk / min * 480;

                if (fga48 > highs.HighFieldGoalsAttempted) highs.HighFieldGoalsAttempted = fga48;
                if (fta48 > highs.HighFreeThrowsAttempted) highs.HighFreeThrowsAttempted = fta48;
                if (tga48 > highs.HighThreePointersAttempted) highs.HighThreePointersAttempted = tga48;
                if (oreb48 > highs.HighOffensiveRebounds) highs.HighOffensiveRebounds = oreb48;
                if (dreb48 > highs.HighDefensiveRebounds) highs.HighDefensiveRebounds = dreb48;
                if (ast48 > highs.HighAssists) highs.HighAssists = ast48;
                if (stl48 > highs.HighSteals) highs.HighSteals = stl48;
                if (to48 > highs.HighTurnovers) highs.HighTurnovers = to48;
                if (blk48 > highs.HighBlocks) highs.HighBlocks = blk48;

                if (stats.Games > highs.HighGames) highs.HighGames = stats.Games;
            }
        }

        // Prevent division by zero
        if (highs.HighFieldGoalsAttempted <= 0) highs.HighFieldGoalsAttempted = 1;
        if (highs.HighFreeThrowsAttempted <= 0) highs.HighFreeThrowsAttempted = 1;
        if (highs.HighThreePointersAttempted <= 0) highs.HighThreePointersAttempted = 1;
        if (highs.HighOffensiveRebounds <= 0) highs.HighOffensiveRebounds = 1;
        if (highs.HighDefensiveRebounds <= 0) highs.HighDefensiveRebounds = 1;
        if (highs.HighAssists <= 0) highs.HighAssists = 1;
        if (highs.HighSteals <= 0) highs.HighSteals = 1;
        if (highs.HighTurnovers <= 0) highs.HighTurnovers = 1;
        if (highs.HighBlocks <= 0) highs.HighBlocks = 1;
        if (highs.HighGames <= 0) highs.HighGames = 82;

        return highs;
    }

    // ── CalculateProjectionRatings ────────────────────────────────────

    /// <summary>
    /// Converts raw season stats into 0-100 projection ratings relative to league highs.
    /// Per-48 normalization with penalty for low-minute players.
    /// Port of CPlayer::SetRatings() — Player.h:1014-1090.
    /// </summary>
    public static void CalculateProjectionRatings(Player player, LeagueHighs highs)
    {
        var stats = player.SimulatedStats;
        var ratings = player.Ratings;

        double min = stats.Minutes;
        if (min <= 0) min = 1;
        int games = stats.Games;
        if (games <= 0) games = 1;

        // Minutes penalty: penalizes players who average less than 24 mpg
        double pen = 1 + ((min / games - 24.0) / 2.0 / 48.0);
        if (pen > 1) pen = 1;

        double fga2 = stats.FieldGoalsAttempted - stats.ThreePointersAttempted;
        if (fga2 <= 0) fga2 = 1;
        double fga3 = stats.ThreePointersAttempted;
        if (fga3 <= 0) fga3 = 1;
        double fta = stats.FreeThrowsAttempted;
        if (fta <= 0) fta = 1;

        // FGA per-48 relative to league high
        double tmp = fga2 / min * 480;
        tmp = tmp * pen / highs.HighFieldGoalsAttempted * 100;
        ratings.ProjectionFieldGoalsAttempted = (int)tmp;

        // FG% (2pt only)
        tmp = (stats.FieldGoalsMade - stats.ThreePointersMade) / fga2 * 100;
        double diff = tmp - (int)tmp;
        ratings.ProjectionFieldGoalPercentage = diff > 0.5 ? (int)tmp + 1 : (int)tmp;

        // FTA per-48 relative to league high
        tmp = stats.FreeThrowsAttempted / min * 480;
        tmp = tmp * pen / highs.HighFreeThrowsAttempted * 100;
        ratings.ProjectionFreeThrowsAttempted = (int)tmp;

        // FT%
        tmp = (double)stats.FreeThrowsMade / fta * 100;
        diff = tmp - (int)tmp;
        ratings.ProjectionFreeThrowPercentage = diff > 0.5 ? (int)tmp + 1 : (int)tmp;

        // 3PA per-48 relative to league high
        tmp = stats.ThreePointersAttempted / min * 480;
        tmp = tmp * pen / highs.HighThreePointersAttempted * 100;
        ratings.ProjectionThreePointersAttempted = (int)tmp;

        // 3P%
        tmp = (double)stats.ThreePointersMade / fga3 * 100;
        diff = tmp - (int)tmp;
        ratings.ProjectionThreePointPercentage = diff > 0.5 ? (int)tmp + 1 : (int)tmp;

        // OREB per-48 relative to league high
        tmp = stats.OffensiveRebounds / min * 480;
        tmp = tmp * pen / highs.HighOffensiveRebounds * 100;
        ratings.ProjectionOffensiveRebounds = (int)tmp;

        // DREB per-48 relative to league high
        tmp = (stats.Rebounds - stats.OffensiveRebounds) / min * 480;
        tmp = tmp * pen / highs.HighDefensiveRebounds * 100;
        ratings.ProjectionDefensiveRebounds = (int)tmp;

        // AST per-48 relative to league high
        tmp = stats.Assists / min * 480;
        tmp = tmp * pen / highs.HighAssists * 100;
        ratings.ProjectionAssists = (int)tmp;

        // STL per-48 relative to league high
        tmp = stats.Steals / min * 480;
        tmp = tmp * pen / highs.HighSteals * 100;
        ratings.ProjectionSteals = (int)tmp;

        // TO per-48 relative to league high (inverted: lower TO = higher rating)
        tmp = stats.Turnovers / min * 480;
        tmp = tmp * pen / highs.HighTurnovers * 100;
        ratings.ProjectionTurnovers = 100 - (int)tmp;

        // BLK per-48 relative to league high
        tmp = stats.Blocks / min * 480;
        tmp = tmp * pen / highs.HighBlocks * 100;
        ratings.ProjectionBlocks = (int)tmp;
    }

    // ── ArchiveSeasonStats ─────────────────────────────────────────

    /// <summary>
    /// Copies the current SimulatedStats to a new CareerSeasonEntry and accumulates into CareerStats.
    /// </summary>
    public static void ArchiveSeasonStats(Player player, int year)
    {
        var sim = player.SimulatedStats;

        // Create career history entry
        var entry = new CareerSeasonEntry
        {
            Year = year,
            TeamName = player.Team,
            Stats = new PlayerStatLine
            {
                Games = sim.Games,
                Minutes = sim.Minutes,
                FieldGoalsMade = sim.FieldGoalsMade,
                FieldGoalsAttempted = sim.FieldGoalsAttempted,
                FreeThrowsMade = sim.FreeThrowsMade,
                FreeThrowsAttempted = sim.FreeThrowsAttempted,
                ThreePointersMade = sim.ThreePointersMade,
                ThreePointersAttempted = sim.ThreePointersAttempted,
                OffensiveRebounds = sim.OffensiveRebounds,
                Rebounds = sim.Rebounds,
                Assists = sim.Assists,
                Steals = sim.Steals,
                Turnovers = sim.Turnovers,
                Blocks = sim.Blocks,
                PersonalFouls = sim.PersonalFouls
            }
        };
        player.CareerHistory.Add(entry);

        // Accumulate into career stats
        var career = player.CareerStats;
        career.Games += sim.Games;
        career.Minutes += sim.Minutes;
        career.FieldGoalsMade += sim.FieldGoalsMade;
        career.FieldGoalsAttempted += sim.FieldGoalsAttempted;
        career.FreeThrowsMade += sim.FreeThrowsMade;
        career.FreeThrowsAttempted += sim.FreeThrowsAttempted;
        career.ThreePointersMade += sim.ThreePointersMade;
        career.ThreePointersAttempted += sim.ThreePointersAttempted;
        career.OffensiveRebounds += sim.OffensiveRebounds;
        career.Rebounds += sim.Rebounds;
        career.Assists += sim.Assists;
        career.Steals += sim.Steals;
        career.Turnovers += sim.Turnovers;
        career.Blocks += sim.Blocks;
        career.PersonalFouls += sim.PersonalFouls;
    }

    // ── CalculateImprovementFactor ──────────────────────────────────

    /// <summary>
    /// Calculates the improvement/decline factor for a stat category.
    /// Factor > 1 means improvement, &lt; 1 means decline.
    /// Port of CCareer::SetImprovementVariable() — Career.cpp:927-1011.
    /// </summary>
    public static double CalculateImprovementFactor(int yearsToPrime, double adjustment,
        double potential, int intangible, Random? random = null)
    {
        random ??= Random.Shared;

        if (potential == 0) potential = 3;
        double adj = adjustment;

        // Handle exact prime: randomly assign to slightly pre or post prime
        if (yearsToPrime == 0)
            yearsToPrime = random.Next(1, 3) * 2 - 3; // -1 or 1

        // Adjustment scaling: past prime gets much larger adj (harder to improve, more decline)
        if (yearsToPrime < 0)
            adj = adj * 12.0;
        else
            adj = adj * 2.0 / 3.0;

        // Major change check (1/12 chance scaled by intangible)
        double majorChange = random.NextDouble() * 72;
        double tempBefore = (intangible + 1.0) / 4.0;
        double tempAfter = (7.0 - intangible) / 4.0;

        if (majorChange <= tempBefore * 1.0 && yearsToPrime > 0)
            adj = adj * 1.0 / 4.0;
        else if (majorChange <= tempBefore * 13.0 && yearsToPrime > 0)
            adj = adj * 1.0 / 2.0;
        else if (majorChange <= tempAfter * 14.0 && yearsToPrime < 0)
            adj = adj * 3.0 / 4.0;

        // Distance from prime calculation
        int absYears = Math.Abs(yearsToPrime);
        double floatYears;

        if (yearsToPrime < 0)
        {
            // Post-prime: gradual scaling
            floatYears = yearsToPrime switch
            {
                -1 => 1.0 / 3.0,
                -2 => 2.0 / 3.0,
                -3 => 1.0,
                -4 => 4.0 / 3.0,
                -5 => 5.0 / 3.0,
                -6 => 2.0,
                _ => 1 + random.NextDouble() * 2 // < -6
            };
        }
        else
        {
            // Pre-prime: use capped abs distance
            int tmpYears = absYears;
            if (tmpYears > 3)
            {
                tmpYears = 8 - tmpYears;
                if (tmpYears < 1) tmpYears = 1;
            }
            floatYears = tmpYears;
        }

        double factor = floatYears / adj;

        // Invert potential for post-prime (higher potential = less decline)
        if (yearsToPrime < 0) potential = 6 - potential;

        double p = (potential + 1) / 4.0;

        factor = random.NextDouble() * factor * 2 * p;

        // 1/12 chance of major random swing
        if (random.NextDouble() <= 1.0 / 12.0 * p)
        {
            factor = random.NextDouble() * 24.0 - 12.0;
            if (yearsToPrime < 0) factor /= 2.0;
            factor = (100.0 + factor) / 100.0;
        }
        else if (yearsToPrime > 0)
        {
            factor = (100.0 + factor) / 100.0;
        }
        else
        {
            factor = (100.0 - factor) / 100.0;
        }

        return factor;
    }

    // ── DevelopPlayers ──────────────────────────────────────────────

    /// <summary>
    /// Applies aging/improvement/decline to all players in the league.
    /// Port of CCareer::AdjustScoring() — Career.cpp:229-925.
    /// </summary>
    public static void DevelopPlayers(League league, Random? random = null)
    {
        random ??= Random.Shared;

        // Phase 1: Compute factor_adj (pre-prime vs post-prime minutes ratio)
        long minBeforePrime = 0, minAfterPrime = 0;

        foreach (var team in league.Teams)
        {
            foreach (var player in team.Roster)
            {
                if (string.IsNullOrEmpty(player.Name)) continue;
                var stats = player.SimulatedStats;
                if (stats.Games <= 0) continue;

                int prime = player.Ratings.Prime;
                // C++ does a random prime extension at this point
                int adjustedPrime = prime;
                double freak;
                do
                {
                    freak = random.NextDouble();
                    if (freak < 0.5) adjustedPrime++;
                } while (freak < 0.5);

                int age = player.Age;
                int min = stats.Minutes;
                if (adjustedPrime == age) { /* at prime - no contribution */ }
                else if (adjustedPrime < age) minBeforePrime += min;
                else minAfterPrime += min;
            }
        }

        if (minAfterPrime <= 0) minAfterPrime = 1;
        double factorAdj = (double)minBeforePrime / minAfterPrime;

        // Phase 2: Apply development to each player
        int highGames = 82;
        foreach (var team in league.Teams)
        {
            foreach (var player in team.Roster)
            {
                if (string.IsNullOrEmpty(player.Name)) continue;
                var stats = player.SimulatedStats;
                if (stats.Games <= 0 || stats.Minutes <= 0) continue;

                DevelopSinglePlayer(player, team, factorAdj, highGames, random);
            }
        }
    }

    private static void DevelopSinglePlayer(Player player, Models.Team.Team team,
        double factorAdj, int highGames, Random random)
    {
        var stats = player.SimulatedStats;
        int g = stats.Games;
        int min = stats.Minutes;
        int origMin = min;

        // Extract stats — C++ separates 2pt and 3pt
        int fgm = stats.FieldGoalsMade - stats.ThreePointersMade;
        int fga = stats.FieldGoalsAttempted - stats.ThreePointersAttempted;
        int tgm = stats.ThreePointersMade;
        int tga = stats.ThreePointersAttempted;
        int origTga = tga;
        int ftm = stats.FreeThrowsMade;
        int fta = stats.FreeThrowsAttempted;
        int orb = stats.OffensiveRebounds;
        int drb = stats.Rebounds - orb;
        int ast = stats.Assists;
        int stl = stats.Steals;
        int to = stats.Turnovers;
        int blk = stats.Blocks;
        int pf = stats.PersonalFouls;

        // Preserve percentages for floor checks
        double tmpPct = fga > 0 ? (double)fgm / fga * 1000 : 0;
        // Floor: fga >= oreb
        if (fga < orb)
        {
            fga = orb;
            fgm = (int)(tmpPct / 1000.0 * fga);
        }
        int fgp = fga > 0 ? (int)((double)fgm / fga * 1000) : 0;

        int tgp = tga > 0 ? (int)((double)tgm / tga * 1000) : 0;

        double pct = fta > 0 ? (double)ftm / fta * 1000 : 0;
        int tmpOrb = (int)(orb * 0.25);
        if (fta < tmpOrb)
        {
            fta = tmpOrb;
            ftm = (int)(pct / 1000.0 * fta);
        }
        int ftp = fta > 0 ? (int)((double)ftm / fta * 1000) : 0;

        double before = StatisticsCalculator.CalculateTrueRatingSimple(stats);

        int prime = player.Ratings.Prime;
        double pt1 = player.Ratings.Potential1;
        double pt2 = player.Ratings.Potential2;
        double eff = player.Ratings.Effort;
        int intangible = player.Ratings.Effort;

        // Coach effects on potential
        var coach = team.Coach;
        if (coach != null)
        {
            pt1 = pt1 - 1 + (coach.CoachPot1 - 1) / 2.0;
            pt2 = pt2 - 1 + (coach.CoachPot2 - 1) / 2.0;
            eff = eff - 1 + (coach.CoachEffort - 1) / 2.0;
            pt1 = Math.Clamp(pt1, 1, 5);
            pt2 = Math.Clamp(pt2, 1, 5);
            eff = Math.Clamp(eff, 1, 5);
        }

        int yrsToPrime = prime - player.Age;
        int coachScoring = coach?.CoachScoring ?? 3;
        int coachShooting = coach?.CoachShooting ?? 3;
        int coachRebounding = coach?.CoachRebounding ?? 3;
        int coachPassing = coach?.CoachPassing ?? 3;
        int coachDefense = coach?.CoachDefense ?? 3;

        // Position index for movement rating checks
        string pos = player.Position;
        int po = pos switch
        {
            "SG" => 2,
            "SF" => 3,
            "PF" => 4,
            " C" => 5,
            _ => 1 // PG
        };

        // ── Apply improvement factors to each stat category ──

        // FGA (scoring volume) — uses pt1, coachScoring
        double factor = CalculateImprovementFactor(yrsToPrime, 1, pt1, intangible, random);
        if (yrsToPrime > 0) factor = 1 + (factor - 1) * factorAdj;
        factor = ApplyCoachEffect(factor, coachScoring);
        fga = (int)(fga * factor);

        // FG% — uses pt1, coachShooting
        factor = CalculateImprovementFactor(yrsToPrime, 2, pt1, intangible, random);
        if (yrsToPrime > 0) factor = 1 + (factor - 1) * factorAdj;
        if (factor > 1.125) factor = 1.125;
        else if (factor < 0.875) factor = 0.875;
        factor = ApplyCoachEffect(factor, coachShooting);
        fgp = fgp + (int)((factor - 1) * (1000 - fgp));
        if (fgp > 600) fgp = 600 + random.Next(1, (int)(10 * pt1) + 1);
        else if (fgp < 275) fgp = 250 + random.Next(1, 51);
        fgm = (int)(fga * (fgp / 1000.0));

        // FTA (foul drawing) — uses pt1, coachScoring
        factor = CalculateImprovementFactor(yrsToPrime, 1, pt1, intangible, random);
        if (yrsToPrime > 0) factor = 1 + (factor - 1) * factorAdj;
        factor = ApplyCoachEffect(factor, coachScoring);
        fta = (int)(fta * factor);

        // FT% — uses pt1, coachShooting
        factor = CalculateImprovementFactor(yrsToPrime, 1, pt1, intangible, random);
        if (yrsToPrime > 0) factor = 1 + (factor - 1) * factorAdj;
        factor = ApplyCoachEffect(factor, coachShooting);
        ftp = ftp + (int)((factor - 1) * (1000 - ftp)) + (int)(random.NextDouble() * eff * 4 - 6);
        if (ftp >= 950) ftp = 940 + random.Next(1, 21);
        else if (ftp < 333) ftp = 300 + random.Next(1, 51);
        ftm = (int)(fta * (ftp / 1000.0));

        // 3PA — uses pt1, coachScoring, with reduced decline for older players
        double primeFactor = yrsToPrime < 0 ? 2 : 1;
        factor = CalculateImprovementFactor(yrsToPrime, primeFactor, pt1, intangible, random);
        if (yrsToPrime > 0) factor = 1 + (factor - 1) * factorAdj;
        factor = ApplyCoachEffect(factor, coachScoring);
        tga = (int)(tga * factor);
        if (tga > 0 && min > 0 && (min / tga) < 4)
        {
            double f2 = (double)min / 4 * (0.9 + random.NextDouble() * 0.2);
            tga = (int)f2;
        }

        // 3P% — uses pt1, coachShooting, with reduced decline for older/good shooters
        primeFactor = (yrsToPrime < 0 || tgp > 350) ? 2 : 1;
        factor = CalculateImprovementFactor(yrsToPrime, primeFactor, pt1, intangible, random);
        if (yrsToPrime > 0) factor = 1 + (factor - 1) * factorAdj;
        int oldTgp = tgp;
        factor = ApplyCoachEffect(factor, coachShooting);
        tgp = tgp + (int)(350 * factor - 350);
        tgp = tgp + (int)(random.NextDouble() * eff * 2 - 3);
        if (oldTgp > 0)
            tga = (int)((double)tgp / oldTgp * tga);

        if (tga <= 3)
        {
            int r = random.Next(1, 4);
            if (r == 1) { tga = random.Next(1, 7) + 3; tgm = random.Next(0, 2); }
            else if (r == 2) { tga = random.Next(1, 10) + 9; tgm = random.Next(0, 3); }
            else { tga = random.Next(1, 4); tgm = 0; }

            if (tga / 2.0 > origTga)
            {
                tga = origTga * 2;
                if (origTga == 0) tga = random.Next(1, 4);
            }
            tgp = tga > 0 ? (int)((double)tgm / tga * 1000) : 0;
        }

        if (tgp > 400) tgp = 400 + random.Next(1, (int)(10 * pt2) + 1);
        else if (tgp < 0) tgp = 0;

        double tgaPer48 = min > 0 ? (double)origTga / min * 48 : 0;
        if (tgp < tgaPer48 * 12)
        {
            tgp = (int)(tgaPer48 * 12);
            if (tgp > 240) tgp = 230 + random.Next(1, 21);
        }
        tgm = tga > 0 ? (int)(tga * (tgp / 1000.0)) : 0;
        fga += tga;
        fgm += tgm;

        // OREB — uses pt2, coachRebounding
        factor = CalculateImprovementFactor(yrsToPrime, 1, pt2, intangible, random);
        if (yrsToPrime > 0) factor = 1 + (factor - 1) * factorAdj;
        factor = ApplyCoachEffect(factor, coachRebounding);
        orb = (int)(orb * factor);

        // DREB — uses pt2, coachRebounding
        factor = CalculateImprovementFactor(yrsToPrime, 1, pt2, intangible, random);
        if (yrsToPrime > 0) factor = 1 + (factor - 1) * factorAdj;
        factor = ApplyCoachEffect(factor, coachRebounding);
        drb = (int)(drb * factor);

        // OREB floor by position
        double rebFactor = (drb + orb) > 0 ? (double)orb / (drb + orb) : 0;
        if (rebFactor < 1.0 / 12.0 && pos == "PG") orb = (int)((drb + orb) * 1.0 / 12.0);
        else if (rebFactor < 1.0 / 8.0 && (pos == "SG" || pos == "SF")) orb = (int)((drb + orb) * 1.0 / 8.0);
        else if (rebFactor < 1.0 / 6.0) orb = (int)((drb + orb) * 1.0 / 6.0);

        // AST — uses pt2, coachPassing
        double oldAst = ast;
        double toAst = oldAst > 0 ? (double)to / oldAst : 0;
        factor = CalculateImprovementFactor(yrsToPrime, 1, pt2, intangible, random);
        if (yrsToPrime > 0) factor = 1 + (factor - 1) * factorAdj;
        factor = ApplyCoachEffect(factor, coachPassing);
        ast = (int)(ast * factor);

        // STL — uses pt2, coachDefense, with floor/ceiling per-48
        factor = CalculateImprovementFactor(yrsToPrime, 1, pt2, intangible, random);
        if (yrsToPrime > 0) factor = 1 + (factor - 1) * factorAdj;
        factor = 1 + (factor - 1) * 1.2;
        factor = ApplyCoachEffect(factor, coachDefense);
        double stl48 = min > 0 ? (double)stl / min * 48 : 0;
        if (stl48 < 0.5)
        {
            stl = (int)(0.5 * min / 48.0);
            factor = 0.9 + random.NextDouble() * 0.2;
        }
        else if (stl48 > 4)
        {
            stl = (int)(4.0 * min / 48.0);
            factor = 0.9 + random.NextDouble() * 0.2;
        }
        stl = (int)(stl * factor);

        // TO — uses pt2, coachPassing (inverted: more = worse, factor applied as 2-factor)
        factor = CalculateImprovementFactor(yrsToPrime, 1, pt2, intangible, random);
        if (yrsToPrime > 0) factor = 1 + (factor - 1) * factorAdj;
        double to48 = min > 0 ? (double)to / min * 48 : 0;
        if (to48 < 1.0)
        {
            to = (int)(1.0 * min / 48.0);
            factor = 0.9 + random.NextDouble() * 0.2;
        }
        else if (to48 > 8.0)
        {
            to = (int)(8.0 * min / 48.0);
            factor = 0.9 + random.NextDouble() * 0.2;
        }
        double add = (ast - oldAst) * toAst;
        if (add < 0) add = 0;
        if (yrsToPrime > 0) factor = 1 + (factor - 1) * factorAdj;
        factor = 1 + (factor - 1) * 50.0 / 100.0;
        factor = ApplyCoachEffect(factor, coachPassing);
        to = (int)(to * (2 - factor) + add);

        // Adjust low assist-to-TO ratio
        if (to > 0)
        {
            double adjLoAst = (double)ast / to;
            if (adjLoAst < 0.1)
            {
                ast = (int)((double)to / 10);
            }
        }

        // BLK — uses pt2, coachDefense
        factor = CalculateImprovementFactor(yrsToPrime, 1, pt2, intangible, random);
        if (yrsToPrime > 0) factor = 1 + (factor - 1) * factorAdj;
        factor = ApplyCoachEffect(factor, coachDefense);
        blk = (int)(blk * factor);

        // PF — uses pt2, coachDefense (inverted like TO)
        factor = CalculateImprovementFactor(yrsToPrime, 4, pt2, intangible, random);
        if (yrsToPrime > 0) factor = 1 + (factor - 1) * factorAdj;
        factor = ApplyCoachEffect(factor, coachDefense);
        pf = (int)(pf * (2 - factor));

        // ── Movement ratings (discrete ±1 changes) ──

        // MovementOffense — pt1
        factor = CalculateImprovementFactor(yrsToPrime, 1, pt1, intangible, random);
        int movDelta = CalculateMovementDelta(factor, 3, random);
        int o = player.Ratings.MovementOffenseRaw + movDelta;
        o = Math.Clamp(o, 1, 9);

        // PenetrationOffense — pt1, biased by position
        factor = CalculateImprovementFactor(yrsToPrime, 1, pt1, intangible, random);
        movDelta = CalculateMovementDelta(factor, 6 - po, random);
        int p = player.Ratings.PenetrationOffenseRaw + movDelta;
        p = Math.Clamp(p, 1, 9);

        // PostOffense — pt1, biased by position (inverse)
        factor = CalculateImprovementFactor(yrsToPrime, 1, pt1, intangible, random);
        movDelta = CalculateMovementDelta(factor, po, random);
        int io = player.Ratings.PostOffenseRaw + movDelta;
        io = Math.Clamp(io, 1, 9);

        // Weight changes based on post offense
        if (movDelta > 0) player.Weight++;
        else player.Weight--;

        // TransitionOffense — pt1
        factor = CalculateImprovementFactor(yrsToPrime, 1, pt1, intangible, random);
        movDelta = CalculateMovementDelta(factor, 6 - po, random);
        int fo = player.Ratings.TransitionOffenseRaw + movDelta;
        fo = Math.Clamp(fo, 1, 9);

        if (movDelta > 0) player.Weight--;
        else player.Weight++;

        // MovementDefense — eff
        factor = CalculateImprovementFactor(yrsToPrime, 1, eff, intangible, random);
        movDelta = CalculateMovementDelta(factor, 3, random);
        int od = player.Ratings.MovementDefenseRaw + movDelta;
        od = Math.Clamp(od, 1, 9);

        // PenetrationDefense — eff
        factor = CalculateImprovementFactor(yrsToPrime, 1, eff, intangible, random);
        movDelta = CalculateMovementDelta(factor, 6 - po, random);
        int pd = player.Ratings.PenetrationDefenseRaw + movDelta;
        pd = Math.Clamp(pd, 1, 9);

        // PostDefense — eff
        factor = CalculateImprovementFactor(yrsToPrime, 1, eff, intangible, random);
        movDelta = CalculateMovementDelta(factor, po, random);
        int id = player.Ratings.PostDefenseRaw + movDelta;
        id = Math.Clamp(id, 1, 9);

        if (movDelta > 0) player.Weight++;
        else player.Weight--;

        // TransitionDefense — eff
        factor = CalculateImprovementFactor(yrsToPrime, 1, eff, intangible, random);
        movDelta = CalculateMovementDelta(factor, 6 - po, random);
        int fd = player.Ratings.TransitionDefenseRaw + movDelta;
        fd = Math.Clamp(fd, 1, 9);

        if (movDelta > 0) player.Weight--;
        else player.Weight++;

        // Apply movement ratings
        player.Ratings.MovementOffenseRaw = o;
        player.Ratings.PenetrationOffenseRaw = p;
        player.Ratings.PostOffenseRaw = io;
        player.Ratings.TransitionOffenseRaw = fo;
        player.Ratings.MovementDefenseRaw = od;
        player.Ratings.PenetrationDefenseRaw = pd;
        player.Ratings.PostDefenseRaw = id;
        player.Ratings.TransitionDefenseRaw = fd;

        // Random weight fluctuation and young player height
        player.Weight = player.Weight - 2 + random.Next(1, 6);
        if (player.Age <= 21 && random.Next(1, 13) == 1)
            player.Height++;

        // Write back intermediate stats for true rating recalculation
        stats.Games = g;
        stats.Minutes = min;
        stats.FieldGoalsMade = fgm;
        stats.FieldGoalsAttempted = fga;
        stats.FreeThrowsMade = ftm;
        stats.FreeThrowsAttempted = fta;
        stats.ThreePointersMade = tgm;
        stats.ThreePointersAttempted = tga;
        stats.OffensiveRebounds = orb;
        stats.Rebounds = orb + drb;
        stats.Assists = ast;
        stats.Steals = stl;
        stats.Turnovers = to;
        stats.Blocks = blk;
        stats.PersonalFouls = pf;

        // Recalculate true rating with developed stats
        double after = StatisticsCalculator.CalculateTrueRatingSimple(stats);

        // ── Minutes/games projection ──
        double tru48 = min > 0 ? after / min * 48 : 0;
        double minAdj = before != 0 ? after / before : 1;
        if (after < 0 || before < 0)
        {
            minAdj = after > before ? 1 + random.NextDouble() * 0.1 : 1 - random.NextDouble() * 0.08;
        }
        double gAdj = minAdj;
        double prevMpg = (double)min / g;

        double mpg = tru48 * 3 - 8;
        mpg = Math.Clamp(mpg, 6, 40);

        // Past prime: don't increase minutes
        if (yrsToPrime < 0 && mpg > prevMpg)
        {
            mpg = Math.Max(prevMpg, 6);
        }
        mpg = mpg - 1 + random.NextDouble() * 2;

        // Games projection
        double temp = highGames > 0 ? (double)g / highGames * 82 : 82;
        if (temp <= 70)
        {
            temp = 50 + mpg;
            if (temp >= 70) temp = 70;
        }

        g = (int)(temp * gAdj * (0.95 + random.NextDouble() * 0.10));
        if (g > 82) g = 82;
        if (g <= 0) g = 1;
        min = (int)((double)g * mpg);
        if (min <= 0) min = 1;

        // Scale all stats proportionally to new minutes
        if (origMin > 0)
        {
            double ratio = (double)min / origMin;
            fgm = (int)(fgm * ratio);
            fga = (int)(fga * ratio);
            tgm = (int)(tgm * ratio);
            tga = (int)(tga * ratio);
            ftm = (int)(ftm * ratio);
            fta = (int)(fta * ratio);
            orb = (int)(orb * ratio);
            drb = (int)(drb * ratio);
            ast = (int)(ast * ratio);
            stl = (int)(stl * ratio);
            to = (int)(to * ratio);
            blk = (int)(blk * ratio);
            pf = (int)(pf * ratio);
        }

        // Ensure non-negative
        if (fgm < 0) fgm = 0;
        if (fga < 0) fga = 0;
        if (tgm < 0) tgm = 0;
        if (tga < 0) tga = 0;
        if (ftm < 0) ftm = 0;
        if (fta < 0) fta = 0;
        if (orb < 0) orb = 0;
        if (drb < 0) drb = 0;
        if (ast < 0) ast = 0;
        if (stl < 0) stl = 0;
        if (to < 0) to = 0;
        if (blk < 0) blk = 0;
        if (pf < 0) pf = 0;

        // TO per-48 floor
        if (min > 0)
        {
            double toPer48 = (double)to / min * 48;
            if (toPer48 < 0.9)
            {
                double toPer48Adj = (random.NextDouble() + 0.5) / 48.0 * min;
                to = (int)toPer48Adj + 1;
            }
        }

        // Final write-back
        stats.Games = g;
        stats.Minutes = min;
        stats.FieldGoalsMade = fgm;
        stats.FieldGoalsAttempted = fga;
        stats.FreeThrowsMade = ftm;
        stats.FreeThrowsAttempted = fta;
        stats.ThreePointersMade = tgm;
        stats.ThreePointersAttempted = tga;
        stats.OffensiveRebounds = orb;
        stats.Rebounds = orb + drb;
        stats.Assists = ast;
        stats.Steals = stl;
        stats.Turnovers = to;
        stats.Blocks = blk;
        stats.PersonalFouls = pf;
    }

    /// <summary>
    /// Applies coach effect to an improvement factor.
    /// Good coaches amplify improvement, bad coaches dampen it.
    /// </summary>
    private static double ApplyCoachEffect(double factor, int coachRating)
    {
        double coachEffect;
        if (factor >= 1)
            coachEffect = (factor - 1) * (1 + (coachRating - 3) / 10.0);
        else
            coachEffect = (factor - 1) * (1 + (3 - coachRating) / 10.0);
        return 1 + coachEffect;
    }

    /// <summary>
    /// Calculates discrete movement rating delta (typically 0, ±1, rarely ±2).
    /// Port of the movement rating logic in Career.cpp.
    /// </summary>
    private static int CalculateMovementDelta(double factor, int threshold, Random random)
    {
        int delta;
        if (random.Next(1, 19) <= threshold && factor > 1)
            delta = 1;
        else if (random.Next(1, 19) <= threshold && factor <= 1)
            delta = -1;
        else
            return 0;

        // Rare chance of double change
        if (random.Next(1, 19) <= 1)
            delta = -delta * 2;

        return delta;
    }
}
