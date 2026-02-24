using JumpShotBasketball.Core.Constants;

namespace JumpShotBasketball.Core.Models.League;

/// <summary>
/// Internal state tracking during the free agency period.
/// Tracks per-team payroll, roster counts, and position values.
/// </summary>
public class FreeAgencyState
{
    public int Stage { get; set; }
    public int[] RosterCount { get; set; } = new int[LeagueConstants.MaxTeams + 1];
    public double[,] PositionValues { get; set; } = new double[LeagueConstants.MaxTeams + 1, 6];
    public double[] LeagueAvgPositionValues { get; set; } = new double[6];
    public int[] TempPayroll { get; set; } = new int[LeagueConstants.MaxTeams + 1];
    public int[] SignedCount { get; set; } = new int[LeagueConstants.MaxTeams + 1];
}
