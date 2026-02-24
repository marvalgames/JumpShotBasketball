using JumpShotBasketball.Core.Enums;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Team;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Financial simulation: per-game revenue/expenses, end-of-season adjustments,
/// arena/city/finance generation, relocation evaluation, and budget reset.
/// Port of C++ TeamFinancial.cpp (~816 lines, 35+ methods).
/// </summary>
public static class FinancialSimulationService
{
    // ── Generation ─────────────────────────────────────────────

    /// <summary>
    /// Generates random arena dimensions and stadium value.
    /// Port of CTeamFinancial::GenerateRandomArenas().
    /// </summary>
    public static void GenerateRandomArena(TeamFinancial fin, Random random)
    {
        fin.Capacity = 15000 + IntRandom(random, 5000);
        if (random.NextDouble() < 0.1)
            fin.Capacity = 12000 + IntRandom(random, 12000);

        int f = (int)(fin.Capacity / 100);
        fin.Suites = IntRandom(random, f);
        f = (int)(fin.Capacity / 10);
        fin.ClubSeats = fin.Suites * 10 + IntRandom(random, f);
        f = (int)(fin.Capacity / 4);
        fin.ParkingSpots = fin.Capacity / 10 + IntRandom(random, f);

        int cost = (int)(fin.Capacity + fin.ClubSeats * 4 + fin.Suites * 100);
        double tmp = (double)cost / 200.0;
        tmp = tmp * (0.8 + random.NextDouble() * 0.4);
        fin.StadiumValue = (int)tmp;
    }

    /// <summary>
    /// Generates random financial attributes from stadium value.
    /// Port of CTeamFinancial::GenerateRandomFinances().
    /// </summary>
    public static void GenerateRandomFinances(TeamFinancial fin, Random random)
    {
        fin.CurrentValue = (int)((double)fin.StadiumValue / 2 * 10) + IntRandom(random, 2500);
        if (fin.CurrentValue <= fin.StadiumValue * 10)
            fin.CurrentValue = fin.StadiumValue * 10 + IntRandom(random, 160);

        fin.TicketPrice = (24 + IntRandom(random, 36)) * 10;
        fin.SuitePrice = (250 + IntRandom(random, 200)) * 10;
        fin.ClubPrice = (10 + IntRandom(random, 10)) * 10;
        fin.NetworkShare = (int)(660000.0 / 29.0 / 82.0);
        fin.LocalTv = 40000 + IntRandom(random, (int)fin.CurrentValue * 40);
        fin.LocalRadio = 20000 + IntRandom(random, (int)fin.CurrentValue * 20);
        if (fin.LocalRadio > fin.LocalTv / 2)
            fin.LocalRadio = fin.LocalTv / 2;

        fin.Concessions = (15 + IntRandom(random, 10)) * 10;
        fin.Parking = 4 + IntRandom(random, 5);
        int fa = (int)((fin.CurrentValue + fin.StadiumValue * 10) * 10);
        int fs = (int)((fin.CurrentValue + fin.StadiumValue * 10) * 5);
        fin.Advertising = 10000 + IntRandom(random, fa);
        fin.Sponsorship = 10000 + IntRandom(random, fs);
        fin.OperatingLevel = (int)(((double)fin.CurrentValue + (double)(fin.StadiumValue * 10)) * 1000.0 / 20.0 / 82.0);
        fin.MarketingLevel = (int)((double)fin.CurrentValue * 1000.0 / 50.0 / 82.0);
        fin.ArenaLevel = (int)((double)fin.CurrentValue * 1000.0 / 100.0 / 82.0);
    }

    /// <summary>
    /// Generates random city metrics (all 1-7).
    /// Port of CTeamFinancial::GenerateRandomCities().
    /// </summary>
    public static void GenerateRandomCity(TeamFinancial fin, Random random)
    {
        fin.FanSupport = IntRandom(random, 7);
        fin.Economy = IntRandom(random, 7);
        fin.PoliticalSupport = IntRandom(random, 7);
        fin.Interest = IntRandom(random, 7);
        fin.Population = IntRandom(random, 7);
        fin.CostOfLiving = IntRandom(random, 7);
        fin.Competition = IntRandom(random, 7);
    }

    // ── Roster Valuation ───────────────────────────────────────

    /// <summary>
    /// Sets a team's roster value to the sum of all players' TradeValue.
    /// Port of CTeamFinancial::SetTeamRosterValue().
    /// </summary>
    public static void SetTeamRosterValue(TeamFinancial fin, Team team)
    {
        double total = 0;
        foreach (var player in team.Roster)
        {
            total += player.Ratings.TradeValue;
        }
        fin.TeamRosterValue = total;
    }

    /// <summary>
    /// Returns the average roster value across all league teams.
    /// </summary>
    public static double CalculateLeagueAverageRosterValue(League league)
    {
        if (league.Teams.Count == 0) return 1.0;
        double total = 0;
        foreach (var team in league.Teams)
        {
            total += team.Financial.TeamRosterValue;
        }
        double avg = total / league.Teams.Count;
        return avg > 0 ? avg : 1.0;
    }

    // ── Per-Game Revenue ───────────────────────────────────────

