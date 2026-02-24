using JumpShotBasketball.Core.Constants;
using JumpShotBasketball.Core.Models.League;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Draft pick ownership, chart rollover, and pick value calculations.
/// Port of Own.cpp (InitOwnData, SetEndOfSeasonDraftChart, SetDraftPickAddition)
/// and Future.cpp (SetDraftChart).
/// </summary>
public static class DraftService
{
    /// <summary>
    /// Initializes the draft chart so every team owns its own picks for all years and rounds.
    /// Port of COwn::InitOwnData() — Own.cpp:16-25.
    /// </summary>
    public static void InitializeDraftChart(DraftBoard board, int numTeams)
    {
        for (int y = 0; y < LeagueConstants.MaxDraftYears; y++)
            for (int r = 0; r < LeagueConstants.MaxDraftRounds; r++)
                for (int p = 0; p <= numTeams; p++)
                    board.DraftChart[y, r, p] = p;
    }

    /// <summary>
    /// Shifts draft chart forward one year at end of season.
    /// Year 0 ← Year 1, Year 1 ← Year 2, Year 2 ← Year 3, Year 3 ← self-ownership.
    /// Port of COwn::SetEndOfSeasonDraftChart() — Own.cpp:135-151.
    /// Note: C++ iterates y=1..3 shifting chart[y] = chart[y+1] which shifts
    /// years 1,2,3 down (1←2, 2←3, 3←self). Year 0 (current) is consumed by draft.
    /// We implement the intended semantics: slide everything forward.
    /// </summary>
    public static void RollDraftChartForward(DraftBoard board)
    {
        int maxTeam = board.DraftChart.GetLength(2) - 1;

        // Shift: year 0 ← year 1, year 1 ← year 2, year 2 ← year 3
        for (int y = 0; y < LeagueConstants.MaxDraftYears - 1; y++)
        {
            for (int r = 0; r < LeagueConstants.MaxDraftRounds; r++)
            {
                for (int p = 0; p <= maxTeam; p++)
                {
                    board.DraftChart[y, r, p] = board.DraftChart[y + 1, r, p];
                }
            }
        }

        // Reset the furthest year (year 3) to self-ownership
        int lastYear = LeagueConstants.MaxDraftYears - 1;
        for (int r = 0; r < LeagueConstants.MaxDraftRounds; r++)
            for (int p = 0; p <= maxTeam; p++)
                board.DraftChart[lastYear, r, p] = p;
    }

    /// <summary>
    /// Transfers a draft pick from one team to another.
    /// Port of CFuture::SetDraftChart() — Future.cpp:63-76.
    /// </summary>
    public static void TransferPick(DraftBoard board, int fromTeam, int toTeam, int year, int round)
    {
        if (year < 0 || year >= LeagueConstants.MaxDraftYears) return;
        if (round < 0 || round >= LeagueConstants.MaxDraftRounds) return;
        if (fromTeam < 0 || fromTeam > LeagueConstants.MaxTeams) return;

        board.DraftChart[year, round, fromTeam] = toTeam;
    }

    /// <summary>
    /// Calculates the trade value of a draft pick based on pick number.
    /// Higher picks are worth more, with a bonus for top-5 picks.
    /// Port of the pick valuation logic from Own.cpp:54-76.
    /// </summary>
    public static int CalculatePickValue(int pickNumber)
    {
        if (pickNumber <= 0) return 0;

        int value = 101 - pickNumber;
        if (value < 1) value = 1;

        // Top-5 bonus
        if (pickNumber <= 5)
            value += (6 - pickNumber) * 5;

        return value;
    }

    /// <summary>
    /// Returns the team index that owns a specific pick.
    /// </summary>
    public static int GetPickOwner(DraftBoard board, int year, int round, int team)
    {
        if (year < 0 || year >= LeagueConstants.MaxDraftYears) return team;
        if (round < 0 || round >= LeagueConstants.MaxDraftRounds) return team;
        if (team < 0 || team > LeagueConstants.MaxTeams) return team;

        return board.DraftChart[year, round, team];
    }

    /// <summary>
    /// Decodes a YRPP-encoded pick value into year, round, and team components.
    /// YRPP format: Year*1000 + Round*100 + OriginalTeamIndex.
    /// </summary>
    public static (int Year, int Round, int Team) DecodeYrpp(int yrpp)
    {
        int year = yrpp / 1000;
        int round = (yrpp - year * 1000) / 100;
        int team = yrpp - (year * 1000 + round * 100);
        return (year, round, team);
    }

    /// <summary>
    /// Encodes year, round, and team into a YRPP value.
    /// </summary>
    public static int EncodeYrpp(int year, int round, int team)
    {
        return year * 1000 + round * 100 + team;
    }
}
