using JumpShotBasketball.Core.Constants;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// In-season contract extension negotiation for computer-controlled teams.
/// Port of CAverage::ComputerNegotiatesContracts() — Average.cpp:1561-1683,
/// CPlayer::SetExtensionRequests() — Player.cpp:1236-1428,
/// CPlayer::AcceptComputerExtension() — Player.cpp:1510-1601,
/// CHireStaff::SetTopFreeAgentExtension() — HireStaff.cpp:1050-1155.
/// </summary>
public static class ContractNegotiationService
{
    /// <summary>
    /// Orchestrator called from ProcessDayBoundary.
    /// Iterates computer-controlled teams and attempts extensions with final-year players.
    /// </summary>
    public static void ProcessDayExtensions(League league, int daysPassed, Random random)
    {
        if (league.Teams.Count == 0) return;

        double avgTradeTrue = CalculateAverageTradeTrue(league);
        if (avgTradeTrue <= 0) avgTradeTrue = 1.0;

        int daysInSeason = league.Schedule.GamesInSeason > 0 ? league.Schedule.GamesInSeason : 82;

        for (int t = 0; t < league.Teams.Count; t++)
        {
            var team = league.Teams[t];
            if (team.Record.Control == "Player") continue;

            AttemptTeamExtensions(league, team, t, daysPassed, daysInSeason, avgTradeTrue, random);
        }
    }

    /// <summary>
    /// Attempts contract extensions for all final-year players on a team.
    /// Port of the inner loop of ComputerNegotiatesContracts().
    /// </summary>
    public static void AttemptTeamExtensions(
        League league, Models.Team.Team team, int teamIndex,
        int daysPassed, int daysInSeason, double avgTradeTrue, Random random)
    {
        double rosterValue = CalculateTeamRosterValue(team);
        int currentPayroll = CalculateTeamPayroll(team);
        int ownerCap = (int)team.Financial.OwnerSalaryCap;
        if (!league.Settings.FinancialEnabled)
            ownerCap = 355000;
        if (ownerCap <= 0)
            ownerCap = (int)(league.Settings.SalaryCap * 10);

        int salaryCap = (int)league.Settings.SalaryCap;

        foreach (var player in team.Roster)
        {
            if (string.IsNullOrEmpty(player.Name)) continue;
            if (player.Contract.CurrentContractYear != player.Contract.ContractYears) continue;
            if (player.Contract.ContractYears == 0) continue;
            if (player.Contract.IsFreeAgent) continue;

            double probability = CalculateExtensionProbability(
                player, team, rosterValue, avgTradeTrue,
                daysPassed, daysInSeason, currentPayroll, ownerCap);

            if (random.NextDouble() >= probability) continue;

            // Generate offer from team
            var (offerYears, offerSalaries, offerTotalSalary) = GenerateExtensionOffer(
                player, team, ownerCap, salaryCap, currentPayroll, random);

            // Calculate player's extension request (seeking factor)
            int seekingFactor = CalculateExtensionRequest(
                player, league, team, teamIndex, random);

            // Evaluate acceptance
            bool accepted = EvaluateExtensionOffer(
                player, seekingFactor, offerYears, offerTotalSalary);

            // Apply side effects
            ApplyExtensionResult(player, accepted, offerYears, offerSalaries, offerTotalSalary, random);

            if (accepted)
            {
                // Record transaction
                league.Settings.NumberOfTransactions++;
                league.Transactions.Add(new Transaction
                {
                    Id = league.Settings.NumberOfTransactions,
                    Type = TransactionType.Extension,
                    Description = $"{player.Position} {player.Name} signed {offerYears - 1}yr/${offerTotalSalary - player.Contract.CurrentYearSalary} extension with {team.Name}",
                    TeamIndex1 = teamIndex,
                    Team1Name = team.Name,
                    PlayersInvolved = new List<int> { player.Id }
                });

                // Update payroll for subsequent negotiations this day
                currentPayroll = CalculateTeamPayroll(team);
            }
        }
    }

