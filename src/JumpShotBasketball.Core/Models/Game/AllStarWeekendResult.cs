namespace JumpShotBasketball.Core.Models.Game;

/// <summary>
/// Complete results of an All-Star Weekend: team rosters, contest results, game outcomes.
/// </summary>
public class AllStarWeekendResult
{
    // All-Star Game
    public List<int> Conference1Roster { get; set; } = new();
    public List<int> Conference2Roster { get; set; } = new();
    public GameResult? AllStarGameResult { get; set; }
    public string Conference1Name { get; set; } = string.Empty;
    public string Conference2Name { get; set; } = string.Empty;

    // Rookie/Soph Game (or All-Defense fallback)
    public List<int> RookieRoster { get; set; } = new();
    public List<int> SophomoreRoster { get; set; } = new();
    public GameResult? RookieGameResult { get; set; }
    public bool IsAllDefenseGame { get; set; }

    // 3-Point Contest
    public List<ContestParticipant> ThreePointContestants { get; set; } = new();
    public int ThreePointWinnerId { get; set; }

    // Dunk Contest
    public List<ContestParticipant> DunkContestants { get; set; } = new();
    public int DunkWinnerId { get; set; }
}

/// <summary>
/// Individual contestant in a 3-Point or Dunk contest.
/// </summary>
public class ContestParticipant
{
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public int[] RoundScores { get; set; } = new int[4]; // [0]=selection score, [1..3]=round scores
    public int HighestRoundReached { get; set; } // 1, 2, or 3 (3pt) / 1 or 2 (dunk)
}