    /// <summary>
    /// Calculates ticket sales for a home game.
    /// Port of CTeamFinancial::GameTicketSales().
    /// </summary>
    public static void CalculateTicketSales(TeamFinancial fin, int personality,
        double avgRosterValue, int wins, int losses, int hiGames, int visitorCurrentValue, Random random)
    {
        if (avgRosterValue <= 0) avgRosterValue = 1.0;
        if (hiGames <= 0) hiGames = 1;

        double price = (double)fin.TicketPrice / 10.0;
        long capacity = fin.Capacity - fin.ClubSeats - fin.Suites * 20;
        double avgRevenue = 35000000.0 / 41.0;
        double avgPrice = 48.0;
        double rosterFactor = fin.TeamRosterValue / avgRosterValue;

        int tot = wins + losses;
        double maxRevenue = rosterFactor * avgRevenue * (((double)fin.StadiumValue + 200.0) / 400.0);
        maxRevenue = maxRevenue * (((double)visitorCurrentValue + 8000.0) / 10000.0);

        double pct = 0.5;
        if (tot > 0) pct = (double)wins / (double)tot;
        double f = (double)tot / (double)hiGames;
        double m = 0.5 + pct;
        maxRevenue = maxRevenue + (maxRevenue * m - maxRevenue) * f;
        maxRevenue = maxRevenue * (21.0 + fin.Economy) / 25.0;
        maxRevenue = maxRevenue * (11.0 + fin.FanSupport) / 15.0;
        maxRevenue = maxRevenue * (31.0 + fin.Population) / 35.0;
        maxRevenue = maxRevenue * (41.0 + (8 - fin.CostOfLiving)) / 45.0;
        maxRevenue = maxRevenue * (51.0 + (8 - fin.Competition)) / 55.0;
        maxRevenue = maxRevenue * (61.0 + personality) / 65.0;

        double bestPrice = avgPrice * (21.0 + fin.Economy) / 25.0 * rosterFactor;
        bestPrice = bestPrice * (11.0 + fin.FanSupport) / 15.0;
        bestPrice = bestPrice * (31.0 + fin.Population) / 35.0;
        bestPrice = bestPrice * (41.0 + (8 - fin.CostOfLiving)) / 45.0;
        bestPrice = bestPrice * (51.0 + (8 - fin.Competition)) / 55.0;
        bestPrice = bestPrice * (61.0 + personality) / 65.0;
        bestPrice = bestPrice * (((double)fin.StadiumValue + 200.0) / 400.0);

        double pctFactor = price / bestPrice;
        if (pctFactor > 1) pctFactor = bestPrice / price;

        pctFactor = (1.0 - pctFactor) * (1.0 - (double)fin.Interest / 8.0);
        double projectedRevenue = maxRevenue * (1.0 - pctFactor);

        double sold = projectedRevenue / price;
        sold = (0.9 + random.NextDouble() * 0.2) * sold;
        sold = sold * 0.98;

        fin.TicketsSold = (long)sold;
        if (fin.TicketsSold > capacity) fin.TicketsSold = capacity;
        fin.TicketRevenue = (long)((double)fin.TicketsSold * price);
    }

    /// <summary>
    /// Calculates club seat sales for a home game.
    /// Port of CTeamFinancial::GameClubSales().
    /// </summary>
    public static void CalculateClubSales(TeamFinancial fin, int personality,
        double avgRosterValue, int wins, int losses, int hiGames, int visitorCurrentValue, Random random)
    {
        if (avgRosterValue <= 0) avgRosterValue = 1.0;
        if (hiGames <= 0) hiGames = 1;

        int tot = wins + losses;
        double pct = 0.5;
        if (tot > 0) pct = (double)wins / (double)tot;
        double f = (double)tot / (double)hiGames;
        double m = 0.5 + pct;

        double price = (double)fin.ClubPrice;
        long capacity = fin.ClubSeats;
        double avgRevenue = 2200.0 * 150.0;
        double avgPrice = 150.0;
        double rosterFactor = fin.TeamRosterValue / avgRosterValue;

        double maxRevenue = rosterFactor * avgRevenue * (((double)fin.StadiumValue + 200.0) / 400.0);
        maxRevenue = maxRevenue * (((double)visitorCurrentValue + 8000.0) / 10000.0);
        maxRevenue = maxRevenue + (maxRevenue * m - maxRevenue) * f;

        maxRevenue = maxRevenue * (16.0 + fin.Economy) / 20.0;
        maxRevenue = maxRevenue * (16.0 + fin.FanSupport) / 20.0;
        maxRevenue = maxRevenue * (36.0 + fin.Population) / 40.0;
        maxRevenue = maxRevenue * (26.0 + (8 - fin.CostOfLiving)) / 30.0;
        maxRevenue = maxRevenue * (36.0 + (8 - fin.Competition)) / 40.0;
        maxRevenue = maxRevenue * (46.0 + personality) / 50.0;

        double bestPrice = avgPrice * (16.0 + fin.Economy) / 20.0 * rosterFactor;
        bestPrice = bestPrice * (16.0 + fin.FanSupport) / 20.0;
        bestPrice = bestPrice * (36.0 + fin.Population) / 40.0;
        bestPrice = bestPrice * (26.0 + (8 - fin.CostOfLiving)) / 30.0;
        bestPrice = bestPrice * (36.0 + (8 - fin.Competition)) / 40.0;
        bestPrice = bestPrice * (((double)fin.StadiumValue + 200.0) / 400.0);
        bestPrice = bestPrice * (46.0 + personality) / 50.0;

        double pctFactor = price / bestPrice;
        if (pctFactor > 1 && price > 0) pctFactor = bestPrice / price;

        pctFactor = (1.0 - pctFactor) * (1.0 - (double)fin.Interest / 8.0);
        double projectedRevenue = maxRevenue * (1.0 - pctFactor);

        double sold = 0;
        if (price > 0)
        {
            sold = projectedRevenue / price;
            sold = (0.9 + random.NextDouble() * 0.2) * sold;
            sold = sold * 0.98;
        }

        fin.ClubsSold = (long)sold;
        if (fin.ClubsSold > capacity) fin.ClubsSold = capacity;
        fin.ClubRevenue = (long)((double)fin.ClubsSold * price);
    }

