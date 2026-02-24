namespace JumpShotBasketball.Core.Models.History;

/// <summary>
/// A single entry in a record book list (one player's performance for one stat).
/// </summary>
public class RecordBookEntry
{
    public string PlayerName { get; set; } = string.Empty;
    public int PlayerId { get; set; }
    public int TeamIndex { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public int Year { get; set; }
    public double Value { get; set; }
}