    /// <summary>
    /// Calculates the probability that a team attempts an extension with a player.
    /// Port of Average.cpp:1583-1591.
    /// </summary>
    public static double CalculateExtensionProbability(
        Player player, Models.Team.Team team, double rosterValue,
        double avgTradeTrue, int daysPassed, int daysInSeason,
        int currentPayroll, int ownerCap)
    {
        if (avgTradeTrue <= 0) return 0;
        if (daysInSeason <= 0) return 0;

        double playerVal = player.Ratings.TradeTrueRating / avgTradeTrue * 2;
        double loyaltyVal = 0.7 + (double)player.Contract.LoyaltyFactor / 10.0;
        double teamVal = rosterValue / 100.0;
        double recordVal = team.Record.LeaguePercentage;

        double seasonPt = (double)daysPassed / daysInSeason;
        if (seasonPt < 0.25) recordVal = 0.5;

        double payrollVal = ownerCap > 0
            ? (double)currentPayroll * 10 / ownerCap * 2
            : 1.0;

        double sc = playerVal * loyaltyVal * teamVal * recordVal * payrollVal;
        double probability = sc / daysInSeason;

        return Math.Max(0, probability);
    }

    /// <summary>
    /// Calculates a player's extension salary request (seeking factor).
    /// Port of CPlayer::SetExtensionRequests() — Player.cpp:1236-1428.
    /// Returns the per-year average seeking amount used in acceptance evaluation.
    /// </summary>
    public static int CalculateExtensionRequest(
        Player player, League league, Models.Team.Team team, int teamIndex, Random random)
    {
        double tradeValue = player.Ratings.TradeTrueRating;
        if (player.SimulatedStats.Games > 4)
        {
            double simValue = player.Ratings.TrueRatingSimple;
            if (simValue > tradeValue) tradeValue = simValue;
        }

        int yos = player.Contract.YearsOfService;
        int yosIdx = Math.Min(yos, 10);

        // Base salary: 10% premium over market value
        double money = tradeValue * 100 * 1.10;
        if (money < player.Contract.CurrentYearSalary)
            money = player.Contract.CurrentYearSalary;

        // C++ also checks against last contract year salary
        if (player.Contract.ContractYears > 0 &&
            player.Contract.ContractYears - 1 < player.Contract.ContractSalaries.Length)
        {
            double lastYearSalary = player.Contract.ContractSalaries[player.Contract.ContractYears - 1] * 1.1;
            if (money < lastYearSalary) money = lastYearSalary;
        }

        // Cap by YOS maximums
        if (money > LeagueConstants.SalaryMaximumByYos[10] && yos >= 10)
            money = LeagueConstants.SalaryMaximumByYos[10];
        else if (money > LeagueConstants.SalaryMaximumByYos[7] && yos >= 7)
            money = LeagueConstants.SalaryMaximumByYos[7];
        else if (money > LeagueConstants.SalaryMaximumByYos[0])
            money = LeagueConstants.SalaryMaximumByYos[0];

        // Apply minimum salary
        if (money < LeagueConstants.SalaryMinimumByYos[yosIdx])
            money = LeagueConstants.SalaryMinimumByYos[yosIdx];

        // Contract years wanted
        int contractMax = 5;
        if (player.Age > 28) contractMax = 35 - player.Age;
        if (contractMax > 5) contractMax = 5;
        else if (contractMax < 2) contractMax = 2;

        int currentMoney = (int)money;
        player.Contract.QualifyingSalaries[0] = currentMoney;
        int totalSalary = currentMoney;

        // Years calculation using security factor
        double dMpy = money / 100.0;
        int mpy = (int)dMpy;
        int oMpy = mpy + 1;
        mpy = mpy - 3 + player.Contract.SecurityFactor;
        if (mpy < 1) mpy = 1;

        int contractYears = 1 + random.Next(0, mpy);
        if (contractYears < 1) contractYears = 1;
        else if (contractYears > 5) contractYears = 5;
        if (contractYears > contractMax) contractYears = contractMax;
        if (contractYears > oMpy) contractYears = oMpy;

        // Multi-year: 12.5% annual raises
        if (contractYears > 1)
        {
            double inc = currentMoney * 0.125;
            for (int j = 1; j < contractYears; j++)
            {
                currentMoney = (int)(currentMoney + inc);
                if (j < LeagueConstants.MaxContractYears)
                    player.Contract.QualifyingSalaries[j] = currentMoney;
                totalSalary += currentMoney;
            }
        }

        player.Contract.TotalSalarySeeking = totalSalary;
        player.Contract.QualifyingOfferYears = contractYears;
        double avgMoney = (double)totalSalary / contractYears;

        // Adjust for winning factor (franchise record)
        double wPct;
        int franchiseW = team.Record.Wins;
        int franchiseL = team.Record.Losses;
        if (franchiseL == 0)
            wPct = 0.5;
        else
            wPct = (double)franchiseW / (franchiseW + franchiseL);

        double winFactor = (0.5 - wPct) * player.Contract.TraditionFactor / 10.0;
        double factor = 1 + winFactor;
        double salaryRequest = avgMoney * factor;

        // Loyalty discount
        factor = 1;
        int loyal = player.Contract.LoyaltyFactor;
        if (player.Contract.YearsOnTeam < 3) loyal *= 2;
        double loy = loyal * 3.0 / 60.0;
        factor = 1 - loy;
        salaryRequest *= factor;

        // Coach factor
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
            factor = (200 - (99 + (double)r / 200)) / 100.0;
            salaryRequest *= factor;
        }

