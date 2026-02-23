namespace JumpShotBasketball.Core.Models.Awards;

public class AwardRecipient
{
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public int TeamIndex { get; set; }
    public string Position { get; set; } = string.Empty;
    public double Value { get; set; }
    public int Rank { get; set; }
}

public class AwardResult
{
    public string AwardName { get; set; } = string.Empty;
    public List<AwardRecipient> Recipients { get; set; } = new();
}