    /// <summary>
    /// Calculates suite sales for a home game.
    /// Port of CTeamFinancial::GameSuiteSales().
    /// </summary>
    public static void CalculateSuiteSales(TeamFinancial fin, int personality,
        double avgRosterValue, int wins, int losses, int hiGames, int visitorCurrentValue, Random random)
    {
        if (avgRosterValue <= 0) avgRosterValue = 1.0;
        if (hiGames <= 0) hiGames = 1;

        double price = (double)fin.SuitePrice;
        long capacity = fin.Suites;
        double avgRevenue = 85.0 * 3500.0;
        double avgPrice = 3500.0;
        double rosterFactor = fin.TeamRosterValue / avgRosterValue;

        int tot = wins + losses;
        double maxRevenue = rosterFactor * avgRevenue * (((double)fin.StadiumValue + 200.0) / 400.0);
        maxRevenue = maxRevenue * (((double)visitorCurrentValue + 8000.0) / 10000.0);

        double pct = 0.5;
        if (tot > 0) pct = (double)wins / (double)tot;
        double f = (double)tot / (double)hiGames;
        double m = 0.5 + pct;
        maxRevenue = maxRevenue + (maxRevenue * m - maxRevenue) * f;

        maxRevenue = maxRevenue * (16.0 + fin.Economy) / 10.0;
        maxRevenue = maxRevenue * (26.0 + fin.FanSupport) / 30.0;
        maxRevenue = maxRevenue * (26.0 + fin.Population) / 30.0;
        maxRevenue = maxRevenue * (26.0 + (8 - fin.CostOfLiving)) / 30.0;
        maxRevenue = maxRevenue * (36.0 + (8 - fin.Competition)) / 40.0;
        maxRevenue = maxRevenue * (46.0 + personality) / 50.0;

        double bestPrice = avgPrice * (16.0 + fin.Economy) / 10.0 * rosterFactor;
        bestPrice = bestPrice * (26.0 + fin.FanSupport) / 30.0;
        bestPrice = bestPrice * (26.0 + fin.Population) / 30.0;
        bestPrice = bestPrice * (26.0 + (8 - fin.CostOfLiving)) / 30.0;
        bestPrice = bestPrice * (36.0 + (8 - fin.Competition)) / 40.0;
        bestPrice = bestPrice * (((double)fin.StadiumValue + 200.0) / 400.0);
        bestPrice = bestPrice * (46.0 + personality) / 50.0;

        double pctFactor = price / bestPrice;
        if (pctFactor > 1) pctFactor = bestPrice / price;

        pctFactor = (1.0 - pctFactor) * (1.0 - (double)fin.Interest / 8.0);
        double projectedRevenue = maxRevenue * (1.0 - pctFactor);

        double sold = projectedRevenue / price;
        sold = (0.9 + random.NextDouble() * 0.2) * sold;
        sold = sold * 0.98;

        fin.SuitesSold = (long)sold;
        if (fin.SuitesSold > capacity) fin.SuitesSold = capacity;
        fin.SuiteRevenue = (long)((double)fin.SuitesSold * price);
    }

    /// <summary>
    /// Calculates concession sales based on attendance.
    /// Port of CTeamFinancial::GameConcessionSales().
    /// </summary>
    public static void CalculateConcessionSales(TeamFinancial fin, double avgRosterValue)
    {
        if (avgRosterValue <= 0) avgRosterValue = 1.0;

        double avgRevenue = 200.0;
        double avgPrice = 700.0;

        long att = (long)fin.TicketsSold + (long)fin.ClubsSold + (long)fin.SuitesSold * 20;

        double rosterFactor = fin.TeamRosterValue / avgRosterValue;
        double maxRevenue = rosterFactor * avgRevenue;

        double price = (double)fin.Concessions + (double)fin.TicketPrice;

        maxRevenue = maxRevenue * (26.0 + fin.Economy) / 30.0;
        maxRevenue = maxRevenue * (46.0 + fin.FanSupport) / 50.0;
        maxRevenue = maxRevenue * (106.0 + fin.Population) / 110.0;
        maxRevenue = maxRevenue * (16.0 + (8 - fin.CostOfLiving)) / 20.0;
        maxRevenue = maxRevenue * (106.0 + (8 - fin.Competition)) / 110.0;

        double bestPrice = avgPrice * (26.0 + fin.Economy) / 30.0 * rosterFactor;
        bestPrice = bestPrice * (46.0 + fin.FanSupport) / 40.0;
        bestPrice = bestPrice * (106.0 + fin.Population) / 100.0;
        bestPrice = bestPrice * (16.0 + (8 - fin.CostOfLiving)) / 20.0;
        bestPrice = bestPrice * (106.0 + (8 - fin.Competition)) / 100.0;
        bestPrice = bestPrice * (((double)fin.StadiumValue + 500.0) / 700.0);

        double pctFactor = price / bestPrice;
        if (pctFactor > 1) pctFactor = bestPrice / price;

        pctFactor = (1.0 - pctFactor) * (1.0 - (double)fin.Interest / 8.0);
        double projectedRevenue = maxRevenue * (1.0 - pctFactor);

        double revenue = (double)att * projectedRevenue;
        // C++ line 336: m_concession_revenue = (long) revenue / 10; — integer division
        fin.ConcessionRevenue = (long)revenue / 10;
    }

