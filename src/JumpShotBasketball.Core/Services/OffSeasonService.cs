using JumpShotBasketball.Core.Models.History;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Off-season pipeline orchestrator.
/// Transitions a completed season into the starting state for the next season.
/// </summary>
public static class OffSeasonService
{
    /// <summary>
    /// Runs the full off-season pipeline.
    /// Pipeline: heal injuries → clear rookie flags → archive stats → projection ratings →
    /// retirements → draft chart rollover → generate rookies → execute draft → free agency →
    /// player development → reset season → increment year →
    /// apply start-season attributes → set rotations → return result.
    /// </summary>
    public static OffSeasonResult AdvanceSeason(League league, Random? random = null)
    {
        random ??= Random.Shared;

        int previousYear = league.Settings.CurrentYear;

        // 1. Heal remaining injuries (~120 days off-season)
        InjuryService.HealInjuries(league, 120, random);

        // 1b. Generate off-season injuries (players can get hurt during off-season)
        int offSeasonInjuries = 0;
        if (league.Settings.InjuriesEnabled)
            offSeasonInjuries = InjuryService.ProcessOffSeasonInjuries(league, random);

        // 2. Clear rookie flags
        ClearRookieFlags(league);

        // 3. Archive career stats for all players
        foreach (var team in league.Teams)
        {
            foreach (var player in team.Roster)
            {
                if (string.IsNullOrEmpty(player.Name)) continue;
                if (player.SimulatedStats.Games > 0)
                    PlayerDevelopmentService.ArchiveSeasonStats(player, previousYear);
            }
        }

        // 3a. Update record book season and career records
        if (league.RecordBook != null)
        {
            RecordBookService.UpdateSeasonRecords(league.RecordBook, league, previousYear);
            RecordBookService.UpdateCareerRecords(league.RecordBook, league, previousYear);
        }

        // 4. Compute league highs → projection ratings for all players
        var highs = PlayerDevelopmentService.CalculateLeagueHighs(league);
        foreach (var team in league.Teams)
        {
            foreach (var player in team.Roster)
            {
                if (string.IsNullOrEmpty(player.Name)) continue;
                if (player.SimulatedStats.Games > 0)
                    PlayerDevelopmentService.CalculateProjectionRatings(player, highs);
            }
        }

        // 4a. Apply scout adjustments to projection ratings
        ScoutingService.ApplyAllScoutAdjustments(league);

        // 5. Process retirements
        var retiredNames = RetirementService.ProcessRetirements(league, random);

        // 5-hof. Evaluate retired players for Hall of Fame
        var hofInductees = HallOfFameService.EvaluateRetiredPlayersForHallOfFame(league);
        league.HallOfFame.AddRange(hofInductees);

        // 5-staff. Run staff lifecycle (after player retirements, before draft)
        StaffManagementResult? staffResult = null;
        if (league.StaffPool.Count > 0)
        {
            StaffManagementService.UpdateStaffPerformance(league);
            staffResult = StaffManagementService.RunStaffLifecycle(league, random);
        }

        // 5a. Roll draft chart forward (consume current year, shift future years)
        if (league.DraftBoard != null)
            DraftService.RollDraftChartForward(league.DraftBoard);

        // 5b. Generate rookie pool from benchmark data
        int rookiesGenerated = 0;
        if (league.Teams.Count > 0)
        {
            var rookiePool = RookieGenerationService.CreateRookies(league, league.Teams.Count * 3, random);
            league.DraftPool = rookiePool;
            rookiesGenerated = rookiePool.Rookies.Count;
        }

        // 5c. Execute draft (place rookies on team rosters)
        DraftResult? draftResult = null;
        if (league.DraftPool != null && league.DraftPool.Rookies.Count > 0)
        {
            draftResult = DraftExecutionService.ExecuteDraft(league, random);
        }

        // 5d. Run free agency period (sign expired-contract players + undrafted rookies)
        FreeAgencyResult? faResult = null;
        if (league.Settings.FreeAgencyEnabled)
        {
            faResult = FreeAgencyService.RunFreeAgencyPeriod(league, 15, random);
        }

        // 5d-b. Clear in-season free agent pool (absorbed into FA process)
        league.FreeAgentPool.Clear();

        // 5e. Off-season trading window (after FA period)
        if (league.Settings.ComputerTradesEnabled)
        {
            TradeService.RunTradingPeriod(league, 50, 5, random);
        }

        // 5f. Sort rosters after draft, FA, and trades
        RosterSortingService.SortAllTeamRosters(league);

        // 6. Apply player development (aging/improvement/decline)
        PlayerDevelopmentService.DevelopPlayers(league, random);

        // 6b. Recompute per-48 stats from developed SimulatedStats and
        // copy to SeasonStats for engine eligibility (SeasonStats.Minutes >= 72)
        foreach (var team in league.Teams)
        {
            foreach (var player in team.Roster)
            {
                if (string.IsNullOrEmpty(player.Name)) continue;
                if (player.SimulatedStats.Minutes <= 0 || player.SimulatedStats.Games <= 0) continue;

                // Copy developed stats → SeasonStats (engine reads SeasonStats for eligibility)
                CopyStatLine(player.SimulatedStats, player.SeasonStats);

                // Recompute per-48 from developed stats
                StatisticsCalculator.CalculatePer48Stats(player, player.SimulatedStats);
            }
        }

        // 6c. Calculate teammate chemistry ratings (Better)
        TeamChemistryService.CalculateBetterForLeague(league, seasonStarted: false);

        // 6a. Archive franchise season records
        FranchiseHistoryService.ArchiveSeasonRecords(league, previousYear);

        // 7. Reset season state
        ResetSeasonState(league);

        // 8. Increment year
        league.Settings.CurrentYear = previousYear + 1;

        // 9. Apply start-season attributes
        int contractsExpired = 0;
        int newFreeAgents = 0;
        ApplyStartSeasonAttributes(league, true, ref contractsExpired, ref newFreeAgents, random);

        // 9b. Apply coach adjustments to true ratings and trade values
        ScoutingService.ApplyAllCoachAdjustments(league);

        // 9a. End-of-season financial adjustments
        if (league.Settings.FinancialEnabled)
        {
            FinancialSimulationService.ProcessEndOfSeasonFinancials(league, random);
        }

        // 10. Set computer rotations
        RotationService.SetComputerRotations(league);

        // 10b. Final roster sort by position and value
        RosterSortingService.SortAllTeamRosters(league);

        return new OffSeasonResult
        {
            PreviousYear = previousYear,
            NewYear = league.Settings.CurrentYear,
            RetiredPlayerNames = retiredNames,
            PlayersRetired = retiredNames.Count,
            ContractsExpired = contractsExpired,
            NewFreeAgents = newFreeAgents,
            RookiesGenerated = rookiesGenerated,
            DraftResult = draftResult,
            FreeAgencyResult = faResult,
            StaffResult = staffResult,
            OffSeasonInjuries = offSeasonInjuries
        };
    }

