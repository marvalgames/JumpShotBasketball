namespace JumpShotBasketball.Core.Constants;

public static class GameConstants
{
    // Screen dimensions (from C++ WIDTH/HEIGHT)
    public const int ScreenWidth = 1366;
    public const int ScreenHeight = 720;

    // Minutes distribution thresholds (MIN0-MIN10 from C++)
    public static readonly int[] MinutesThresholds =
    {
        35, 51, 59, 61, 64, 70, 76, 82, 89, 100, 103
    };

    // Maximum rating caps (MAX0-MAX10 from C++)
    public static readonly int[] MaxRatingCaps =
    {
        1063, 1063, 1063, 1063, 1063, 1063, 1063, 1275, 1275, 1275, 1488
    };

    // Exhibition game constants
    public const int ExhibitionMid = 450;
    public const int ExhibitionMin = 120;

    // Roster/stat view modes (from C++ PLAYERS, TEAMS, TEAMDEFENSE)
    public const int PlayersView = 1;
    public const int TeamsView = 2;
    public const int TeamDefenseView = 3;

    // Stat modes (from C++ SIMULATED, ACTUAL)
    public const int SimulatedStats = 1;
    public const int ActualStats = 2;

    // Quarters and periods
    public const int QuarterMinutes = 12;
    public const int OvertimeMinutes = 5;
    public const int QuartersPerGame = 4;
}
