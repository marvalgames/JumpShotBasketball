namespace JumpShotBasketball.Core.Models.Game;

/// <summary>
/// Tracks timeout counts and flags during a game simulation.
/// Maps to C++ m_toVisitor/m_toHome/m_toOk/m_toCalled.
/// </summary>
public class TimeoutState
{
    /// <summary>Full timeouts remaining: [0] unused, [1]=visitor, [2]=home. Initial: 7 each.</summary>
    public int[] FullTimeouts { get; set; } = { 0, 7, 7 };

    /// <summary>20-second timeouts remaining: [0] unused, [1]=visitor, [2]=home. Initial: 1 each.</summary>
    public int[] TwentySecTimeouts { get; set; } = { 0, 1, 1 };

    /// <summary>Whether a full timeout was just called by each team.</summary>
    public bool[] TimeoutCalled { get; set; } = new bool[3];

    /// <summary>Whether a 20-sec timeout was just called by each team.</summary>
    public bool[] TwentySecCalled { get; set; } = new bool[3];

    /// <summary>Whether a timeout is currently allowed (dead ball, etc.).</summary>
    public bool TimeoutAllowed { get; set; }

    /// <summary>Whether a timeout was called during this possession (suppresses fastbreak).</summary>
    public bool TimeoutCalledThisPossession { get; set; }
}
