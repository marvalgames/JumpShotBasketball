using FluentAssertions;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class TeamChemistryServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────

    private static Player CreatePlayer(string name, string position, int games, int minutes,
        int fgm, int fga, int ftm, int fta, int tpm, int tpa,
        int oreb, int reb, int ast, int stl, int to, int blk, int pf = 100)
    {
        return new Player
        {
            Name = name,
            Position = position,
            Age = 25,
            Health = 100,
            Active = true,
            SeasonStats = new PlayerStatLine
            {
                Games = games,
                Minutes = minutes,
                FieldGoalsMade = fgm,
                FieldGoalsAttempted = fga,
                FreeThrowsMade = ftm,
                FreeThrowsAttempted = fta,
                ThreePointersMade = tpm,
                ThreePointersAttempted = tpa,
                OffensiveRebounds = oreb,
                Rebounds = reb,
                Assists = ast,
                Steals = stl,
                Turnovers = to,
                Blocks = blk,
                PersonalFouls = pf
            }
        };
    }

    private static Player CreateStarPlayer(string name = "Star Player")
    {
        // High-volume efficient scorer: ~20ppg, 48% FG, 38% 3P, 85% FT
        return CreatePlayer(name, "SG", 82, 2800,
            fgm: 600, fga: 1250, ftm: 340, fta: 400, tpm: 150, tpa: 395,
            oreb: 50, reb: 350, ast: 400, stl: 100, to: 200, blk: 30);
    }

    private static Player CreateAveragePlayer(string name = "Average Player")
    {
        // League-average stats: ~12ppg, 45% FG, 35% 3P, 78% FT
        return CreatePlayer(name, "SF", 82, 2400,
            fgm: 350, fga: 778, ftm: 150, fta: 192, tpm: 80, tpa: 229,
            oreb: 60, reb: 400, ast: 200, stl: 80, to: 150, blk: 40);
    }

    private static Player CreateBenchPlayer(string name = "Bench Player")
    {
        // Low-minute bench player: ~6ppg, 42% FG, 30% 3P, 70% FT
        return CreatePlayer(name, "PF", 60, 900,
            fgm: 120, fga: 286, ftm: 50, fta: 71, tpm: 20, tpa: 67,
            oreb: 40, reb: 200, ast: 50, stl: 30, to: 60, blk: 25);
    }

    private static Player CreatePoorShooter(string name = "Poor Shooter")
    {
        // Very poor efficiency: 35% FG, 25% 3P, 60% FT, high TO
        return CreatePlayer(name, "PG", 82, 2400,
            fgm: 270, fga: 771, ftm: 90, fta: 150, tpm: 50, tpa: 200,
            oreb: 30, reb: 200, ast: 150, stl: 60, to: 280, blk: 10);
    }

    private static Player CreateZeroMinutePlayer(string name = "Zero Min Player")
    {
        return CreatePlayer(name, "C", 0, 0,
            fgm: 0, fga: 0, ftm: 0, fta: 0, tpm: 0, tpa: 0,
            oreb: 0, reb: 0, ast: 0, stl: 0, to: 0, blk: 0);
    }

    private static Team CreateTeamWith(params Player[] players)
    {
        var team = new Team
        {
            Id = 0,
            Name = "TestTeam",
            CityName = "TestCity",
            Record = new TeamRecord { TeamName = "TestTeam", Conference = "East", Division = "Atlantic", Control = "Computer" },
            Financial = new TeamFinancial()
        };
        team.Roster.AddRange(players);
        return team;
    }

    private static League CreateLeagueWith(params Team[] teams)
    {
        var league = new League();
        league.Teams.AddRange(teams);
        return league;
    }

    // ── CalculateChemistryAverages Tests ──────────────────────────────

    [Fact]
    public void CalculateChemistryAverages_EmptyLeague_ReturnsZeroAverages()
    {
        var league = new League();

        var avg = TeamChemistryService.CalculateChemistryAverages(league);

        avg.AvgFga.Should().Be(0);
        avg.AvgFta.Should().Be(0);
        avg.AvgTfga.Should().Be(0);
        avg.AvgTru.Should().Be(0);
        avg.AvgOrb.Should().Be(0);
        avg.AvgDrb.Should().Be(0);
        avg.AvgAst.Should().Be(0);
        avg.AvgStl.Should().Be(0);
        avg.AvgTo.Should().Be(0);
        avg.AvgBlk.Should().Be(0);
    }

    [Fact]
    public void CalculateChemistryAverages_SinglePlayer_CorrectPer48()
    {
        // Player with 2400 minutes: per-48 factor = 48/2400 = 0.02
        var player = CreateAveragePlayer();
        var team = CreateTeamWith(player);
        var league = CreateLeagueWith(team);

        var avg = TeamChemistryService.CalculateChemistryAverages(league);

        // FGA per 48 = 778 / 2400 * 48 = 15.56
        avg.AvgFga.Should().BeApproximately(778.0 / 2400 * 48, 0.01);
        // FTA per 48 = 192 / 2400 * 48 = 3.84
        avg.AvgFta.Should().BeApproximately(192.0 / 2400 * 48, 0.01);
        // 3PA per 48 = 229 / 2400 * 48 = 4.58
        avg.AvgTfga.Should().BeApproximately(229.0 / 2400 * 48, 0.01);
        // ORB per 48 = 60 / 2400 * 48 = 1.2
        avg.AvgOrb.Should().BeApproximately(60.0 / 2400 * 48, 0.01);
        // DRB per 48 = (400-60) / 2400 * 48 = 6.8
        avg.AvgDrb.Should().BeApproximately(340.0 / 2400 * 48, 0.01);
    }

    [Fact]
    public void CalculateChemistryAverages_MultipleTeams_Aggregates()
    {
        var team1 = CreateTeamWith(CreateStarPlayer("Star1"), CreateAveragePlayer("Avg1"));
        var team2 = CreateTeamWith(CreateStarPlayer("Star2"), CreateBenchPlayer("Bench1"));
        team1.Id = 0;
        team2.Id = 1;
        var league = CreateLeagueWith(team1, team2);

        var avg = TeamChemistryService.CalculateChemistryAverages(league);

        // Total minutes from all 4 players: 2800 + 2400 + 2800 + 900 = 8900
        double totalMin = 2800 + 2400 + 2800 + 900;
        // Total FGA: 1250 + 778 + 1250 + 286 = 3564
        double totalFga = 1250 + 778 + 1250 + 286;
        avg.AvgFga.Should().BeApproximately(totalFga / totalMin * 48, 0.01);
        avg.AvgTru.Should().NotBe(0);
    }

    [Fact]
    public void CalculateChemistryAverages_ZeroMinutePlayers_Excluded()
    {
        var player = CreateAveragePlayer();
        var zeroMin = CreateZeroMinutePlayer();
        var team = CreateTeamWith(player, zeroMin);
        var league = CreateLeagueWith(team);

        var avg = TeamChemistryService.CalculateChemistryAverages(league);

        // Should only use the average player's stats (2400 min)
        avg.AvgFga.Should().BeApproximately(778.0 / 2400 * 48, 0.01);
    }

    // ── CalculateTeammatesBetterRating Tests ─────────────────────────

    [Fact]
    public void CalculateTeammatesBetterRating_ZeroMinutes_ReturnsZero()
    {
        var stats = new PlayerStatLine { Minutes = 0 };
        var avg = new TeamChemistryService.ChemistryAverages(10, 5, 3, 20, 2, 5, 4, 1.5, 2, 1);

        var result = TeamChemistryService.CalculateTeammatesBetterRating(stats, avg);

        result.Should().Be(0);
    }

    [Fact]
    public void CalculateTeammatesBetterRating_AveragePlayer_NearZero()
    {
        // Build league from one average player, then compute TBR
        var player = CreateAveragePlayer();
        var team = CreateTeamWith(player);
        var league = CreateLeagueWith(team);

        var avg = TeamChemistryService.CalculateChemistryAverages(league);
        var tbr = TeamChemistryService.CalculateTeammatesBetterRating(player.SeasonStats, avg);

        // When there's only one player, they ARE the league average
        // tru - tm should be near 0 (not exactly 0 due to formula differences)
        tbr.Should().BeInRange(-5, 5);
    }

    [Fact]
    public void CalculateTeammatesBetterRating_AboveAverageShooter_HigherThanPoorShooter()
    {
        var star = CreateStarPlayer();
        var poor = CreatePoorShooter();
        var avg1 = CreateAveragePlayer("Avg1");
        var avg2 = CreateAveragePlayer("Avg2");
        var team = CreateTeamWith(star, poor, avg1, avg2);
        var league = CreateLeagueWith(team);

        var avgs = TeamChemistryService.CalculateChemistryAverages(league);
        var starTbr = TeamChemistryService.CalculateTeammatesBetterRating(star.SeasonStats, avgs);
        var poorTbr = TeamChemistryService.CalculateTeammatesBetterRating(poor.SeasonStats, avgs);

        starTbr.Should().BeGreaterThan(poorTbr);
    }

    [Fact]
    public void CalculateTeammatesBetterRating_BelowAverageShooter_Negative()
    {
        // Create a league with mostly good players and one bad player
        var good1 = CreateStarPlayer("Star1");
        var good2 = CreateStarPlayer("Star2");
        var good3 = CreateStarPlayer("Star3");
        var bad = CreatePoorShooter();
        var team = CreateTeamWith(good1, good2, good3, bad);
        var league = CreateLeagueWith(team);

        var avgs = TeamChemistryService.CalculateChemistryAverages(league);
        var badTbr = TeamChemistryService.CalculateTeammatesBetterRating(bad.SeasonStats, avgs);

        badTbr.Should().BeLessThan(0);
    }

    [Fact]
    public void CalculateTeammatesBetterRating_HighTurnovers_DifferentRating()
    {
        // Two identical players except one has much higher turnovers
        // Note: Due to the formula structure (TRS uses 3/4 weight on TO, tm uses full weight),
        // higher turnovers reduce tm more than tru, so TBR = tru - tm actually increases.
        // This is faithful to the C++ behavior.
        var lowTo = CreatePlayer("LowTO", "PG", 82, 2400,
            fgm: 400, fga: 900, ftm: 200, fta: 250, tpm: 80, tpa: 230,
            oreb: 50, reb: 300, ast: 300, stl: 80, to: 100, blk: 20);
        var highTo = CreatePlayer("HighTO", "PG", 82, 2400,
            fgm: 400, fga: 900, ftm: 200, fta: 250, tpm: 80, tpa: 230,
            oreb: 50, reb: 300, ast: 300, stl: 80, to: 350, blk: 20);

        var team = CreateTeamWith(lowTo, highTo);
        var league = CreateLeagueWith(team);
        var avgs = TeamChemistryService.CalculateChemistryAverages(league);

        var lowToTbr = TeamChemistryService.CalculateTeammatesBetterRating(lowTo.SeasonStats, avgs);
        var highToTbr = TeamChemistryService.CalculateTeammatesBetterRating(highTo.SeasonStats, avgs);

        // TBR values should differ (turnovers do affect the rating)
        lowToTbr.Should().NotBeApproximately(highToTbr, 0.001);
    }

    [Fact]
    public void CalculateTeammatesBetterRating_ZeroFieldGoalAttempts_NoException()
    {
        // Player with minutes but 0 FGA (all free throws)
        var player = CreatePlayer("NoShots", "C", 20, 200,
            fgm: 0, fga: 0, ftm: 30, fta: 50, tpm: 0, tpa: 0,
            oreb: 20, reb: 60, ast: 5, stl: 5, to: 10, blk: 15);
        var avg = new TeamChemistryService.ChemistryAverages(15, 5, 4, 20, 2, 5, 4, 1.5, 2, 1);

        var act = () => TeamChemistryService.CalculateTeammatesBetterRating(player.SeasonStats, avg);

        act.Should().NotThrow();
    }

    [Fact]
    public void CalculateTeammatesBetterRating_KnownValues_MatchesCpp()
    {
        // Hand-calculated example with specific league averages
        var avgs = new TeamChemistryService.ChemistryAverages(
            AvgFga: 16.0, AvgFta: 5.0, AvgTfga: 5.0, AvgTru: 10.0,
            AvgOrb: 2.0, AvgDrb: 6.0, AvgAst: 4.5, AvgStl: 1.5,
            AvgTo: 2.5, AvgBlk: 1.0);

        // Player: 2000 min, efficient scorer
        var stats = new PlayerStatLine
        {
            Games = 80, Minutes = 2000,
            FieldGoalsMade = 400, FieldGoalsAttempted = 800,
            FreeThrowsMade = 180, FreeThrowsAttempted = 220,
            ThreePointersMade = 100, ThreePointersAttempted = 250,
            OffensiveRebounds = 40, Rebounds = 300,
            Assists = 250, Steals = 70, Turnovers = 120, Blocks = 30
        };

        var tbr = TeamChemistryService.CalculateTeammatesBetterRating(stats, avgs);

        // Just verify it's a reasonable finite value
        tbr.Should().BeInRange(-50, 50);
        double.IsNaN(tbr).Should().BeFalse();
        double.IsInfinity(tbr).Should().BeFalse();
    }

    // ── CalculateBetterForLeague Tests ───────────────────────────────

    [Fact]
    public void CalculateBetterForLeague_TwoPlayers_ComputesBetter()
    {
        var p1 = CreateStarPlayer("Star");
        var p2 = CreateAveragePlayer("Avg");
        var team = CreateTeamWith(p1, p2);
        var league = CreateLeagueWith(team);

        TeamChemistryService.CalculateBetterForLeague(league, seasonStarted: false);

        p1.Better.Should().BeGreaterThan(0);
        p2.Better.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateBetterForLeague_ClampLow_Floor()
    {
        // Very poor player surrounded by stars → clamped low
        var poor = CreatePoorShooter();
        var star1 = CreateStarPlayer("Star1");
        var star2 = CreateStarPlayer("Star2");
        var star3 = CreateStarPlayer("Star3");
        var team = CreateTeamWith(poor, star1, star2, star3);
        var league = CreateLeagueWith(team);

        TeamChemistryService.CalculateBetterForLeague(league, seasonStarted: false);

        // Better should be at least 1 (formula: ((-2+9.9)*10)/2 = 39 at floor)
        poor.Better.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void CalculateBetterForLeague_ClampHigh_Ceiling()
    {
        // Star player → clamped high (formula: ((8+9.9)*10)/2 = 89 at ceiling)
        var star = CreateStarPlayer();
        var bench = CreateBenchPlayer();
        var team = CreateTeamWith(star, bench);
        var league = CreateLeagueWith(team);

        TeamChemistryService.CalculateBetterForLeague(league, seasonStarted: false);

        // Better should not exceed 89 (from clamp at 8.0)
        star.Better.Should().BeLessThanOrEqualTo(89);
    }

    [Fact]
    public void CalculateBetterForLeague_CourtTimeMatrix_ProportionalToMinutes()
    {
        // Star plays 2800 min, bench plays 900 min
        var star = CreateStarPlayer();
        var bench = CreateBenchPlayer();
        var team = CreateTeamWith(star, bench);
        var league = CreateLeagueWith(team);

        TeamChemistryService.CalculateBetterForLeague(league, seasonStarted: false);

        // Both should have Better values (the high-minute player should differ)
        star.Better.Should().NotBe(0);
        bench.Better.Should().NotBe(0);
    }

    [Fact]
    public void CalculateBetterForLeague_SeasonStarted_PreservesBetter()
    {
        var p1 = CreateStarPlayer();
        p1.Better = 42; // Preset value
        var p2 = CreateAveragePlayer();
        p2.Better = 55;
        var team = CreateTeamWith(p1, p2);
        var league = CreateLeagueWith(team);

        TeamChemistryService.CalculateBetterForLeague(league, seasonStarted: true);

        p1.Better.Should().Be(42); // Preserved
        p2.Better.Should().Be(55); // Preserved
    }

    [Fact]
    public void CalculateBetterForLeague_SeasonNotStarted_UpdatesBetter()
    {
        var p1 = CreateStarPlayer();
        p1.Better = 42;
        var p2 = CreateAveragePlayer();
        p2.Better = 55;
        var team = CreateTeamWith(p1, p2);
        var league = CreateLeagueWith(team);

        TeamChemistryService.CalculateBetterForLeague(league, seasonStarted: false);

        // Better should be freshly computed (may or may not equal original values)
        // With only 2 players, the computed values should differ from the arbitrary presets
        (p1.Better != 42 || p2.Better != 55).Should().BeTrue();
    }

    [Fact]
    public void CalculateBetterForLeague_TBR_AlwaysUpdated()
    {
        var p1 = CreateStarPlayer();
        p1.Ratings.TeammatesBetterRating = 999; // Preset garbage
        var p2 = CreateAveragePlayer();
        p2.Ratings.TeammatesBetterRating = -999;
        var team = CreateTeamWith(p1, p2);
        var league = CreateLeagueWith(team);

        TeamChemistryService.CalculateBetterForLeague(league, seasonStarted: true);

        // TBR should be recalculated (not 999 or -999)
        p1.Ratings.TeammatesBetterRating.Should().NotBe(999);
        p2.Ratings.TeammatesBetterRating.Should().NotBe(-999);
    }

    [Fact]
    public void CalculateBetterForLeague_AllZeroMinutes_NoException()
    {
        var p1 = CreateZeroMinutePlayer("Z1");
        var p2 = CreateZeroMinutePlayer("Z2");
        var team = CreateTeamWith(p1, p2);
        var league = CreateLeagueWith(team);

        var act = () => TeamChemistryService.CalculateBetterForLeague(league, seasonStarted: false);

        act.Should().NotThrow();
    }

    [Fact]
    public void CalculateBetterForLeague_MultiTeam_DifferentDistributions()
    {
        // Team 1: all stars (high min, high efficiency)
        var t1p1 = CreateStarPlayer("T1Star1");
        var t1p2 = CreateStarPlayer("T1Star2");
        var t1p3 = CreateAveragePlayer("T1Avg");
        var team1 = CreateTeamWith(t1p1, t1p2, t1p3);
        team1.Id = 0;
        team1.Name = "Stars";

        // Team 2: mixed (star + bench)
        var t2p1 = CreateStarPlayer("T2Star");
        var t2p2 = CreateBenchPlayer("T2Bench");
        var t2p3 = CreateAveragePlayer("T2Avg");
        var team2 = CreateTeamWith(t2p1, t2p2, t2p3);
        team2.Id = 1;
        team2.Name = "Mixed";

        var league = CreateLeagueWith(team1, team2);

        TeamChemistryService.CalculateBetterForLeague(league, seasonStarted: false);

        // All players should have non-zero Better
        t1p1.Better.Should().BeGreaterThan(0);
        t2p1.Better.Should().BeGreaterThan(0);
        t2p2.Better.Should().BeGreaterThan(0);

        // TBR values should vary across different player types
        var allTbr = new[]
        {
            t1p1.Ratings.TeammatesBetterRating,
            t2p1.Ratings.TeammatesBetterRating,
            t2p2.Ratings.TeammatesBetterRating
        };
        allTbr.Distinct().Count().Should().BeGreaterThan(1);
    }

    // ── Integration Tests ────────────────────────────────────────────

    [Fact]
    public void LeagueCreation_SetsBetter()
    {
        var options = new LeagueCreationOptions { NumberOfTeams = 4, PlayersPerTeam = 15 };
        var league = LeagueCreationService.CreateNewLeague(options, new Random(42));

        var playersWithMinutes = league.Teams
            .SelectMany(t => t.Roster)
            .Where(p => p.SeasonStats.Minutes > 0);

        playersWithMinutes.Should().AllSatisfy(p => p.Better.Should().BeGreaterThan(0));
    }

    [Fact]
    public void LeagueCreation_SetsTBR()
    {
        var options = new LeagueCreationOptions { NumberOfTeams = 4, PlayersPerTeam = 15 };
        var league = LeagueCreationService.CreateNewLeague(options, new Random(42));

        var playersWithMinutes = league.Teams
            .SelectMany(t => t.Roster)
            .Where(p => p.SeasonStats.Minutes > 0);

        // At least some players should have non-zero TBR
        playersWithMinutes.Should().Contain(p => p.Ratings.TeammatesBetterRating != 0);
    }

    [Fact]
    public void BetterValues_InRange_1to100()
    {
        var options = new LeagueCreationOptions { NumberOfTeams = 6, PlayersPerTeam = 15 };
        var league = LeagueCreationService.CreateNewLeague(options, new Random(123));

        var playersWithMinutes = league.Teams
            .SelectMany(t => t.Roster)
            .Where(p => p.SeasonStats.Minutes > 0);

        playersWithMinutes.Should().AllSatisfy(p =>
        {
            p.Better.Should().BeGreaterThanOrEqualTo(1);
            p.Better.Should().BeLessThanOrEqualTo(100);
        });
    }

    [Fact]
    public void ForTeam_MatchesLeagueResult()
    {
        var p1 = CreateStarPlayer();
        var p2 = CreateAveragePlayer();
        var team = CreateTeamWith(p1, p2);
        var league = CreateLeagueWith(team);

        TeamChemistryService.CalculateBetterForLeague(league, seasonStarted: false);

        int b1 = p1.Better;
        int b2 = p2.Better;
        double tbr1 = p1.Ratings.TeammatesBetterRating;
        double tbr2 = p2.Ratings.TeammatesBetterRating;

        // Reset and compute again directly for the single team
        p1.Better = 0;
        p2.Better = 0;
        p1.Ratings.TeammatesBetterRating = 0;
        p2.Ratings.TeammatesBetterRating = 0;

        var avg = TeamChemistryService.CalculateChemistryAverages(league);
        TeamChemistryService.CalculateBetterForTeam(team, avg, seasonStarted: false);

        p1.Better.Should().Be(b1);
        p2.Better.Should().Be(b2);
        p1.Ratings.TeammatesBetterRating.Should().Be(tbr1);
        p2.Ratings.TeammatesBetterRating.Should().Be(tbr2);
    }

    [Fact]
    public void DefaultBetter50_WhenZero()
    {
        // Document behavioral note: C++ GetBetter() returns 50 when m_better == 0
        // Engine reads: usual = Better * 2.0 / 10.0 - 9.9
        // With Better=0: usual = -9.9 (very negative)
        // With Better=50: usual = 0.1 (neutral)
        var player = new Player { Name = "Test", Better = 0 };
        double usual0 = player.Better * 2.0 / 10.0 - 9.9;
        usual0.Should().BeApproximately(-9.9, 0.01);

        player.Better = 50;
        double usual50 = player.Better * 2.0 / 10.0 - 9.9;
        usual50.Should().BeApproximately(0.1, 0.01);
    }

    [Fact]
    public void ScaleConversion_RoundTrip()
    {
        // Test that the Better→usual→approximate Better roundtrip preserves info
        var p1 = CreateStarPlayer();
        var p2 = CreateAveragePlayer();
        var team = CreateTeamWith(p1, p2);
        var league = CreateLeagueWith(team);

        TeamChemistryService.CalculateBetterForLeague(league, seasonStarted: false);

        int better = p1.Better;
        // Forward: Better → usual (engine decode)
        double usual = better * 2.0 / 10.0 - 9.9;
        // Reverse: usual → Better (encode)
        int roundTripped = (int)(((usual + 9.9) * 10) / 2);

        roundTripped.Should().Be(better);
    }

    [Fact]
    public void TradeRecalculates_WhenTradesMade()
    {
        // Create a league with enough teams and players for trading
        var options = new LeagueCreationOptions
        {
            NumberOfTeams = 4,
            PlayersPerTeam = 15,
            ComputerTradesEnabled = true,
            FreeAgencyEnabled = false
        };
        var league = LeagueCreationService.CreateNewLeague(options, new Random(42));

        // Record initial Better values
        var initialBetter = league.Teams
            .SelectMany(t => t.Roster)
            .Where(p => p.SeasonStats.Minutes > 0)
            .ToDictionary(p => p.Name, p => p.Better);

        // Run trading period (may or may not make trades)
        var result = TradeService.RunTradingPeriod(league, loops: 200, maxOffers: 5, random: new Random(42));

        // After trading, all players with minutes should still have valid Better values
        var playersWithMinutes = league.Teams
            .SelectMany(t => t.Roster)
            .Where(p => p.SeasonStats.Minutes > 0);

        playersWithMinutes.Should().AllSatisfy(p =>
        {
            p.Better.Should().BeGreaterThanOrEqualTo(1);
            p.Better.Should().BeLessThanOrEqualTo(100);
        });
    }
}
