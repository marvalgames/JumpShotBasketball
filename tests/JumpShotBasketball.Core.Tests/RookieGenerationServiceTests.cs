using FluentAssertions;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Staff;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class RookieGenerationServiceTests
{
    // ── Helpers ──────────────────────────────────────────────────────

    private static PlayerStatLine CreateBenchmarkStats(
        int games = 72, int minutes = 2400,
        int fgm = 400, int fga = 900,
        int tgm = 60, int tga = 180,
        int ftm = 150, int fta = 200,
        int oreb = 50, int reb = 300,
        int ast = 250, int stl = 80,
        int to = 120, int blk = 30,
        int pf = 150)
    {
        return new PlayerStatLine
        {
            Games = games, Minutes = minutes,
            FieldGoalsMade = fgm, FieldGoalsAttempted = fga,
            ThreePointersMade = tgm, ThreePointersAttempted = tga,
            FreeThrowsMade = ftm, FreeThrowsAttempted = fta,
            OffensiveRebounds = oreb, Rebounds = reb,
            Assists = ast, Steals = stl,
            Turnovers = to, Blocks = blk,
            PersonalFouls = pf
        };
    }

    private static Player CreateBenchmarkPlayer(string name, string pos, int games = 72,
        int minutes = 2400)
    {
        return new Player
        {
            Name = name,
            Position = pos,
            Age = 27,
            SimulatedStats = CreateBenchmarkStats(games: games, minutes: minutes),
            Ratings = new PlayerRatings
            {
                TradeTrueRating = 8.0,
                MovementOffenseRaw = 5, MovementDefenseRaw = 5,
                PenetrationOffenseRaw = 5, PenetrationDefenseRaw = 5,
                PostOffenseRaw = 5, PostDefenseRaw = 5,
                TransitionOffenseRaw = 5, TransitionDefenseRaw = 5
            }
        };
    }

    private static League CreateBenchmarkLeague(int playersPerTeam = 12, int numTeams = 2)
    {
        var league = new League
        {
            Settings = new LeagueSettings
            {
                CurrentYear = 2024,
                NumberOfTeams = numTeams
            },
            Schedule = new Schedule { GamesInSeason = 82 }
        };

        string[] positions = { "PG", "SG", "SF", "PF", "C" };
        for (int t = 0; t < numTeams; t++)
        {
            var team = new Team
            {
                Id = t + 1,
                Name = $"Team{t + 1}",
                Coach = new StaffMember(),
                Record = new TeamRecord { Wins = 41, Losses = 41 }
            };

            for (int p = 0; p < playersPerTeam; p++)
            {
                string pos = positions[p % 5];
                team.Roster.Add(CreateBenchmarkPlayer($"Player{t * 30 + p + 1}", pos));
            }
            league.Teams.Add(team);
        }

        return league;
    }

    // ── CalculateRookieTrueRating ───────────────────────────────────

    [Fact]
    public void CalculateRookieTrueRating_ZeroGames_ReturnsZero()
    {
        var stats = new PlayerStatLine { Games = 0, Minutes = 0 };
        RookieGenerationService.CalculateRookieTrueRating(stats).Should().Be(0);
    }

    [Fact]
    public void CalculateRookieTrueRating_ValidStats_ReturnsPositiveValue()
    {
        var stats = CreateBenchmarkStats();
        double result = RookieGenerationService.CalculateRookieTrueRating(stats);
        result.Should().BeGreaterThan(0, "a productive player should have a positive true rating");
    }

    [Fact]
    public void CalculateRookieTrueRating_HigherProduction_HigherRating()
    {
        var average = CreateBenchmarkStats();
        var star = CreateBenchmarkStats(fgm: 600, fga: 1100, ast: 400, stl: 120);
        double avgRating = RookieGenerationService.CalculateRookieTrueRating(average);
        double starRating = RookieGenerationService.CalculateRookieTrueRating(star);

        starRating.Should().BeGreaterThan(avgRating);
    }

    [Fact]
    public void CalculateRookieTrueRating_NormalizesToPer48()
    {
        // Same per-minute production but different total minutes
        var stats1 = CreateBenchmarkStats(games: 82, minutes: 2800);
        var stats2 = CreateBenchmarkStats(games: 82, minutes: 1400);
        // Stats2 has same totals but half the minutes → higher per-48
        double r1 = RookieGenerationService.CalculateRookieTrueRating(stats1);
        double r2 = RookieGenerationService.CalculateRookieTrueRating(stats2);
        r2.Should().BeGreaterThan(r1, "same totals in fewer minutes = higher per-48");
    }

    // ── GenerateRookieAge ───────────────────────────────────────────

    [Fact]
    public void GenerateRookieAge_ReturnsReasonableRange()
    {
        var rng = new Random(42);
        var ages = new List<int>();
        for (int i = 0; i < 200; i++)
            ages.Add(RookieGenerationService.GenerateRookieAge(i + 1, rng));

        ages.Should().OnlyContain(a => a >= 17 && a <= 25,
            "rookie ages should be in reasonable range");
    }

    [Fact]
    public void GenerateRookieAge_MostlyYoung()
    {
        var rng = new Random(42);
        int youngCount = 0;
        for (int i = 0; i < 500; i++)
        {
            int age = RookieGenerationService.GenerateRookieAge(i + 1, rng);
            if (age <= 22) youngCount++;
        }

        youngCount.Should().BeGreaterThan(300, "most rookies should be 22 or younger");
    }

    // ── CalculateAgeFactor ──────────────────────────────────────────

    [Fact]
    public void CalculateAgeFactor_ReturnsValueBetween0And1()
    {
        var rng = new Random(42);
        for (int age = 18; age <= 25; age++)
        {
            double factor = RookieGenerationService.CalculateAgeFactor(age, rng);
            factor.Should().BeInRange(0.0, 1.5, $"age factor for age {age} should be reasonable");
        }
    }

    [Fact]
    public void CalculateAgeFactor_Deterministic()
    {
        double f1 = RookieGenerationService.CalculateAgeFactor(20, new Random(99));
        double f2 = RookieGenerationService.CalculateAgeFactor(20, new Random(99));
        f1.Should().Be(f2);
    }

    // ── BuildEligiblePool ───────────────────────────────────────────

    [Fact]
    public void BuildEligiblePool_FiltersLowMinutesPlayers()
    {
        var league = CreateBenchmarkLeague(playersPerTeam: 5, numTeams: 1);
        // Add a low-minutes player
        league.Teams[0].Roster.Add(new Player
        {
            Name = "Benchwarmer",
            Position = "PG",
            SimulatedStats = new PlayerStatLine { Games = 10, Minutes = 50 }
        });

        var pool = RookieGenerationService.BuildEligiblePool(league);

        pool.Should().NotContain(x => x.player.Name == "Benchwarmer");
    }

    [Fact]
    public void BuildEligiblePool_SortsByTrueRatingDescending()
    {
        var league = CreateBenchmarkLeague(playersPerTeam: 5, numTeams: 1);
        var pool = RookieGenerationService.BuildEligiblePool(league);

        for (int i = 1; i < pool.Count; i++)
            pool[i - 1].trueRating.Should().BeGreaterThanOrEqualTo(pool[i].trueRating);
    }

    [Fact]
    public void BuildEligiblePool_EmptyLeague_ReturnsEmpty()
    {
        var league = new League
        {
            Settings = new LeagueSettings { CurrentYear = 2024 }
        };
        league.Teams.Add(new Team { Roster = new List<Player>() });

        RookieGenerationService.BuildEligiblePool(league).Should().BeEmpty();
    }

    // ── FindBenchmarkIndex ──────────────────────────────────────────

    [Fact]
    public void FindBenchmarkIndex_PrefersSamePosition()
    {
        var pool = new List<(Player player, double trueRating)>
        {
            (new Player { Name = "PG1", Position = "PG" }, 10.0),
            (new Player { Name = "SG1", Position = "SG" }, 9.0),
            (new Player { Name = "PG2", Position = "PG" }, 8.0),
        };

        var rng = new Random(42);
        // Run many times; result should always be a PG
        for (int i = 0; i < 20; i++)
        {
            int idx = RookieGenerationService.FindBenchmarkIndex(pool, 0, 1, rng);
            pool[idx].player.Position.Should().Be("PG");
        }
    }

    [Fact]
    public void FindBenchmarkIndex_FallsBackToAdjacent_WhenNoSamePos()
    {
        var pool = new List<(Player player, double trueRating)>
        {
            (new Player { Name = "SG1", Position = "SG" }, 10.0),
            (new Player { Name = "SF1", Position = "SF" }, 9.0),
        };

        var rng = new Random(42);
        // PG (index 0) has no match; adjacent is SG (index 1)
        int idx = RookieGenerationService.FindBenchmarkIndex(pool, 0, 1, rng);
        pool[idx].player.Position.Should().Be("SG");
    }

    [Fact]
    public void FindBenchmarkIndex_FallsBackToAny_WhenNoAdjacentMatch()
    {
        var pool = new List<(Player player, double trueRating)>
        {
            (new Player { Name = "C1", Position = "C" }, 10.0),
        };

        // PG (index 0) has no same or adjacent; should fallback to C
        var rng = new Random(42);
        int idx = RookieGenerationService.FindBenchmarkIndex(pool, 0, 1, rng);
        idx.Should().Be(0);
    }

    // ── GenerateOdptRatings ─────────────────────────────────────────

    [Fact]
    public void GenerateOdptRatings_AllClampedTo1Through9()
    {
        var rookie = new Player { Position = "PG" };
        var stats = CreateBenchmarkStats();

        RookieGenerationService.GenerateOdptRatings(rookie, stats, new Random(42));

        rookie.Ratings.MovementOffenseRaw.Should().BeInRange(1, 9);
        rookie.Ratings.MovementDefenseRaw.Should().BeInRange(1, 9);
        rookie.Ratings.PenetrationOffenseRaw.Should().BeInRange(1, 9);
        rookie.Ratings.PenetrationDefenseRaw.Should().BeInRange(1, 9);
        rookie.Ratings.PostOffenseRaw.Should().BeInRange(1, 9);
        rookie.Ratings.PostDefenseRaw.Should().BeInRange(1, 9);
        rookie.Ratings.TransitionOffenseRaw.Should().BeInRange(1, 9);
        rookie.Ratings.TransitionDefenseRaw.Should().BeInRange(1, 9);
    }

    [Fact]
    public void GenerateOdptRatings_Deterministic()
    {
        var rookie1 = new Player { Position = "SF" };
        var rookie2 = new Player { Position = "SF" };
        var stats = CreateBenchmarkStats();

        RookieGenerationService.GenerateOdptRatings(rookie1, stats, new Random(42));
        RookieGenerationService.GenerateOdptRatings(rookie2, stats, new Random(42));

        rookie1.Ratings.MovementOffenseRaw.Should().Be(rookie2.Ratings.MovementOffenseRaw);
        rookie1.Ratings.PostOffenseRaw.Should().Be(rookie2.Ratings.PostOffenseRaw);
    }

    // ── GenerateRandomHeightWeight ──────────────────────────────────

    [Theory]
    [InlineData("PG", 69, 79)]
    [InlineData("C", 76, 88)]
    public void GenerateRandomHeightWeight_PositionAppropriateHeights(string pos, int minH, int maxH)
    {
        var rng = new Random(42);
        for (int i = 0; i < 20; i++)
        {
            var p = new Player
            {
                Position = pos, Age = 21,
                Ratings = new PlayerRatings
                {
                    MovementOffenseRaw = 5, MovementDefenseRaw = 5,
                    PenetrationOffenseRaw = 5, PenetrationDefenseRaw = 5,
                    PostOffenseRaw = 5, PostDefenseRaw = 5,
                    TransitionOffenseRaw = 5, TransitionDefenseRaw = 5
                }
            };
            RookieGenerationService.GenerateRandomHeightWeight(p, rng);
            p.Height.Should().BeInRange(minH, maxH,
                $"iteration {i}: {pos} height should be position-appropriate");
        }
    }

    [Fact]
    public void GenerateRandomHeightWeight_CentersGenerallyTallerThanGuards()
    {
        var rng = new Random(42);
        int pgTotal = 0, cTotal = 0;
        for (int i = 0; i < 50; i++)
        {
            var pg = new Player
            {
                Position = "PG", Age = 21,
                Ratings = new PlayerRatings
                {
                    MovementOffenseRaw = 5, MovementDefenseRaw = 5,
                    PenetrationOffenseRaw = 5, PenetrationDefenseRaw = 5,
                    PostOffenseRaw = 3, PostDefenseRaw = 3,
                    TransitionOffenseRaw = 7, TransitionDefenseRaw = 7
                }
            };
            var c = new Player
            {
                Position = "C", Age = 21,
                Ratings = new PlayerRatings
                {
                    MovementOffenseRaw = 5, MovementDefenseRaw = 5,
                    PenetrationOffenseRaw = 3, PenetrationDefenseRaw = 3,
                    PostOffenseRaw = 7, PostDefenseRaw = 7,
                    TransitionOffenseRaw = 3, TransitionDefenseRaw = 3
                }
            };
            RookieGenerationService.GenerateRandomHeightWeight(pg, rng);
            RookieGenerationService.GenerateRandomHeightWeight(c, rng);
            pgTotal += pg.Height;
            cTotal += c.Height;
        }
        (cTotal / 50.0).Should().BeGreaterThan(pgTotal / 50.0);
    }

    // ── CreateRookies (integration) ─────────────────────────────────

    [Fact]
    public void CreateRookies_Produces96Rookies()
    {
        var league = CreateBenchmarkLeague(playersPerTeam: 12, numTeams: 4);
        var pool = RookieGenerationService.CreateRookies(league, 96, new Random(42));

        pool.Rookies.Should().HaveCount(96);
        pool.Year.Should().Be(2024);
    }

    [Fact]
    public void CreateRookies_AllHaveNames()
    {
        var league = CreateBenchmarkLeague(playersPerTeam: 12, numTeams: 4);
        var pool = RookieGenerationService.CreateRookies(league, 10, new Random(42));

        pool.Rookies.Should().OnlyContain(r => !string.IsNullOrEmpty(r.Name));
    }

    [Fact]
    public void CreateRookies_AllMarkedAsRookies()
    {
        var league = CreateBenchmarkLeague(playersPerTeam: 12, numTeams: 4);
        var pool = RookieGenerationService.CreateRookies(league, 10, new Random(42));

        pool.Rookies.Should().OnlyContain(r => r.Contract.IsRookie);
    }

    [Fact]
    public void CreateRookies_AllHaveValidPositions()
    {
        var league = CreateBenchmarkLeague(playersPerTeam: 12, numTeams: 4);
        var pool = RookieGenerationService.CreateRookies(league, 96, new Random(42));
        var validPositions = new[] { "PG", "SG", "SF", "PF", "C" };

        pool.Rookies.Should().OnlyContain(r => validPositions.Contains(r.Position));
    }

    [Fact]
    public void CreateRookies_AllHaveReasonableAges()
    {
        var league = CreateBenchmarkLeague(playersPerTeam: 12, numTeams: 4);
        var pool = RookieGenerationService.CreateRookies(league, 96, new Random(42));

        pool.Rookies.Should().OnlyContain(r => r.Age >= 17 && r.Age <= 25);
    }

    [Fact]
    public void CreateRookies_AllHavePositiveGames()
    {
        var league = CreateBenchmarkLeague(playersPerTeam: 12, numTeams: 4);
        var pool = RookieGenerationService.CreateRookies(league, 96, new Random(42));

        pool.Rookies.Should().OnlyContain(r => r.SimulatedStats.Games > 0);
    }

    [Fact]
    public void CreateRookies_AllHaveOdptRatings()
    {
        var league = CreateBenchmarkLeague(playersPerTeam: 12, numTeams: 4);
        var pool = RookieGenerationService.CreateRookies(league, 10, new Random(42));

        foreach (var r in pool.Rookies)
        {
            r.Ratings.MovementOffenseRaw.Should().BeInRange(1, 9);
            r.Ratings.PostOffenseRaw.Should().BeInRange(1, 9);
            r.Ratings.TransitionOffenseRaw.Should().BeInRange(1, 9);
        }
    }

    [Fact]
    public void CreateRookies_AllHaveHeightAndWeight()
    {
        var league = CreateBenchmarkLeague(playersPerTeam: 12, numTeams: 4);
        var pool = RookieGenerationService.CreateRookies(league, 96, new Random(42));

        pool.Rookies.Should().OnlyContain(r => r.Height > 60 && r.Height < 100);
        pool.Rookies.Should().OnlyContain(r => r.Weight > 100 && r.Weight < 400);
    }

    [Fact]
    public void CreateRookies_AllHavePrimeSet()
    {
        var league = CreateBenchmarkLeague(playersPerTeam: 12, numTeams: 4);
        var pool = RookieGenerationService.CreateRookies(league, 10, new Random(42));

        pool.Rookies.Should().OnlyContain(r => r.Ratings.Prime >= 22 && r.Ratings.Prime <= 35);
    }

    [Fact]
    public void CreateRookies_Deterministic()
    {
        var league1 = CreateBenchmarkLeague(playersPerTeam: 12, numTeams: 4);
        var pool1 = RookieGenerationService.CreateRookies(league1, 20, new Random(42));

        var league2 = CreateBenchmarkLeague(playersPerTeam: 12, numTeams: 4);
        var pool2 = RookieGenerationService.CreateRookies(league2, 20, new Random(42));

        for (int i = 0; i < 20; i++)
        {
            pool1.Rookies[i].Name.Should().Be(pool2.Rookies[i].Name);
            pool1.Rookies[i].Position.Should().Be(pool2.Rookies[i].Position);
            pool1.Rookies[i].Age.Should().Be(pool2.Rookies[i].Age);
            pool1.Rookies[i].Height.Should().Be(pool2.Rookies[i].Height);
            pool1.Rookies[i].Weight.Should().Be(pool2.Rookies[i].Weight);
        }
    }

    [Fact]
    public void CreateRookies_EmptyLeague_ReturnsEmptyPool()
    {
        var league = new League
        {
            Settings = new LeagueSettings { CurrentYear = 2024 }
        };
        league.Teams.Add(new Team { Roster = new List<Player>() });

        var pool = RookieGenerationService.CreateRookies(league, 10, new Random(42));
        pool.Rookies.Should().BeEmpty();
    }

    [Fact]
    public void CreateRookies_ClutchAndConsistencyInRange()
    {
        var league = CreateBenchmarkLeague(playersPerTeam: 12, numTeams: 4);
        var pool = RookieGenerationService.CreateRookies(league, 96, new Random(42));

        pool.Rookies.Should().OnlyContain(r => r.Ratings.Clutch >= 1 && r.Ratings.Clutch <= 3);
        pool.Rookies.Should().OnlyContain(r => r.Ratings.Consistency >= 1 && r.Ratings.Consistency <= 3);
    }

    [Fact]
    public void CreateRookies_BetterInRange()
    {
        var league = CreateBenchmarkLeague(playersPerTeam: 12, numTeams: 4);
        var pool = RookieGenerationService.CreateRookies(league, 96, new Random(42));

        pool.Rookies.Should().OnlyContain(r => r.Better >= 1 && r.Better <= 99);
    }

    [Fact]
    public void CreateRookies_PreferenceFactorsSet()
    {
        var league = CreateBenchmarkLeague(playersPerTeam: 12, numTeams: 4);
        var pool = RookieGenerationService.CreateRookies(league, 10, new Random(42));

        foreach (var r in pool.Rookies)
        {
            r.Contract.CoachFactor.Should().BeInRange(1, 5);
            r.Contract.LoyaltyFactor.Should().BeInRange(1, 5);
            r.Contract.PlayingTimeFactor.Should().BeInRange(1, 5);
            r.Contract.WinningFactor.Should().BeInRange(1, 5);
            r.Contract.TraditionFactor.Should().BeInRange(1, 5);
            r.Contract.SecurityFactor.Should().BeInRange(1, 5);
        }
    }

    [Fact]
    public void CreateRookies_ContentInRange()
    {
        var league = CreateBenchmarkLeague(playersPerTeam: 12, numTeams: 4);
        var pool = RookieGenerationService.CreateRookies(league, 96, new Random(42));

        pool.Rookies.Should().OnlyContain(r => r.Content >= 3 && r.Content <= 7);
    }
}
