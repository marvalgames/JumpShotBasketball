using JumpShotBasketball.Core.Constants;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// AI draft pick selection and roster placement.
/// Port of RookieDlg.cpp (SortDraftSlots, GetComputerSelection, SelectionMade, FinalizeNewRosters).
/// </summary>
public static class DraftExecutionService
{
    /// <summary>
    /// Cumulative lottery odds favoring worst teams.
    /// Port of RookieDlg::InitLotteryOdds() — RookieDlg.cpp:1653-1662.
    /// Index 0 = 0 (baseline), index i = cumulative probability through team i.
    /// Worst team (index 1) has 250/1025 ≈ 24.4% chance of #1 pick.
    /// </summary>
    private static readonly int[] LotteryOdds =
    {
        0, 250, 450, 607, 727, 816, 880, 924, 953, 971,
        982, 989, 995, 1000, 1004, 1007, 1009, 1010, 1011, 1012,
        1013, 1014, 1015, 1016, 1017, 1018, 1019, 1020, 1021, 1022,
        1023, 1024, 1025
    };

    /// <summary>
    /// Builds the draft order for 2 rounds, sorted by worst record first.
    /// Port of RookieDlg::SortDraftSlots() — RookieDlg.cpp:968-1027.
    /// Score = leaguePercentage + (isPlayoffTeam ? 1 : 0); lower = picks earlier.
    /// </summary>
    public static List<(int Round, int PickNumber, int PickingTeamIndex, int OriginalTeamIndex)> BuildDraftOrder(
        League league)
    {
        var order = new List<(int Round, int PickNumber, int PickingTeamIndex, int OriginalTeamIndex)>();
        int numTeams = league.Teams.Count;
        if (numTeams == 0) return order;

        // Sort teams by record: worst first (lowest pct + playoff bonus)
        var sortedTeams = league.Teams
            .Select((t, i) => new
            {
                Index = i,
                Score = t.Record.LeaguePercentage + (t.Record.IsPlayoffTeam ? 1.0 : 0.0)
            })
            .OrderBy(x => x.Score)
            .ThenBy(x => x.Index) // stable tiebreak
            .ToList();

        for (int round = 0; round < 2; round++)
        {
            for (int pick = 0; pick < numTeams; pick++)
            {
                int originalTeamIndex = sortedTeams[pick].Index;
                int pickingTeamIndex = originalTeamIndex;

                if (league.DraftBoard != null)
                {
                    int owner = DraftService.GetPickOwner(league.DraftBoard, 0, round, originalTeamIndex);
                    if (owner >= 0 && owner < numTeams)
                        pickingTeamIndex = owner;
                }

                order.Add((round, pick + 1, pickingTeamIndex, originalTeamIndex));
            }
        }

        return order;
    }

    /// <summary>
    /// Applies a weighted draft lottery to round 1 of the draft order.
    /// Port of RookieDlg::OnButtonLottery() — RookieDlg.cpp:1552-1651.
    /// Draws up to 3 winners from non-playoff teams using weighted odds;
    /// winners move to top picks while remaining teams stay in original order.
    /// Only modifies round 1; round 2 stays unchanged.
    /// </summary>
    public static List<(int Pick, int TeamIndex, string TeamName)> ApplyLottery(
        List<(int Round, int PickNumber, int PickingTeamIndex, int OriginalTeamIndex)> order,
        League league,
        Random random)
    {
        var winners = new List<(int Pick, int TeamIndex, string TeamName)>();

        // Count non-playoff teams in round 1.
        // Non-playoff teams always sort first (score = pct + 0.0 < pct + 1.0).
        int lotteryTeams = 0;
        for (int i = 0; i < order.Count; i++)
        {
            if (order[i].Round != 0) break;
            int origIdx = order[i].OriginalTeamIndex;
            if (origIdx >= 0 && origIdx < league.Teams.Count && !league.Teams[origIdx].Record.IsPlayoffTeam)
                lotteryTeams++;
            else
                break;
        }

        if (lotteryTeams < 2) return winners;

        int numPicks = Math.Min(3, lotteryTeams);
        int oddsIndex = Math.Min(lotteryTeams, LotteryOdds.Length - 1);
        int maxBall = LotteryOdds[oddsIndex];

        // Extract lottery-eligible slots (first lotteryTeams entries of round 1)
        var lotterySlots = new List<(int PickingTeamIndex, int OriginalTeamIndex)>();
        for (int i = 0; i < lotteryTeams; i++)
            lotterySlots.Add((order[i].PickingTeamIndex, order[i].OriginalTeamIndex));

        // Draw weighted winners (do-while prevents duplicate draws)
        var selectedIndices = new HashSet<int>();
        var winnerIndices = new List<int>();

        for (int p = 0; p < numPicks; p++)
        {
            int winnerIdx;
            do
            {
                int ball = random.Next(1, maxBall + 1);
                winnerIdx = 0;
                for (int i = 0; i < lotteryTeams; i++)
                {
                    if (ball <= LotteryOdds[i + 1])
                    {
                        winnerIdx = i;
                        break;
                    }
                }
            } while (selectedIndices.Contains(winnerIdx));

            selectedIndices.Add(winnerIdx);
            winnerIndices.Add(winnerIdx);
        }

        // Rebuild: winners first in draw order, then remaining in original worst-first order
        var reordered = new List<(int PickingTeamIndex, int OriginalTeamIndex)>();
        foreach (int wi in winnerIndices)
            reordered.Add(lotterySlots[wi]);
        for (int i = 0; i < lotteryTeams; i++)
        {
            if (!selectedIndices.Contains(i))
                reordered.Add(lotterySlots[i]);
        }

        // Update round 1 lottery slots in place
        for (int i = 0; i < lotteryTeams; i++)
            order[i] = (0, i + 1, reordered[i].PickingTeamIndex, reordered[i].OriginalTeamIndex);

        // Record lottery winners
        for (int p = 0; p < winnerIndices.Count; p++)
        {
            var slot = reordered[p];
            string teamName = slot.PickingTeamIndex >= 0 && slot.PickingTeamIndex < league.Teams.Count
                ? league.Teams[slot.PickingTeamIndex].Name
                : "Unknown";
            winners.Add((p + 1, slot.PickingTeamIndex, teamName));
        }

        return winners;
    }

