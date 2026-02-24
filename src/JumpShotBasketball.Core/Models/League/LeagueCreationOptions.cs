namespace JumpShotBasketball.Core.Models.League;

/// <summary>
/// Configuration options for creating a new league from scratch.
/// </summary>
public class LeagueCreationOptions
{
    public int NumberOfTeams { get; set; } = 30;
    public int PlayersPerTeam { get; set; } = 15;
    public int GamesPerSeason { get; set; } = 82;
    public int StartingYear { get; set; } = 2025;
    public bool FinancialEnabled { get; set; } = true;
    public bool FreeAgencyEnabled { get; set; } = true;
    public bool ComputerTradesEnabled { get; set; } = true;
    public string LeagueName { get; set; } = "JumpShot Basketball League";
}
