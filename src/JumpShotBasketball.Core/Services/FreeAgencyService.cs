using JumpShotBasketball.Core.Constants;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Multi-stage free agency orchestrator combining team AI and player decision logic.
/// Port of FreeAgentDlg.cpp (stage loop), HireStaff.cpp (team offers), Player.h (player decisions).
/// </summary>
public static class FreeAgencyService
{
    // ── Player-Side Methods ─────────────────────────────────────────

    /// <summary>
    /// Calculates what salary each team must offer to attract this player.
    /// Port of CPlayer::SetRequests() — Player.h:3572-3776.
    /// Applies 5 preference factors: winning, tradition, loyalty, coach quality, playing time.
    /// </summary>
    public static void CalculatePlayerSalaryRequests(
        Player player,
        League league,
        FreeAgencyState state,
        Random random)
    {
        var contract = player.Contract;
        double tradeValue = player.Ratings.TradeTrueRating;

        // Use sim rating if better
        if (player.SimulatedStats.Games > 4)
        {
            double simValue = player.Ratings.TrueRatingSimple;
            if (simValue > tradeValue) tradeValue = simValue;
        }

        // Bird player eligibility
        if (contract.YearsOnTeam >= 3)
            contract.IsBirdPlayer = true;

        // Base salary from trade value
        double money = tradeValue * 100;
        money *= 0.875 + random.Next(0, 26) / 100.0;
        if (money < contract.CurrentYearSalary) money = contract.CurrentYearSalary;

        // Apply max salary by YOS
        int yos = contract.YearsOfService;
        int yosIdx = Math.Min(yos, 10);

        if (money > LeagueConstants.SalaryMaximumByYos[10] && yos >= 10)
            money = LeagueConstants.SalaryMaximumByYos[10];
        else if (money > LeagueConstants.SalaryMaximumByYos[7] && yos >= 7)
            money = LeagueConstants.SalaryMaximumByYos[7];
        else if (money > LeagueConstants.SalaryMaximumByYos[0])
            money = LeagueConstants.SalaryMaximumByYos[0];

        // Apply minimum salary by YOS
        if (money < LeagueConstants.SalaryMinimumByYos[yosIdx] || yos == 0)
            money = LeagueConstants.SalaryMinimumByYos[yosIdx];

        // Contract years wanted (based on age)
        int contractMax = 6;
        if (player.Age > 28) contractMax = 35 - player.Age;
        if (contractMax > 6) contractMax = 6;
        if (contractMax < 2) contractMax = 2;

        int currentMoney = (int)money;
        contract.QualifyingSalaries[0] = currentMoney;
        int totalSalary = currentMoney;

        // Years calculation using security factor
        double dMpy = money / 100.0;
        int mpy = (int)dMpy;
        int oMpy = mpy + 1;
        mpy = mpy - 3 + contract.SecurityFactor;
        if (mpy < 1) mpy = 1;

        int contractYears = 1 + random.Next(0, mpy);
        if (contractYears < 1) contractYears = 1;
        if (contractYears > 6) contractYears = 6;
        if (contractYears > contractMax) contractYears = contractMax;
        if (contractYears > oMpy) contractYears = oMpy;

        // Multi-year: add 10% annual raises
        if (contractYears > 1)
        {
            double inc = currentMoney * 0.10;
            for (int j = 1; j < contractYears; j++)
            {
                currentMoney = (int)(currentMoney + inc);
                if (j < LeagueConstants.MaxContractYears)
                    contract.QualifyingSalaries[j] = currentMoney;
                totalSalary += currentMoney;
            }
        }

        contract.TotalSalarySeeking = totalSalary;
        contract.QualifyingOfferYears = contractYears;
        double avgMoney = (double)totalSalary / contractYears;

        // Per-team salary requests based on preference factors
        int numTeams = league.Teams.Count;
        for (int t = 0; t < numTeams; t++)
        {
            var team = league.Teams[t];
            var record = team.Record;

            double wPct = record.LeaguePercentage;
            if (record.Losses == 0 && team.Financial.SeasonPayroll == 0)
                wPct = 0.1; // expansion team

            // Winning factor: discount for good teams
            double factor;
            double winFactor = (0.5 - wPct) * contract.WinningFactor / 8.0;
            if (record.Losses == 0 && team.Financial.SeasonPayroll > 0)
                factor = 1;
            else
                factor = 1 + winFactor;

            double var1 = avgMoney * (0.875 + random.Next(0, 26) / 100.0);
            double salaryRequest = var1 * factor;

            // Tradition factor: franchise history (use team wins/losses as proxy)
            double franchiseWPct = record.Wins + record.Losses > 0
                ? (double)record.Wins / (record.Wins + record.Losses)
                : 0.5;
            winFactor = (0.5 - franchiseWPct) * contract.TraditionFactor / 8.0;
            factor = 1 + winFactor;
            salaryRequest *= factor;

            // Loyalty factor: discount for returning to previous team
            factor = 1;
            int loyal = contract.LoyaltyFactor;
            if (contract.YearsOnTeam < 3) loyal *= 2;
            double loy = loyal * 3.0 / 1000.0;
            if (contract.PreviousTeam == t) factor = 1 - loy;
            salaryRequest *= factor;

            // Coach factor: composite coach rating
            if (team.Coach != null)
            {
                var coach = team.Coach;
                int coachRating1 = (coach.Pot1Rating + coach.Pot2Rating + coach.EffortRating +
                    coach.ScoringRating + coach.ShootingRating + coach.ReboundingRating +
                    coach.PassingRating + coach.DefenseRating);
                int coachRating2 = (coach.CoachPot1 + coach.CoachPot2 + coach.CoachEffort +
                    coach.CoachScoring + coach.CoachShooting + coach.CoachRebounding +
                    coach.CoachPassing + coach.CoachDefense) * 3;
                int coachRating3 = (coach.CoachOutside + coach.CoachPenetration +
                    coach.CoachInside + coach.CoachFastbreak + coach.CoachOutsideDefense +
                    coach.CoachPenetrationDefense + coach.CoachInsideDefense +
                    coach.CoachFastbreakDefense) * 3;
                int coachRating4 = coach.Personality * 24;
                int r = coachRating1 + coachRating2 + coachRating3 + coachRating4;
                factor = (200 - (99 + r / 200.0)) / 100.0;
                salaryRequest *= factor;
            }

            // Playing time factor: positional opportunity vs league avg
            int posIdx = PositionToIndex(player.Position);
            double pvFactor = 0;
            if (posIdx >= 1 && posIdx <= 5)
            {
                double leagueAvg = state.LeagueAvgPositionValues[posIdx];
                double teamVal = state.PositionValues[t, posIdx];

                if (leagueAvg > 0 && teamVal > leagueAvg)
                    pvFactor = teamVal / leagueAvg;
                else if (teamVal > 0 && teamVal <= leagueAvg)
                    pvFactor = -(leagueAvg / teamVal);
            }

            if (pvFactor > 4) pvFactor = 4;
            else if (pvFactor < -4) pvFactor = -4;

            pvFactor = (94 + contract.PlayingTimeFactor * 2) / 100.0 * (1 + pvFactor / 10.0);

            if (string.IsNullOrEmpty(record.TeamName)) pvFactor = 0;

            salaryRequest *= pvFactor;

            // Previous team discount
            if (contract.PreviousTeam == t) salaryRequest *= 0.99;

            // General discount
            salaryRequest *= 0.99;

            contract.Seeking[t] = (int)salaryRequest;
        }
    }