    /// <summary>
    /// Calculates parking sales for a home game.
    /// Port of CTeamFinancial::GameParkingSales().
    /// </summary>
    public static void CalculateParkingSales(TeamFinancial fin, double avgRosterValue, Random random)
    {
        if (avgRosterValue <= 0) avgRosterValue = 1.0;

        double price = (double)fin.Parking;
        double avgRevenue = 3750.0 * 7.0;
        double rosterFactor = fin.TeamRosterValue / avgRosterValue;
        double maxRevenue = rosterFactor * avgRevenue;
        double stadiumF = (((double)fin.StadiumValue + 500.0) / 700.0);

        double sc = (double)fin.Economy * 6.0 +
            (double)fin.FanSupport * 1.0 +
            (double)fin.Population * 6.0 +
            (double)(8 - fin.CostOfLiving) * 1.0 +
            (double)fin.Competition * 1.0;

        sc = (4.0 + sc / 15.0) / 5.0 * rosterFactor * stadiumF;

        maxRevenue = maxRevenue * (6.0 + sc) / 10.0;

        double bestPrice = 3.0 + (int)sc;

        double pctFactor = price / bestPrice;
        if (pctFactor > 1) pctFactor = bestPrice / price;

        pctFactor = (1.0 - pctFactor) * (1.0 - (double)fin.Interest / 8.0);
        double projectedRevenue = maxRevenue * (1.0 - pctFactor);

        double sold = projectedRevenue / price;
        sold = (0.9 + random.NextDouble() * 0.2) * sold;
        sold = sold * 0.98;

        fin.ParkingSold = (long)sold;
        if (fin.ParkingSold > fin.ParkingSpots) fin.ParkingSold = fin.ParkingSpots;
        fin.ParkingRevenue = (long)((double)fin.ParkingSold * price);
    }

    /// <summary>
    /// Sums all per-game revenue components.
    /// Port of CTeamFinancial::GameTotalRevenues().
    /// </summary>
    public static void CalculateGameTotalRevenue(TeamFinancial fin)
    {
        fin.GameTotalRevenue =
            fin.TicketRevenue
            + fin.SuiteRevenue
            + fin.ClubRevenue
            + fin.ParkingRevenue
            + fin.ConcessionRevenue
            + (fin.NetworkShare * 1000)
            + fin.LocalTv
            + fin.LocalRadio
            + fin.Advertising
            + fin.Sponsorship;
    }

    /// <summary>
    /// Accumulates per-game revenue into season totals (home games).
    /// Port of CTeamFinancial::SeasonRevenues().
    /// </summary>
    public static void AccumulateSeasonRevenue(TeamFinancial fin)
    {
        fin.SeasonAttendance += fin.TicketsSold;
        fin.AttendanceRevenue += fin.TicketRevenue;
        fin.SeasonSuitesSold += fin.SuitesSold;
        fin.SeasonClubsSold += fin.ClubsSold;
        fin.SeasonParkingSold += fin.ParkingSold;
        fin.SeasonSuiteRevenue += fin.SuiteRevenue;
        fin.SeasonClubRevenue += fin.ClubRevenue;
        fin.SeasonParkingRevenue += fin.ParkingRevenue;
        fin.SeasonConcessionRevenue += fin.ConcessionRevenue;
        fin.SeasonTotalRevenue += fin.GameTotalRevenue;
        fin.HomeGames += 1;
    }

    /// <summary>
    /// Accumulates road game revenue (media/sponsorship only).
    /// Port of CTeamFinancial::SeasonRevenueRoadGame().
    /// </summary>
    public static void AccumulateRoadGameRevenue(TeamFinancial fin)
    {
        fin.GameTotalRevenue =
            (fin.NetworkShare * 1000)
            + fin.LocalTv
            + fin.LocalRadio
            + fin.Advertising
            + fin.Sponsorship;

        fin.SeasonTotalRevenue += fin.GameTotalRevenue;
    }

    // ── Per-Game Expenses ──────────────────────────────────────

    /// <summary>
    /// Calculates player payroll expense for one game.
    /// Port of CTeamFinancial::GamePlayerPayroll().
    /// </summary>
    public static void CalculatePlayerPayroll(TeamFinancial fin, Team team)
    {
        long totalSalary = 0;
        int count = Math.Min(team.Roster.Count, 15);
        for (int i = 0; i < count; i++)
        {
            long money = team.Roster[i].Contract.CurrentYearSalary;
            double salary = (double)money * 10000.0 / 82.0;
            totalSalary += (long)salary;
        }
        fin.GamePayroll = totalSalary;
    }

    /// <summary>
    /// Calculates per-game operating expenses.
    /// Port of CTeamFinancial::GameOperatingPayroll().
    /// </summary>
    public static void CalculateOperatingExpenses(TeamFinancial fin, Random random)
    {
        double exp = 0.8 + random.NextDouble() * 0.4;
        exp = (double)fin.OperatingLevel * 102.0 * exp;
        fin.OperatingExpenses = (long)exp;
    }

    /// <summary>
    /// Calculates per-game arena expenses.
    /// Port of CTeamFinancial::GameArenaPayroll().
    /// </summary>
    public static void CalculateArenaExpenses(TeamFinancial fin, Random random)
    {
        double exp = 0.8 + random.NextDouble() * 0.4;
        exp = (double)fin.ArenaLevel * 100.0 * exp;
        fin.ArenaExpenses = (long)exp;
    }

