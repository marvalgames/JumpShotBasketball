namespace JumpShotBasketball.Core.Models.Awards;

public class AllTeamSelection
{
    public string TeamLabel { get; set; } = string.Empty;
    public int TeamNumber { get; set; }
    public List<AwardRecipient> Players { get; set; } = new();
}
