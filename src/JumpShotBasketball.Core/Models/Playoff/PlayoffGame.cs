namespace JumpShotBasketball.Core.Models.Playoff;

/// <summary>
/// A single game within a playoff series.
/// </summary>
public class PlayoffGame
{
    public int GameNumberInSeries { get; set; }
    public int HomeTeamIndex { get; set; }
    public int VisitorTeamIndex { get; set; }
    public int HomeScore { get; set; }
    public int VisitorScore { get; set; }
    public bool Played { get; set; }
}
