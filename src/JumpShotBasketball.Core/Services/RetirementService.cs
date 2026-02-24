using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Retirement evaluation for players at end of season.
/// Port of CRetirement::SetRetirements() — Retirement.cpp:18-56.
/// </summary>
public static class RetirementService
{
    /// <summary>
    /// Evaluates all players in the league for retirement.
    /// Returns list of retired player names.
    /// </summary>
    public static List<string> ProcessRetirements(League league, Random? random = null)
    {
        random ??= Random.Shared;
        var retiredNames = new List<string>();

        foreach (var team in league.Teams)
        {
            foreach (var player in team.Roster)
            {
                if (string.IsNullOrEmpty(player.Name) || string.IsNullOrEmpty(player.Position))
                    continue;

                if (ShouldRetire(player, random))
                {
                    player.Retired = true;
                    retiredNames.Add(player.Name);
                }
                else
                {
                    player.Retired = false;
                }
            }
        }

        return retiredNames;
    }

    /// <summary>
    /// Determines whether a player should retire.
    /// Port of the retirement logic in Retirement.cpp:28-48.
    /// Base probability 0.0005; increases past prime+5; multiplied for age>40.
    /// Performance cutoff: prFga &lt; 3 AND prFta &lt; 3 AND prFgp &lt; 25 → forced retire.
    /// </summary>
    public static bool ShouldRetire(Player player, Random? random = null)
    {
        random ??= Random.Shared;

        double f = 0.0005;
        int age = player.Age;
        int prime = player.Ratings.Prime;

        if (prime + 5 < age)
        {
            f = (age - prime + player.Ratings.TradeTrueRating) / 250.0;
        }

        if (age > 40)
        {
            f *= (age - 39);
        }

        // Performance cutoff: if projection ratings are too low, forced retirement
        bool performanceOk = player.Ratings.ProjectionFieldGoalsAttempted >= 3
                          && player.Ratings.ProjectionFreeThrowsAttempted >= 3
                          && player.Ratings.ProjectionFieldGoalPercentage >= 25;

        if (!performanceOk)
            return true;

        return random.NextDouble() < f;
    }
}
