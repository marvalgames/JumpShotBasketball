namespace JumpShotBasketball.Core.Models.Awards;

public class StatLeaderEntry
{
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public int TeamIndex { get; set; }
    public string Position { get; set; } = string.Empty;
    public double PerGameAverage { get; set; }
    public int GamesPlayed { get; set; }
}
