using JumpShotBasketball.Core.Constants;

namespace JumpShotBasketball.Core.Models.Team;

/// <summary>
/// Team financial data: arena, pricing, revenue, expenses, city metrics.
/// Maps to C++ CTeamFinancial. Uses decimal for monetary values.
/// </summary>
public class TeamFinancial
{
    public int OriginalSlot { get; set; }

    // Identity
    public string TeamName { get; set; } = string.Empty;
    public string CityName { get; set; } = string.Empty;
    public string ArenaName { get; set; } = string.Empty;

    // Expense budget levels (1-5 scale)
    public int ArenaLevel { get; set; }
    public int MarketingLevel { get; set; }
    public int OperatingLevel { get; set; }

    // Per-game expenses
    public int HomeGames { get; set; }
    public decimal ScoutExpenses { get; set; }
    public decimal CoachExpenses { get; set; }
    public decimal GmExpenses { get; set; }
    public decimal ArenaExpenses { get; set; }
    public decimal MarketingExpenses { get; set; }
    public decimal OperatingExpenses { get; set; }
    public decimal GameTotalExpenses { get; set; }

    // Season expenses
    public decimal SeasonTotalExpenses { get; set; }
    public decimal SeasonScoutExpenses { get; set; }
    public decimal SeasonCoachExpenses { get; set; }
    public decimal SeasonGmExpenses { get; set; }
    public decimal SeasonArenaExpenses { get; set; }
    public decimal SeasonMarketingExpenses { get; set; }
    public decimal SeasonOperatingExpenses { get; set; }

    // Owner settings
    public decimal OwnerSalaryCap { get; set; }
    public int OwnerPatience { get; set; } = 3;
    public int PlayerSpending { get; set; } = 50;

    // Per-game revenue
    public decimal TicketsSold { get; set; }
    public decimal TicketRevenue { get; set; }
    public decimal GamePayroll { get; set; }
    public decimal SuitesSold { get; set; }
    public decimal ClubsSold { get; set; }
    public decimal ParkingSold { get; set; }
    public decimal SuiteRevenue { get; set; }
    public decimal ClubRevenue { get; set; }
    public decimal ParkingRevenue { get; set; }
    public decimal ConcessionRevenue { get; set; }
    public decimal GameTotalRevenue { get; set; }

    // Season revenue
    public decimal SeasonAttendance { get; set; }
    public decimal AttendanceRevenue { get; set; }
    public decimal SeasonPayroll { get; set; }
    public decimal SeasonSuitesSold { get; set; }
    public decimal SeasonClubsSold { get; set; }
    public decimal SeasonParkingSold { get; set; }
    public decimal SeasonSuiteRevenue { get; set; }
    public decimal SeasonClubRevenue { get; set; }
    public decimal SeasonParkingRevenue { get; set; }
    public decimal SeasonConcessionRevenue { get; set; }
    public decimal SeasonTotalRevenue { get; set; }

    // Pricing
    public decimal TicketPrice { get; set; } = LeagueConstants.DefaultTicketPrice;
    public decimal SuitePrice { get; set; } = LeagueConstants.DefaultSuitePrice;
    public decimal ClubPrice { get; set; } = LeagueConstants.DefaultClubPrice;
    public decimal Concessions { get; set; } = LeagueConstants.DefaultConcessions;
    public decimal Parking { get; set; } = LeagueConstants.DefaultParking;

    // Media and sponsorship revenue
    public decimal NetworkShare { get; set; } = LeagueConstants.DefaultNetworkShare;
    public decimal LocalTv { get; set; } = LeagueConstants.DefaultLocalTv;
    public decimal LocalRadio { get; set; } = LeagueConstants.DefaultLocalRadio;
    public decimal Advertising { get; set; } = LeagueConstants.DefaultAdvertising;
    public decimal Sponsorship { get; set; } = LeagueConstants.DefaultSponsorship;

    // Team value
    public decimal CurrentValue { get; set; } = LeagueConstants.DefaultCurrentValue;
    public double TeamRosterValue { get; set; }

    // Arena capacity
    public long Capacity { get; set; } = LeagueConstants.DefaultCapacity;
    public long Suites { get; set; } = LeagueConstants.DefaultSuites;
    public long ClubSeats { get; set; } = LeagueConstants.DefaultClubSeats;
    public long ParkingSpots { get; set; } = LeagueConstants.DefaultParkingSpots;

    // Possible expansion capacity
    public long PossibleCapacity { get; set; }
    public long PossibleSuites { get; set; }
    public long PossibleClubSeats { get; set; }
    public long PossibleParkingSpots { get; set; }

    // City metrics (1-5 scale)
    public int FanSupport { get; set; } = LeagueConstants.DefaultCityRating;
    public int Economy { get; set; } = LeagueConstants.DefaultCityRating;
    public int PoliticalSupport { get; set; } = LeagueConstants.DefaultCityRating;
    public int Interest { get; set; } = LeagueConstants.DefaultCityRating;
    public int Population { get; set; } = LeagueConstants.DefaultCityRating;
    public int CostOfLiving { get; set; } = LeagueConstants.DefaultCityRating;
    public int Competition { get; set; } = LeagueConstants.DefaultCityRating;

    // Stadium
    public int StadiumValue { get; set; }

    // Relocation
    public int PossibleMoveScore { get; set; }
    public bool PossibleMoveCity { get; set; }
    public int PossibleMoveYears { get; set; }
    public int PossibleNewCity { get; set; }
    public bool MoveCity { get; set; }
    public int MoveYears { get; set; }
    public int NewCity { get; set; }
}
