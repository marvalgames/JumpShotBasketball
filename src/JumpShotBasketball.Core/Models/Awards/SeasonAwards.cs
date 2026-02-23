namespace JumpShotBasketball.Core.Models.Awards;

public class SeasonAwards
{
    public int Year { get; set; }

    // Individual awards (top 5 each)
    public AwardResult Mvp { get; set; } = new() { AwardName = "MVP" };
    public AwardResult PlayoffMvp { get; set; } = new() { AwardName = "Playoff MVP" };
    public AwardResult DefensivePlayerOfYear { get; set; } = new() { AwardName = "DPOY" };
    public AwardResult RookieOfYear { get; set; } = new() { AwardName = "ROY" };
    public AwardResult SixthMan { get; set; } = new() { AwardName = "6th Man" };

    // Statistical leaders (top 5 each)
    public AwardResult ScoringLeader { get; set; } = new() { AwardName = "Scoring" };
    public AwardResult ReboundingLeader { get; set; } = new() { AwardName = "Rebounding" };
    public AwardResult AssistsLeader { get; set; } = new() { AwardName = "Assists" };
    public AwardResult StealsLeader { get; set; } = new() { AwardName = "Steals" };
    public AwardResult BlocksLeader { get; set; } = new() { AwardName = "Blocks" };

    // Positional teams
    public List<AllTeamSelection> AllLeagueTeams { get; set; } = new();
    public List<AllTeamSelection> AllDefenseTeams { get; set; } = new();
    public List<AllTeamSelection> AllRookieTeams { get; set; } = new();

    // Championship
    public int ChampionTeamIndex { get; set; } = -1;
    public List<int> RingRecipientPlayerIds { get; set; } = new();
}
