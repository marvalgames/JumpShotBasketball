namespace JumpShotBasketball.Core.Models.History;

/// <summary>
/// One team's record for one season.
/// </summary>
public class FranchiseSeasonRecord
{
    public int Year { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public bool MadePlayoffs { get; set; }
    public bool WonChampionship { get; set; }
    public string CoachName { get; set; } = string.Empty;
    public string MvpName { get; set; } = string.Empty;
    public double MvpValue { get; set; }
}
