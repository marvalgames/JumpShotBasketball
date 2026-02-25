namespace JumpShotBasketball.Core.Models.History;

/// <summary>
/// Represents a player inducted into the Hall of Fame.
/// Ported from CPlayer::HallofFamer() — Player.cpp:1708-1732.
/// </summary>
public class HallOfFameEntry
{
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public int AwardPoints { get; set; }
    public int MajorPoints { get; set; }
    public int YearsOfService { get; set; }
    public double CareerPpg { get; set; }
    public double CareerRpg { get; set; }
    public double CareerApg { get; set; }
    public int InductionYear { get; set; }
}
