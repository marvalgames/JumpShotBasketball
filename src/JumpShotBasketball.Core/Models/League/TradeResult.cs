namespace JumpShotBasketball.Core.Models.League;

/// <summary>
/// Result of a trading period or trading block session.
/// </summary>
public class TradeResult
{
    public int TradesMade { get; set; }
    public int ProposalsGenerated { get; set; }
    public int ProposalsRejected { get; set; }
    public List<TradeProposal> AcceptedTrades { get; set; } = new();
}
