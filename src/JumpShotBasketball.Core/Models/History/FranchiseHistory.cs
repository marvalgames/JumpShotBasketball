namespace JumpShotBasketball.Core.Models.History;

/// <summary>
/// A team's complete multi-season history.
/// </summary>
public class FranchiseHistory
{
    public int TeamIndex { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public List<FranchiseSeasonRecord> Seasons { get; set; } = new();
    public int TotalChampionships => Seasons.Count(s => s.WonChampionship);
    public int TotalPlayoffAppearances => Seasons.Count(s => s.MadePlayoffs);
    public int BestWins => Seasons.Count > 0 ? Seasons.Max(s => s.Wins) : 0;
}