    /// <summary>
    /// Evaluates all pending offers and selects the best team.
    /// Port of CPlayer::ChooseBestContract() — Player.h:3901-3946.
    /// </summary>
    public static int ChooseBestContract(Player player, int[] rosterCounts, int targetRosterSize)
    {
        var contract = player.Contract;
        int best = -1;

        double requestedSalaryAvg = contract.QualifyingOfferYears > 0
            ? (double)contract.TotalSalarySeeking / contract.QualifyingOfferYears
            : 0;

        double hiDiff = 0;

        for (int t = 0; t < rosterCounts.Length; t++)
        {
            if (rosterCounts[t] >= targetRosterSize || contract.Seeking[t] == 0) continue;

            int offeredYears = contract.YearOffer[t];
            if (offeredYears == 0) continue;

            double offeredSalaryAvg = (double)contract.TotalSalaryOffer[t] / offeredYears;

            // Check if offered years are acceptable based on salary ratio
            double value = requestedSalaryAvg * (0.7 + (6 - contract.SecurityFactor) / 10.0);
            double tr = value > 0 ? offeredSalaryAvg / value : 1;

            double maxYears = 6 * tr;
            int iMaxYears = (int)maxYears;
            double rem = maxYears - iMaxYears;
            if (rem >= 0.5) iMaxYears++;
            if (iMaxYears < 1) iMaxYears = 1;

            if (offeredYears > iMaxYears && contract.QualifyingOfferYears != offeredYears) continue;

            // Score: salary ratio weighted by security preference
            double secFactor = (100 + contract.SecurityFactor * (double)offeredYears / 2) / 100.0;
            double seekingVal = contract.Seeking[t] > 0 ? contract.Seeking[t] : 1;
            double diff = offeredSalaryAvg * secFactor / (requestedSalaryAvg > 0 ? requestedSalaryAvg : 1)
                          / seekingVal;

            if (diff > hiDiff &&
                (!contract.IsRookie || (contract.IsRookie && player.RoundSelected == 0)))
            {
                hiDiff = diff;
                best = t;
            }
        }

        // Drafted rookies auto-sign with their team
        if (player.RoundSelected > 0 && contract.IsRookie)
            best = contract.CurrentTeam;

        contract.BestContract = best;
        return best;
    }

