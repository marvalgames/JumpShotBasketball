using JumpShotBasketball.Core.Constants;

namespace JumpShotBasketball.Core.Models.League;

/// <summary>
/// League-wide configuration settings.
/// Maps to C++ CBBallSettings.
/// </summary>
public class LeagueSettings
{
    public int NumberOfTeams { get; set; }
    public int CurrentYear { get; set; }
    public int CurrentStage { get; set; }
    public double SalaryCap { get; set; } = LeagueConstants.DefaultSalaryCap;
    public double ScoringFactor { get; set; }
    public int SimSpeed { get; set; }

    // Feature toggles (maps to m_scouts_on, m_financial_on, etc.)
    public bool ScoutsEnabled { get; set; } = true;
    public bool FinancialEnabled { get; set; } = true;
    public bool FreeAgencyEnabled { get; set; }
    public bool ComputerTradesEnabled { get; set; }
    public bool TradeDeadlineEnabled { get; set; }
    public bool InjuriesEnabled { get; set; }
    public bool InjuryPromptEnabled { get; set; }
    public bool SalaryMatchingEnabled { get; set; }
    public bool CareerStatsEnabled { get; set; }
    public bool ThreePointLineEnabled { get; set; }
    public bool ThreePointFreeThrowEnabled { get; set; }
    public bool LoadBitmaps { get; set; }
    public bool Expanding { get; set; }

    // Stat options per team
    public int AstGmMode { get; set; }
    public int NumberOfTransactions { get; set; }

    // League structure
    public string LeagueName { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string FullPathName { get; set; } = string.Empty;

    // Playoff format
    public string PlayoffFormat { get; set; } = string.Empty;
    public string Round1Format { get; set; } = string.Empty;
    public string Round2Format { get; set; } = string.Empty;
    public string Round3Format { get; set; } = string.Empty;
    public string Round4Format { get; set; } = string.Empty;

    // Conference/division names
    public string ConferenceName1 { get; set; } = string.Empty;
    public string ConferenceName2 { get; set; } = string.Empty;
    public string DivisionName1 { get; set; } = string.Empty;
    public string DivisionName2 { get; set; } = string.Empty;
    public string DivisionName3 { get; set; } = string.Empty;
    public string DivisionName4 { get; set; } = string.Empty;

    // All-star/special events
    public int AllStar { get; set; }
    public int RookieStar { get; set; }
    public int ThreePointContest { get; set; }
    public int DunkContest { get; set; }
    public int AllStarIndex { get; set; }
    public int RookieStarIndex { get; set; }
    public int ThreePointContestIndex { get; set; }
    public int DunkContestIndex { get; set; }
}
