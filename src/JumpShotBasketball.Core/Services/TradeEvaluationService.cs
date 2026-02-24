using JumpShotBasketball.Core.Constants;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Trade valuation, salary matching, and acceptance evaluation.
/// Port of valuation logic from Computer.cpp (lines 337-776) and Own.cpp (105-133).
/// </summary>
public static class TradeEvaluationService
{
    /// <summary>
    /// Checks whether two salary amounts satisfy the salary matching rule.
    /// Port of Computer.cpp:415-417: sa2 must be in [sa1*0.85 - 10, sa1*1.15 + 10].
    /// </summary>
    public static bool CheckSalaryMatching(int salary1, int salary2)
    {
        double sa1 = salary1;
        double min = sa1 * 0.85 - 10;
        double max = sa1 * 1.15 + 10;
        return salary2 >= min && salary2 <= max;
    }

    /// <summary>
    /// Calculates the sum of TradeTrueRating for the top eligible players on a roster,
    /// weighted by rotation role. Simplified port of ComputerRotationForTrades.
    /// Returns the total roster value used for pre/post trade comparison.
    /// </summary>
    public static double CalculateRosterValue(List<Player> roster)
    {
        double total = 0;
        var eligible = roster
            .Where(p => !string.IsNullOrEmpty(p.Name) && p.Active && p.Injury == 0)
            .OrderByDescending(p => p.Ratings.TradeTrueRating)
            .Take(15)
            .ToList();

        for (int i = 0; i < eligible.Count; i++)
        {
            double rating = eligible[i].Ratings.TradeTrueRating;
            // Top 5 get full weight, 6-10 get 0.75, 11-15 get 0.5
            if (i < 5)
                total += rating;
            else if (i < 10)
                total += rating * 0.75;
            else
                total += rating * 0.5;
        }

        return total;
    }

    /// <summary>
    /// Calculates the trade value of a draft pick based on team record and league averages.
    /// Port of COwn::SetDraftPickAddition() — Own.cpp:105-133.
    /// </summary>
    public static double CalculateDraftPickTradeValue(
        DraftBoard board, int pickYrpp, double avgTru, double teamAvgTru,
        int teamWins, int teamLosses, int gamesInSeason)
    {
        if (pickYrpp == 0) return 0;

        var (y, r, p) = DraftService.DecodeYrpp(pickYrpp);

        double truF = avgTru + (teamAvgTru - avgTru) * 2;
        if (truF < 2) truF = 2;

        // C++ uses 1-based rounds: r=1→ro=1, r=2→ro=3
        // Our rounds are 0-based: r=0→round 1(ro=1), r=1→round 2(ro=3), r=2→round 3(ro=4)
        double ro;
        if (r == 0) ro = 1;
        else if (r == 1) ro = 3;
        else ro = r + 2;

        double rating = avgTru / truF / ro * (4.0 / (3.0 + y));

        int totalG = teamWins + teamLosses;
        double percent = totalG > 0
            ? (double)teamWins / totalG
            : 1.0;

        double f = gamesInSeason > 0 ? (double)totalG / gamesInSeason : 1.0;
        if (f > 1) f = 1;

        double nu = rating * (f * (1 - percent) * 2) + rating * (1 - f);
        if (nu > 2) nu = 2;

        return nu * avgTru;
    }

    /// <summary>
    /// Collects all YRPP-encoded picks owned by a team.
    /// Port of CComputer::SetOwnDraftPicks() — Computer.cpp:1292-1327.
    /// </summary>
    public static List<int> CollectTeamOwnedPicks(DraftBoard board, int teamIndex, int numTeams)
    {
        var picks = new List<int>();

        // C++ iterates y=1..3, r=1..2, p=1..32 (1-based)
        // Our chart is 0-based for year/round dimensions but teamIndex matches
        for (int y = 1; y < LeagueConstants.MaxDraftYears; y++)
        {
            for (int r = 0; r < Math.Min(2, LeagueConstants.MaxDraftRounds); r++)
            {
                for (int p = 1; p <= numTeams; p++)
                {
                    int owner = board.DraftChart[y, r, p];
                    if (owner == teamIndex && teamIndex > 0)
                    {
                        picks.Add(DraftService.EncodeYrpp(y, r, p));
                    }
                }
            }
        }

        return picks;
    }

