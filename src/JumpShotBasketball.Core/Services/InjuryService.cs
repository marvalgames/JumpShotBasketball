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
}