    /// <summary>
    /// Determines if a player will accept the best offer this stage.
    /// Port of CPlayer::AcceptOffer() — Player.h:4160-4237.
    /// </summary>
    public static bool AcceptOffer(
        Player player,
        int stage,
        int[] teamPayrolls,
        int numTeams,
        int salaryCap,
        double avgTradeTru,
        Random random)
    {
        var contract = player.Contract;
        int bestOffer = contract.BestContract;
        if (bestOffer < 0 || bestOffer >= numTeams) return false;
        if (!contract.TopOffer[bestOffer]) return false;
        if (contract.Signed) return false;
        if (string.IsNullOrEmpty(player.Name)) return false;

        bool accept = false;

        int offer1 = contract.SalaryOffer[bestOffer].Length > 0
            ? contract.SalaryOffer[bestOffer][0]
            : 0;

        // Count teams with room to offer competitive deals
        int possible = 0;
        double acceptFactor = Math.Min(contract.AcceptFactor * 2, 1.0);
        var adjustedPayrolls = (int[])teamPayrolls.Clone();

        for (int i = 0; i < Math.Min(numTeams, adjustedPayrolls.Length); i++)
        {
            if (i < contract.SalaryOffer.Length && contract.SalaryOffer[i].Length > 0)
                adjustedPayrolls[i] -= contract.SalaryOffer[i][0];

            int room = salaryCap - adjustedPayrolls[i];
            double f = Math.Max(0, 1.0 - acceptFactor);
            double want = contract.Seeking[i] * f;

            if (((room - want) > 0 || i == contract.PreviousTeam) && offer1 < want)
                possible++;
        }

        // Flexibility formula: more options → more holdout
        double tradeVal = player.Ratings.TradeValue;
        if (tradeVal <= 0) tradeVal = 1;
        double avgTru = avgTradeTru > 0 ? avgTradeTru : 1;
        double flexibility = ((double)possible / numTeams * 10 + (10 - acceptFactor * 10))
                             * tradeVal / avgTru;

        // Primary acceptance check
        double minF = Math.Max(0, contract.AcceptFactor * 1.1);
        int yr = Math.Max(1, 7 - stage);
        int totalOfferedBest = 0;
        for (int y = 0; y < yr && y < contract.SalaryOffer[bestOffer].Length; y++)
            totalOfferedBest += contract.SalaryOffer[bestOffer][y];

        double minMoneyToAccept = contract.Seeking[bestOffer] * (1 - minF) * 1.05 * yr;

        double r = random.NextDouble() * 20;
        if (r > flexibility && totalOfferedBest >= minMoneyToAccept)
            accept = true;

        // Secondary check: favorite teams at lower threshold
        minF = Math.Max(0, contract.AcceptFactor * 1.125);
        minMoneyToAccept = contract.Seeking[bestOffer] * (1 - minF) * 1.05 * yr;

        var favorites = RankFavoriteTeams(player, numTeams);
        int checkCount = Math.Min(stage, numTeams);
        for (int i = 0; i < checkCount; i++)
        {
            if (favorites[i] == bestOffer && totalOfferedBest >= minMoneyToAccept)
                accept = true;
        }

        return accept;
    }