    /// <summary>
    /// AI selects the best available rookie for a team.
    /// Port of RookieDlg::GetComputerSelection() — RookieDlg.cpp:794-937.
    /// 4-tier fallback: fill missing positions → draft by need → draft by need (expanded) → BPA.
    /// </summary>
    public static int GetComputerSelection(
        List<Player> available,
        Models.Team.Team team,
        double avgTradeTru,
        Random random)
    {
        if (available.Count == 0) return -1;

        // Phase 0: Scan team roster for position needs
        int[] eligibleByPos = new int[6]; // [1]=PG, [2]=SG, [3]=SF, [4]=PF, [5]=C
        bool[] needPos = { false, true, true, true, true, true }; // all positions start as needed
        bool[] starAtPos = new bool[6];

        int gmNeed = team.GeneralManager?.Power1 ?? 3;

        foreach (var player in team.Roster)
        {
            if (string.IsNullOrEmpty(player.Name)) continue;
            if (player.Contract.IsFreeAgent) continue;

            int posIdx = PositionToIndex(player.Position);
            if (posIdx < 1) continue;

            double tradeTru = player.Ratings.TradeTrueRating;

            // Count eligible players at this position
            if (player.Ratings.ProjectionFieldGoalsAttempted >= 5)
                eligibleByPos[posIdx]++;

            // Check if this player satisfies the need at this position
            int beforePrime = player.Ratings.Prime - player.Age;
            bool draftByNeed = beforePrime > 0 && random.Next(1, 13) > (4 + beforePrime + (6 - gmNeed));
            if (tradeTru > avgTradeTru && draftByNeed)
                needPos[posIdx] = false;

            // Check for star prospects (1.375x avg)
            if (beforePrime > 0 && tradeTru > avgTradeTru * 1.375)
                starAtPos[posIdx] = true;
        }

        // Phase 1: Fill positions with 0 eligible players (and no star prospect)
        for (int i = 0; i < available.Count; i++)
        {
            int posIdx = PositionToIndex(available[i].Position);
            if (posIdx < 1) continue;

            if (eligibleByPos[posIdx] < 1 && !starAtPos[posIdx])
                return i;
        }

        // Phase 2: Pick by need (limited scan based on GM need factor)
        int needLimit = gmNeed + random.Next(0, 5);
        int scanLimit = Math.Min(needLimit, available.Count);
        for (int i = 0; i < scanLimit; i++)
        {
            if (string.IsNullOrEmpty(available[i].Name)) continue;
            int posIdx = PositionToIndex(available[i].Position);
            if (posIdx < 1) continue;

            if (needPos[posIdx] && !starAtPos[posIdx])
                return i;
        }

        // Phase 3: Best player available (first remaining rookie)
        for (int i = 0; i < available.Count; i++)
        {
            if (!string.IsNullOrEmpty(available[i].Name))
                return i;
        }

        return 0;
    }

