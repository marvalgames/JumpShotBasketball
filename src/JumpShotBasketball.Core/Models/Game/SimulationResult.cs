namespace JumpShotBasketball.Core.Models.Game;

/// <summary>
/// Result of a batch simulation (day/week/month/season).
/// Contains all individual game results and summary data.
/// </summary>
public class SimulationResult
{
    public List<GameResult> GameResults { get; set; } = new();
    public int DaysSimulated { get; set; }
    public int GamesSimulated { get; set; }
    public bool SeasonComplete { get; set; }
    public List<int> InvalidRosterTeamIndices { get; set; } = new();
}
