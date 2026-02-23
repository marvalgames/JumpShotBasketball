using JumpShotBasketball.Core.Enums;

namespace JumpShotBasketball.Core.Models.Game;

/// <summary>
/// Mutable per-game state tracked during simulation.
/// Maps to C++ CEngine member variables (m_quarter, m_time, m_score, Lineup, etc.).
/// </summary>
public class GameState
{
    // Timing
    public int Quarter { get; set; } = 1;
    public int TimeRemainingSeconds { get; set; } = 720;
    public int PreviousTimeElapsed { get; set; }

    // Possession
    public int Possession { get; set; }
    public int FirstPossession { get; set; }
    public bool FastbreakChance { get; set; }
    public bool MilkClock { get; set; }

    // Scores
    public int[] Score { get; set; } = new int[3]; // [0] unused, [1]=visitor, [2]=home
    public int[,] QuarterScores { get; set; } = new int[3, 6]; // [team, quarter] — quarter 1-5

    // Team fouls per quarter
    public int[] TeamFouls { get; set; } = new int[3]; // [1]=visitor, [2]=home

    // Lineups: [team][position] = player index in flat array
    public int[,] Lineup { get; set; } = new int[3, 6]; // [team 1-2][pos 1-5]
    public int[,] Defense { get; set; } = new int[3, 6]; // defensive matchups

    // Rotation depth chart: [team][position][slot 0-9]
    public RotationEntry[,,] Rotation { get; set; } = InitRotation();

    // Consistency grid: [statSlot][cell] for FG attempt tracking
    public bool[,] ConsistencyGrid { get; set; } = new bool[61, 100];

    // Defensive matchup types: m_match[1-10] where 1-5=home, 6-10=visitor
    public int[] MatchupType { get; set; } = new int[11];

    // Game flags
    public bool GameOver { get; set; }
    public bool GameStarted { get; set; }
    public bool LineupSet { get; set; }
    public bool QuarterEnded { get; set; }
    public bool GarbageTime { get; set; }

    // Player tracking
    public int BallHandler { get; set; }
    public int PositionBallHandler { get; set; }
    public double DefensiveAdjustment { get; set; }

    // Auto-sub flags per team
    public bool[] AutoSubEnabled { get; set; } = { false, true, true };

    // Must-sub tracking (fouled out / injured player)
    public int[] MustSub { get; set; } = new int[3];

    // Playing players list (up to 60 entries: 1-30 visitor, 31-60 home)
    public int[] PlayingPlayersList { get; set; } = new int[61];

    // Starters record (for garbage time logic)
    public int[] Starters { get; set; } = new int[11];

    // Average possession time (seconds)
    public double AverageTime { get; set; }

    // Scoring factor (pace modifier)
    public double ScoringFactor { get; set; } = 1.0;

    // Average player minutes factor
    public double AveragePlayerMinFactor { get; set; } = 1.0;

    // Injury settings
    public bool InjuriesEnabled { get; set; } = true;

    // Team indices (1-based)
    public int VisitorIndex { get; set; }
    public int HomeIndex { get; set; }

    // Play-by-play
    public List<string> PlayByPlay { get; set; } = new();

    private static RotationEntry[,,] InitRotation()
    {
        var rot = new RotationEntry[3, 6, 10];
        for (int t = 0; t < 3; t++)
            for (int p = 0; p < 6; p++)
                for (int s = 0; s < 10; s++)
                    rot[t, p, s] = new RotationEntry();
        return rot;
    }
}