    /// <summary>
    /// Calculates per-game marketing expenses.
    /// Port of CTeamFinancial::GameMarketingPayroll().
    /// </summary>
    public static void CalculateMarketingExpenses(TeamFinancial fin, Random random)
    {
        double exp = 0.8 + random.NextDouble() * 0.4;
        exp = (double)fin.MarketingLevel * 102.0 * exp;
        fin.MarketingExpenses = (long)exp;
    }

    /// <summary>
    /// Sums all per-game expenses. Playoff games exclude payroll and staff.
    /// Port of CTeamFinancial::GameTotalExpenses().
    /// </summary>
    public static void CalculateGameTotalExpenses(TeamFinancial fin, GameType gameType)
    {
        if (gameType != GameType.Playoff)
        {
            fin.GameTotalExpenses =
                fin.GamePayroll
                + fin.ScoutExpenses
                + fin.CoachExpenses
                + fin.GmExpenses
                + fin.OperatingExpenses
                + fin.ArenaExpenses
                + fin.MarketingExpenses;
        }
        else
        {
            fin.GameTotalExpenses =
                fin.OperatingExpenses
                + fin.ArenaExpenses
                + fin.MarketingExpenses;
        }
    }

    /// <summary>
    /// Accumulates per-game expenses into season totals.
    /// Port of CTeamFinancial::SeasonExpenses().
    /// </summary>
    public static void AccumulateSeasonExpenses(TeamFinancial fin)
    {
        fin.SeasonPayroll += fin.GamePayroll;
        fin.SeasonScoutExpenses += fin.ScoutExpenses;
        fin.SeasonCoachExpenses += fin.CoachExpenses;
        fin.SeasonGmExpenses += fin.GmExpenses;
        fin.SeasonOperatingExpenses += fin.OperatingExpenses;
        fin.SeasonArenaExpenses += fin.ArenaExpenses;
        fin.SeasonMarketingExpenses += fin.MarketingExpenses;
        fin.SeasonTotalExpenses += fin.GameTotalExpenses;
    }

    // ── Per-Game Orchestrators ─────────────────────────────────

    /// <summary>
    /// Full per-game financial processing for the home team.
    /// </summary>
    public static void ProcessHomeGameFinancials(League league, int homeTeamIndex,
        int visitorTeamIndex, GameType gameType, Random random)
    {
        var homeTeam = league.Teams[homeTeamIndex];
        var fin = homeTeam.Financial;
        var visitorFin = league.Teams[visitorTeamIndex].Financial;

        // Set roster values for all teams (needed for average)
        foreach (var team in league.Teams)
            SetTeamRosterValue(team.Financial, team);

        double avgRosterValue = CalculateLeagueAverageRosterValue(league);

        int personality = 4; // default personality
        if (homeTeam.Coach != null)
            personality = homeTeam.Coach.Power;

        int hiGames = league.Schedule.GamesInSeason > 0 ? league.Schedule.GamesInSeason / 2 : 41;
        int wins = homeTeam.Record.Wins;
        int losses = homeTeam.Record.Losses;
        int visitorCurrentValue = (int)visitorFin.CurrentValue;

        // Revenue
        CalculateTicketSales(fin, personality, avgRosterValue, wins, losses, hiGames, visitorCurrentValue, random);
        CalculateClubSales(fin, personality, avgRosterValue, wins, losses, hiGames, visitorCurrentValue, random);
        CalculateSuiteSales(fin, personality, avgRosterValue, wins, losses, hiGames, visitorCurrentValue, random);
        CalculateConcessionSales(fin, avgRosterValue);
        CalculateParkingSales(fin, avgRosterValue, random);
        CalculateGameTotalRevenue(fin);
        AccumulateSeasonRevenue(fin);

        // Expenses
        CalculatePlayerPayroll(fin, homeTeam);
        CalculateOperatingExpenses(fin, random);
        CalculateArenaExpenses(fin, random);
        CalculateMarketingExpenses(fin, random);
        CalculateGameTotalExpenses(fin, gameType);
        AccumulateSeasonExpenses(fin);
    }

    /// <summary>
    /// Per-game financial processing for the road team (media/sponsorship only).
    /// </summary>
    public static void ProcessRoadGameFinancials(League league, int teamIndex)
    {
        var fin = league.Teams[teamIndex].Financial;
        AccumulateRoadGameRevenue(fin);
    }

    // ── End-of-Season Adjustments ──────────────────────────────

    /// <summary>
    /// Adjusts ticket price toward market best price.
    /// Port of CTeamFinancial::SetEndOfSeasonTicketPrice().
    /// </summary>
    public static void AdjustTicketPrice(TeamFinancial fin, int personality, double avgRosterValue,
        int wins, int losses, int hiGames)
    {
        if (avgRosterValue <= 0) avgRosterValue = 1.0;
        double rosterFactor = fin.TeamRosterValue / avgRosterValue;

        double avgPrice = 48.0;

        double bestPrice = avgPrice * (21.0 + fin.Economy) / 25.0 * rosterFactor;
        bestPrice = bestPrice * (11.0 + fin.FanSupport) / 15.0;
        bestPrice = bestPrice * (31.0 + fin.Population) / 35.0;
        bestPrice = bestPrice * (41.0 + (8 - fin.CostOfLiving)) / 45.0;
        bestPrice = bestPrice * (51.0 + (8 - fin.Competition)) / 55.0;
        bestPrice = bestPrice * (61.0 + personality) / 65.0;
        bestPrice = bestPrice * (((double)fin.StadiumValue + 200.0) / 400.0);

        double price = (double)fin.TicketPrice / 10.0;
        double factor = bestPrice - price;
        fin.TicketPrice = fin.TicketPrice + (int)factor;
        fin.TicketPrice = (int)((double)fin.TicketPrice / 10.0) * 10;

        if (fin.TicketPrice < 50) fin.TicketPrice = 50;
    }

