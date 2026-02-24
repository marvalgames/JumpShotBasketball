using JumpShotBasketball.Core.Constants;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Contract generation and preference factor assignment.
/// Port of CAverage::GenerateRandomContracts() — Average.cpp:43-307.
/// </summary>
public static class ContractService
{
    /// <summary>
    /// Generates contracts for all players in the league.
    /// Port of CAverage::GenerateRandomContracts().
    /// </summary>
    public static void GenerateContracts(League league, bool skipAges, bool skipContracts,
        bool skipPotentials, Random? random = null)
    {
        random ??= Random.Shared;

        foreach (var team in league.Teams)
        {
            foreach (var player in team.Roster)
            {
                if (string.IsNullOrEmpty(player.Name)) continue;

                // Generate random prime for all players
                PlayerDevelopmentService.GenerateRandomPrime(player, random);

                if (!skipPotentials)
                {
                    player.Ratings.Potential2 = random.Next(1, 6);
                    player.Ratings.Potential1 = random.Next(1, 6);
                    player.Ratings.Effort = random.Next(1, 6);
                }
            }
        }

        // Generate contracts for rostered players (first 32 players per team in C++ = indices 1-960)
        foreach (var team in league.Teams)
        {
            for (int p = 0; p < team.Roster.Count; p++)
            {
                var player = team.Roster[p];
                if (string.IsNullOrEmpty(player.Name)) continue;

                if (!skipAges)
                {
                    int yos = player.Contract.YearsOfService;
                    if (yos > 0)
                        player.Contract.YearsOnTeam = random.Next(1, yos + 1);
                }

                if (!skipContracts)
                {
                    for (int j = 0; j < LeagueConstants.MaxContractYears; j++)
                        player.Contract.ContractSalaries[j] = 0;
                }

                GeneratePlayerContract(player, random);

                if (skipContracts)
                {
                    // Restore original contract data — handled by caller
                }

                player.Contract.CurrentTeam = team.Id;
                player.Contract.PreviousTeam = team.Id;
            }
        }

        // Generate preference factors for all players
        foreach (var team in league.Teams)
        {
            foreach (var player in team.Roster)
            {
                GeneratePreferenceFactors(player, random);
            }
        }
    }

    /// <summary>
    /// Generates a contract for a single player based on their trade true rating and years of service.
    /// Port of the per-player logic in Average.cpp:95-278.
    /// Bug fix: C++ line 275 adds player index 'i' instead of salary value; fixed to use ContractSalaries[y].
    /// </summary>
    public static void GeneratePlayerContract(Player player, Random? random = null)
    {
        random ??= Random.Shared;

        var contract = player.Contract;
        int age = player.Age;
        int yos = contract.YearsOfService;

        // Calculate base salary from trade true rating
        double money = player.Ratings.TradeTrueRating * 98;
        money *= 0.8 + random.Next(1, 41) / 100.0;

        // Apply minimum salary by years of service
        int yosIdx = Math.Min(yos, 10);
        if (money < LeagueConstants.SalaryMinimumByYos[yosIdx])
            money = LeagueConstants.SalaryMinimumByYos[yosIdx];

        // Maximum contract years
        int contractMax = 6;
        if (age > 28) contractMax = 35 - age;
        if (contractMax < 1) contractMax = 1;

        double mpyF = money / 200;
        int mpyI = (int)mpyF;

        int possible = contractMax + mpyI - 3;
        if (possible < 2) possible = 2;

        int contractYears = random.Next(1, possible + 1);
        if (contractYears > 6) contractYears = 6;
        if (contractYears == 1 && random.Next(1, 5) <= 2)
            contractYears = 2;

        int currentContractYear = random.Next(1, contractYears + 1);
        if (currentContractYear == contractYears)
            currentContractYear = random.Next(1, contractYears + 1);

        // Young players: contract tied to YOS
        if (yos <= 4)
        {
            contract.YearsOnTeam = yos;
            if (random.Next(1, 5) == 1 && yos > 0)
                contract.YearsOnTeam = random.Next(1, yos + 1);
            contractYears = 4;
            currentContractYear = yos;
            if (currentContractYear > contractYears) currentContractYear = contractYears;
            if (currentContractYear < 1) currentContractYear = 1;
        }

        // Apply maximum salary by YOS
        bool maxRaise = false;
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

        // Rookie scale caps
        if (yos <= 1 && money > LeagueConstants.RookieSalaryCapYear1)
            money = LeagueConstants.RookieSalaryCapYear1;
        if (yos == 2 && money > LeagueConstants.RookieSalaryCapYear2)
            money = LeagueConstants.RookieSalaryCapYear2;
        if (yos == 3 && money > LeagueConstants.RookieSalaryCapYear3)
            money = LeagueConstants.RookieSalaryCapYear3;

        // Rookie YOS overrides
        if (yos <= 1)
        {
            contractYears = 3;
            currentContractYear = 1;
        }
        else if (yos == 2)
        {
            contractYears = 3;
            currentContractYear = 2;
        }
        else if (yos == 3)
        {
            contractYears = 3;
            currentContractYear = 3;
        }

        int currentMoney = (int)money;
        int totalSalary = 0;

        // Set current year salary
        if (currentContractYear >= 1 && currentContractYear <= LeagueConstants.MaxContractYears)
            contract.ContractSalaries[currentContractYear - 1] = currentMoney;
        totalSalary += currentMoney;

        if (contractYears != currentContractYear)
        {
            // Not at end of contract: fill future years with raises
            contract.IsFreeAgent = false;
            int raise = random.Next(1, contractYears * 3 + 1);
            if (maxRaise) raise = 10;
            if (raise > 10) raise = 10;
            totalSalary = 0;
            double inc = currentMoney * raise / 100.0;

            for (int j = currentContractYear + 1; j <= contractYears; j++)
            {
                double yearMoney = currentMoney + inc;
                currentMoney = (int)yearMoney;
                if (j - 1 >= 0 && j - 1 < LeagueConstants.MaxContractYears)
                    contract.ContractSalaries[j - 1] = currentMoney;
                totalSalary += currentMoney;
            }
        }
        else
        {
            // At end of contract: free agent
            contract.IsFreeAgent = true;
        }

        contract.ContractYears = contractYears;
        contract.CurrentContractYear = currentContractYear;
        contract.TotalSalary = totalSalary;
        contract.CurrentYearSalary = contract.ContractSalaries[
            Math.Max(0, Math.Min(currentContractYear - 1, LeagueConstants.MaxContractYears - 1))];

        // Calculate remaining salary (Bug fix: C++ adds index 'i' instead of salary)
        int remainingSalary = 0;
        for (int y = 0; y < contractYears; y++)
        {
            if (y >= currentContractYear - 1)
                remainingSalary += contract.ContractSalaries[y];
        }
        contract.RemainingSalary = remainingSalary;
    }

    /// <summary>
    /// Generates random preference factors for free agency decisions.
    /// Port of Average.cpp:293-303.
    /// </summary>
    public static void GeneratePreferenceFactors(Player player, Random? random = null)
    {
        random ??= Random.Shared;

        player.Contract.CoachFactor = random.Next(1, 6);
        player.Contract.SecurityFactor = random.Next(1, 6);
        player.Contract.LoyaltyFactor = random.Next(1, 6);
        player.Contract.WinningFactor = random.Next(1, 6);
        player.Contract.PlayingTimeFactor = random.Next(1, 6);
        player.Contract.TraditionFactor = random.Next(1, 6);
    }
}