    /// <summary>
    /// Executes the full draft: builds order, AI selects for each pick,
    /// places rookies on team rosters, marks undrafted as free agents.
    /// Port of RookieDlg main loop + SelectionMade + FinalizeNewRosters.
    /// </summary>
    public static DraftResult ExecuteDraft(League league, Random? random = null)
    {
        random ??= Random.Shared;
        var result = new DraftResult();

        if (league.DraftPool == null || league.DraftPool.Rookies.Count == 0)
            return result;

        var available = new List<Player>(league.DraftPool.Rookies);
        var draftOrder = BuildDraftOrder(league);

        // Apply lottery to round 1 (weighted random reorder of non-playoff teams)
        result.LotteryWinners = ApplyLottery(draftOrder, league, random);

        // Compute league average trade true rating
        double avgTradeTru = CalculateLeagueAvgTradeTru(league);

        // Sort rookies by TradeTrueRating descending (best first)
        available.Sort((a, b) => b.Ratings.TradeTrueRating.CompareTo(a.Ratings.TradeTrueRating));

        // Assign IDs starting from max existing ID + 1
        int nextId = GetMaxPlayerId(league) + 1;

        foreach (var (round, pickNumber, pickingTeamIndex, originalTeamIndex) in draftOrder)
        {
            if (available.Count == 0) break;
            if (pickingTeamIndex < 0 || pickingTeamIndex >= league.Teams.Count) continue;

            var team = league.Teams[pickingTeamIndex];
            int selectedIdx = GetComputerSelection(available, team, avgTradeTru, random);
            if (selectedIdx < 0 || selectedIdx >= available.Count) continue;

            var rookie = available[selectedIdx];

            // Mark draft info
            rookie.Id = nextId++;
            rookie.RoundSelected = round + 1; // 1-based round for display
            rookie.PickSelected = pickNumber;
            rookie.TeamIndex = pickingTeamIndex;
            rookie.Team = team.Name;
            rookie.Contract.IsRookie = true;
            rookie.Contract.IsFreeAgent = false;
            rookie.Contract.Signed = true;
            rookie.Contract.CurrentTeam = pickingTeamIndex;
            rookie.Contract.PreviousTeam = pickingTeamIndex;
            rookie.Contract.YearsOnTeam = 0;
            rookie.Contract.YearsOfService = 0;
            rookie.Content = 5;
            rookie.Active = true;

            // Generate rookie contract
            GenerateRookieContract(rookie, random);

            // Generate preference factors
            ContractService.GeneratePreferenceFactors(rookie, random);

            // Add to team roster
            team.Roster.Add(rookie);

            // Record selection
            result.Selections.Add(new DraftSelection
            {
                Round = round + 1,
                Pick = pickNumber,
                TeamIndex = pickingTeamIndex,
                TeamName = team.Name,
                PlayerName = rookie.Name,
                Position = rookie.Position
            });

            // Remove from available pool
            available.RemoveAt(selectedIdx);
        }

        result.TotalPicks = result.Selections.Count;

        // Undrafted rookies: mark as free agents
        foreach (var undrafted in available)
        {
            undrafted.Id = nextId++;
            undrafted.Contract.IsFreeAgent = true;
            undrafted.Contract.CurrentTeam = -1;
            undrafted.Contract.Signed = false;
            undrafted.Active = true;
            ContractService.GeneratePreferenceFactors(undrafted, random);
        }
        result.UndraftedCount = available.Count;

        // Keep undrafted in DraftPool for FA to pick up; remove drafted ones
        league.DraftPool.Rookies = available;
        league.LastDraftResult = result;

        return result;
    }

    /// <summary>
    /// Generates a rookie-scale contract.
    /// Years: 3 (standard rookie contract), salary based on TradeTrueRating capped by rookie scale.
    /// </summary>
    private static void GenerateRookieContract(Player rookie, Random random)
    {
        var contract = rookie.Contract;

        double money = rookie.Ratings.TradeTrueRating * 98;
        money *= 0.8 + random.Next(1, 41) / 100.0;

        // Apply minimum salary
        if (money < LeagueConstants.SalaryMinimumByYos[0])
            money = LeagueConstants.SalaryMinimumByYos[0];

        // Apply rookie scale cap
        if (money > LeagueConstants.RookieSalaryCapYear1)
            money = LeagueConstants.RookieSalaryCapYear1;

        int currentMoney = (int)money;
        contract.ContractYears = 3;
        contract.CurrentContractYear = 1;

        // Fill salaries with small raises
        int totalSalary = 0;
        for (int y = 0; y < 3; y++)
        {
            contract.ContractSalaries[y] = currentMoney;
            totalSalary += currentMoney;
            currentMoney = (int)(currentMoney * 1.05); // 5% annual raise
        }

        contract.TotalSalary = totalSalary;
        contract.CurrentYearSalary = contract.ContractSalaries[0];
        contract.RemainingSalary = totalSalary;
        contract.IsFreeAgent = false;
    }

    private static double CalculateLeagueAvgTradeTru(League league)
    {
        double total = 0;
        int count = 0;
        foreach (var team in league.Teams)
        {
            foreach (var player in team.Roster)
            {
                if (string.IsNullOrEmpty(player.Name)) continue;
                if (player.Ratings.TradeTrueRating > 0)
                {
                    total += player.Ratings.TradeTrueRating;
                    count++;
                }
            }
        }
        return count > 0 ? total / count : 5.0;
    }

    private static int GetMaxPlayerId(League league)
    {
        int maxId = 0;
        foreach (var team in league.Teams)
            foreach (var player in team.Roster)
                if (player.Id > maxId) maxId = player.Id;
        return maxId;
    }

    private static int PositionToIndex(string position)
    {
        return position?.Trim() switch
        {
            "PG" => 1,
            "SG" => 2,
            "SF" => 3,
            "PF" => 4,
            "C" => 5,
            _ => 0
        };
    }
}
