namespace JumpShotBasketball.Core.Models.League;

/// <summary>
/// Result of a single season cycle: full season + off-season transition.
/// </summary>
public class SeasonCycleResult
{
    public SeasonResult SeasonResult { get; set; } = new();
    public OffSeasonResult? OffSeasonResult { get; set; }
}

/// <summary>
/// Result of simulating multiple consecutive seasons.
/// </summary>
public class MultiSeasonResult
{
    public List<SeasonCycleResult> Seasons { get; set; } = new();
    public int TotalSeasonsSimulated { get; set; }
    public int TotalGamesSimulated { get; set; }
}