    /// <summary>
    /// Adjusts suite price toward market best price.
    /// Port of CTeamFinancial::SetEndOfSeasonSuitePrice().
    /// </summary>
    public static void AdjustSuitePrice(TeamFinancial fin, int personality, double avgRosterValue,
        int wins, int losses, int hiGames)
    {
        if (avgRosterValue <= 0) avgRosterValue = 1.0;
        double rosterFactor = fin.TeamRosterValue / avgRosterValue;

        double avgPrice = 3500.0;

        double bestPrice = avgPrice * (16.0 + fin.Economy) / 20.0 * rosterFactor;
        bestPrice = bestPrice * (26.0 + fin.FanSupport) / 30.0;
        bestPrice = bestPrice * (26.0 + fin.Population) / 30.0;
        bestPrice = bestPrice * (26.0 + (8 - fin.CostOfLiving)) / 30.0;
        bestPrice = bestPrice * (36.0 + (8 - fin.Competition)) / 40.0;
        bestPrice = bestPrice * (((double)fin.StadiumValue + 200.0) / 400.0);
        bestPrice = bestPrice * (46.0 + personality) / 50.0;

        double price = (double)fin.SuitePrice;
        double factor = (bestPrice - price) / 10.0;
        fin.SuitePrice = fin.SuitePrice + (int)factor;
        if (fin.SuitePrice < 500) fin.SuitePrice = 500;
    }

    /// <summary>
    /// Adjusts club seat price toward market best price.
    /// Port of CTeamFinancial::SetEndOfSeasonClubPrice().
    /// </summary>
    public static void AdjustClubPrice(TeamFinancial fin, int personality, double avgRosterValue,
        int wins, int losses, int hiGames)
    {
        if (avgRosterValue <= 0) avgRosterValue = 1.0;
        double rosterFactor = fin.TeamRosterValue / avgRosterValue;

        double avgPrice = 150.0;

        double bestPrice = avgPrice * (16.0 + fin.Economy) / 20.0 * rosterFactor;
        bestPrice = bestPrice * (16.0 + fin.FanSupport) / 20.0;
        bestPrice = bestPrice * (36.0 + fin.Population) / 40.0;
        bestPrice = bestPrice * (26.0 + (8 - fin.CostOfLiving)) / 30.0;
        bestPrice = bestPrice * (36.0 + (8 - fin.Competition)) / 40.0;
        bestPrice = bestPrice * (((double)fin.StadiumValue + 200.0) / 400.0);
        bestPrice = bestPrice * (46.0 + personality) / 50.0;

        double price = (double)fin.ClubPrice;
        double factor = (bestPrice - price) / 10.0;
        fin.ClubPrice = fin.ClubPrice + (int)factor;
        if (fin.ClubPrice < 15) fin.ClubPrice = 15;
    }

    /// <summary>
    /// Adjusts concession price toward market best price.
    /// Port of CTeamFinancial::SetEndOfSeasonConcessionPrice().
    /// </summary>
    public static void AdjustConcessionPrice(TeamFinancial fin, double avgRosterValue)
    {
        if (avgRosterValue <= 0) avgRosterValue = 1.0;
        double rosterFactor = fin.TeamRosterValue / avgRosterValue;

        double price = (double)fin.Concessions + (double)fin.TicketPrice;
        double avgPrice = 700.0;

        double bestPrice = avgPrice * (26.0 + fin.Economy) / 30.0 * rosterFactor;
        bestPrice = bestPrice * (46.0 + fin.FanSupport) / 50.0;
        bestPrice = bestPrice * (106.0 + fin.Population) / 110.0;
        bestPrice = bestPrice * (16.0 + (8 - fin.CostOfLiving)) / 20.0;
        bestPrice = bestPrice * (106.0 + (8 - fin.Competition)) / 110.0;
        bestPrice = bestPrice * (((double)fin.StadiumValue + 200.0) / 400.0);

        double factor = (bestPrice - price) / 10.0;
        fin.Concessions = fin.Concessions + (int)factor;
        fin.Concessions = (int)((double)fin.Concessions / 10.0) * 10;
        if (fin.Concessions < 50) fin.Concessions = 50;
    }

    /// <summary>
    /// Adjusts parking price based on composite city score.
    /// Port of CTeamFinancial::SetEndOfSeasonParkingPrice().
    /// </summary>
    public static void AdjustParkingPrice(TeamFinancial fin, double avgRosterValue)
    {
        if (avgRosterValue <= 0) avgRosterValue = 1.0;
        double rosterFactor = fin.TeamRosterValue / avgRosterValue;

        double sc = (double)fin.Economy * 6.0 +
            (double)fin.FanSupport * 1.0 +
            (double)fin.Population * 6.0 +
            (double)(8 - fin.CostOfLiving) * 1.0 +
            (double)(8 - fin.Competition) * 1.0;

        double f = (((double)fin.StadiumValue + 500.0) / 700.0);

        sc = (4.0 + sc / 15.0) / 5.0 * rosterFactor * f;

        fin.Parking = 3 + (int)sc;
        if (fin.Parking < 1) fin.Parking = 1;
    }

