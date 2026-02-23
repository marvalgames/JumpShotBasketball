namespace JumpShotBasketball.Core.Models.Game;

/// <summary>
/// Pre-computed league-wide per-48-minute averages used during game simulation.
/// Maps to C++ avg.m_avg_fga[0], avg.m_avg_blk[0], etc.
/// Values are per-player averages (multiply by 5 for per-team lineup totals).
/// </summary>
public class LeagueAverages
{
    /// <summary>Per-player FGA per 48 min (avg.m_avg_fga[0]).</summary>
    public double FieldGoalsAttempted { get; set; }

    /// <summary>Per-player 3PA per 48 min (avg.m_avg_fga_t[0] or avg.m_avg_tfga).</summary>
    public double ThreePointersAttempted { get; set; }

    /// <summary>Per-player FG% * 1000 (avg.m_avg_fgp[i] by position, [0]=overall).</summary>
    public double[] FieldGoalPercentageByPosition { get; set; } = new double[6];

    /// <summary>Per-player OREB per 48 min (avg.m_avg_orb[0]).</summary>
    public double OffensiveRebounds { get; set; }

    /// <summary>Per-player DREB per 48 min (avg.m_avg_drb[0]).</summary>
    public double DefensiveRebounds { get; set; }

    /// <summary>Per-player AST per 48 min by position (avg.m_avg_ast[i]).</summary>
    public double[] AssistsByPosition { get; set; } = new double[6];

    /// <summary>Per-player STL per 48 min (avg.m_avg_stl[0]).</summary>
    public double Steals { get; set; }

    /// <summary>Per-player TO per 48 min (avg.m_avg_to[0]).</summary>
    public double Turnovers { get; set; }

    /// <summary>Per-player BLK per 48 min (avg.m_avg_blk[0]).</summary>
    public double Blocks { get; set; }

    /// <summary>Per-player PF per 48 min (avg.m_avg_pf[0]).</summary>
    public double PersonalFouls { get; set; }

    /// <summary>Per-player FTA per 48 min (avg.m_avg_fta[0]).</summary>
    public double FreeThrowsAttempted { get; set; }
}
