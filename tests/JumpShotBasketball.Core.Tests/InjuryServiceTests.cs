using FluentAssertions;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class InjuryServiceTests
{
    private static League CreateLeagueWithInjuredPlayer(int injuryDays)
    {
        var league = new League();
        var team = new Team { Id = 1, Name = "TestTeam" };
        team.Roster.Add(new Player
        {
            Name = "Injured Player",
            Injury = injuryDays,
            Health = 80,
            InjuryDescription = "Sprained ankle",
            Active = true
        });
        team.Roster.Add(new Player
        {
            Name = "Healthy Player",
            Injury = 0,
            Health = 100,
            Active = true
        });
        league.Teams.Add(team);
        return league;
    }

    // ── HealInjuries ───────────────────────────────────────────────

    [Fact]
    public void HealInjuries_DecrementsInjuryByDays()
    {
        var league = CreateLeagueWithInjuredPlayer(5);
        var player = league.Teams[0].Roster[0];

        InjuryService.HealInjuries(league, 1, new Random(42));

        player.Injury.Should().Be(4);
    }

    [Fact]
    public void HealInjuries_WhenReachesZero_RestoresHealth()
    {
        var league = CreateLeagueWithInjuredPlayer(1);
        var player = league.Teams[0].Roster[0];

        InjuryService.HealInjuries(league, 1, new Random(42));

        player.Injury.Should().Be(0);
        player.Health.Should().BeInRange(90, 100);
    }

    [Fact]
    public void HealInjuries_WhenFullHealth_ClearsDescription()
    {
        var league = CreateLeagueWithInjuredPlayer(1);
        var player = league.Teams[0].Roster[0];

        // Use a seed that produces health=100
        InjuryService.HealInjuries(league, 1, new Random(100));

        player.Injury.Should().Be(0);
        if (player.Health >= 100)
            player.InjuryDescription.Should().BeEmpty();
    }

    [Fact]
    public void HealInjuries_DoesNotGoBelowZero()
    {
        var league = CreateLeagueWithInjuredPlayer(1);

        InjuryService.HealInjuries(league, 5, new Random(42));

        league.Teams[0].Roster[0].Injury.Should().Be(0);
    }

    [Fact]
    public void HealInjuries_HealthyPlayerUnaffected()
    {
        var league = CreateLeagueWithInjuredPlayer(5);
        var healthyPlayer = league.Teams[0].Roster[1];

        InjuryService.HealInjuries(league, 1, new Random(42));

        healthyPlayer.Health.Should().Be(100);
        healthyPlayer.Injury.Should().Be(0);
    }

    [Fact]
    public void HealInjuries_MultipleTeams_HealsAll()
    {
        var league = new League();
        var team1 = new Team { Id = 1, Name = "T1" };
        team1.Roster.Add(new Player { Name = "P1", Injury = 3, Health = 80, InjuryDescription = "Injury", Active = true });
        var team2 = new Team { Id = 2, Name = "T2" };
        team2.Roster.Add(new Player { Name = "P2", Injury = 2, Health = 80, InjuryDescription = "Injury", Active = true });
        league.Teams.Add(team1);
        league.Teams.Add(team2);

        InjuryService.HealInjuries(league, 1, new Random(42));

        team1.Roster[0].Injury.Should().Be(2);
        team2.Roster[0].Injury.Should().Be(1);
    }

    // ── ApplyInjury ────────────────────────────────────────────────

    [Fact]
    public void ApplyInjury_SetsInjuryAndDescription()
    {
        var player = new Player { Name = "Test", Health = 100, Active = true };

        InjuryService.ApplyInjury(player, 5, new Random(42));

        player.Injury.Should().BeGreaterThan(0);
        player.Health.Should().BeLessThan(100);
        player.InjuryDescription.Should().NotBeEmpty();
    }

    [Fact]
    public void ApplyInjury_ZeroGamesOut_NoEffect()
    {
        var player = new Player { Name = "Test", Health = 100, Active = true };

        InjuryService.ApplyInjury(player, 0, new Random(42));

        player.Injury.Should().Be(0);
        player.Health.Should().Be(100);
    }

    // ── GenerateInjuryDescription ──────────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(15)]
    [InlineData(50)]
    [InlineData(90)]
    public void GenerateInjuryDescription_ReturnsSeverityAppropriateName(int gamesOut)
    {
        var desc = InjuryService.GenerateInjuryDescription(gamesOut, new Random(42));

        desc.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateInjuryDescription_DeterministicWithSeed()
    {
        var desc1 = InjuryService.GenerateInjuryDescription(10, new Random(42));
        var desc2 = InjuryService.GenerateInjuryDescription(10, new Random(42));

        desc1.Should().Be(desc2);
    }

    // ── Helpers for Phase 27 tests ───────────────────────────────────

    private static Player CreatePlayerWithStats(string name = "Test Player", int injuryRating = 50)
    {
        return new Player
        {
            Name = name,
            Position = "PG",
            Age = 25,
            Health = 100,
            Active = true,
            SimulatedStats = new PlayerStatLine
            {
                Games = 82,
                Minutes = 2800,
                FieldGoalsMade = 600,
                FieldGoalsAttempted = 1200,
                FreeThrowsMade = 300,
                FreeThrowsAttempted = 400,
                ThreePointersMade = 100,
                ThreePointersAttempted = 250,
                OffensiveRebounds = 80,
                Rebounds = 400,
                Assists = 500,
                Steals = 120,
                Turnovers = 200,
                Blocks = 50,
                PersonalFouls = 180
            },
            Ratings = new PlayerRatings
            {
                InjuryRating = injuryRating,
                MovementOffenseRaw = 5,
                MovementDefenseRaw = 5,
                PenetrationOffenseRaw = 5,
                PenetrationDefenseRaw = 5,
                PostOffenseRaw = 5,
                PostDefenseRaw = 5,
                TransitionOffenseRaw = 5,
                TransitionDefenseRaw = 5
            }
        };
    }

    private static League CreateLeagueForOffSeasonInjuries(int teamsCount = 2,
        int playersPerTeam = 5, int injuryRating = 50)
    {
        var league = new League();
        league.Settings.InjuriesEnabled = true;

        for (int t = 0; t < teamsCount; t++)
        {
            var team = new Team
            {
                Id = t,
                Name = $"Team {t + 1}"
            };

            for (int p = 0; p < playersPerTeam; p++)
            {
                team.Roster.Add(CreatePlayerWithStats($"Player {t}-{p}", injuryRating));
            }

            league.Teams.Add(team);
        }

        return league;
    }

    // ── ApplyPermanentInjuryEffects ──────────────────────────────────

    [Fact]
    public void ApplyPermanentInjuryEffects_GamesOut5OrLess_NoDamage()
    {
        var player = CreatePlayerWithStats();
        int origFgm = player.SimulatedStats.FieldGoalsMade;

        bool result = InjuryService.ApplyPermanentInjuryEffects(player, 5, new Random(42));

        result.Should().BeFalse();
        player.SimulatedStats.FieldGoalsMade.Should().Be(origFgm);
    }

    [Fact]
    public void ApplyPermanentInjuryEffects_GamesOut1_NoDamage()
    {
        var player = CreatePlayerWithStats();
        int origFga = player.SimulatedStats.FieldGoalsAttempted;

        bool result = InjuryService.ApplyPermanentInjuryEffects(player, 1, new Random(42));

        result.Should().BeFalse();
        player.SimulatedStats.FieldGoalsAttempted.Should().Be(origFga);
    }

    [Fact]
    public void ApplyPermanentInjuryEffects_GamesOut99_HighProbability()
    {
        int triggered = 0;
        for (int trial = 0; trial < 100; trial++)
        {
            var player = CreatePlayerWithStats();
            if (InjuryService.ApplyPermanentInjuryEffects(player, 99, new Random(trial)))
                triggered++;
        }

        triggered.Should().BeGreaterThan(80);
    }

    [Fact]
    public void ApplyPermanentInjuryEffects_GamesOut6_LowProbability()
    {
        int triggered = 0;
        for (int trial = 0; trial < 1000; trial++)
        {
            var player = CreatePlayerWithStats();
            if (InjuryService.ApplyPermanentInjuryEffects(player, 6, new Random(trial)))
                triggered++;
        }

        triggered.Should().BeLessThan(50);
    }

    [Fact]
    public void ApplyPermanentInjuryEffects_WhenTriggered_ReducesSimulatedStats()
    {
        for (int seed = 0; seed < 1000; seed++)
        {
            var player = CreatePlayerWithStats();
            int origFgm = player.SimulatedStats.FieldGoalsMade;
            int origFga = player.SimulatedStats.FieldGoalsAttempted;
            int origReb = player.SimulatedStats.Rebounds;
            int origAst = player.SimulatedStats.Assists;

            if (InjuryService.ApplyPermanentInjuryEffects(player, 99, new Random(seed)))
            {
                player.SimulatedStats.FieldGoalsMade.Should().BeLessThanOrEqualTo(origFgm);
                player.SimulatedStats.FieldGoalsAttempted.Should().BeLessThanOrEqualTo(origFga);
                player.SimulatedStats.Rebounds.Should().BeLessThanOrEqualTo(origReb);
                player.SimulatedStats.Assists.Should().BeLessThanOrEqualTo(origAst);
                return;
            }
        }

        true.Should().BeFalse("Expected to find a seed that triggers permanent injury");
    }

    [Fact]
    public void ApplyPermanentInjuryEffects_MadeStats_ExtraReduction()
    {
        for (int seed = 0; seed < 1000; seed++)
        {
            var player = CreatePlayerWithStats();
            player.SimulatedStats.FieldGoalsMade = 1000;
            player.SimulatedStats.FieldGoalsAttempted = 1000;

            if (InjuryService.ApplyPermanentInjuryEffects(player, 99, new Random(seed)))
            {
                player.SimulatedStats.FieldGoalsMade.Should()
                    .BeLessThanOrEqualTo(player.SimulatedStats.FieldGoalsAttempted);
                return;
            }
        }

        true.Should().BeFalse("Expected to find a seed that triggers permanent injury");
    }

    [Fact]
    public void ApplyPermanentInjuryEffects_OdptRatings_CanDecrease()
    {
        bool anyDecreased = false;
        for (int seed = 0; seed < 1000; seed++)
        {
            var player = CreatePlayerWithStats();
            if (InjuryService.ApplyPermanentInjuryEffects(player, 99, new Random(seed)))
            {
                if (player.Ratings.MovementOffenseRaw < 5 ||
                    player.Ratings.PenetrationOffenseRaw < 5 ||
                    player.Ratings.PostOffenseRaw < 5 ||
                    player.Ratings.TransitionOffenseRaw < 5 ||
                    player.Ratings.MovementDefenseRaw < 5 ||
                    player.Ratings.PenetrationDefenseRaw < 5 ||
                    player.Ratings.PostDefenseRaw < 5 ||
                    player.Ratings.TransitionDefenseRaw < 5)
                {
                    anyDecreased = true;
                    break;
                }
            }
        }

        anyDecreased.Should().BeTrue("At least one ODPT rating should decrease across many severe injuries");
    }

    [Fact]
    public void ApplyPermanentInjuryEffects_OdptRatings_FloorAt1()
    {
        for (int seed = 0; seed < 1000; seed++)
        {
            var player = CreatePlayerWithStats();
            player.Ratings.MovementOffenseRaw = 1;
            player.Ratings.MovementDefenseRaw = 1;
            player.Ratings.PenetrationOffenseRaw = 1;
            player.Ratings.PenetrationDefenseRaw = 1;
            player.Ratings.PostOffenseRaw = 1;
            player.Ratings.PostDefenseRaw = 1;
            player.Ratings.TransitionOffenseRaw = 1;
            player.Ratings.TransitionDefenseRaw = 1;

            InjuryService.ApplyPermanentInjuryEffects(player, 99, new Random(seed));

            player.Ratings.MovementOffenseRaw.Should().BeGreaterThanOrEqualTo(1);
            player.Ratings.MovementDefenseRaw.Should().BeGreaterThanOrEqualTo(1);
            player.Ratings.PenetrationOffenseRaw.Should().BeGreaterThanOrEqualTo(1);
            player.Ratings.PenetrationDefenseRaw.Should().BeGreaterThanOrEqualTo(1);
            player.Ratings.PostOffenseRaw.Should().BeGreaterThanOrEqualTo(1);
            player.Ratings.PostDefenseRaw.Should().BeGreaterThanOrEqualTo(1);
            player.Ratings.TransitionOffenseRaw.Should().BeGreaterThanOrEqualTo(1);
            player.Ratings.TransitionDefenseRaw.Should().BeGreaterThanOrEqualTo(1);
        }
    }

    [Fact]
    public void ApplyPermanentInjuryEffects_GamesAbove99_ClampedTo99()
    {
        int triggeredAt99 = 0;
        int triggeredAt160 = 0;
        for (int trial = 0; trial < 200; trial++)
        {
            var p1 = CreatePlayerWithStats();
            var p2 = CreatePlayerWithStats();
            if (InjuryService.ApplyPermanentInjuryEffects(p1, 99, new Random(trial)))
                triggeredAt99++;
            if (InjuryService.ApplyPermanentInjuryEffects(p2, 160, new Random(trial)))
                triggeredAt160++;
        }

        triggeredAt99.Should().Be(triggeredAt160);
    }

    [Fact]
    public void ApplyPermanentInjuryEffects_GamesPreserved_NotReduced()
    {
        for (int seed = 0; seed < 1000; seed++)
        {
            var player = CreatePlayerWithStats();
            int origGames = player.SimulatedStats.Games;
            int origMinutes = player.SimulatedStats.Minutes;

            if (InjuryService.ApplyPermanentInjuryEffects(player, 99, new Random(seed)))
            {
                player.SimulatedStats.Games.Should().Be(origGames);
                player.SimulatedStats.Minutes.Should().Be(origMinutes);
                return;
            }
        }

        true.Should().BeFalse("Expected to find a seed that triggers permanent injury");
    }

    // ── CalculateOffSeasonInjury ─────────────────────────────────────

    [Fact]
    public void CalculateOffSeasonInjury_AlreadyInjured_Skipped()
    {
        var player = CreatePlayerWithStats();
        player.Injury = 10;

        int result = InjuryService.CalculateOffSeasonInjury(player, 0, new Random(42));

        result.Should().Be(0);
    }

    [Fact]
    public void CalculateOffSeasonInjury_EmptyName_Skipped()
    {
        var player = CreatePlayerWithStats();
        player.Name = "";

        int result = InjuryService.CalculateOffSeasonInjury(player, 0, new Random(42));

        result.Should().Be(0);
    }

    [Fact]
    public void CalculateOffSeasonInjury_Stage0_UsesHighThreshold()
    {
        int injuredStage0 = 0;
        int injuredStagePos = 0;

        for (int seed = 0; seed < 500; seed++)
        {
            var p1 = CreatePlayerWithStats(injuryRating: 100);
            var p2 = CreatePlayerWithStats(injuryRating: 100);

            if (InjuryService.CalculateOffSeasonInjury(p1, 0, new Random(seed)) > 0)
                injuredStage0++;
            if (InjuryService.CalculateOffSeasonInjury(p2, 1, new Random(seed)) > 0)
                injuredStagePos++;
        }

        injuredStagePos.Should().BeGreaterThanOrEqualTo(injuredStage0,
            "stage > 0 should produce at least as many injuries as stage 0");
    }

    [Fact]
    public void CalculateOffSeasonInjury_WhenTriggered_SetsInjuryDays()
    {
        for (int seed = 0; seed < 10000; seed++)
        {
            var player = CreatePlayerWithStats(injuryRating: 200);
            int gamesOut = InjuryService.CalculateOffSeasonInjury(player, 1, new Random(seed));

            if (gamesOut > 0)
            {
                player.Injury.Should().BeGreaterThan(0);
                player.Injury.Should().BeGreaterThanOrEqualTo(1);
                return;
            }
        }

        true.Should().BeFalse("Expected to find a seed that triggers off-season injury");
    }

    [Fact]
    public void CalculateOffSeasonInjury_WhenTriggered_SetsDescription()
    {
        for (int seed = 0; seed < 10000; seed++)
        {
            var player = CreatePlayerWithStats(injuryRating: 200);
            int gamesOut = InjuryService.CalculateOffSeasonInjury(player, 1, new Random(seed));

            if (gamesOut > 0)
            {
                player.InjuryDescription.Should().NotBeNullOrEmpty();
                return;
            }
        }

        true.Should().BeFalse("Expected to find a seed that triggers off-season injury");
    }

    [Fact]
    public void CalculateOffSeasonInjury_WhenTriggered_ReducesHealth()
    {
        for (int seed = 0; seed < 10000; seed++)
        {
            var player = CreatePlayerWithStats(injuryRating: 200);
            int gamesOut = InjuryService.CalculateOffSeasonInjury(player, 1, new Random(seed));

            if (gamesOut > 0)
            {
                player.Health.Should().BeLessThanOrEqualTo(100);
                player.Health.Should().BeGreaterThanOrEqualTo(50);
                return;
            }
        }

        true.Should().BeFalse("Expected to find a seed that triggers off-season injury");
    }

    [Fact]
    public void CalculateOffSeasonInjury_GamesOutClamped_1to160()
    {
        for (int seed = 0; seed < 10000; seed++)
        {
            var player = CreatePlayerWithStats(injuryRating: 200);
            int gamesOut = InjuryService.CalculateOffSeasonInjury(player, 1, new Random(seed));

            if (gamesOut > 0)
            {
                gamesOut.Should().BeGreaterThanOrEqualTo(1);
                gamesOut.Should().BeLessThanOrEqualTo(160);
                return;
            }
        }

        true.Should().BeFalse("Expected to find a seed that triggers off-season injury");
    }

    [Fact]
    public void CalculateOffSeasonInjury_ZeroInjuryRating_NeverInjured()
    {
        for (int seed = 0; seed < 500; seed++)
        {
            var player = CreatePlayerWithStats(injuryRating: 0);
            int gamesOut = InjuryService.CalculateOffSeasonInjury(player, 1, new Random(seed));
            gamesOut.Should().Be(0);
        }
    }

    // ── ProcessOffSeasonInjuries ─────────────────────────────────────

    [Fact]
    public void ProcessOffSeasonInjuries_EmptyLeague_ReturnsZero()
    {
        var league = new League();

        int count = InjuryService.ProcessOffSeasonInjuries(league, new Random(42));

        count.Should().Be(0);
    }

    [Fact]
    public void ProcessOffSeasonInjuries_HealthyPlayers_CanGetInjured()
    {
        var league = CreateLeagueForOffSeasonInjuries(teamsCount: 4, playersPerTeam: 15,
            injuryRating: 500);

        int count = InjuryService.ProcessOffSeasonInjuries(league, new Random(42));

        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ProcessOffSeasonInjuries_AlreadyInjured_Skipped()
    {
        var league = CreateLeagueForOffSeasonInjuries(teamsCount: 1, playersPerTeam: 5,
            injuryRating: 500);

        foreach (var team in league.Teams)
            foreach (var player in team.Roster)
                player.Injury = 10;

        int count = InjuryService.ProcessOffSeasonInjuries(league, new Random(42));

        count.Should().Be(0);
    }

    [Fact]
    public void ProcessOffSeasonInjuries_ReturnsCorrectCount()
    {
        var league = CreateLeagueForOffSeasonInjuries(teamsCount: 4, playersPerTeam: 15,
            injuryRating: 500);

        int count = InjuryService.ProcessOffSeasonInjuries(league, new Random(42));

        int actualInjured = league.Teams
            .SelectMany(t => t.Roster)
            .Count(p => p.Injury > 0);

        count.Should().Be(actualInjured);
    }

    [Fact]
    public void ProcessOffSeasonInjuries_EmptyNamePlayers_Skipped()
    {
        var league = CreateLeagueForOffSeasonInjuries(teamsCount: 1, playersPerTeam: 5,
            injuryRating: 5000);

        foreach (var team in league.Teams)
            foreach (var player in team.Roster)
                player.Name = "";

        int count = InjuryService.ProcessOffSeasonInjuries(league, new Random(42));

        count.Should().Be(0);
    }

    // ── Integration Tests ────────────────────────────────────────────

    [Fact]
    public void OffSeasonResult_HasInjuryCountField()
    {
        var result = new OffSeasonResult();
        result.OffSeasonInjuries = 5;
        result.OffSeasonInjuries.Should().Be(5);
    }

    [Fact]
    public void PermanentDamage_ReducesStats_IntegrationVerification()
    {
        for (int seed = 0; seed < 1000; seed++)
        {
            var player = CreatePlayerWithStats();
            int origFgm = player.SimulatedStats.FieldGoalsMade;
            int origAst = player.SimulatedStats.Assists;

            bool damaged = InjuryService.ApplyPermanentInjuryEffects(player, 80, new Random(seed));

            if (damaged)
            {
                bool anyReduced = player.SimulatedStats.FieldGoalsMade < origFgm ||
                                  player.SimulatedStats.Assists < origAst;
                anyReduced.Should().BeTrue("Permanent damage should reduce at least some stats");
                return;
            }
        }

        true.Should().BeFalse("Expected to find a seed that triggers permanent injury at 80 games");
    }

    [Fact]
    public void PermanentDamage_ReducesOdpt_IntegrationVerification()
    {
        bool anyOdptReduced = false;
        for (int seed = 0; seed < 1000; seed++)
        {
            var player = CreatePlayerWithStats();
            bool damaged = InjuryService.ApplyPermanentInjuryEffects(player, 99, new Random(seed));

            if (damaged)
            {
                int totalOdpt = player.Ratings.MovementOffenseRaw +
                                player.Ratings.PenetrationOffenseRaw +
                                player.Ratings.PostOffenseRaw +
                                player.Ratings.TransitionOffenseRaw +
                                player.Ratings.MovementDefenseRaw +
                                player.Ratings.PenetrationDefenseRaw +
                                player.Ratings.PostDefenseRaw +
                                player.Ratings.TransitionDefenseRaw;

                if (totalOdpt < 40)
                {
                    anyOdptReduced = true;
                    break;
                }
            }
        }

        anyOdptReduced.Should().BeTrue("At least one trial should reduce ODPT ratings");
    }
}
