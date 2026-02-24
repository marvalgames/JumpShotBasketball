namespace JumpShotBasketball.Core.Models.League;

/// <summary>
/// A proposed trade between two teams including players and draft picks.
/// Port of the trade proposal data from Computer.cpp Trade().
/// </summary>
public class TradeProposal
{
    public int Team1Index { get; set; }
    public int Team2Index { get; set; }

    // Roster indices of players going TO the other team
    public List<int> Team1PlayerIndices { get; set; } = new(); // team1 players going to team2
    public List<int> Team2PlayerIndices { get; set; } = new(); // team2 players going to team1

    // YRPP-encoded draft picks (0 = none)
    public int Team1DraftPick { get; set; }
    public int Team2DraftPick { get; set; }

    // Computed evaluation fields
    public double Team1OldTrue { get; set; }
    public double Team2OldTrue { get; set; }
    public double Team1NewTrue { get; set; }
    public double Team2NewTrue { get; set; }
    public int Team1Salary { get; set; }
    public int Team2Salary { get; set; }
    public double Team1PickValue { get; set; }
    public double Team2PickValue { get; set; }
    public double Team1ValueFactor { get; set; }
    public double Team2ValueFactor { get; set; }
    public bool SalaryFit { get; set; }
}
