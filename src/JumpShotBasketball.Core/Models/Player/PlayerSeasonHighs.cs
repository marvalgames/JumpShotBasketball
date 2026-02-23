namespace JumpShotBasketball.Core.Models.Player;

/// <summary>
/// Season, career, and playoff highs for a player.
/// Maps to CPlayer's m_hi_pts, m_hi_reb, m_hi_ast, etc.
/// </summary>
public class PlayerSeasonHighs
{
    // Season highs
    public int SeasonPoints { get; set; }
    public int SeasonRebounds { get; set; }
    public int SeasonAssists { get; set; }
    public int SeasonSteals { get; set; }
    public int SeasonBlocks { get; set; }
    public int SeasonDoubleDoubles { get; set; }
    public int SeasonTripleDoubles { get; set; }

    // Career highs
    public int CareerPoints { get; set; }
    public int CareerRebounds { get; set; }
    public int CareerAssists { get; set; }
    public int CareerSteals { get; set; }
    public int CareerBlocks { get; set; }
    public int CareerDoubleDoubles { get; set; }
    public int CareerTripleDoubles { get; set; }

    // Playoff season highs
    public int PlayoffPoints { get; set; }
    public int PlayoffRebounds { get; set; }
    public int PlayoffAssists { get; set; }
    public int PlayoffSteals { get; set; }
    public int PlayoffBlocks { get; set; }

    // Career playoff highs
    public int CareerPlayoffPoints { get; set; }
    public int CareerPlayoffRebounds { get; set; }
    public int CareerPlayoffAssists { get; set; }
    public int CareerPlayoffSteals { get; set; }
    public int CareerPlayoffBlocks { get; set; }
}
