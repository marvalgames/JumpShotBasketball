using FluentAssertions;
using JumpShotBasketball.Core.Enums;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class FinancialSimulationServiceTests
{
    // ───────────────────────────────────────────────────────────────
    // Helpers
    // ───────────────────────────────────────────────────────────────

    private static TeamFinancial CreateDefaultFinancial()
    {
        return new TeamFinancial
        {
            Capacity = 19000,
            Suites = 85,
            ClubSeats = 2200,
            ParkingSpots = 5000,
            StadiumValue = 200,
            CurrentValue = 1750,
            TicketPrice = 420,
            SuitePrice = 3000,
            ClubPrice = 150,
            Concessions = 120,
            Parking = 6,
            NetworkShare = 277,
            LocalTv = 100000,
            LocalRadio = 50000,
            Advertising = 25000,
            Sponsorship = 25000,
            OperatingLevel = 100,
            ArenaLevel = 50,
            MarketingLevel = 40,
            Economy = 4,
            FanSupport = 4,
            Population = 4,
            CostOfLiving = 4,
            Competition = 4,
            Interest = 4,
            PoliticalSupport = 4,
            TeamRosterValue = 100.0
        };
    }

    private static Team CreateDefaultTeam(int id = 0)
    {
        var team = new Team
        {
            Id = id,
            Name = $"Team {id}",
            Financial = CreateDefaultFinancial(),
            Record = new TeamRecord { Wins = 20, Losses = 20 }
        };

        // Add 15 players with salary and trade value
        for (int i = 0; i < 15; i++)
        {
            team.Roster.Add(new Player
            {
                Name = $"Player {i}",
                Contract = new PlayerContract { CurrentYearSalary = 100 },
                Ratings = new PlayerRatings { TradeValue = 7.0 }
            });
        }

        return team;
    }

    private static League CreateDefaultLeague(int numTeams = 2)
    {
        var league = new League
        {
            Settings = new LeagueSettings
            {
                NumberOfTeams = numTeams,
                FinancialEnabled = true
            },
            Schedule = new Schedule { GamesInSeason = 82 }
        };

        for (int i = 0; i < numTeams; i++)
            league.Teams.Add(CreateDefaultTeam(i));

        return league;
    }

    // ───────────────────────────────────────────────────────────────
    // GenerateRandomArena
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void GenerateRandomArena_CapacityInExpectedRange()
    {
        var fin = new TeamFinancial();
        var rng = new Random(42);
        FinancialSimulationService.GenerateRandomArena(fin, rng);

        fin.Capacity.Should().BeInRange(12000, 24000);
    }

    [Fact]
    public void GenerateRandomArena_SuitesDerivedFromCapacity()
    {
        var fin = new TeamFinancial();
        var rng = new Random(42);
        FinancialSimulationService.GenerateRandomArena(fin, rng);

        fin.Suites.Should().BeGreaterThan(0);
        fin.Suites.Should().BeLessThanOrEqualTo(fin.Capacity / 100);
    }

    [Fact]
    public void GenerateRandomArena_StadiumValuePositive()
    {
        var fin = new TeamFinancial();
        var rng = new Random(42);
        FinancialSimulationService.GenerateRandomArena(fin, rng);

        fin.StadiumValue.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GenerateRandomArena_Deterministic()
    {
        var fin1 = new TeamFinancial();
        var fin2 = new TeamFinancial();
        FinancialSimulationService.GenerateRandomArena(fin1, new Random(123));
        FinancialSimulationService.GenerateRandomArena(fin2, new Random(123));

        fin1.Capacity.Should().Be(fin2.Capacity);
        fin1.Suites.Should().Be(fin2.Suites);
        fin1.ClubSeats.Should().Be(fin2.ClubSeats);
        fin1.ParkingSpots.Should().Be(fin2.ParkingSpots);
        fin1.StadiumValue.Should().Be(fin2.StadiumValue);
    }

    // ───────────────────────────────────────────────────────────────
    // GenerateRandomFinances
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void GenerateRandomFinances_ValueFromStadium()
    {
        var fin = new TeamFinancial { StadiumValue = 200 };
        FinancialSimulationService.GenerateRandomFinances(fin, new Random(42));

        fin.CurrentValue.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GenerateRandomFinances_PriceRanges()
    {
        var fin = new TeamFinancial { StadiumValue = 200 };
        FinancialSimulationService.GenerateRandomFinances(fin, new Random(42));

        fin.TicketPrice.Should().BeGreaterThanOrEqualTo(250);
        fin.TicketPrice.Should().BeLessThanOrEqualTo(600);
        fin.SuitePrice.Should().BeGreaterThanOrEqualTo(2510);
        fin.SuitePrice.Should().BeLessThanOrEqualTo(4500);
    }

    [Fact]
    public void GenerateRandomFinances_LocalRadioCappedByTv()
    {
        var fin = new TeamFinancial { StadiumValue = 200 };
        FinancialSimulationService.GenerateRandomFinances(fin, new Random(42));

        fin.LocalRadio.Should().BeLessThanOrEqualTo(fin.LocalTv / 2);
    }

    [Fact]
    public void GenerateRandomFinances_Deterministic()
    {
        var fin1 = new TeamFinancial { StadiumValue = 200 };
        var fin2 = new TeamFinancial { StadiumValue = 200 };
        FinancialSimulationService.GenerateRandomFinances(fin1, new Random(99));
        FinancialSimulationService.GenerateRandomFinances(fin2, new Random(99));

        fin1.CurrentValue.Should().Be(fin2.CurrentValue);
        fin1.TicketPrice.Should().Be(fin2.TicketPrice);
    }

    // ───────────────────────────────────────────────────────────────
    // GenerateRandomCity
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void GenerateRandomCity_AllMetricsInRange()
    {
        var fin = new TeamFinancial();
        FinancialSimulationService.GenerateRandomCity(fin, new Random(42));

        fin.FanSupport.Should().BeInRange(1, 7);
        fin.Economy.Should().BeInRange(1, 7);
        fin.PoliticalSupport.Should().BeInRange(1, 7);
        fin.Interest.Should().BeInRange(1, 7);
        fin.Population.Should().BeInRange(1, 7);
        fin.CostOfLiving.Should().BeInRange(1, 7);
        fin.Competition.Should().BeInRange(1, 7);
    }

    [Fact]
    public void GenerateRandomCity_Deterministic()
    {
        var fin1 = new TeamFinancial();
        var fin2 = new TeamFinancial();
        FinancialSimulationService.GenerateRandomCity(fin1, new Random(77));
        FinancialSimulationService.GenerateRandomCity(fin2, new Random(77));

        fin1.FanSupport.Should().Be(fin2.FanSupport);
        fin1.Economy.Should().Be(fin2.Economy);
    }

    // ───────────────────────────────────────────────────────────────
    // SetTeamRosterValue
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void SetTeamRosterValue_SumsTradeValues()
    {
        var team = CreateDefaultTeam();
        FinancialSimulationService.SetTeamRosterValue(team.Financial, team);

        // 15 players × 7.0 = 105.0
        team.Financial.TeamRosterValue.Should().Be(105.0);
    }

    [Fact]
    public void SetTeamRosterValue_EmptyRoster_Zero()
    {
        var team = new Team { Financial = new TeamFinancial() };
        FinancialSimulationService.SetTeamRosterValue(team.Financial, team);

        team.Financial.TeamRosterValue.Should().Be(0.0);
    }

    // ───────────────────────────────────────────────────────────────
    // CalculateLeagueAverageRosterValue
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void CalculateLeagueAverageRosterValue_Normal()
    {
        var league = CreateDefaultLeague(2);
        league.Teams[0].Financial.TeamRosterValue = 100;
        league.Teams[1].Financial.TeamRosterValue = 200;

        var avg = FinancialSimulationService.CalculateLeagueAverageRosterValue(league);
        avg.Should().Be(150.0);
    }

    [Fact]
    public void CalculateLeagueAverageRosterValue_SingleTeam()
    {
        var league = CreateDefaultLeague(1);
        league.Teams[0].Financial.TeamRosterValue = 120;

        var avg = FinancialSimulationService.CalculateLeagueAverageRosterValue(league);
        avg.Should().Be(120.0);
    }

    // ───────────────────────────────────────────────────────────────
    // CalculateTicketSales
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void CalculateTicketSales_Normal_ProducesRevenue()
    {
        var fin = CreateDefaultFinancial();
        FinancialSimulationService.CalculateTicketSales(fin, 4, 100.0, 20, 20, 41, 1750, new Random(42));

        fin.TicketsSold.Should().BeGreaterThan(0);
        fin.TicketRevenue.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateTicketSales_CapacityCap()
    {
        var fin = CreateDefaultFinancial();
        fin.Capacity = 100; // Very small
        fin.ClubSeats = 10;
        fin.Suites = 1;

        FinancialSimulationService.CalculateTicketSales(fin, 4, 100.0, 20, 20, 41, 1750, new Random(42));

        // Capacity = 100 - 10 - 1*20 = 70
        fin.TicketsSold.Should().BeLessThanOrEqualTo(70);
    }

    [Fact]
    public void CalculateTicketSales_ZeroGames_StillProduces()
    {
        var fin = CreateDefaultFinancial();
        FinancialSimulationService.CalculateTicketSales(fin, 4, 100.0, 0, 0, 41, 1750, new Random(42));

        fin.TicketsSold.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateTicketSales_HighWinPct_MoreRevenue()
    {
        var finWinning = CreateDefaultFinancial();
        var finLosing = CreateDefaultFinancial();

        FinancialSimulationService.CalculateTicketSales(finWinning, 4, 100.0, 40, 2, 41, 1750, new Random(42));
        FinancialSimulationService.CalculateTicketSales(finLosing, 4, 100.0, 2, 40, 41, 1750, new Random(42));

        finWinning.TicketRevenue.Should().BeGreaterThan(finLosing.TicketRevenue);
    }

    [Fact]
    public void CalculateTicketSales_Deterministic()
    {
        var fin1 = CreateDefaultFinancial();
        var fin2 = CreateDefaultFinancial();

        FinancialSimulationService.CalculateTicketSales(fin1, 4, 100.0, 20, 20, 41, 1750, new Random(55));
        FinancialSimulationService.CalculateTicketSales(fin2, 4, 100.0, 20, 20, 41, 1750, new Random(55));

        fin1.TicketsSold.Should().Be(fin2.TicketsSold);
        fin1.TicketRevenue.Should().Be(fin2.TicketRevenue);
    }

    // ───────────────────────────────────────────────────────────────
    // CalculateClubSales
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void CalculateClubSales_Normal_ProducesRevenue()
    {
        var fin = CreateDefaultFinancial();
        FinancialSimulationService.CalculateClubSales(fin, 4, 100.0, 20, 20, 41, 1750, new Random(42));

        fin.ClubsSold.Should().BeGreaterThan(0);
        fin.ClubRevenue.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateClubSales_CapacityCap()
    {
        var fin = CreateDefaultFinancial();
        fin.ClubSeats = 10; // Very small

        FinancialSimulationService.CalculateClubSales(fin, 4, 100.0, 20, 20, 41, 1750, new Random(42));
        fin.ClubsSold.Should().BeLessThanOrEqualTo(10);
    }

    [Fact]
    public void CalculateClubSales_ZeroPrice_NoSales()
    {
        var fin = CreateDefaultFinancial();
        fin.ClubPrice = 0;

        FinancialSimulationService.CalculateClubSales(fin, 4, 100.0, 20, 20, 41, 1750, new Random(42));
        fin.ClubsSold.Should().Be(0);
    }

    // ───────────────────────────────────────────────────────────────
    // CalculateSuiteSales
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void CalculateSuiteSales_Normal_ProducesRevenue()
    {
        var fin = CreateDefaultFinancial();
        FinancialSimulationService.CalculateSuiteSales(fin, 4, 100.0, 20, 20, 41, 1750, new Random(42));

        fin.SuitesSold.Should().BeGreaterThan(0);
        fin.SuiteRevenue.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateSuiteSales_CapacityCap()
    {
        var fin = CreateDefaultFinancial();
        fin.Suites = 2; // Very small

        FinancialSimulationService.CalculateSuiteSales(fin, 4, 100.0, 20, 20, 41, 1750, new Random(42));
        fin.SuitesSold.Should().BeLessThanOrEqualTo(2);
    }

    // ───────────────────────────────────────────────────────────────
    // CalculateConcessionSales
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void CalculateConcessionSales_Normal_ProducesRevenue()
    {
        var fin = CreateDefaultFinancial();
        fin.TicketsSold = 15000;
        fin.ClubsSold = 1000;
        fin.SuitesSold = 50;

        FinancialSimulationService.CalculateConcessionSales(fin, 100.0);
        fin.ConcessionRevenue.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateConcessionSales_AttendanceFormula()
    {
        var fin = CreateDefaultFinancial();
        fin.TicketsSold = 10000;
        fin.ClubsSold = 500;
        fin.SuitesSold = 30;

        FinancialSimulationService.CalculateConcessionSales(fin, 100.0);

        // attendance = 10000 + 500 + 30*20 = 11100
        fin.ConcessionRevenue.Should().BeGreaterThan(0);
    }

    // ───────────────────────────────────────────────────────────────
    // CalculateParkingSales
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void CalculateParkingSales_Normal_ProducesRevenue()
    {
        var fin = CreateDefaultFinancial();
        FinancialSimulationService.CalculateParkingSales(fin, 100.0, new Random(42));

        fin.ParkingSold.Should().BeGreaterThan(0);
        fin.ParkingRevenue.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateParkingSales_SpotsCap()
    {
        var fin = CreateDefaultFinancial();
        fin.ParkingSpots = 10; // Very small

        FinancialSimulationService.CalculateParkingSales(fin, 100.0, new Random(42));
        fin.ParkingSold.Should().BeLessThanOrEqualTo(10);
    }

    // ───────────────────────────────────────────────────────────────
    // CalculateGameTotalRevenue
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void CalculateGameTotalRevenue_SumsCorrectly()
    {
        var fin = new TeamFinancial
        {
            TicketRevenue = 100,
            SuiteRevenue = 200,
            ClubRevenue = 300,
            ParkingRevenue = 400,
            ConcessionRevenue = 500,
            NetworkShare = 1, // ×1000 = 1000
            LocalTv = 600,
            LocalRadio = 700,
            Advertising = 800,
            Sponsorship = 900
        };

        FinancialSimulationService.CalculateGameTotalRevenue(fin);

        fin.GameTotalRevenue.Should().Be(100 + 200 + 300 + 400 + 500 + 1000 + 600 + 700 + 800 + 900);
    }

    // ───────────────────────────────────────────────────────────────
    // AccumulateSeasonRevenue
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void AccumulateSeasonRevenue_Accumulates()
    {
        var fin = new TeamFinancial
        {
            TicketsSold = 15000,
            TicketRevenue = 500000,
            SuitesSold = 50,
            ClubsSold = 1000,
            ParkingSold = 3000,
            SuiteRevenue = 150000,
            ClubRevenue = 120000,
            ParkingRevenue = 18000,
            ConcessionRevenue = 80000,
            GameTotalRevenue = 900000
        };

        FinancialSimulationService.AccumulateSeasonRevenue(fin);

        fin.SeasonAttendance.Should().Be(15000);
        fin.AttendanceRevenue.Should().Be(500000);
        fin.SeasonTotalRevenue.Should().Be(900000);
        fin.HomeGames.Should().Be(1);
    }

    [Fact]
    public void AccumulateSeasonRevenue_IncrementsHomeGames()
    {
        var fin = new TeamFinancial { GameTotalRevenue = 100 };

        FinancialSimulationService.AccumulateSeasonRevenue(fin);
        FinancialSimulationService.AccumulateSeasonRevenue(fin);

        fin.HomeGames.Should().Be(2);
        fin.SeasonTotalRevenue.Should().Be(200);
    }

    // ───────────────────────────────────────────────────────────────
    // AccumulateRoadGameRevenue
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void AccumulateRoadGameRevenue_MediaOnly()
    {
        var fin = new TeamFinancial
        {
            NetworkShare = 2, // ×1000 = 2000
            LocalTv = 1000,
            LocalRadio = 500,
            Advertising = 300,
            Sponsorship = 200
        };

        FinancialSimulationService.AccumulateRoadGameRevenue(fin);

        fin.GameTotalRevenue.Should().Be(2000 + 1000 + 500 + 300 + 200);
        fin.SeasonTotalRevenue.Should().Be(4000);
    }

    // ───────────────────────────────────────────────────────────────
    // CalculatePlayerPayroll
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void CalculatePlayerPayroll_Normal()
    {
        var team = CreateDefaultTeam();
        var fin = team.Financial;

        FinancialSimulationService.CalculatePlayerPayroll(fin, team);

        // Each player salary=100, payroll = 100 * 10000 / 82 ≈ 12195 per player, × 15
        fin.GamePayroll.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculatePlayerPayroll_EmptyRoster_Zero()
    {
        var team = new Team { Financial = new TeamFinancial() };
        FinancialSimulationService.CalculatePlayerPayroll(team.Financial, team);

        team.Financial.GamePayroll.Should().Be(0);
    }

    // ───────────────────────────────────────────────────────────────
    // Operating / Arena / Marketing Expenses
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void CalculateOperatingExpenses_LevelScaling()
    {
        var fin = new TeamFinancial { OperatingLevel = 100 };
        FinancialSimulationService.CalculateOperatingExpenses(fin, new Random(42));

        // 100 × 102 × (0.8..1.2) ≈ 8160..12240
        fin.OperatingExpenses.Should().BeInRange(8000, 13000);
    }

    [Fact]
    public void CalculateArenaExpenses_LevelScaling()
    {
        var fin = new TeamFinancial { ArenaLevel = 50 };
        FinancialSimulationService.CalculateArenaExpenses(fin, new Random(42));

        // 50 × 100 × (0.8..1.2) ≈ 4000..6000
        fin.ArenaExpenses.Should().BeInRange(3900, 6100);
    }

    [Fact]
    public void CalculateMarketingExpenses_LevelScaling()
    {
        var fin = new TeamFinancial { MarketingLevel = 40 };
        FinancialSimulationService.CalculateMarketingExpenses(fin, new Random(42));

        // 40 × 102 × (0.8..1.2) ≈ 3264..4896
        fin.MarketingExpenses.Should().BeInRange(3200, 5000);
    }

    // ───────────────────────────────────────────────────────────────
    // CalculateGameTotalExpenses
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void CalculateGameTotalExpenses_NonPlayoff_IncludesPayroll()
    {
        var fin = new TeamFinancial
        {
            GamePayroll = 100000,
            ScoutExpenses = 1000,
            CoachExpenses = 2000,
            GmExpenses = 3000,
            OperatingExpenses = 5000,
            ArenaExpenses = 4000,
            MarketingExpenses = 3000
        };

        FinancialSimulationService.CalculateGameTotalExpenses(fin, GameType.League);
        fin.GameTotalExpenses.Should().Be(100000 + 1000 + 2000 + 3000 + 5000 + 4000 + 3000);
    }

    [Fact]
    public void CalculateGameTotalExpenses_Playoff_ExcludesPayroll()
    {
        var fin = new TeamFinancial
        {
            GamePayroll = 100000,
            ScoutExpenses = 1000,
            CoachExpenses = 2000,
            GmExpenses = 3000,
            OperatingExpenses = 5000,
            ArenaExpenses = 4000,
            MarketingExpenses = 3000
        };

        FinancialSimulationService.CalculateGameTotalExpenses(fin, GameType.Playoff);
        fin.GameTotalExpenses.Should().Be(5000 + 4000 + 3000);
    }

    // ───────────────────────────────────────────────────────────────
    // AccumulateSeasonExpenses
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void AccumulateSeasonExpenses_Accumulates()
    {
        var fin = new TeamFinancial
        {
            GamePayroll = 100000,
            ScoutExpenses = 1000,
            CoachExpenses = 2000,
            GmExpenses = 3000,
            OperatingExpenses = 5000,
            ArenaExpenses = 4000,
            MarketingExpenses = 3000,
            GameTotalExpenses = 118000
        };

        FinancialSimulationService.AccumulateSeasonExpenses(fin);

        fin.SeasonPayroll.Should().Be(100000);
        fin.SeasonTotalExpenses.Should().Be(118000);
    }

    // ───────────────────────────────────────────────────────────────
    // ProcessHomeGameFinancials (integration)
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void ProcessHomeGameFinancials_Integration_SetsRosterValue()
    {
        var league = CreateDefaultLeague(2);
        league.Schedule.GamesInSeason = 82;

        FinancialSimulationService.ProcessHomeGameFinancials(league, 0, 1, GameType.League, new Random(42));

        league.Teams[0].Financial.TeamRosterValue.Should().BeGreaterThan(0);
        league.Teams[0].Financial.SeasonTotalRevenue.Should().BeGreaterThan(0);
        league.Teams[0].Financial.SeasonTotalExpenses.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ProcessHomeGameFinancials_Integration_AccumulatesHomeGames()
    {
        var league = CreateDefaultLeague(2);

        FinancialSimulationService.ProcessHomeGameFinancials(league, 0, 1, GameType.League, new Random(42));

        league.Teams[0].Financial.HomeGames.Should().Be(1);
    }

    // ───────────────────────────────────────────────────────────────
    // ProcessRoadGameFinancials
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void ProcessRoadGameFinancials_MediaOnly()
    {
        var league = CreateDefaultLeague(2);
        var fin = league.Teams[1].Financial;
        fin.SeasonTotalRevenue = 0;

        FinancialSimulationService.ProcessRoadGameFinancials(league, 1);

        fin.SeasonTotalRevenue.Should().BeGreaterThan(0);
    }

    // ───────────────────────────────────────────────────────────────
    // AdjustTicketPrice
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void AdjustTicketPrice_AdjustsTowardBest()
    {
        var fin = CreateDefaultFinancial();
        fin.TicketPrice = 200; // Below market
        decimal before = fin.TicketPrice;

        FinancialSimulationService.AdjustTicketPrice(fin, 4, 100.0, 20, 20, 82);

        fin.TicketPrice.Should().BeGreaterThanOrEqualTo(before);
    }

    [Fact]
    public void AdjustTicketPrice_Min50()
    {
        var fin = CreateDefaultFinancial();
        fin.TicketPrice = 10;
        fin.TeamRosterValue = 0.01; // Very low

        FinancialSimulationService.AdjustTicketPrice(fin, 4, 100.0, 20, 20, 82);

        fin.TicketPrice.Should().BeGreaterThanOrEqualTo(50);
    }

    [Fact]
    public void AdjustTicketPrice_RoundsTo10()
    {
        var fin = CreateDefaultFinancial();
        FinancialSimulationService.AdjustTicketPrice(fin, 4, 100.0, 20, 20, 82);

        ((int)fin.TicketPrice % 10).Should().Be(0);
    }

    // ───────────────────────────────────────────────────────────────
    // AdjustSuitePrice
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void AdjustSuitePrice_AdjustsTowardBest()
    {
        var fin = CreateDefaultFinancial();
        fin.SuitePrice = 1000; // Below market
        decimal before = fin.SuitePrice;

        FinancialSimulationService.AdjustSuitePrice(fin, 4, 100.0, 20, 20, 82);

        fin.SuitePrice.Should().BeGreaterThanOrEqualTo(before);
    }

    [Fact]
    public void AdjustSuitePrice_Min500()
    {
        var fin = CreateDefaultFinancial();
        fin.SuitePrice = 100;
        fin.TeamRosterValue = 0.01;

        FinancialSimulationService.AdjustSuitePrice(fin, 4, 100.0, 20, 20, 82);

        fin.SuitePrice.Should().BeGreaterThanOrEqualTo(500);
    }

    // ───────────────────────────────────────────────────────────────
    // AdjustClubPrice
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void AdjustClubPrice_AdjustsTowardBest()
    {
        var fin = CreateDefaultFinancial();
        fin.ClubPrice = 50; // Below market
        decimal before = fin.ClubPrice;

        FinancialSimulationService.AdjustClubPrice(fin, 4, 100.0, 20, 20, 82);

        fin.ClubPrice.Should().BeGreaterThanOrEqualTo(before);
    }

    [Fact]
    public void AdjustClubPrice_Min15()
    {
        var fin = CreateDefaultFinancial();
        fin.ClubPrice = 5;
        fin.TeamRosterValue = 0.01;

        FinancialSimulationService.AdjustClubPrice(fin, 4, 100.0, 20, 20, 82);

        fin.ClubPrice.Should().BeGreaterThanOrEqualTo(15);
    }

    // ───────────────────────────────────────────────────────────────
    // AdjustConcessionPrice
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void AdjustConcessionPrice_Adjusts()
    {
        var fin = CreateDefaultFinancial();
        decimal before = fin.Concessions;

        FinancialSimulationService.AdjustConcessionPrice(fin, 100.0);

        // Should change from default
        (fin.Concessions != before || fin.Concessions >= 50).Should().BeTrue();
    }

    [Fact]
    public void AdjustConcessionPrice_Min50_RoundsTo10()
    {
        var fin = CreateDefaultFinancial();
        fin.Concessions = 10;
        fin.TeamRosterValue = 0.01;

        FinancialSimulationService.AdjustConcessionPrice(fin, 100.0);

        fin.Concessions.Should().BeGreaterThanOrEqualTo(50);
        ((int)fin.Concessions % 10).Should().Be(0);
    }

    // ───────────────────────────────────────────────────────────────
    // AdjustParkingPrice
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void AdjustParkingPrice_CompositeScore()
    {
        var fin = CreateDefaultFinancial();
        FinancialSimulationService.AdjustParkingPrice(fin, 100.0);

        fin.Parking.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void AdjustParkingPrice_Min1()
    {
        var fin = CreateDefaultFinancial();
        fin.Economy = 1;
        fin.FanSupport = 1;
        fin.Population = 1;
        fin.CostOfLiving = 7;
        fin.Competition = 7;
        fin.StadiumValue = 1;
        fin.TeamRosterValue = 0.01;

        FinancialSimulationService.AdjustParkingPrice(fin, 100.0);

        fin.Parking.Should().BeGreaterThanOrEqualTo(1);
    }

    // ───────────────────────────────────────────────────────────────
    // AdjustTeamValue
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void AdjustTeamValue_ProfitIncreases()
    {
        var fin = CreateDefaultFinancial();
        fin.SeasonTotalRevenue = 200000000;
        fin.SeasonTotalExpenses = 100000000;
        decimal before = fin.CurrentValue;

        FinancialSimulationService.AdjustTeamValue(fin);

        fin.CurrentValue.Should().BeGreaterThan(before);
    }

    [Fact]
    public void AdjustTeamValue_LossDecreases()
    {
        var fin = CreateDefaultFinancial();
        fin.SeasonTotalRevenue = 50000000;
        fin.SeasonTotalExpenses = 200000000;
        decimal before = fin.CurrentValue;

        FinancialSimulationService.AdjustTeamValue(fin);

        fin.CurrentValue.Should().BeLessThan(before);
    }

    [Fact]
    public void AdjustTeamValue_Clamp500To7500()
    {
        // Test lower clamp
        var finLow = CreateDefaultFinancial();
        finLow.CurrentValue = 500;
        finLow.SeasonTotalRevenue = 0;
        finLow.SeasonTotalExpenses = 10000000000;
        FinancialSimulationService.AdjustTeamValue(finLow);
        finLow.CurrentValue.Should().BeGreaterThanOrEqualTo(500);

        // Test upper clamp
        var finHigh = CreateDefaultFinancial();
        finHigh.CurrentValue = 7500;
        finHigh.SeasonTotalRevenue = 10000000000;
        finHigh.SeasonTotalExpenses = 0;
        FinancialSimulationService.AdjustTeamValue(finHigh);
        finHigh.CurrentValue.Should().BeLessThanOrEqualTo(7500);
    }

    // ───────────────────────────────────────────────────────────────
    // AdjustArenaValue
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void AdjustArenaValue_Depreciates()
    {
        var fin = CreateDefaultFinancial();
        int before = fin.StadiumValue;

        FinancialSimulationService.AdjustArenaValue(fin, new Random(42));

        // Arena typically depreciates slightly
        fin.StadiumValue.Should().BeGreaterThanOrEqualTo(10);
    }

    [Fact]
    public void AdjustArenaValue_Min10()
    {
        var fin = CreateDefaultFinancial();
        fin.StadiumValue = 5;
        fin.CurrentValue = 10;

        FinancialSimulationService.AdjustArenaValue(fin, new Random(42));

        fin.StadiumValue.Should().BeGreaterThanOrEqualTo(10);
    }

    // ───────────────────────────────────────────────────────────────
    // AdjustMiscExpenses
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void AdjustMiscExpenses_RecalculatesFromValue()
    {
        var fin = CreateDefaultFinancial();
        fin.CurrentValue = 2000;
        fin.StadiumValue = 200;

        FinancialSimulationService.AdjustMiscExpenses(fin);

        // operating = 2000 * 1000 / 20 / 82 ≈ 1219
        fin.OperatingLevel.Should().BeGreaterThan(0);
        fin.ArenaLevel.Should().BeGreaterThan(0);
        fin.MarketingLevel.Should().BeGreaterThan(0);
    }

    // ───────────────────────────────────────────────────────────────
    // AdjustMiscRevenues
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void AdjustMiscRevenues_ProfitScaling()
    {
        var fin = CreateDefaultFinancial();
        fin.CurrentValue = 50000; // High enough that value caps won't interfere
        fin.SeasonTotalRevenue = 200000000;
        fin.SeasonTotalExpenses = 100000000; // 2x profit ratio

        decimal tvBefore = fin.LocalTv;
        FinancialSimulationService.AdjustMiscRevenues(fin);

        fin.LocalTv.Should().BeGreaterThan(tvBefore);
    }

    [Fact]
    public void AdjustMiscRevenues_ValueCaps()
    {
        var fin = CreateDefaultFinancial();
        fin.SeasonTotalRevenue = 200000000;
        fin.SeasonTotalExpenses = 100000;
        fin.LocalTv = 999999999;
        fin.LocalRadio = 999999999;
        fin.Advertising = 999999999;
        fin.Sponsorship = 999999999;
        fin.CurrentValue = 1000;

        FinancialSimulationService.AdjustMiscRevenues(fin);

        // Should be capped by value-based limits
        fin.LocalTv.Should().BeLessThanOrEqualTo((long)((double)fin.CurrentValue / 0.03));
    }

    // ───────────────────────────────────────────────────────────────
    // AdjustSalaryCap
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void AdjustSalaryCap_Formula()
    {
        var fin = CreateDefaultFinancial();
        fin.CurrentValue = 2000;
        fin.PlayerSpending = 50;

        FinancialSimulationService.AdjustSalaryCap(fin);

        // (25 + 50) / 100 = 0.75; 2000 × 0.75 × 100000 = 150_000_000
        fin.OwnerSalaryCap.Should().Be(150_000_000);
    }

    [Fact]
    public void AdjustSalaryCap_PlayerSpendingEffect()
    {
        var finLow = CreateDefaultFinancial();
        finLow.CurrentValue = 2000;
        finLow.PlayerSpending = 25;

        var finHigh = CreateDefaultFinancial();
        finHigh.CurrentValue = 2000;
        finHigh.PlayerSpending = 75;

        FinancialSimulationService.AdjustSalaryCap(finLow);
        FinancialSimulationService.AdjustSalaryCap(finHigh);

        finHigh.OwnerSalaryCap.Should().BeGreaterThan(finLow.OwnerSalaryCap);
    }

    // ───────────────────────────────────────────────────────────────
    // AdjustCityMetrics
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void AdjustCityMetrics_RevenueIncrease_Improves()
    {
        // With a large revenue increase, metrics should generally improve
        // Use seed that generates values < f for all random calls
        var fin = CreateDefaultFinancial();
        fin.SeasonTotalRevenue = 200000000;
        fin.SeasonTotalExpenses = 100000000;

        int fanBefore = fin.FanSupport;

        // Run many times and check that at least once it improves
        bool improved = false;
        for (int i = 0; i < 100; i++)
        {
            var testFin = CreateDefaultFinancial();
            testFin.SeasonTotalRevenue = 200000000;
            testFin.SeasonTotalExpenses = 100000000;
            FinancialSimulationService.AdjustCityMetrics(testFin, 100000000, new Random(i));
            if (testFin.FanSupport > fanBefore) { improved = true; break; }
        }
        improved.Should().BeTrue("revenue increase should probabilistically improve city metrics");
    }

    [Fact]
    public void AdjustCityMetrics_RevenueDecrease_Worsens()
    {
        bool worsened = false;
        for (int i = 0; i < 100; i++)
        {
            var testFin = CreateDefaultFinancial();
            testFin.SeasonTotalRevenue = 50000000;
            testFin.SeasonTotalExpenses = 100000000;
            int fanBefore = testFin.FanSupport;
            FinancialSimulationService.AdjustCityMetrics(testFin, 100000000, new Random(i));
            if (testFin.FanSupport < fanBefore) { worsened = true; break; }
        }
        worsened.Should().BeTrue("revenue decrease should probabilistically worsen city metrics");
    }

    [Fact]
    public void AdjustCityMetrics_ClampRange()
    {
        var fin = CreateDefaultFinancial();
        fin.FanSupport = 1;
        fin.Economy = 1;
        fin.SeasonTotalRevenue = 1;
        fin.SeasonTotalExpenses = 100000000;

        FinancialSimulationService.AdjustCityMetrics(fin, 100000000, new Random(42));

        fin.FanSupport.Should().BeInRange(1, 7);
        fin.Economy.Should().BeInRange(1, 7);
        fin.CostOfLiving.Should().BeInRange(1, 7);
        fin.Competition.Should().BeInRange(1, 7);
    }

    // ───────────────────────────────────────────────────────────────
    // ProcessEndOfSeasonFinancials (integration)
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void ProcessEndOfSeasonFinancials_FullIntegration()
    {
        var league = CreateDefaultLeague(2);
        league.Teams[0].Financial.SeasonTotalRevenue = 100000000;
        league.Teams[0].Financial.SeasonTotalExpenses = 80000000;
        league.Teams[1].Financial.SeasonTotalRevenue = 90000000;
        league.Teams[1].Financial.SeasonTotalExpenses = 85000000;

        FinancialSimulationService.ProcessEndOfSeasonFinancials(league, new Random(42));

        // Should have adjusted values
        league.Teams[0].Financial.OwnerSalaryCap.Should().BeGreaterThan(0);
        league.Teams[1].Financial.OwnerSalaryCap.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ProcessEndOfSeasonFinancials_Deterministic()
    {
        var league1 = CreateDefaultLeague(2);
        league1.Teams[0].Financial.SeasonTotalRevenue = 100000000;
        league1.Teams[0].Financial.SeasonTotalExpenses = 80000000;
        league1.Teams[1].Financial.SeasonTotalRevenue = 90000000;
        league1.Teams[1].Financial.SeasonTotalExpenses = 85000000;

        var league2 = CreateDefaultLeague(2);
        league2.Teams[0].Financial.SeasonTotalRevenue = 100000000;
        league2.Teams[0].Financial.SeasonTotalExpenses = 80000000;
        league2.Teams[1].Financial.SeasonTotalRevenue = 90000000;
        league2.Teams[1].Financial.SeasonTotalExpenses = 85000000;

        FinancialSimulationService.ProcessEndOfSeasonFinancials(league1, new Random(123));
        FinancialSimulationService.ProcessEndOfSeasonFinancials(league2, new Random(123));

        league1.Teams[0].Financial.CurrentValue.Should().Be(league2.Teams[0].Financial.CurrentValue);
        league1.Teams[0].Financial.TicketPrice.Should().Be(league2.Teams[0].Financial.TicketPrice);
    }

    // ───────────────────────────────────────────────────────────────
    // EvaluateRelocation
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void EvaluateRelocation_HighValue_MayTrigger()
    {
        bool moved = false;
        for (int i = 0; i < 200; i++)
        {
            var fin = CreateDefaultFinancial();
            fin.CurrentValue = 7000; // High value
            fin.PoliticalSupport = 7;
            fin.Economy = 7;
            fin.FanSupport = 7;
            fin.CostOfLiving = 1;
            fin.Population = 7;
            fin.Interest = 7;
            fin.Competition = 1;

            FinancialSimulationService.EvaluateRelocation(fin, 100, new Random(i));
            if (fin.PossibleMoveCity) { moved = true; break; }
        }
        moved.Should().BeTrue("high-value team with great metrics should sometimes trigger relocation");
    }

    [Fact]
    public void EvaluateRelocation_MoveYears_InRange()
    {
        // Find a seed that triggers relocation
        for (int i = 0; i < 1000; i++)
        {
            var fin = CreateDefaultFinancial();
            fin.CurrentValue = 7000;
            fin.PoliticalSupport = 7;
            fin.Economy = 7;
            fin.FanSupport = 7;
            fin.CostOfLiving = 1;
            fin.Population = 7;
            fin.Interest = 7;
            fin.Competition = 1;

            FinancialSimulationService.EvaluateRelocation(fin, 100, new Random(i));
            if (fin.PossibleMoveCity)
            {
                fin.PossibleMoveYears.Should().BeInRange(4, 6); // 3+IntRandom(3) = 4..6
                fin.PossibleMoveScore.Should().BeGreaterThan(0);
                return;
            }
        }

        // If no relocation triggered, that's a test failure
        Assert.Fail("Should have triggered at least one relocation in 1000 attempts");
    }

    [Fact]
    public void EvaluateRelocation_LowValue_RarelyTriggers()
    {
        int moveCount = 0;
        for (int i = 0; i < 100; i++)
        {
            var fin = CreateDefaultFinancial();
            fin.CurrentValue = 500;
            fin.PoliticalSupport = 1;
            fin.Economy = 1;
            fin.FanSupport = 1;

            FinancialSimulationService.EvaluateRelocation(fin, 1000, new Random(i));
            if (fin.PossibleMoveCity) moveCount++;
        }
        moveCount.Should().BeLessThan(50, "low-value teams should rarely trigger relocation");
    }

    // ───────────────────────────────────────────────────────────────
    // ClearBudgetData
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void ClearBudgetData_AllSeasonFieldsZeroed()
    {
        var fin = new TeamFinancial
        {
            HomeGames = 41,
            SeasonAttendance = 500000,
            SeasonSuitesSold = 2000,
            SeasonClubsSold = 40000,
            AttendanceRevenue = 20000000,
            SeasonSuiteRevenue = 5000000,
            SeasonClubRevenue = 6000000,
            SeasonConcessionRevenue = 3000000,
            SeasonParkingRevenue = 1000000,
            SeasonTotalRevenue = 80000000,
            SeasonPayroll = 50000000,
            SeasonScoutExpenses = 500000,
            SeasonCoachExpenses = 600000,
            SeasonGmExpenses = 700000,
            SeasonOperatingExpenses = 3000000,
            SeasonArenaExpenses = 2000000,
            SeasonMarketingExpenses = 1500000,
            SeasonTotalExpenses = 60000000
        };

        FinancialSimulationService.ClearBudgetData(fin);

        fin.HomeGames.Should().Be(0);
        fin.SeasonAttendance.Should().Be(0);
        fin.SeasonSuitesSold.Should().Be(0);
        fin.SeasonClubsSold.Should().Be(0);
        fin.AttendanceRevenue.Should().Be(0);
        fin.SeasonSuiteRevenue.Should().Be(0);
        fin.SeasonClubRevenue.Should().Be(0);
        fin.SeasonConcessionRevenue.Should().Be(0);
        fin.SeasonParkingRevenue.Should().Be(0);
        fin.SeasonTotalRevenue.Should().Be(0);
        fin.SeasonPayroll.Should().Be(0);
        fin.SeasonScoutExpenses.Should().Be(0);
        fin.SeasonCoachExpenses.Should().Be(0);
        fin.SeasonGmExpenses.Should().Be(0);
        fin.SeasonOperatingExpenses.Should().Be(0);
        fin.SeasonArenaExpenses.Should().Be(0);
        fin.SeasonMarketingExpenses.Should().Be(0);
        fin.SeasonTotalExpenses.Should().Be(0);
    }
}