    /// <summary>
    /// Finalizes a contract signing.
    /// Port of CPlayer::InkedNewDeal() — Player.h:225-248.
    /// </summary>
    public static void FinalizeContract(Player player, int teamIndex)
    {
        var contract = player.Contract;

        contract.JustSigned = true;
        for (int y = 0; y < LeagueConstants.MaxContractYears; y++)
            contract.ContractSalaries[y] = 0;

        int offeredYears = contract.YearOffer[teamIndex];
        contract.ContractYears = offeredYears;
        contract.CurrentContractYear = 0;
        contract.IsFreeAgent = false;
        contract.Signed = true;

        int sal = 0;
        for (int y = 0; y < offeredYears && y < LeagueConstants.MaxContractYears; y++)
        {
            contract.ContractSalaries[y] = contract.SalaryOffer[teamIndex][y];
            sal += contract.ContractSalaries[y];
        }

        contract.RemainingSalary = sal;
        contract.TotalSalary = sal;
        contract.CurrentTeam = teamIndex;
    }

    // ── Team-Side Methods ───────────────────────────────────────────

    /// <summary>
    /// Calculates total trade true rating for each position on a team (non-FA players only).
    /// Port of CHireStaff::SetPositionValues() — HireStaff.cpp:358-386.
    /// </summary>
    public static double[] CalculatePositionValues(Models.Team.Team team)
    {
        double[] values = new double[6]; // [1]=PG, [2]=SG, [3]=SF, [4]=PF, [5]=C

        foreach (var player in team.Roster)
        {
            if (string.IsNullOrEmpty(player.Name)) continue;
            if (player.Contract.IsFreeAgent) continue;

            int posIdx = PositionToIndex(player.Position);
            if (posIdx < 1) continue;

            double r = player.Ratings.TradeTrueRating;
            if (r < 0) r = 0;
            values[posIdx] += r;
        }

        return values;
    }

    /// <summary>
    /// Generates contract offers from a team to the best available free agents.
    /// Port of CHireStaff::SetTopFreeAgent() — HireStaff.cpp:421-618.
    /// Finds FAs by position need differential and generates contract offers.
    /// </summary>
    public static void GenerateTeamOffers(
        League league,
        int teamIndex,
        FreeAgencyState state,
        int stage,
        List<Player> freeAgents,
        Random random)
    {
        if (teamIndex < 0 || teamIndex >= league.Teams.Count) return;
        var team = league.Teams[teamIndex];

        int offersThisRound = 0;
        int signedPlayers = state.SignedCount[teamIndex];
        int salaryCap = (int)league.Settings.SalaryCap;
        int ownerCap = (int)team.Financial.OwnerSalaryCap;
        if (ownerCap <= 0) ownerCap = salaryCap * 10;

        var alreadyOffered = new HashSet<int>();

        while (signedPlayers + offersThisRound <= 14)
        {
            double hiDiff = -65535;
            int hiFaIdx = -1;
            int hiYears = 0;
            int[] hiSalaries = new int[LeagueConstants.MaxContractYears];
            int hiTotalSalary = 0;

            for (int i = 0; i < freeAgents.Count; i++)
            {
                if (alreadyOffered.Contains(i)) continue;

                var fa = freeAgents[i];
                if (string.IsNullOrEmpty(fa.Name)) continue;
                if (!fa.Contract.IsFreeAgent) continue;
                if (fa.Contract.Signed) continue;
                if (fa.Retired) continue;

                int posIdx = PositionToIndex(fa.Position);
                if (posIdx < 1) continue;

                double tradeTru = fa.Ratings.TradeTrueRating;
                double diff = tradeTru - state.PositionValues[teamIndex, posIdx];

                // Generate a trial offer
                var (years, salaries, totalSalary) = GenerateContractOffer(
                    fa, stage, state.TempPayroll[teamIndex], salaryCap, ownerCap, random);

                // Bird rights bonus: returning bird-eligible player
                int projPayroll = state.TempPayroll[teamIndex] + (salaries.Length > 0 ? salaries[0] : 0);
                if (projPayroll * 10 < ownerCap && projPayroll > salaryCap &&
                    fa.Contract.IsBirdPlayer &&
                    state.TempPayroll[teamIndex] * 10 < (ownerCap - LeagueConstants.MidLevelExceptionMinimum) &&
                    fa.Contract.PreviousTeam == teamIndex)
                {
                    diff += tradeTru;
                }

                if (diff > hiDiff)
                {
                    hiDiff = diff;
                    hiFaIdx = i;
                    hiYears = years;
                    hiSalaries = salaries;
                    hiTotalSalary = totalSalary;
                }
            }

            if (hiFaIdx < 0) break;

            // Store the offer on the player's contract
            var bestFa = freeAgents[hiFaIdx];
            var fc = bestFa.Contract;

            fc.TopOffer[teamIndex] = true;
            fc.YearOffer[teamIndex] = hiYears;
            fc.TotalSalaryOffer[teamIndex] = hiTotalSalary;
            for (int y = 0; y < hiYears && y < LeagueConstants.MaxContractYears; y++)
                fc.SalaryOffer[teamIndex][y] = hiSalaries[y];

            // Accumulate payroll
            if (hiSalaries.Length > 0)
                state.TempPayroll[teamIndex] += hiSalaries[0];

            alreadyOffered.Add(hiFaIdx);
            offersThisRound++;
        }
    }

