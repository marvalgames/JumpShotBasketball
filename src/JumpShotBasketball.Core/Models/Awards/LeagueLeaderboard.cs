namespace JumpShotBasketball.Core.Models.Awards;

public class LeagueLeaderboard
{
    public int Year { get; set; }
    public List<StatLeaderEntry> PointsLeaders { get; set; } = new();
    public List<StatLeaderEntry> ReboundsLeaders { get; set; } = new();
    public List<StatLeaderEntry> AssistsLeaders { get; set; } = new();
    public List<StatLeaderEntry> StealsLeaders { get; set; } = new();
    public List<StatLeaderEntry> BlocksLeaders { get; set; } = new();
    public List<StatLeaderEntry> FieldGoalPctLeaders { get; set; } = new();
    public List<StatLeaderEntry> FreeThrowPctLeaders { get; set; } = new();
    public List<StatLeaderEntry> ThreePointPctLeaders { get; set; } = new();
}