    /// <summary>
    /// Counts how many picks a team owns in a specific draft year.
    /// Used to enforce the max 6 picks per year limit.
    /// </summary>
    public static int CountPicksInYear(DraftBoard board, int teamIndex, int year, int numTeams)
    {
        int count = 0;
        for (int r = 0; r < LeagueConstants.MaxDraftRounds; r++)
        {
            for (int p = 1; p <= numTeams; p++)
            {
                if (board.DraftChart[year, r, p] == teamIndex)
                    count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Validates that receiving a pick wouldn't exceed the 6-pick-per-year limit.
    /// </summary>
    public static bool ValidateDraftPickLimits(
        DraftBoard board, int receivingTeam, int pickYrpp, int numTeams)
    {
        if (pickYrpp == 0) return true;

        var (year, _, _) = DraftService.DecodeYrpp(pickYrpp);
        int currentCount = CountPicksInYear(board, receivingTeam, year, numTeams);
        return currentCount < 6;
    }

    /// <summary>
    /// Calculates the value factor for traded players — how underpaid they are
    /// relative to the team's current value.
    /// Port of Computer.cpp:401-408.
    /// </summary>
    public static double CalculateValueFactor(
        double totalTrueInTrade, int totalSalaryInTrade, double teamCurrentValue)
    {
        if (teamCurrentValue <= 0) return 0;

        double underPaid = totalTrueInTrade - (double)totalSalaryInTrade / 100.0;
        return underPaid * 400.0 / teamCurrentValue;
    }

    /// <summary>
    /// Calculates the trade worth of a player: tradeValue * yearsLeft, with salary capped.
    /// Port of Computer.cpp:355-366 per-player accumulation.
    /// </summary>
    public static (double trueContribution, int cappedSalary) CalculatePlayerTradeWorth(Player player)
    {
        int yearsLeft = 1 + player.Contract.ContractYears - player.Contract.CurrentContractYear;
        if (yearsLeft < 1) yearsLeft = 1;

        double trueContribution = player.Ratings.TradeTrueRating * yearsLeft;

        int remSal = player.Contract.RemainingSalary;
        int maxSal = LeagueConstants.SalaryMaximumByYos[10] * yearsLeft; // MAX10
        if (remSal > maxSal) remSal = maxSal;

        return (trueContribution, remSal);
    }

    /// <summary>
    /// Evaluates whether AI teams accept a trade based on before/after roster values.
    /// Port of Computer.cpp:727-776 acceptance thresholds.
    /// Computer-vs-computer: both sides ratio in [0.98, 1.06].
    /// Player-offer-to-computer: computer side in (1.08, 1.16), player side &lt; 0.98.
    /// </summary>
    public static bool EvaluateAcceptance(
        double team1OldTrue, double team1NewTrue,
        double team2OldTrue, double team2NewTrue,
        string team1Control, string team2Control)
    {
        if (team1OldTrue <= 0 || team2OldTrue <= 0) return false;

        double f1 = 0.98;
        double f2 = 1.06;

        // Computer vs Computer
        if (team1Control != "Player" && team2Control != "Player")
        {
            double vt = team1NewTrue / team1OldTrue;
            double ht = team2NewTrue / team2OldTrue;
            return vt > f1 && ht > f1 && vt < f2 && ht < f2;
        }

        // Player offering to Computer (team1 = player, team2 = computer)
        if (team1Control == "Player" && team2Control != "Player")
        {
            double compOffer = team2NewTrue / team2OldTrue;
            double playerOffer = team1NewTrue / team1OldTrue;
            return compOffer > 1.08 && compOffer < 1.16 && playerOffer < 0.98;
        }

        // Computer offering to Player (team2 = player, team1 = computer)
        if (team2Control == "Player" && team1Control != "Player")
        {
            double compOffer = team1NewTrue / team1OldTrue;
            double playerOffer = team2NewTrue / team2OldTrue;
            return compOffer > 1.08 && compOffer < 1.16 && playerOffer < 0.98;
        }

        // Player vs Player — not valid for AI trades
        return false;
    }
}
