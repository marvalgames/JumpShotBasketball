using JumpShotBasketball.Core.Constants;

namespace JumpShotBasketball.Core.Models.League;

/// <summary>
/// Draft pick ownership per team.
/// Replaces C++ COwn with its m_draft_chart[4][3][33] 3D array.
/// Key: (year, round) → team index that owns the pick.
/// </summary>
public class DraftBoard
{
    // draft_chart[year][round][originalTeam] = owning team index
    // year: 0-3 (current + 3 future), round: 0-2 (3 rounds), team: 0-32
    public int[,,] DraftChart { get; set; } = new int[
        LeagueConstants.MaxDraftYears,
        LeagueConstants.MaxDraftRounds,
        LeagueConstants.MaxTeams + 1];

    public int NumberProposedPicks { get; set; }
    public int NumberActualPicks { get; set; }
    public int SalaryInTrade { get; set; }
    public int TotalSalaryInTrade { get; set; }
    public double TotalTrueInTrade { get; set; }
    public int TotalYearsInTrade { get; set; }
    public double DraftPickAddition { get; set; }

    public int[] ProposedPicks { get; set; } = new int[8];
    public int[] ActualPicks { get; set; } = new int[193];
    public int[] NumberPicksYear { get; set; } = new int[LeagueConstants.MaxDraftYears];
    public int[] NumberProposedPicksYear { get; set; } = new int[LeagueConstants.MaxDraftYears];

    public DraftBoard()
    {
        // Initialize draft chart: each team owns its own pick by default
        for (int y = 0; y < LeagueConstants.MaxDraftYears; y++)
            for (int r = 0; r < LeagueConstants.MaxDraftRounds; r++)
                for (int p = 0; p <= LeagueConstants.MaxTeams; p++)
                    DraftChart[y, r, p] = p;
    }
}
