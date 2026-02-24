namespace JumpShotBasketball.Core.Models.League;

/// <summary>
/// Summary of draft execution outcomes.
/// </summary>
public class DraftResult
{
    public List<DraftSelection> Selections { get; set; } = new();
    public int TotalPicks { get; set; }
    public int UndraftedCount { get; set; }
    public List<(int Pick, int TeamIndex, string TeamName)> LotteryWinners { get; set; } = new();
}

/// <summary>
/// A single draft pick record.
/// </summary>
public class DraftSelection
{
    public int Round { get; set; }
    public int Pick { get; set; }
    public int TeamIndex { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
}
