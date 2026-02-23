using JumpShotBasketball.Core.Models.Player;

namespace JumpShotBasketball.Core.Models.Game;

/// <summary>
/// Immutable result of a simulated game.
/// The caller is responsible for post-game side effects (updating standings, accumulating stats).
/// </summary>
public class GameResult
{
    public int VisitorScore { get; init; }
    public int HomeScore { get; init; }

    /// <summary>Quarter-by-quarter scores: [team 1-2, quarter 1-5].</summary>
    public int[,] QuarterScores { get; init; } = new int[3, 6];

    /// <summary>Number of quarters played (4 = regulation, 5+ = overtime).</summary>
    public int QuartersPlayed { get; init; }

    /// <summary>Per-player game stats for the visitor team.</summary>
    public List<PlayerGameState> VisitorBoxScore { get; init; } = new();

    /// <summary>Per-player game stats for the home team.</summary>
    public List<PlayerGameState> HomeBoxScore { get; init; } = new();

    /// <summary>Index of the game MVP player (-1 if not determined).</summary>
    public int MvpPlayerIndex { get; init; } = -1;

    /// <summary>Play-by-play text entries.</summary>
    public List<string> PlayByPlay { get; init; } = new();

    /// <summary>Visitor team index (1-based).</summary>
    public int VisitorTeamIndex { get; init; }

    /// <summary>Home team index (1-based).</summary>
    public int HomeTeamIndex { get; init; }

    public bool IsOvertime => QuartersPlayed > 4;
    public bool HomeWin => HomeScore > VisitorScore;
    public bool VisitorWin => VisitorScore > HomeScore;
}
