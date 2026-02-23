namespace JumpShotBasketball.Core.Models.League;

/// <summary>
/// A trade, signing, or other transaction log entry.
/// Maps to C++ CTransaction concept.
/// </summary>
public class Transaction
{
    public int Id { get; set; }
    public TransactionType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public int TeamIndex1 { get; set; }
    public int TeamIndex2 { get; set; }
    public string Team1Name { get; set; } = string.Empty;
    public string Team2Name { get; set; } = string.Empty;
    public List<int> PlayersInvolved { get; set; } = new();
    public List<int> DraftPicksInvolved { get; set; } = new();
}

public enum TransactionType
{
    Trade,
    Signing,
    Waiver,
    DraftPick,
    Extension
}
