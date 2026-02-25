using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Injury healing, application, and description generation.
/// Port of CBBallDoc::HealInjury() BBallDoc.cpp L1402-1418.
/// </summary>
public static class InjuryService
{
    private static readonly string[] MinorInjuries =
    {
        "Sprained ankle", "Bruised knee", "Jammed finger",
        "Minor hip strain", "Sore back"
    };

    private static readonly string[] ModerateInjuries =
    {
        "Strained hamstring", "Groin injury", "Sprained wrist",
        "Calf strain", "Shoulder soreness"
    };

    private static readonly string[] SeriousInjuries =
    {
        "MCL sprain", "High ankle sprain", "Fractured finger",
        "Bruised ribs", "Quad strain"
    };

    private static readonly string[] SevereInjuries =
    {
        "Torn meniscus", "Stress fracture", "Dislocated shoulder",
        "Broken hand", "Torn labrum"
    };

    private static readonly string[] CriticalInjuries =
    {
        "Torn ACL", "Torn Achilles", "Broken leg",
        "Fractured vertebra", "Torn patellar tendon"
    };

    /// <summary>
    /// Heals all injured players by the specified number of days.
    /// When Injury reaches 0: Health restored to 90-100, InjuryDescription cleared.
    /// Port of CBBallDoc::HealInjury().
    /// </summary>
    public static void HealInjuries(League league, int days = 1, Random? random = null)
    {
        random ??= Random.Shared;

        foreach (var team in league.Teams)
        {
            foreach (var player in team.Roster)
            {
                if (player.Injury <= 0) continue;

                int remaining = player.Injury - days;
                if (remaining <= 0)
                {
                    remaining = 0;
                    // When just healed, small chance of returning at partial health
                    player.Health = 90 + random.Next(11); // 90-100
                    if (player.Health >= 100)
                        player.InjuryDescription = string.Empty;
                }

                player.Injury = remaining;

                if (player.Injury == 0 && player.Health >= 100)
                    player.InjuryDescription = string.Empty;
            }
        }
    }

    /// <summary>
    /// Applies a game injury to a player.
    /// Sets injury duration, reduces health, generates description.
    /// </summary>
    public static void ApplyInjury(Player player, int gamesOut, Random? random = null)
    {
        random ??= Random.Shared;

        if (gamesOut <= 0) return;

        // Convert games out to days: ~2.25 days per game with random variance
        int days = Math.Max(1, (int)(gamesOut * 2.25 + random.Next(-1, 2)));
        player.Injury = days;
        player.Health = Math.Max(50, 100 - gamesOut * 3);
        player.InjuryDescription = GenerateInjuryDescription(gamesOut, random);
    }

    /// <summary>
    /// Generates injury description text based on severity.
    /// Severity brackets: 1-3 minor, 4-10 moderate, 11-30 serious, 31-80 severe, 81+ critical.
    /// </summary>
    public static string GenerateInjuryDescription(int gamesOut, Random? random = null)
    {
        random ??= Random.Shared;

        string[] pool;
        if (gamesOut <= 3)
            pool = MinorInjuries;
        else if (gamesOut <= 10)
            pool = ModerateInjuries;
        else if (gamesOut <= 30)
            pool = SeriousInjuries;
        else if (gamesOut <= 80)
            pool = SevereInjuries;
        else
            pool = CriticalInjuries;

        return pool[random.Next(pool.Length)];
    }

