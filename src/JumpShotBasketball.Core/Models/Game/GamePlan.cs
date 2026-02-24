namespace JumpShotBasketball.Core.Models.Game;

/// <summary>
/// Per-team coaching instructions that persist across games.
/// Used by GamePlanService to apply/capture player settings.
/// </summary>
public class GamePlan
{
    public Dictionary<int, PlayerGamePlanEntry> PlayerPlans { get; set; } = new();
    public int DefaultOffensiveFocus { get; set; }
    public int DefaultDefensiveFocus { get; set; }
    public int DefaultOffensiveIntensity { get; set; }
    public int DefaultDefensiveIntensity { get; set; }
    public int DefaultPlayMaker { get; set; }

    /// <summary>Player ID designated to receive the ball (0 = none). Called Shot system.</summary>
    public int DesignatedBallHandler { get; set; }
}

/// <summary>
/// Per-player game plan settings within a GamePlan.
/// </summary>
public class PlayerGamePlanEntry
{
    public int PlayerId { get; set; }
    public int OffensiveFocus { get; set; }
    public int DefensiveFocus { get; set; }
    public int OffensiveIntensity { get; set; }
    public int DefensiveIntensity { get; set; }
    public int PlayMaker { get; set; }
    public int GameMinutes { get; set; }
}