    /// <summary>
    /// Resets all season-level state to prepare for a new season.
    /// </summary>
    public static void ResetSeasonState(League league)
    {
        // Clear schedule
        league.Schedule.SeasonStarted = false;
        league.Schedule.RegularSeasonEnded = false;
        league.Schedule.PlayoffsStarted = false;
        league.Schedule.Games.Clear();

        // Clear transactions
        league.Transactions.Clear();
        league.Settings.NumberOfTransactions = 0;

        // Archive awards before clearing
        if (league.Awards != null)
            league.AwardsHistory.Add(league.Awards);

        // Archive all-star weekend data before clearing
        if (league.AllStarWeekend != null)
            league.AllStarWeekendHistory.Add(league.AllStarWeekend);

        // Clear bracket, awards, leaderboard, all-star
        league.Bracket = null;
        league.Awards = null;
        league.Leaderboard = null;
        league.AllStarWeekend = null;

        // Clear season single-game records (preserves all-time season/career)
        if (league.RecordBook != null)
            RecordBookService.ClearSeasonGameHighs(league.RecordBook);

        // Clear player season stats and flags
        foreach (var team in league.Teams)
        {
            foreach (var player in team.Roster)
            {
                player.SimulatedStats.Reset();
                player.PlayoffStats.Reset();
                player.SeasonHighs.SeasonPoints = 0;
                player.SeasonHighs.SeasonRebounds = 0;
                player.SeasonHighs.SeasonAssists = 0;
                player.SeasonHighs.SeasonSteals = 0;
                player.SeasonHighs.SeasonBlocks = 0;
                player.SeasonHighs.SeasonDoubleDoubles = 0;
                player.SeasonHighs.SeasonTripleDoubles = 0;
                player.SeasonHighs.PlayoffPoints = 0;
                player.SeasonHighs.PlayoffRebounds = 0;
                player.SeasonHighs.PlayoffAssists = 0;
                player.SeasonHighs.PlayoffSteals = 0;
                player.SeasonHighs.PlayoffBlocks = 0;

                // Clear all-star/contest flags
                player.AllStar = 0;
                player.RookieStar = 0;
                player.ThreePointContest = 0;
                player.DunkContest = 0;
                player.AllStarIndex = 0;
                player.RookieStarIndex = 0;
                player.ThreePointContestIndex = 0;
                player.DunkContestIndex = 0;
                player.ThreePointScores = new int[4];
                player.DunkScores = new int[4];
            }

            // Clear financial data
            if (league.Settings?.FinancialEnabled == true)
            {
                FinancialSimulationService.ClearBudgetData(team.Financial);
            }

            // Reset salary cap exception flags
            team.Financial.MidLevelExceptionUsed = false;
            team.Financial.MidLevelExceptionOffered = -1;
            team.Financial.MillionDollarExceptionUsed = false;
            team.Financial.MillionDollarExceptionOffered = -1;

            // Reset team records
            team.Record.Wins = 0;
            team.Record.Losses = 0;
        }
    }