    /// <summary>
    /// Checks for permanent injury effects after a severe injury.
    /// Port of CPlayer::Disabled() — Player.cpp:2092-2145.
    /// If gamesOut > 5, there is a (gamesOut/100)^2 probability of permanent stat reduction.
    /// Made stats (FGM, FTM, 3PM) get an additional ×0.98 factor.
    /// Each ODPT raw rating has an independent chance to decrease by 1.
    /// </summary>
    public static bool ApplyPermanentInjuryEffects(Player player, int gamesOut, Random? random = null)
    {
        random ??= Random.Shared;

        if (gamesOut <= 5) return false;

        int ga = Math.Min(gamesOut, 99);
        double g = ga / 100.0;
        double f = g * g;

        if (random.NextDouble() > f) return false;

        // Permanent injury triggered
        double eff = g * 0.2;
        double tMin = 1.0 - random.NextDouble() * eff;

        var s = player.SimulatedStats;
        // Reduce all stats by tMin; made stats also ×0.98
        s.FieldGoalsMade = (int)(s.FieldGoalsMade * tMin * 0.98);
        s.FieldGoalsAttempted = (int)(s.FieldGoalsAttempted * tMin);
        s.FreeThrowsMade = (int)(s.FreeThrowsMade * tMin * 0.98);
        s.FreeThrowsAttempted = (int)(s.FreeThrowsAttempted * tMin);
        s.ThreePointersMade = (int)(s.ThreePointersMade * tMin * 0.98);
        s.ThreePointersAttempted = (int)(s.ThreePointersAttempted * tMin);
        s.OffensiveRebounds = (int)(s.OffensiveRebounds * tMin);
        s.Rebounds = (int)(s.Rebounds * tMin);
        s.Assists = (int)(s.Assists * tMin);
        s.Steals = (int)(s.Steals * tMin);
        s.Turnovers = (int)(s.Turnovers * tMin);
        s.Blocks = (int)(s.Blocks * tMin);
        s.PersonalFouls = (int)(s.PersonalFouls * tMin);
        // Games and Minutes are NOT reduced (player still played those games)

        // Each ODPT raw rating has independent chance to decrease by 1
        var r = player.Ratings;
        if (random.NextDouble() > tMin && r.MovementOffenseRaw > 1) r.MovementOffenseRaw--;
        if (random.NextDouble() > tMin && r.PenetrationOffenseRaw > 1) r.PenetrationOffenseRaw--;
        if (random.NextDouble() > tMin && r.PostOffenseRaw > 1) r.PostOffenseRaw--;
        if (random.NextDouble() > tMin && r.TransitionOffenseRaw > 1) r.TransitionOffenseRaw--;
        if (random.NextDouble() > tMin && r.MovementDefenseRaw > 1) r.MovementDefenseRaw--;
        if (random.NextDouble() > tMin && r.PenetrationDefenseRaw > 1) r.PenetrationDefenseRaw--;
        if (random.NextDouble() > tMin && r.PostDefenseRaw > 1) r.PostDefenseRaw--;
        if (random.NextDouble() > tMin && r.TransitionDefenseRaw > 1) r.TransitionDefenseRaw--;

        return true;
    }

    /// <summary>
    /// Determines if a player sustains an off-season injury and applies it.
    /// Port of CPlayer::OffSeasonInjury() — Player.cpp:2148-2183.
    /// Uses injuryRating-based probability; severity uses same escalating bucket formula
    /// as in-game injuries.
    /// </summary>
    /// <returns>Games out (0 if no injury).</returns>
    internal static int CalculateOffSeasonInjury(Player player, int stage, Random random)
    {
        if (string.IsNullOrEmpty(player.Name)) return 0;
        if (player.Injury > 0) return 0;

        int threshold = stage > 0 ? 5833 : 11667;
        int ch = random.Next(threshold);
        if (ch > player.Ratings.InjuryRating) return 0;

        // Same severity formula as GameSimulationEngine.ProcessInjury
        double adjInjury = Math.Sqrt(player.Ratings.InjuryRating);

        double factor1 = adjInjury * 2.0 / 9.0;
        double factor2 = factor1 + adjInjury * 2.0 / 9.0 * 2.0 / 3.0;
        double factor3 = factor2 + adjInjury * 2.0 / 9.0 * 2.0 / 3.0 * 2.0 / 6.0;
        double factor4 = factor3 + adjInjury * 2.0 / 9.0 * 2.0 / 3.0 * 2.0 / 9.0 * 2.0 / 6.0;

        double i = random.NextDouble() * adjInjury;
        if (i <= factor1) i *= 3;
        else if (i <= factor2) i *= 9;
        else if (i <= factor3) i *= 27;
        else if (i <= factor4) i *= 81;

        int gamesOut = Math.Clamp((int)i + 1, 1, 160);

        // Convert to days: C++ m_injury = int(g * 2.25 - Random(1))
        int days = Math.Max(1, (int)(gamesOut * 2.25 - random.NextDouble()));
        player.Injury = days;
        player.Health = Math.Max(50, 100 - gamesOut * 3);
        player.InjuryDescription = GenerateInjuryDescription(gamesOut, random);

        return gamesOut;
    }

    /// <summary>
    /// Processes off-season injuries for all players in the league.
    /// Port of CBBallDoc::OffSeasonInjuries() — BBallDoc.cpp:1285-1338.
    /// Iterates all roster players, generates injuries, and applies permanent damage.
    /// </summary>
    /// <returns>Number of players injured.</returns>
    public static int ProcessOffSeasonInjuries(League league, Random? random = null)
    {
        random ??= Random.Shared;
        int count = 0;

        foreach (var team in league.Teams)
        {
            foreach (var player in team.Roster)
            {
                if (string.IsNullOrEmpty(player.Name)) continue;
                if (player.Injury > 0) continue;

                int gamesOut = CalculateOffSeasonInjury(player, league.Settings.CurrentStage, random);
                if (gamesOut > 0)
                {
                    // Check for permanent damage
                    ApplyPermanentInjuryEffects(player, gamesOut, random);
                    count++;
                }
            }
        }

        return count;
    }
}
