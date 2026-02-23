namespace JumpShotBasketball.Core.Models.Player;

/// <summary>
/// Reusable stat line used for season, playoff, career, and simulated stats.
/// Maps to the repeated m_games/m_min/m_fgm/.../m_pf pattern in C++ CPlayer.
/// </summary>
public class PlayerStatLine
{
    public int Games { get; set; }
    public int Minutes { get; set; }
    public int FieldGoalsMade { get; set; }
    public int FieldGoalsAttempted { get; set; }
    public int FreeThrowsMade { get; set; }
    public int FreeThrowsAttempted { get; set; }
    public int ThreePointersMade { get; set; }
    public int ThreePointersAttempted { get; set; }
    public int OffensiveRebounds { get; set; }
    public int Rebounds { get; set; }
    public int Assists { get; set; }
    public int Steals { get; set; }
    public int Turnovers { get; set; }
    public int Blocks { get; set; }
    public int PersonalFouls { get; set; }

    // Computed per-game averages (maps to m_pointsPerGame, m_minPerGame, etc.)
    public int Points => (FieldGoalsMade * 2) + FreeThrowsMade + ThreePointersMade;

    public double PointsPerGame => Games > 0 ? (double)Points / Games : 0;
    public double MinutesPerGame => Games > 0 ? (double)Minutes / Games : 0;
    public double ReboundsPerGame => Games > 0 ? (double)Rebounds / Games : 0;
    public double AssistsPerGame => Games > 0 ? (double)Assists / Games : 0;
    public double StealsPerGame => Games > 0 ? (double)Steals / Games : 0;
    public double BlocksPerGame => Games > 0 ? (double)Blocks / Games : 0;
    public double TurnoversPerGame => Games > 0 ? (double)Turnovers / Games : 0;
    public double OffensiveReboundsPerGame => Games > 0 ? (double)OffensiveRebounds / Games : 0;
    public double PersonalFoulsPerGame => Games > 0 ? (double)PersonalFouls / Games : 0;

    // Shooting percentages
    public double FieldGoalPercentage => FieldGoalsAttempted > 0 ? (double)FieldGoalsMade / FieldGoalsAttempted : 0;
    public double FreeThrowPercentage => FreeThrowsAttempted > 0 ? (double)FreeThrowsMade / FreeThrowsAttempted : 0;
    public double ThreePointPercentage => ThreePointersAttempted > 0 ? (double)ThreePointersMade / ThreePointersAttempted : 0;

    public void Reset()
    {
        Games = 0;
        Minutes = 0;
        FieldGoalsMade = 0;
        FieldGoalsAttempted = 0;
        FreeThrowsMade = 0;
        FreeThrowsAttempted = 0;
        ThreePointersMade = 0;
        ThreePointersAttempted = 0;
        OffensiveRebounds = 0;
        Rebounds = 0;
        Assists = 0;
        Steals = 0;
        Turnovers = 0;
        Blocks = 0;
        PersonalFouls = 0;
    }
}