    /// <summary>
    /// Applies start-of-season attributes to all players.
    /// Port of CAverage::SetStartSeasonAttributes() — Average.cpp:735-831.
    /// Bug fix: C++ line 800 adds player index 'i' instead of salary value; fixed.
    /// </summary>
    public static void ApplyStartSeasonAttributes(League league, bool incrementAge,
        ref int contractsExpired, ref int newFreeAgents, Random? random = null)
    {
        random ??= Random.Shared;
        int gamesInSeason = league.Schedule.GamesInSeason > 0 ? league.Schedule.GamesInSeason : 82;

        foreach (var team in league.Teams)
        {
            for (int p = 0; p < team.Roster.Count; p++)
            {
                var player = team.Roster[p];
                var contract = player.Contract;

                // Calculate injury rating from previous season stats
                var sim = player.SimulatedStats;
                if (sim.Games > 0)
                {
                    int fga2pt = sim.FieldGoalsAttempted - sim.ThreePointersAttempted;
                    player.Ratings.InjuryRating = PlayerDevelopmentService.CalculateInjuryRating(
                        sim.Games, sim.Minutes, fga2pt, sim.FreeThrowsAttempted,
                        sim.Turnovers, 1, gamesInSeason);
                }

                // Clear all-star contest flags
                player.DunkContest = 0;
                player.ThreePointContest = 0;

                // Team paying info
                contract.TeamPaying = team.Id;
                contract.TeamPayingName = team.Name;

                // Increment years
                if (incrementAge)
                {
                    contract.YearsOnTeam++;
                    contract.YearsOfService++;
                }

                // Set original number
                player.OriginalNumber = p;

                player.Active = true;
                player.Contract.JustSigned = false;

                // Contract expiry check → free agent
                if (contract.ContractYears == contract.CurrentContractYear)
                {
                    contract.IsFreeAgent = true;
                    contractsExpired++;
                    newFreeAgents++;
                }

                // Bird player eligibility
                contract.IsBirdPlayer = contract.YearsOnTeam >= 3;

                // Remaining salary calculation (Bug fix: use salary, not index)
                int remainingSalary = 0;
                for (int y = 0; y < contract.ContractYears && y < contract.ContractSalaries.Length; y++)
                {
                    if (y >= contract.CurrentContractYear - 1)
                        remainingSalary += contract.ContractSalaries[y];
                }
                contract.RemainingSalary = remainingSalary;
                contract.CurrentYearSalary = contract.CurrentContractYear >= 1
                    && contract.CurrentContractYear - 1 < contract.ContractSalaries.Length
                    ? contract.ContractSalaries[contract.CurrentContractYear - 1]
                    : 0;

                if (remainingSalary == 0)
                {
                    contract.IsFreeAgent = true;
                    contract.CurrentContractYear = 0;
                    contract.ContractYears = 0;
                    newFreeAgents++;
                }

                // Contentment: compare pay vs trade value
                double tradeValue = player.Ratings.TradeTrueRating;
                double moneyValue = tradeValue * 100;
                double paid = contract.CurrentYearSalary;
                double f = moneyValue > 0 ? paid / moneyValue : 0;
                double r = random.NextDouble() * (contract.LoyaltyFactor + 1);

                if (player.Content == 0) player.Content = 5;

                if (r < f && player.Content < 9)
                    player.Content++;
                else if (r > f && r < 2 && player.Content > 1)
                    player.Content--;

                // Health reset
                player.Health = 100;
                int healing = 120 - player.Ratings.Effort * 5;
                player.Injury = Math.Max(0, player.Injury - healing);
                if (player.Injury <= 0)
                {
                    player.Injury = 0;
                    player.Health = 100;
                }
            }
        }
    }

    private static void ClearRookieFlags(League league)
    {
        foreach (var team in league.Teams)
        {
            foreach (var player in team.Roster)
            {
                player.Contract.IsRookie = false;
            }
        }
    }

    /// <summary>
    /// Copies all 15 stat fields from source to destination.
    /// </summary>
    internal static void CopyStatLine(PlayerStatLine source, PlayerStatLine dest)
    {
        dest.Games = source.Games;
        dest.Minutes = source.Minutes;
        dest.FieldGoalsMade = source.FieldGoalsMade;
        dest.FieldGoalsAttempted = source.FieldGoalsAttempted;
        dest.FreeThrowsMade = source.FreeThrowsMade;
        dest.FreeThrowsAttempted = source.FreeThrowsAttempted;
        dest.ThreePointersMade = source.ThreePointersMade;
        dest.ThreePointersAttempted = source.ThreePointersAttempted;
        dest.OffensiveRebounds = source.OffensiveRebounds;
        dest.Rebounds = source.Rebounds;
        dest.Assists = source.Assists;
        dest.Steals = source.Steals;
        dest.Turnovers = source.Turnovers;
        dest.Blocks = source.Blocks;
        dest.PersonalFouls = source.PersonalFouls;
    }
}
