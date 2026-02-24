namespace JumpShotBasketball.Core.Models.Player;

/// <summary>
/// One season of archived career stats.
/// Written at end-of-season before stats are reset.
/// </summary>
public class CareerSeasonEntry
{
    public int Year { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public PlayerStatLine Stats { get; set; } = new();
}
