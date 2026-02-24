namespace JumpShotBasketball.Core.Models.League;

/// <summary>
/// Summary of free agency period outcomes.
/// </summary>
public class FreeAgencyResult
{
    public int PlayersSigned { get; set; }
    public int StagesCompleted { get; set; }
    public List<string> SigningDescriptions { get; set; } = new();
}