    /// <summary>
    /// Generates a contract offer for a specific free agent.
    /// Port of CHireStaff::SetTopFreeAgentContract() — HireStaff.cpp:623-770.
    /// </summary>
    public static (int Years, int[] Salaries, int TotalSalary) GenerateContractOffer(
        Player player,
        int stage,
        int teamPayroll,
        int salaryCap,
        int ownerCap,
        Random random)
    {
        double tradeValue = player.Ratings.TradeTrueRating;
        int yos = player.Contract.YearsOfService;
        int yosIdx = Math.Min(yos, 10);

        // Base salary from trade value with random variance
        double money = tradeValue * 100;
        if (money < 32) money = 32;
        bool maxRaise = false;
        money *= 0.9 + random.Next(0, 21) / 100.0;

        // Apply max salary by YOS
        if (money > LeagueConstants.SalaryMaximumByYos[10] && yos >= 10)
        {
            maxRaise = true;
            money = LeagueConstants.SalaryMaximumByYos[10];
        }
        else if (money > LeagueConstants.SalaryMaximumByYos[7] && yos >= 7)
        {
            maxRaise = true;
            money = LeagueConstants.SalaryMaximumByYos[7];
        }
        else if (money > LeagueConstants.SalaryMaximumByYos[0])
        {
            maxRaise = true;
            money = LeagueConstants.SalaryMaximumByYos[0];
        }

        // Contract years
        int contractMax = 6;
        int stageBonus = stage <= 3 ? 4 - stage : 0;
        if (player.Age > 28) contractMax = 35 - player.Age + stage;
        if (contractMax > 6) contractMax = 6;
        if (contractMax < 1) contractMax = 1;

        int contractYears = random.Next(1, contractMax + 1);
        if (contractYears == 1 && random.Next(1, 5) == 1) contractYears = 2;

        bool lower = false;

        // Cap room adjustments
        int projPayroll = teamPayroll + (int)money;
        if (projPayroll * 10 > ownerCap && teamPayroll < (salaryCap - 100))
        {
            lower = true;
            money = ownerCap / 10.0 - teamPayroll;
        }
        if (projPayroll > salaryCap && teamPayroll < (salaryCap - 100) &&
            !player.Contract.IsBirdPlayer)
        {
            lower = true;
            money = salaryCap - teamPayroll;
        }

        if (lower)
        {
            int f = (int)(money / 100);
            if (f > 5) f = 5;
            else if (f > 1) f = 1;
            if (f < 1) f = 1;
            contractYears = random.Next(1, f + 1);
        }

        if (money < LeagueConstants.SalaryMinimumByYos[yosIdx])
            money = LeagueConstants.SalaryMinimumByYos[yosIdx];

        int currentMoney = (int)money;
        var salaries = new int[LeagueConstants.MaxContractYears];
        salaries[0] = currentMoney;
        int totalSalary = currentMoney;

        // Multi-year: apply annual raises
        if (contractYears > 1)
        {
            int raise = random.Next(1, contractYears * 3 + 1);
            if (maxRaise) raise = 15;
            double dRaise = raise;
            if (dRaise > 12.5) dRaise = 12.5;
            if (!player.Contract.IsBirdPlayer && dRaise > 10) dRaise = 10;
            double inc = currentMoney * (dRaise / 100.0);

            for (int j = 1; j < contractYears; j++)
            {
                currentMoney = (int)(currentMoney + inc);
                salaries[j] = currentMoney;
                totalSalary += currentMoney;
            }
        }

        return (contractYears, salaries, totalSalary);
    }

