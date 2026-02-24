using FluentAssertions;
using JumpShotBasketball.Core.Models.Game;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Staff;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class PlayerDevelopmentServiceTests
{
    // ── Helper factories ──────────────────────────────────────────

    private static Player CreatePlayerWithStats(int age = 25, string pos = "PG", int prime = 28,
        int games = 82, int minutes = 2500, int fgm = 400, int fga = 900,
        int ftm = 200, int fta = 250, int tgm = 80, int tga = 220,
        int oreb = 50, int reb = 300, int ast = 400, int stl = 100,
        int to = 200, int blk = 20, int pf = 150)
    {
        return new Player
        {
            Name = "Test Player",
            Position = pos,
            Age = age,
            Height = 75,
            Weight = 200,
            Ratings = new PlayerRatings
            {
                Prime = prime,
                Potential1 = 3,
                Potential2 = 3,
                Effort = 3,
                MovementOffenseRaw = 5,
                PenetrationOffenseRaw = 5,
                PostOffenseRaw = 5,
                TransitionOffenseRaw = 5,
                MovementDefenseRaw = 5,
                PenetrationDefenseRaw = 5,
                PostDefenseRaw = 5,
                TransitionDefenseRaw = 5
            },
            SimulatedStats = new PlayerStatLine
            {
                Games = games,
                Minutes = minutes,
                FieldGoalsMade = fgm,
                FieldGoalsAttempted = fga,
                FreeThrowsMade = ftm,
                FreeThrowsAttempted = fta,
                ThreePointersMade = tgm,
                ThreePointersAttempted = tga,
                OffensiveRebounds = oreb,
                Rebounds = reb,
                Assists = ast,
                Steals = stl,
                Turnovers = to,
                Blocks = blk,
                PersonalFouls = pf
            },
            Team = "TestTeam"
        };
    }

    private static League CreateLeagueWithPlayer(Player player)
    {
        var league = new League();
        var team = new Team
        {
            Id = 1,
            Name = "TestTeam",
            Coach = new StaffMember
            {
                CoachPot1 = 3, CoachPot2 = 3, CoachEffort = 3,
                CoachScoring = 3, CoachShooting = 3,
                CoachRebounding = 3, CoachPassing = 3, CoachDefense = 3
            }
        };
        team.Roster.Add(player);
        league.Teams.Add(team);
        return league;
    }

    // ── GenerateRandomPrime ─────────────────────────────────────────

    [Fact]
    public void GenerateRandomPrime_ProducesReasonableRange()
    {
        var player = new Player { Age = 25, Position = "PG" };
        int min = int.MaxValue, max = int.MinValue;

        for (int seed = 0; seed < 200; seed++)
        {
            var prime = PlayerDevelopmentService.GenerateRandomPrime(player, new Random(seed));
            if (prime < min) min = prime;
            if (prime > max) max = prime;
        }

        min.Should().BeGreaterThanOrEqualTo(22);
        max.Should().BeLessThanOrEqualTo(33);
    }

    [Fact]
    public void GenerateRandomPrime_CenterGetsPlus1()
    {
        var center = new Player { Age = 25, Position = " C" };
        var guard = new Player { Age = 25, Position = "PG" };

        long centerTotal = 0, guardTotal = 0;
        for (int seed = 0; seed < 100; seed++)
        {
            centerTotal += PlayerDevelopmentService.GenerateRandomPrime(center, new Random(seed));
            guardTotal += PlayerDevelopmentService.GenerateRandomPrime(guard, new Random(seed));
        }

        (centerTotal / 100.0).Should().BeGreaterThan(guardTotal / 100.0,
            "centers should average higher prime age");
    }

    [Fact]
    public void GenerateRandomPrime_YoungPlayerGetsMinus1()
    {
        var young = new Player { Age = 20, Position = "PG" };
        var normal = new Player { Age = 25, Position = "PG" };

        long youngTotal = 0, normalTotal = 0;
        for (int seed = 0; seed < 100; seed++)
        {
            youngTotal += PlayerDevelopmentService.GenerateRandomPrime(young, new Random(seed));
            normalTotal += PlayerDevelopmentService.GenerateRandomPrime(normal, new Random(seed));
        }

        (youngTotal / 100.0).Should().BeLessThan(normalTotal / 100.0,
            "young players should average lower prime age");
    }

    [Fact]
    public void GenerateRandomPrime_SetsPrimeOnRatings()
    {
        var player = new Player { Age = 25, Position = "PG" };
        int result = PlayerDevelopmentService.GenerateRandomPrime(player, new Random(42));

        player.Ratings.Prime.Should().Be(result);
    }

    // ── CalculateInjuryRating ──────────────────────────────────────

    [Fact]
    public void CalculateInjuryRating_HealthyPlayer_LowRating()
    {
        // Played all 82 games
        int rating = PlayerDevelopmentService.CalculateInjuryRating(
            games: 82, minutes: 2500, fga: 800, fta: 200, turnovers: 150,
            injurySetting: 1, gamesInSeason: 82);

        rating.Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public void CalculateInjuryRating_InjuredPlayer_HigherRating()
    {
        // Missed many games
        int rating = PlayerDevelopmentService.CalculateInjuryRating(
            games: 40, minutes: 1200, fga: 400, fta: 100, turnovers: 80,
            injurySetting: 1, gamesInSeason: 82);

        rating.Should().BeGreaterThan(1);
    }

    [Fact]
    public void CalculateInjuryRating_ModerateInjurySetting_ClampsTo2Through5()
    {
        // Very injury-prone player
        int rating = PlayerDevelopmentService.CalculateInjuryRating(
            games: 20, minutes: 400, fga: 200, fta: 50, turnovers: 40,
            injurySetting: 2, gamesInSeason: 82);

        rating.Should().BeInRange(2, 5);
    }

    [Fact]
    public void CalculateInjuryRating_RatingCappedAt27()
    {
        int rating = PlayerDevelopmentService.CalculateInjuryRating(
            games: 1, minutes: 10, fga: 5, fta: 2, turnovers: 1,
            injurySetting: 1, gamesInSeason: 82);

        rating.Should().BeLessThanOrEqualTo(27);
    }

    // ── CalculateLeagueHighs ────────────────────────────────────────

    [Fact]
    public void CalculateLeagueHighs_ComputesCorrectHighs()
    {
        var player1 = CreatePlayerWithStats(fga: 900, tga: 200, fta: 300);
        var player2 = CreatePlayerWithStats(fga: 1100, tga: 350, fta: 200);

        var league = new League();
        var team = new Team { Id = 1, Name = "T1" };
        team.Roster.Add(player1);
        team.Roster.Add(player2);
        league.Teams.Add(team);

        var highs = PlayerDevelopmentService.CalculateLeagueHighs(league);

        highs.HighFieldGoalsAttempted.Should().BeGreaterThan(0);
        highs.HighFreeThrowsAttempted.Should().BeGreaterThan(0);
        highs.HighThreePointersAttempted.Should().BeGreaterThan(0);
        highs.HighGames.Should().Be(82);
    }

    [Fact]
    public void CalculateLeagueHighs_SkipsPlayersWithNoMinutes()
    {
        var emptyPlayer = CreatePlayerWithStats(games: 0, minutes: 0);
        emptyPlayer.Name = "Empty";

        var league = new League();
        var team = new Team { Id = 1, Name = "T1" };
        team.Roster.Add(emptyPlayer);
        league.Teams.Add(team);

        var highs = PlayerDevelopmentService.CalculateLeagueHighs(league);

        // Should get defaults (1) since no valid players
        highs.HighFieldGoalsAttempted.Should().Be(1);
    }

    // ── CalculateProjectionRatings ────────────────────────────────────

    [Fact]
    public void CalculateProjectionRatings_ProducesNonNegativeRatings()
    {
        var player = CreatePlayerWithStats();
        var highs = new LeagueHighs
        {
            HighFieldGoalsAttempted = 200,
            HighFreeThrowsAttempted = 100,
            HighThreePointersAttempted = 80,
            HighOffensiveRebounds = 30,
            HighDefensiveRebounds = 60,
            HighAssists = 80,
            HighSteals = 30,
            HighTurnovers = 40,
            HighBlocks = 20,
            HighGames = 82
        };

        PlayerDevelopmentService.CalculateProjectionRatings(player, highs);

        player.Ratings.ProjectionFieldGoalsAttempted.Should().BeGreaterThanOrEqualTo(0);
        player.Ratings.ProjectionFieldGoalPercentage.Should().BeGreaterThanOrEqualTo(0);
        player.Ratings.ProjectionFreeThrowsAttempted.Should().BeGreaterThanOrEqualTo(0);
        player.Ratings.ProjectionAssists.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void CalculateProjectionRatings_HighMinutePlayer_NoPenalty()
    {
        var player = CreatePlayerWithStats(minutes: 82 * 36); // 36mpg
        var highs = new LeagueHighs
        {
            HighFieldGoalsAttempted = 200,
            HighFreeThrowsAttempted = 100,
            HighThreePointersAttempted = 80,
            HighOffensiveRebounds = 30,
            HighDefensiveRebounds = 60,
            HighAssists = 80,
            HighSteals = 30,
            HighTurnovers = 40,
            HighBlocks = 20,
            HighGames = 82
        };

        PlayerDevelopmentService.CalculateProjectionRatings(player, highs);

        // With high minutes, pen = 1, no penalty
        player.Ratings.ProjectionFieldGoalsAttempted.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateProjectionRatings_TurnoverRatingInverted()
    {
        var player = CreatePlayerWithStats(to: 10); // Very low turnovers
        var highs = new LeagueHighs
        {
            HighFieldGoalsAttempted = 200,
            HighFreeThrowsAttempted = 100,
            HighThreePointersAttempted = 80,
            HighOffensiveRebounds = 30,
            HighDefensiveRebounds = 60,
            HighAssists = 80,
            HighSteals = 30,
            HighTurnovers = 40,
            HighBlocks = 20,
            HighGames = 82
        };

        PlayerDevelopmentService.CalculateProjectionRatings(player, highs);

        // Low turnovers = high turnover rating (inverted)
        player.Ratings.ProjectionTurnovers.Should().BeGreaterThan(90);
    }

    // ── ArchiveSeasonStats ─────────────────────────────────────────

    [Fact]
    public void ArchiveSeasonStats_CopiesStatsToCareerHistory()
    {
        var player = CreatePlayerWithStats(games: 82, fgm: 500);
        player.CareerHistory.Clear();
        player.CareerStats.Reset();

        PlayerDevelopmentService.ArchiveSeasonStats(player, 2024);

        player.CareerHistory.Should().HaveCount(1);
        player.CareerHistory[0].Year.Should().Be(2024);
        player.CareerHistory[0].Stats.Games.Should().Be(82);
        player.CareerHistory[0].Stats.FieldGoalsMade.Should().Be(500);
    }

    [Fact]
    public void ArchiveSeasonStats_AccumulatesIntoCareerStats()
    {
        var player = CreatePlayerWithStats(games: 82, fgm: 500, minutes: 2500);
        player.CareerStats.Reset();

        PlayerDevelopmentService.ArchiveSeasonStats(player, 2024);

        player.CareerStats.Games.Should().Be(82);
        player.CareerStats.FieldGoalsMade.Should().Be(500);
        player.CareerStats.Minutes.Should().Be(2500);
    }

    [Fact]
    public void ArchiveSeasonStats_MultipleSeasons_Accumulates()
    {
        var player = CreatePlayerWithStats(games: 82, fgm: 500);
        player.CareerStats.Reset();

        PlayerDevelopmentService.ArchiveSeasonStats(player, 2023);
        PlayerDevelopmentService.ArchiveSeasonStats(player, 2024);

        player.CareerHistory.Should().HaveCount(2);
        player.CareerStats.Games.Should().Be(164);
        player.CareerStats.FieldGoalsMade.Should().Be(1000);
    }

    // ── CalculateImprovementFactor ──────────────────────────────────

    [Fact]
    public void CalculateImprovementFactor_PrePrime_TendsAbove1()
    {
        double total = 0;
        for (int seed = 0; seed < 200; seed++)
        {
            total += PlayerDevelopmentService.CalculateImprovementFactor(
                yearsToPrime: 3, adjustment: 1, potential: 3, intangible: 3, new Random(seed));
        }
        double avg = total / 200;

        avg.Should().BeGreaterThan(1.0, "pre-prime players should tend to improve");
    }

    [Fact]
    public void CalculateImprovementFactor_PostPrime_TendsBelow1()
    {
        double total = 0;
        for (int seed = 0; seed < 200; seed++)
        {
            total += PlayerDevelopmentService.CalculateImprovementFactor(
                yearsToPrime: -5, adjustment: 1, potential: 3, intangible: 3, new Random(seed));
        }
        double avg = total / 200;

        avg.Should().BeLessThan(1.0, "post-prime players should tend to decline");
    }

    [Fact]
    public void CalculateImprovementFactor_HighPotential_BiggerSwings()
    {
        double totalHigh = 0, totalLow = 0;
        for (int seed = 0; seed < 200; seed++)
        {
            totalHigh += PlayerDevelopmentService.CalculateImprovementFactor(
                yearsToPrime: 3, adjustment: 1, potential: 5, intangible: 3, new Random(seed));
            totalLow += PlayerDevelopmentService.CalculateImprovementFactor(
                yearsToPrime: 3, adjustment: 1, potential: 1, intangible: 3, new Random(seed));
        }

        double avgHigh = totalHigh / 200;
        double avgLow = totalLow / 200;

        avgHigh.Should().BeGreaterThan(avgLow,
            "high potential should produce more improvement than low potential");
    }

    [Fact]
    public void CalculateImprovementFactor_Deterministic()
    {
        double r1 = PlayerDevelopmentService.CalculateImprovementFactor(3, 1, 3, 3, new Random(42));
        double r2 = PlayerDevelopmentService.CalculateImprovementFactor(3, 1, 3, 3, new Random(42));

        r1.Should().Be(r2, "same seed should produce same factor");
    }

    // ── DevelopPlayers ──────────────────────────────────────────────

    [Fact]
    public void DevelopPlayers_ModifiesPlayerStats()
    {
        var player = CreatePlayerWithStats(age: 25, prime: 28);
        var league = CreateLeagueWithPlayer(player);

        int originalFga = player.SimulatedStats.FieldGoalsAttempted;

        PlayerDevelopmentService.DevelopPlayers(league, new Random(42));

        // Stats should change
        player.SimulatedStats.FieldGoalsAttempted.Should().NotBe(originalFga);
    }

    [Fact]
    public void DevelopPlayers_Deterministic()
    {
        var player1 = CreatePlayerWithStats(age: 25, prime: 28);
        var league1 = CreateLeagueWithPlayer(player1);
        PlayerDevelopmentService.DevelopPlayers(league1, new Random(42));

        var player2 = CreatePlayerWithStats(age: 25, prime: 28);
        var league2 = CreateLeagueWithPlayer(player2);
        PlayerDevelopmentService.DevelopPlayers(league2, new Random(42));

        player1.SimulatedStats.FieldGoalsAttempted.Should().Be(player2.SimulatedStats.FieldGoalsAttempted);
        player1.SimulatedStats.Minutes.Should().Be(player2.SimulatedStats.Minutes);
    }

    [Fact]
    public void DevelopPlayers_StatsRemainNonNegative()
    {
        var player = CreatePlayerWithStats(age: 38, prime: 28);
        var league = CreateLeagueWithPlayer(player);

        PlayerDevelopmentService.DevelopPlayers(league, new Random(42));

        var stats = player.SimulatedStats;
        stats.FieldGoalsMade.Should().BeGreaterThanOrEqualTo(0);
        stats.FieldGoalsAttempted.Should().BeGreaterThanOrEqualTo(0);
        stats.FreeThrowsMade.Should().BeGreaterThanOrEqualTo(0);
        stats.FreeThrowsAttempted.Should().BeGreaterThanOrEqualTo(0);
        stats.ThreePointersMade.Should().BeGreaterThanOrEqualTo(0);
        stats.ThreePointersAttempted.Should().BeGreaterThanOrEqualTo(0);
        stats.Minutes.Should().BeGreaterThan(0);
        stats.Games.Should().BeGreaterThan(0);
    }

    [Fact]
    public void DevelopPlayers_SkipsEmptyNamePlayers()
    {
        var emptyPlayer = CreatePlayerWithStats();
        emptyPlayer.Name = "";
        var league = CreateLeagueWithPlayer(emptyPlayer);

        int origFga = emptyPlayer.SimulatedStats.FieldGoalsAttempted;

        PlayerDevelopmentService.DevelopPlayers(league, new Random(42));

        emptyPlayer.SimulatedStats.FieldGoalsAttempted.Should().Be(origFga);
    }

    [Fact]
    public void DevelopPlayers_MovementRatingsStayInRange()
    {
        var player = CreatePlayerWithStats(age: 25, prime: 28);
        var league = CreateLeagueWithPlayer(player);

        PlayerDevelopmentService.DevelopPlayers(league, new Random(42));

        player.Ratings.MovementOffenseRaw.Should().BeInRange(1, 9);
        player.Ratings.PenetrationOffenseRaw.Should().BeInRange(1, 9);
        player.Ratings.PostOffenseRaw.Should().BeInRange(1, 9);
        player.Ratings.TransitionOffenseRaw.Should().BeInRange(1, 9);
        player.Ratings.MovementDefenseRaw.Should().BeInRange(1, 9);
        player.Ratings.PenetrationDefenseRaw.Should().BeInRange(1, 9);
        player.Ratings.PostDefenseRaw.Should().BeInRange(1, 9);
        player.Ratings.TransitionDefenseRaw.Should().BeInRange(1, 9);
    }
}
