namespace JumpShotBasketball.Core.Models.Playoff;

/// <summary>
/// A team's playoff seed within its group (conference or division).
/// </summary>
public class PlayoffSeed
{
    public int TeamIndex { get; set; }
    public int SeedNumber { get; set; }
    public string GroupName { get; set; } = string.Empty;
}