    // ── Orchestrator ────────────────────────────────────────────────

    /// <summary>
    /// Runs the full free agency period (up to 12 stages).
    /// Port of CFreeAgentDlg stage loop — FreeAgentDlg.cpp:928-1368.
    /// </summary>
    public static FreeAgencyResult RunFreeAgencyPeriod(
        League league,
        int targetRosterSize = 15,
        Random? random = null)
    {
        random ??= Random.Shared;
        var result = new FreeAgencyResult();
        int numTeams = league.Teams.Count;
        if (numTeams == 0) return result;

        int salaryCap = (int)league.Settings.SalaryCap;
        double avgTradeTru = CalculateLeagueAvgTradeTru(league);

        // Initialize state
        var state = new FreeAgencyState();
        InitializeState(league, state);

        // Collect all free agents
        var freeAgents = CollectFreeAgents(league);
        if (freeAgents.Count == 0) return result;

        // Calculate player requests
        foreach (var fa in freeAgents)
        {
            fa.Contract.Signed = false;
            fa.Contract.AcceptFactor = 0;
            CalculatePlayerSalaryRequests(fa, league, state, random);
        }

        // Stage loop (up to 12 stages)
        for (int stage = 0; stage < 12; stage++)
        {
            state.Stage = stage;

            // Clear previous offers for all FAs
            foreach (var fa in freeAgents)
            {
                if (fa.Contract.Signed) continue;
                for (int t = 0; t < numTeams; t++)
                {
                    fa.Contract.TopOffer[t] = false;
                    fa.Contract.YearOffer[t] = 0;
                    fa.Contract.TotalSalaryOffer[t] = 0;
                    for (int y = 0; y < LeagueConstants.MaxContractYears; y++)
                        fa.Contract.SalaryOffer[t][y] = 0;
                }
            }

            // Reset temp payrolls to current payroll for this round
            for (int t = 0; t < numTeams; t++)
                state.TempPayroll[t] = CalculateTeamPayroll(league.Teams[t]);

            // Team AI: generate offers
            for (int t = 0; t < numTeams; t++)
            {
                var team = league.Teams[t];
                if (team.Record.Control == "Player") continue;

                GenerateTeamOffers(league, t, state, stage, freeAgents, random);
            }

            // Player decisions
            var payrolls = new int[numTeams];
            for (int t = 0; t < numTeams; t++)
                payrolls[t] = state.TempPayroll[t];

            foreach (var fa in freeAgents)
            {
                if (fa.Contract.Signed) continue;

                int bestTeam = ChooseBestContract(fa, state.RosterCount, targetRosterSize);

                if (bestTeam >= 0 && bestTeam < numTeams)
                {
                    bool accepted = AcceptOffer(fa, stage, payrolls, numTeams, salaryCap,
                        avgTradeTru, random);

                    if (accepted)
                    {
                        // Sign the player
                        FinalizeContract(fa, bestTeam);

                        // Move undrafted rookie to team roster if not already there
                        var team = league.Teams[bestTeam];
                        if (!team.Roster.Contains(fa))
                        {
                            fa.TeamIndex = bestTeam;
                            fa.Team = team.Name;
                            team.Roster.Add(fa);

                            // Remove from draft pool if applicable
                            league.DraftPool?.Rookies.Remove(fa);
                        }

                        fa.TeamIndex = bestTeam;
                        fa.Team = team.Name;

                        // Update state
                        state.RosterCount[bestTeam]++;
                        state.SignedCount[bestTeam]++;
                        if (fa.Contract.ContractSalaries.Length > 0)
                            state.TempPayroll[bestTeam] += fa.Contract.ContractSalaries[0];

                        // Recalculate position values for the signing team
                        var newPosValues = CalculatePositionValues(team);
                        for (int p = 1; p <= 5; p++)
                            state.PositionValues[bestTeam, p] = newPosValues[p];

                        result.PlayersSigned++;
                        result.SigningDescriptions.Add(
                            $"{fa.Position} {fa.Name} signed with {team.Name} ({fa.Contract.ContractYears}yr/${fa.Contract.TotalSalary})");
                    }
                }

                // Increase willingness each stage
                fa.Contract.AcceptFactor = Math.Min(fa.Contract.AcceptFactor + 0.1, 1.0);
            }

            result.StagesCompleted = stage + 1;

            // Check if all teams have enough players
            if (EndStage(state, numTeams, targetRosterSize, league))
                break;
        }

        return result;
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static void InitializeState(League league, FreeAgencyState state)
    {
        int numTeams = league.Teams.Count;
        double[] totalPv = new double[6];

        for (int t = 0; t < numTeams; t++)
        {
            var team = league.Teams[t];
            var posValues = CalculatePositionValues(team);

            for (int p = 1; p <= 5; p++)
            {
                state.PositionValues[t, p] = posValues[p];
                totalPv[p] += posValues[p];
            }

            state.RosterCount[t] = CountActivePlayers(team);
            state.TempPayroll[t] = CalculateTeamPayroll(team);
            state.SignedCount[t] = CountActivePlayers(team);
        }

        for (int p = 1; p <= 5; p++)
            state.LeagueAvgPositionValues[p] = numTeams > 0 ? totalPv[p] / numTeams : 0;
    }

    private static List<Player> CollectFreeAgents(League league)
    {
        var freeAgents = new List<Player>();

        // From team rosters
        foreach (var team in league.Teams)
        {
            foreach (var player in team.Roster)
            {
                if (string.IsNullOrEmpty(player.Name)) continue;
                if (player.Contract.IsFreeAgent && !player.Retired)
                    freeAgents.Add(player);
            }
        }

        // From draft pool (undrafted rookies)
        if (league.DraftPool != null)
        {
            foreach (var rookie in league.DraftPool.Rookies)
            {
                if (string.IsNullOrEmpty(rookie.Name)) continue;
                if (rookie.Contract.IsFreeAgent && !rookie.Retired)
                    freeAgents.Add(rookie);
            }
        }

        return freeAgents;
    }

    /// <summary>
    /// Ranks teams by seeking salary (lowest seeking = most favorable).
    /// Port of CPlayer::RankFavorites() — Player.cpp:1476-1508.
    /// Returns 0-indexed array where favorites[0] = most favorable team index.
    /// </summary>
    private static int[] RankFavoriteTeams(Player player, int numTeams)
    {
        var favorites = new int[numTeams];
        var used = new bool[numTeams];

        for (int rank = numTeams - 1; rank >= 0; rank--)
        {
            int hi = 0;
            int best = 0;
            for (int i = 0; i < numTeams; i++)
            {
                if (player.Contract.Seeking[i] >= hi && !used[i])
                {
                    best = i;
                    hi = player.Contract.Seeking[i];
                }
            }
            favorites[rank] = best;
            used[best] = true;
        }

        return favorites;
    }

    private static bool EndStage(FreeAgencyState state, int numTeams, int targetRosterSize, League league)
    {
        if (state.Stage >= 11) return true; // max 12 stages (0-indexed)

        // Update roster counts
        for (int t = 0; t < numTeams; t++)
            state.RosterCount[t] = CountActivePlayers(league.Teams[t]);

        for (int t = 0; t < numTeams; t++)
        {
            var team = league.Teams[t];
            if (team.Record.Control == "Player") continue;
            if (state.RosterCount[t] < targetRosterSize)
                return false;
        }

        return true;
    }

    private static int CountActivePlayers(Models.Team.Team team)
    {
        int count = 0;
        foreach (var p in team.Roster)
            if (!string.IsNullOrEmpty(p.Name) && !p.Contract.IsFreeAgent && !p.Retired)
                count++;
        return count;
    }

    private static int CalculateTeamPayroll(Models.Team.Team team)
    {
        int payroll = 0;
        foreach (var p in team.Roster)
        {
            if (string.IsNullOrEmpty(p.Name)) continue;
            if (p.Contract.IsFreeAgent) continue;
            payroll += p.Contract.CurrentYearSalary;
        }
        return payroll;
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