    /// <summary>
    /// Adjusts team value based on season profit/loss.
    /// Port of CTeamFinancial::SetEndOfSeasonValue().
    /// </summary>
    public static void AdjustTeamValue(TeamFinancial fin)
    {
        long profit = (long)(fin.SeasonTotalRevenue - fin.SeasonTotalExpenses);
        fin.CurrentValue = fin.CurrentValue + profit / 100000;
        if (fin.CurrentValue < 500) fin.CurrentValue = 500;
        else if (fin.CurrentValue > 7500) fin.CurrentValue = 7500;
    }

    /// <summary>
    /// Depreciates arena value.
    /// Port of CTeamFinancial::SetEndOfSeasonArenaValue().
    /// </summary>
    public static void AdjustArenaValue(TeamFinancial fin, Random random)
    {
        double avgTeamValue = 175.0;
        double currentTeamValue = (double)fin.CurrentValue / 10.0;
        if (currentTeamValue <= 0) currentTeamValue = 1.0;
        double factor = 1.0 - avgTeamValue / currentTeamValue * 0.05;
        double newArenaValue = (double)fin.StadiumValue * factor;
        fin.StadiumValue = (int)newArenaValue - 1 + IntRandom(random, 2);
        if (fin.StadiumValue < 10) fin.StadiumValue = 10;
    }

    /// <summary>
    /// Recalculates misc expense levels from current value.
    /// Port of CTeamFinancial::SetEndOfSeasonMiscExpenses().
    /// </summary>
    public static void AdjustMiscExpenses(TeamFinancial fin)
    {
        fin.OperatingLevel = (int)((double)fin.CurrentValue * 1000.0 / 20.0 / 82.0);
        fin.ArenaLevel = (int)((double)fin.StadiumValue * 10000.0 / 100.0 / 82.0);
        fin.MarketingLevel = (int)((double)fin.CurrentValue * 1000.0 / 50.0 / 82.0);
    }

    /// <summary>
    /// Adjusts misc revenue sources based on profitability and city metrics.
    /// Port of CTeamFinancial::SetEndOfSeasonMiscRevenues().
    /// Note: faithfully ports the double sc/48 bug from C++ lines 711-713.
    /// </summary>
    public static void AdjustMiscRevenues(TeamFinancial fin)
    {
        if (fin.SeasonTotalExpenses == 0) return;

        double profitPct = (double)fin.SeasonTotalRevenue / (double)fin.SeasonTotalExpenses;

        // TV/Radio city score
        double sc = (double)fin.Economy * 3.0 + (double)fin.Interest * 4.0 +
            (double)fin.FanSupport * 2.0 +
            (double)fin.Population * 2.0 + (double)(8 - fin.CostOfLiving) * 1.0 +
            (double)(8 - fin.Competition) * 2.0 + (double)fin.PoliticalSupport * 1.0;

        sc = (4.0 + sc / 60.0) / 5.0;

        fin.LocalTv = (long)((double)fin.LocalTv * profitPct * sc);
        fin.LocalRadio = (long)((double)fin.LocalRadio * profitPct * sc);

        // Ad/Sponsorship city score (faithfully porting double sc/48 bug)
        sc = (double)fin.Economy * 3.0 +
            (double)fin.FanSupport * 2.0 + (double)fin.Interest * 2.0 +
            (double)fin.Population * 1.0 + (double)(8 - fin.CostOfLiving) * 2.0 +
            (double)(8 - fin.Competition) * 1.0 + (double)fin.PoliticalSupport * 1.0;

        sc = sc / 48.0;
        sc = (4.0 + sc / 48.0) / 5.0; // double sc/48 — faithful port

        fin.Advertising = (long)((double)fin.Advertising * profitPct * sc);
        fin.Sponsorship = (long)((double)fin.Sponsorship * profitPct * sc);

        // Value-based caps
        if ((double)fin.LocalTv >= (double)fin.CurrentValue / 0.03)
            fin.LocalTv = (long)((double)fin.CurrentValue / 0.03);
        if ((double)fin.LocalRadio >= (double)fin.CurrentValue / 0.06)
            fin.LocalRadio = (long)((double)fin.CurrentValue / 0.06);
        if ((double)fin.Advertising >= (double)fin.CurrentValue / 0.08)
            fin.Advertising = (long)((double)fin.CurrentValue / 0.08);
        if ((double)fin.Sponsorship >= (double)fin.CurrentValue / 0.12)
            fin.Sponsorship = (long)((double)fin.CurrentValue / 0.12);
    }

    /// <summary>
    /// Sets owner salary cap based on team value and spending preference.
    /// Port of CTeamFinancial::SetEndOfSeasonSalaryCap().
    /// </summary>
    public static void AdjustSalaryCap(TeamFinancial fin)
    {
        double f = (25.0 + (double)fin.PlayerSpending) / 100.0;
        fin.OwnerSalaryCap = (long)((double)fin.CurrentValue * f * 100000.0);
    }

