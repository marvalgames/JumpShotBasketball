namespace JumpShotBasketball.Core.Models.League;

/// <summary>
/// Result of in-season roster management (emergency releases and signings).
/// </summary>
public class RosterManagementResult
{
    public List<(int TeamIndex, string PlayerName)> PlayersReleased { get; set; } = new();
    public List<(int TeamIndex, string PlayerName)> PlayersSigned { get; set; } = new();
}
