using JumpShotBasketball.Core.Models.Awards;
using JumpShotBasketball.Core.Models.Game;
using JumpShotBasketball.Core.Models.History;
using JumpShotBasketball.Core.Models.Playoff;

namespace JumpShotBasketball.Core.Models.League;

/// <summary>
/// Top-level league aggregate: teams, schedule, settings, transactions.
/// This is the root of the domain model, replacing CAverage as data owner.
/// </summary>
public class League
{
    public List<Team.Team> Teams { get; set; } = new();
    public Schedule Schedule { get; set; } = new();
    public LeagueSettings Settings { get; set; } = new();
    public List<Transaction> Transactions { get; set; } = new();
    public List<Staff.StaffMember> StaffPool { get; set; } = new();
    public PlayoffBracket? Bracket { get; set; }
    public SeasonAwards? Awards { get; set; }
    public List<SeasonAwards> AwardsHistory { get; set; } = new();
    public LeagueLeaderboard? Leaderboard { get; set; }
    public AllStarWeekendResult? AllStarWeekend { get; set; }
    public DraftBoard? DraftBoard { get; set; }
    public RookiePool? DraftPool { get; set; }
    public DraftResult? LastDraftResult { get; set; }
    public RecordBook RecordBook { get; set; } = new();
    public List<FranchiseHistory> FranchiseHistories { get; set; } = new();
    public List<HallOfFameEntry> HallOfFame { get; set; } = new();
    public List<AllStarWeekendResult> AllStarWeekendHistory { get; set; } = new();

    /// <summary>
    /// Free agents available for in-season signing (released/undrafted players).
    /// In C++, free agents occupy slots 961-1440 in the global player array.
    /// </summary>
    public List<Player.Player> FreeAgentPool { get; set; } = new();
}
