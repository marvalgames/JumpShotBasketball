namespace JumpShotBasketball.Core.Constants;

public static class LeagueConstants
{
    public const int MaxTeams = 32;
    public const int MaxPlayersPerTeam = 30;
    public const int MaxDraftRounds = 3;
    public const int MaxDraftYears = 4;
    public const int MaxContractYears = 7;
    public const int MaxStaffProgressionYears = 6;

    // Financial defaults (from CTeamFinancial constructor)
    public const long DefaultTicketPrice = 420;
    public const long DefaultSuitePrice = 3000;
    public const long DefaultClubPrice = 150;
    public const long DefaultNetworkShare = 500_000;
    public const long DefaultLocalTv = 100_000;
    public const long DefaultLocalRadio = 50_000;
    public const long DefaultConcessions = 120;
    public const long DefaultParking = 6;
    public const long DefaultAdvertising = 25_000;
    public const long DefaultSponsorship = 25_000;
    public const long DefaultCurrentValue = 1750;

    // Arena defaults
    public const long DefaultCapacity = 19_000;
    public const long DefaultSuites = 85;
    public const long DefaultClubSeats = 2200;
    public const long DefaultParkingSpots = 5000;

    // Salary cap default (from CBBallSettings)
    public const double DefaultSalaryCap = 3550;

    // Default city rating
    public const int DefaultCityRating = 3;

    // Salary minimums by years of service (from Constants.h MIN0-MIN10)
    public static readonly int[] SalaryMinimumByYos = { 35, 51, 59, 61, 64, 70, 76, 82, 89, 100, 103 };

    // Salary maximums by years of service (from Constants.h MAX0-MAX10)
    public static readonly int[] SalaryMaximumByYos = { 1063, 1063, 1063, 1063, 1063, 1063, 1063, 1275, 1275, 1275, 1488 };

    // Mid-level exception minimum (from Constants.h MINEX)
    public const int MidLevelExceptionMinimum = 120;

    // Rookie scale salary caps
    public const int RookieSalaryCapYear1 = 295;
    public const int RookieSalaryCapYear2 = 317;
    public const int RookieSalaryCapYear3 = 339;
}