        // Playing time factor
        double pvFactor = 1;
        // Simplified: use position value relative to league average (single-team context)
        pvFactor = (94 + (double)player.Contract.PlayingTimeFactor * 2) / 100.0;
        salaryRequest *= pvFactor;

        // General discount
        salaryRequest *= 0.99;

        return (int)salaryRequest;
    }

    /// <summary>
    /// Generates the team's extension offer to a player.
    /// Port of CHireStaff::SetTopFreeAgentExtension() — HireStaff.cpp:1050-1155.
    /// Returns (years, per-year salaries, total salary).
    /// </summary>
    public static (int Years, int[] Salaries, int TotalSalary) GenerateExtensionOffer(
        Player player, Models.Team.Team team,
        int ownerCap, int salaryCap, int currentPayroll, Random random)
    {
        double tradeValue = player.Ratings.TradeTrueRating;
        int yos = player.Contract.YearsOfService;
        int yosIdx = Math.Min(yos, 10);

        // Base salary from trade value with 80-100% variance
        double money = tradeValue * 100;
        if (money < 32) money = 32;
        money *= 0.80 + (double)random.Next(0, 21) / 100.0;

        // Cap by YOS maximums
        if (money > LeagueConstants.SalaryMaximumByYos[10] && yos >= 10)
            money = LeagueConstants.SalaryMaximumByYos[10];
        else if (money > LeagueConstants.SalaryMaximumByYos[7] && yos >= 7)
            money = LeagueConstants.SalaryMaximumByYos[7];
        else if (money > LeagueConstants.SalaryMaximumByYos[0])
            money = LeagueConstants.SalaryMaximumByYos[0];

        // Contract years
        int contractMax = 5;
        if (player.Age > 28) contractMax = 35 - player.Age;
        if (contractMax > 5) contractMax = 5;
        else if (contractMax < 1) contractMax = 1;

        int contractYears = random.Next(1, contractMax + 1);
        if (contractYears == 1) contractYears = 2; // C++: 1-year extension always bumped to 2

        if (contractYears > contractMax) contractYears = contractMax;

        int currentMoney = (int)money;
        int currentYrSalary = currentMoney;
        var salaries = new int[LeagueConstants.MaxContractYears];
        salaries[0] = currentMoney;
        int totalSalary = currentMoney;

        // Multi-year: 12.5% annual raises
        if (contractYears > 1)
        {
            double inc = currentMoney * 0.125;
            for (int j = 1; j < contractYears; j++)
            {
                currentMoney = (int)(currentMoney + inc);
                salaries[j] = currentMoney;
                totalSalary += currentMoney;
            }
        }

        // Check if team can afford this under owner cap
        bool min = currentYrSalary < LeagueConstants.SalaryMinimumByYos[yosIdx];
        bool ownerAfford = (currentPayroll + currentYrSalary) * 10 <= ownerCap;

        if (!ownerAfford || min)
        {
            // Fall back to minimum salary, 1-year deal
            for (int i = 0; i < salaries.Length; i++)
                salaries[i] = 0;

            contractYears = 1;
            int minSalary = LeagueConstants.SalaryMinimumByYos[yosIdx];
            salaries[0] = minSalary;
            totalSalary = minSalary;
        }

        return (contractYears, salaries, totalSalary);
    }

    /// <summary>
    /// Evaluates whether a player accepts a computer-generated extension offer.
    /// Port of CPlayer::AcceptComputerExtension() — Player.cpp:1510-1601.
    /// </summary>
    public static bool EvaluateExtensionOffer(
        Player player, int seekingFactor, int yearsOffered, int totalSalaryOffered)
    {
        if (player.Contract.QualifyingOfferYears <= 0) return false;
        if (yearsOffered <= 0) return false;

        double requestedSalaryAvg = (double)player.Contract.TotalSalarySeeking / player.Contract.QualifyingOfferYears;
        if (requestedSalaryAvg <= 0) requestedSalaryAvg = 1;

        double offeredSalaryAvg = (double)totalSalaryOffered / yearsOffered;

        // Calculate max acceptable years based on salary ratio
        double value = requestedSalaryAvg * (0.7 + (6 - (double)player.Contract.SecurityFactor) / 10.0);
        if (value <= 0) value = 1;
        double tr = offeredSalaryAvg / value;

        double maxYears = 6 * tr;
        int iMaxYears = (int)maxYears;
        double rem = maxYears - iMaxYears;
        if (rem >= 0.5) iMaxYears++;
        if (iMaxYears < 1) iMaxYears = 1;

        bool accept = false;

        if (yearsOffered <= iMaxYears)
        {
            double secFactor = (100 + (double)player.Contract.SecurityFactor * yearsOffered / 2) / 100.0;
            double diff = offeredSalaryAvg * secFactor / requestedSalaryAvg;

            if (diff > 1.0)
                accept = true;

            // After 3+ rejections, need much better offer
            if (player.Contract.StopNegotiating >= 3 && diff < 2.0)
                accept = false;
        }

        return accept;
    }

    /// <summary>
    /// Applies the result of an extension negotiation to the player.
    /// On accept: sets new contract, content +1, loyalty +1 (probabilistic).
    /// On reject: content -1, loyalty -1, stopNegotiating++ (probabilistic).
    /// Port of AcceptComputerExtension() side effects + ComputerNegotiatesContracts() contract assignment.
    /// </summary>
    public static void ApplyExtensionResult(
        Player player, bool accepted, int offerYears, int[] offerSalaries,
        int offerTotalSalary, Random random)
    {
        if (accepted)
        {
            // Keep current year salary in slot 0
            player.Contract.ContractSalaries[0] = player.Contract.CurrentYearSalary;

            // Set extension years: current year becomes year 1, extension starts at year 2
            player.Contract.CurrentContractYear = 1;
            player.Contract.ContractYears = offerYears + 1; // +1 for remaining current year

            // Copy offered salaries into slots 1..offerYears
            for (int y = 0; y < offerYears && y + 1 < LeagueConstants.MaxContractYears; y++)
                player.Contract.ContractSalaries[y + 1] = offerSalaries[y];

            player.Contract.IsFreeAgent = false;
            player.Contract.Signed = true;

            // Recalculate totals
            int sal = 0;
            for (int y = 0; y < player.Contract.ContractYears && y < player.Contract.ContractSalaries.Length; y++)
                sal += player.Contract.ContractSalaries[y];
            player.Contract.RemainingSalary = sal;
            player.Contract.TotalSalary = sal;

            // Loyalty/content boost (probabilistic based on effort)
            int n = random.Next(1, 7);
            if (n <= player.Ratings.Effort)
            {
                if (player.Contract.LoyaltyFactor < 5)
                    player.Contract.LoyaltyFactor++;
                if (player.Content < 9)
                    player.Content++;
            }
        }
        else
        {
            // Loyalty/content decline (probabilistic based on effort)
            int n = random.Next(1, 11);
            int ne = 6 - player.Ratings.Effort;
            if (n <= ne)
            {
                if (player.Contract.LoyaltyFactor > 1)
                    player.Contract.LoyaltyFactor--;
                if (player.Content > 1)
                    player.Content--;
            }

            // Possibly stop negotiating
            double r = random.NextDouble() * (2.6 - player.Contract.LoyaltyFactor * 2.0 / 10.0);
            if (r > 0.5) // simplified threshold
                player.Contract.StopNegotiating++;
        }
    }

    /// <summary>
    /// Calculates the sum of TradeTrueRating for all named players on a team.
    /// </summary>
    public static double CalculateTeamRosterValue(Models.Team.Team team)
    {
        double value = 0;
        foreach (var player in team.Roster)
        {
            if (string.IsNullOrEmpty(player.Name)) continue;
            value += player.Ratings.TradeTrueRating;
        }
        return value;
    }

    /// <summary>
    /// Calculates the league-wide average TradeTrueRating.
    /// </summary>
    public static double CalculateAverageTradeTrue(League league)
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
}
