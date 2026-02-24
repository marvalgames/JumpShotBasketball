namespace JumpShotBasketball.Core.Models.Game;

/// <summary>
/// League-wide per-48-minute stat highs used as denominators for projection rating calculation.
/// Port of the hi_fga/hi_fta/etc. variables passed to SetRatings in Retirement.cpp.
/// </summary>
public class LeagueHighs
{
    public double HighFieldGoalsAttempted { get; set; }
    public double HighFreeThrowsAttempted { get; set; }
    public double HighThreePointersAttempted { get; set; }
    public double HighOffensiveRebounds { get; set; }
    public double HighDefensiveRebounds { get; set; }
    public double HighAssists { get; set; }
    public double HighSteals { get; set; }
    public double HighTurnovers { get; set; }
    public double HighBlocks { get; set; }
    public int HighGames { get; set; }
}
