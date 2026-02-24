using JumpShotBasketball.Core.Models.Game;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Team;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Manages game plan application, capture, validation, and defaults.
/// </summary>
public static class GamePlanService
{
    /// <summary>
    /// Applies a GamePlan to a team, setting each player's game plan fields.
    /// </summary>
    public static void ApplyGamePlan(Team team, GamePlan plan)
    {
        if (team == null || plan == null) return;

        foreach (var player in team.Roster)
        {
            if (plan.PlayerPlans.TryGetValue(player.Id, out var entry))
            {
                player.OffensiveFocus = entry.OffensiveFocus;
                player.DefensiveFocus = entry.DefensiveFocus;
                player.OffensiveIntensity = entry.OffensiveIntensity;
                player.DefensiveIntensity = entry.DefensiveIntensity;
                player.PlayMaker = entry.PlayMaker;
                player.GameMinutes = entry.GameMinutes;
            }
            else
            {
                // Apply defaults for players not in the plan
                player.OffensiveFocus = plan.DefaultOffensiveFocus;
                player.DefensiveFocus = plan.DefaultDefensiveFocus;
                player.OffensiveIntensity = plan.DefaultOffensiveIntensity;
                player.DefensiveIntensity = plan.DefaultDefensiveIntensity;
                player.PlayMaker = plan.DefaultPlayMaker;
            }
        }
    }

    /// <summary>
    /// Captures the current player settings into a GamePlan.
    /// </summary>
    public static GamePlan CaptureCurrentPlan(Team team)
    {
        var plan = new GamePlan();

        foreach (var player in team.Roster)
        {
            plan.PlayerPlans[player.Id] = new PlayerGamePlanEntry
            {
                PlayerId = player.Id,
                OffensiveFocus = player.OffensiveFocus,
                DefensiveFocus = player.DefensiveFocus,
                OffensiveIntensity = player.OffensiveIntensity,
                DefensiveIntensity = player.DefensiveIntensity,
                PlayMaker = player.PlayMaker,
                GameMinutes = player.GameMinutes
            };
        }

        return plan;
    }

    /// <summary>
    /// Validates a game plan and returns a list of validation errors.
    /// Empty list means the plan is valid.
    /// </summary>
    public static List<string> ValidatePlan(GamePlan plan)
    {
        var errors = new List<string>();

        if (plan == null)
        {
            errors.Add("Plan cannot be null.");
            return errors;
        }

        ValidateRange(errors, "DefaultOffensiveFocus", plan.DefaultOffensiveFocus, 0, 3);
        ValidateRange(errors, "DefaultDefensiveFocus", plan.DefaultDefensiveFocus, 0, 3);
        ValidateRange(errors, "DefaultOffensiveIntensity", plan.DefaultOffensiveIntensity, -2, 2);
        ValidateRange(errors, "DefaultDefensiveIntensity", plan.DefaultDefensiveIntensity, -2, 2);
        ValidateRange(errors, "DefaultPlayMaker", plan.DefaultPlayMaker, -2, 2);

        foreach (var kvp in plan.PlayerPlans)
        {
            var entry = kvp.Value;
            string prefix = $"Player {entry.PlayerId}";
            ValidateRange(errors, $"{prefix} OffensiveFocus", entry.OffensiveFocus, 0, 3);
            ValidateRange(errors, $"{prefix} DefensiveFocus", entry.DefensiveFocus, 0, 3);
            ValidateRange(errors, $"{prefix} OffensiveIntensity", entry.OffensiveIntensity, -2, 2);
            ValidateRange(errors, $"{prefix} DefensiveIntensity", entry.DefensiveIntensity, -2, 2);
            ValidateRange(errors, $"{prefix} PlayMaker", entry.PlayMaker, -2, 2);
            ValidateRange(errors, $"{prefix} GameMinutes", entry.GameMinutes, 0, 48);
        }

        return errors;
    }

    /// <summary>
    /// Creates a default game plan with all values at 0 (neutral).
    /// </summary>
    public static GamePlan CreateDefaultPlan()
    {
        return new GamePlan();
    }

    /// <summary>
    /// Resets all entries in a game plan to defaults (0).
    /// </summary>
    public static void ResetPlan(GamePlan plan)
    {
        plan.DefaultOffensiveFocus = 0;
        plan.DefaultDefensiveFocus = 0;
        plan.DefaultOffensiveIntensity = 0;
        plan.DefaultDefensiveIntensity = 0;
        plan.DefaultPlayMaker = 0;
        plan.DesignatedBallHandler = 0;
        plan.PlayerPlans.Clear();
    }

    private static void ValidateRange(List<string> errors, string field, int value, int min, int max)
    {
        if (value < min || value > max)
            errors.Add($"{field} must be between {min} and {max}, got {value}.");
    }
}