    /// <summary>
    /// Probabilistically adjusts city metrics based on revenue change.
    /// Port of CTeamFinancial::SetEndOfSeasonCityAdjustments().
    /// </summary>
    public static void AdjustCityMetrics(TeamFinancial fin, decimal lastYearTotalRevenue, Random random)
    {
        if (fin.SeasonTotalExpenses == 0 || lastYearTotalRevenue == 0) return;

        double revenueIncreasePct = (double)fin.SeasonTotalRevenue / (double)lastYearTotalRevenue;

        if (revenueIncreasePct < 1.0)
        {
            double f = 1.0 - revenueIncreasePct;
            if (random.NextDouble() < f) fin.FanSupport--;
            if (random.NextDouble() < f) fin.Economy--;
            if (random.NextDouble() < f) fin.CostOfLiving++;
            if (random.NextDouble() < f) fin.PoliticalSupport--;
            if (random.NextDouble() < f) fin.Interest--;
            if (random.NextDouble() < f) fin.Population--;
            if (random.NextDouble() < f) fin.Competition++;
        }
        else
        {
            double f = revenueIncreasePct - 1.0;
            if (random.NextDouble() < f) fin.FanSupport++;
            if (random.NextDouble() < f) fin.Economy++;
            if (random.NextDouble() < f) fin.CostOfLiving--;
            if (random.NextDouble() < f) fin.PoliticalSupport++;
            if (random.NextDouble() < f) fin.Interest++;
            if (random.NextDouble() < f) fin.Population++;
            if (random.NextDouble() < f) fin.Competition--;
        }

        // Clamp all to [1, 7]
        fin.FanSupport = Math.Clamp(fin.FanSupport, 1, 7);
        fin.Economy = Math.Clamp(fin.Economy, 1, 7);
        fin.CostOfLiving = Math.Clamp(fin.CostOfLiving, 1, 7);
        fin.PoliticalSupport = Math.Clamp(fin.PoliticalSupport, 1, 7);
        fin.Interest = Math.Clamp(fin.Interest, 1, 7);
        fin.Population = Math.Clamp(fin.Population, 1, 7);
        fin.Competition = Math.Clamp(fin.Competition, 1, 7);
    }

    // ── End-of-Season Orchestrator ─────────────────────────────

    /// <summary>
    /// Runs all end-of-season financial adjustments for all teams.
    /// </summary>
    public static void ProcessEndOfSeasonFinancials(League league, Random? random = null)
    {
        random ??= Random.Shared;

        // Compute roster values
        foreach (var team in league.Teams)
            SetTeamRosterValue(team.Financial, team);

        double avgRosterValue = CalculateLeagueAverageRosterValue(league);

        foreach (var team in league.Teams)
        {
            var fin = team.Financial;
            decimal lastYearRevenue = fin.SeasonTotalRevenue;

            int personality = 4;
            if (team.Coach != null)
                personality = team.Coach.Power;

            int wins = team.Record.Wins;
            int losses = team.Record.Losses;
            int hiGames = league.Schedule.GamesInSeason > 0 ? league.Schedule.GamesInSeason : 82;

            // Price adjustments
            AdjustTicketPrice(fin, personality, avgRosterValue, wins, losses, hiGames);
            AdjustSuitePrice(fin, personality, avgRosterValue, wins, losses, hiGames);
            AdjustClubPrice(fin, personality, avgRosterValue, wins, losses, hiGames);
            AdjustConcessionPrice(fin, avgRosterValue);
            AdjustParkingPrice(fin, avgRosterValue);

            // Value adjustments
            AdjustTeamValue(fin);
            AdjustArenaValue(fin, random);

            // Misc recalculations
            AdjustMiscExpenses(fin);
            AdjustMiscRevenues(fin);
            AdjustSalaryCap(fin);

            // City metrics
            AdjustCityMetrics(fin, lastYearRevenue, random);

            // Relocation evaluation
            EvaluateRelocation(fin, (int)(fin.CurrentValue / 10), random);
        }
    }

    // ── Relocation ─────────────────────────────────────────────

    /// <summary>
    /// Evaluates whether a team should consider relocating.
    /// Port of CTeamFinancial::PossibleMoveCity().
    /// </summary>
    public static void EvaluateRelocation(TeamFinancial fin, int cityCost, Random random)
    {
        int competition = 8 - fin.Competition;
        int costOfLiving = 8 - fin.CostOfLiving;
        int sc = fin.PoliticalSupport * 7 + fin.Economy * 6 +
                fin.FanSupport * 5 + costOfLiving * 4 +
                fin.Population * 3 + fin.Interest * 2 +
                competition * 1;

        double fa = (double)fin.CurrentValue * (double)sc / 28.0;

        int factor = (int)fa - cityCost * 20;
        int n = IntRandom(random, 25000);
        if (n <= factor)
        {
            fin.PossibleMoveCity = true;
            fin.PossibleMoveYears = IntRandom(random, 3) + 3;
            fin.PossibleMoveScore = factor;
        }
    }

    // ── Budget Reset ───────────────────────────────────────────

    /// <summary>
    /// Clears all season budget accumulators.
    /// Port of CTeamFinancial::ClearBudgetData().
    /// </summary>
    public static void ClearBudgetData(TeamFinancial fin)
    {
        fin.HomeGames = 0;
        fin.SeasonAttendance = 0;
        fin.SeasonSuitesSold = 0;
        fin.SeasonClubsSold = 0;
        fin.AttendanceRevenue = 0;
        fin.SeasonSuiteRevenue = 0;
        fin.SeasonClubRevenue = 0;
        fin.SeasonConcessionRevenue = 0;
        fin.SeasonParkingRevenue = 0;
        fin.SeasonTotalRevenue = 0;
        fin.SeasonPayroll = 0;
        fin.SeasonScoutExpenses = 0;
        fin.SeasonCoachExpenses = 0;
        fin.SeasonGmExpenses = 0;
        fin.SeasonOperatingExpenses = 0;
        fin.SeasonArenaExpenses = 0;
        fin.SeasonMarketingExpenses = 0;
        fin.SeasonTotalExpenses = 0;
    }

    // ── Helpers ────────────────────────────────────────────────

    /// <summary>
    /// Returns a random integer from 1 to n (inclusive), matching C++ IntRandom(n).
    /// </summary>
    private static int IntRandom(Random random, int n)
    {
        if (n <= 0) return 1;
        return random.Next(1, n + 1);
    }
}
