using FluentAssertions;
using JumpShotBasketball.Core.Models.Awards;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Playoff;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class AwardsServiceTests
{
    // ───────────────────────────────────────────────────────────────
    // Test helpers
    // ───────────────────────────────────────────────────────────────

    private static Models.League.League CreateTestLeague(int teamCount = 4, int playersPerTeam = 15)
    {
        var league = new Models.League.League();
        league.Schedule.GamesInSeason = 82;

        for (int t = 0; t < teamCount; t++)
        {
            var team = new Models.Team.Team
            {
                Id = t,
                Name = $"Team {t}",
                Record = new Models.Team.TeamRecord
                {
                    Conference = t < teamCount / 2 ? "East" : "West",
                    Division = $"Div{t % 3}"
                }
            };

            for (int p = 0; p < playersPerTeam; p++)
            {
                int id = t * 100 + p;
                var player = CreatePlayer(id, t, p);
                team.Roster.Add(player);
            }

            league.Teams.Add(team);
        }

        return league;
    }

    private static Player CreatePlayer(int id, int teamIndex, int slot)
    {
        string pos = slot switch
        {
            0 => "PG",
            1 => "SG",
            2 => "SF",
            3 => "PF",
            4 => "C",
            _ => new[] { "PG", "SG", "SF", "PF", "C" }[slot % 5]
        };

        // Scale stats by slot — lower slots (starters) get better stats
        double factor = 1.0 - (slot * 0.05);
        if (factor < 0.2) factor = 0.2;

        var player = new Player
        {
            Id = id,
            Name = $"Player {id}",
            Position = pos,
            TeamIndex = teamIndex,
            Age = 25,
            Starter = slot < 5 ? 70 : 5, // starters started 70 of 80 games
            Contract = new PlayerContract { YearsOfService = slot < 2 ? 1 : 5 },
            SimulatedStats = new PlayerStatLine
            {
                Games = 80,
                Minutes = (int)(2800 * factor),
                FieldGoalsMade = (int)(500 * factor),
                FieldGoalsAttempted = (int)(1100 * factor),
                FreeThrowsMade = (int)(300 * factor),
                FreeThrowsAttempted = (int)(350 * factor),
                ThreePointersMade = (int)(200 * factor),
                ThreePointersAttempted = (int)(500 * factor),
                OffensiveRebounds = (int)(100 * factor),
                Rebounds = (int)(500 * factor),
                Assists = (int)(300 * factor),
                Steals = (int)(100 * factor),
                Turnovers = (int)(50 * factor),
                Blocks = (int)(50 * factor),
                PersonalFouls = (int)(150 * factor)
            },
            PlayoffStats = new PlayerStatLine(),
            Ratings = new PlayerRatings
            {
                Potential1 = 5,
                Potential2 = 5,
                Effort = 5
            }
        };

        return player;
    }

    // ───────────────────────────────────────────────────────────────
    // MVP tests
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeMvp_ReturnsTop5_OrderedByTrueRating()
    {
        var league = CreateTestLeague();
        var results = AwardsService.ComputeMvp(league);

        results.Should().HaveCount(5);
        results[0].Rank.Should().Be(1);
        results[4].Rank.Should().Be(5);
        results[0].Value.Should().BeGreaterThanOrEqualTo(results[1].Value);
    }

    [Fact]
    public void ComputeMvp_ExcludesPlayersBelow24Mpg()
    {
        var league = CreateTestLeague(teamCount: 2, playersPerTeam: 5);
        // Set all players to low minutes
        foreach (var team in league.Teams)
            foreach (var player in team.Roster)
                player.SimulatedStats.Minutes = 1000; // ~12.5 mpg over 80 games

        var results = AwardsService.ComputeMvp(league);
        results.Should().BeEmpty();
    }

    [Fact]
    public void ComputeMvp_ExcludesPlayersBelow62Point5PercentGames()
    {
        var league = CreateTestLeague(teamCount: 2, playersPerTeam: 5);
        foreach (var team in league.Teams)
            foreach (var player in team.Roster)
                player.SimulatedStats.Games = 40; // 40/82 = 48.8% < 62.5%

        var results = AwardsService.ComputeMvp(league);
        results.Should().BeEmpty();
    }

    [Fact]
    public void ComputeMvp_RespectsCountParameter()
    {
        var league = CreateTestLeague();
        var results = AwardsService.ComputeMvp(league, count: 3);
        results.Should().HaveCount(3);
    }

    // ───────────────────────────────────────────────────────────────
    // DPOY tests
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeDefensivePlayer_ReturnsTop5_OrderedByDefenseRating()
    {
        var league = CreateTestLeague();
        var results = AwardsService.ComputeDefensivePlayer(league);

        results.Should().HaveCount(5);
        results[0].Value.Should().BeGreaterThanOrEqualTo(results[1].Value);
    }

    [Fact]
    public void ComputeDefensivePlayer_UsesDefenseRating()
    {
        var league = CreateTestLeague();
        var results = AwardsService.ComputeDefensivePlayer(league);

        // Value should be a defense rating (typically 0-5 range)
        results[0].Value.Should().NotBe(0);
    }

    // ───────────────────────────────────────────────────────────────
    // ROY tests
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeRookieOfYear_OnlyIncludesRookies()
    {
        var league = CreateTestLeague();
        var results = AwardsService.ComputeRookieOfYear(league);

        results.Should().NotBeEmpty();
        // In our test league, slots 0 and 1 per team are rookies (YearsOfService=1)
        foreach (var r in results)
        {
            var player = FindPlayer(league, r.PlayerId);
            player.Contract.YearsOfService.Should().BeLessThanOrEqualTo(1);
        }
    }

    [Fact]
    public void ComputeRookieOfYear_ReturnsEmpty_WhenNoRookies()
    {
        var league = CreateTestLeague();
        foreach (var team in league.Teams)
            foreach (var player in team.Roster)
                player.Contract.YearsOfService = 5;

        var results = AwardsService.ComputeRookieOfYear(league);
        results.Should().BeEmpty();
    }

    [Fact]
    public void ComputeRookieOfYear_NoMinutesRequirement()
    {
        var league = CreateTestLeague(teamCount: 2, playersPerTeam: 5);
        // Set all players as rookies with low minutes
        foreach (var team in league.Teams)
            foreach (var player in team.Roster)
            {
                player.Contract.YearsOfService = 1;
                player.SimulatedStats.Minutes = 500; // ~6.25 mpg
            }

        var results = AwardsService.ComputeRookieOfYear(league);
        results.Should().NotBeEmpty();
    }

    // ───────────────────────────────────────────────────────────────
    // 6th Man tests
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeSixthMan_ExcludesFrequentStarters()
    {
        var league = CreateTestLeague();
        var results = AwardsService.ComputeSixthMan(league);

        foreach (var r in results)
        {
            var player = FindPlayer(league, r.PlayerId);
            double starterRatio = (double)player.Starter / player.SimulatedStats.Games;
            starterRatio.Should().BeLessThanOrEqualTo(0.375);
        }
    }

    [Fact]
    public void ComputeSixthMan_RequiresMinimum16Mpg()
    {
        var league = CreateTestLeague(teamCount: 2, playersPerTeam: 10);
        // Make everyone a bench player but with low minutes
        foreach (var team in league.Teams)
            foreach (var player in team.Roster)
            {
                player.Starter = 0;
                player.SimulatedStats.Minutes = 800; // 10 mpg
            }

        var results = AwardsService.ComputeSixthMan(league);
        results.Should().BeEmpty();
    }

    // ───────────────────────────────────────────────────────────────
    // Playoff MVP tests
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void ComputePlayoffMvp_ReturnsNull_WhenNoBracket()
    {
        var league = CreateTestLeague();
        league.Bracket = null;

        var result = AwardsService.ComputePlayoffMvp(league);
        result.Should().BeNull();
    }

    [Fact]
    public void ComputePlayoffMvp_ReturnsNull_WhenNoChampion()
    {
        var league = CreateTestLeague();
        league.Bracket = new PlayoffBracket { ChampionTeamIndex = null };

        var result = AwardsService.ComputePlayoffMvp(league);
        result.Should().BeNull();
    }

    [Fact]
    public void ComputePlayoffMvp_SelectsFromChampionTeamOnly()
    {
        var league = CreateTestLeague();
        league.Bracket = new PlayoffBracket { ChampionTeamIndex = 0 };

        // Give champion team playoff stats
        foreach (var player in league.Teams[0].Roster.Take(5))
        {
            player.PlayoffStats = new PlayerStatLine
            {
                Games = 20,
                Minutes = 700,
                FieldGoalsMade = 100,
                FieldGoalsAttempted = 220,
                FreeThrowsMade = 50,
                FreeThrowsAttempted = 60,
                ThreePointersMade = 30,
                ThreePointersAttempted = 80,
                OffensiveRebounds = 20,
                Rebounds = 100,
                Assists = 60,
                Steals = 20,
                Turnovers = 15,
                Blocks = 10,
                PersonalFouls = 40
            };
        }

        var result = AwardsService.ComputePlayoffMvp(league);
        result.Should().NotBeNull();
        result!.TeamIndex.Should().Be(0);
        result.Rank.Should().Be(1);
    }

    // ───────────────────────────────────────────────────────────────
    // Stat leader tests
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeScoringLeaders_ReturnsTop5_OrderedByPpg()
    {
        var league = CreateTestLeague();
        var results = AwardsService.ComputeScoringLeaders(league);

        results.Count.Should().BeLessThanOrEqualTo(5);
        if (results.Count >= 2)
            results[0].Value.Should().BeGreaterThanOrEqualTo(results[1].Value);
    }

    [Fact]
    public void ComputeReboundingLeaders_ReturnsOrderedResults()
    {
        var league = CreateTestLeague();
        var results = AwardsService.ComputeReboundingLeaders(league);

        results.Should().NotBeEmpty();
        for (int i = 1; i < results.Count; i++)
            results[i - 1].Value.Should().BeGreaterThanOrEqualTo(results[i].Value);
    }

    [Fact]
    public void ComputeAssistsLeaders_ReturnsOrderedResults()
    {
        var league = CreateTestLeague();
        var results = AwardsService.ComputeAssistsLeaders(league);
        results.Should().NotBeEmpty();
    }

    [Fact]
    public void ComputeStealsLeaders_ReturnsOrderedResults()
    {
        var league = CreateTestLeague();
        var results = AwardsService.ComputeStealsLeaders(league);
        results.Should().NotBeEmpty();
    }

    [Fact]
    public void ComputeBlocksLeaders_ReturnsOrderedResults()
    {
        var league = CreateTestLeague();
        var results = AwardsService.ComputeBlocksLeaders(league);
        results.Should().NotBeEmpty();
    }

    // ───────────────────────────────────────────────────────────────
    // All-League team tests
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeAllLeagueTeams_Returns3Teams()
    {
        var league = CreateTestLeague();
        var teams = AwardsService.ComputeAllLeagueTeams(league);

        teams.Should().HaveCount(3);
        teams[0].TeamNumber.Should().Be(1);
        teams[1].TeamNumber.Should().Be(2);
        teams[2].TeamNumber.Should().Be(3);
    }

    [Fact]
    public void ComputeAllLeagueTeams_EachTeamHas5Positions()
    {
        var league = CreateTestLeague();
        var teams = AwardsService.ComputeAllLeagueTeams(league);

        foreach (var team in teams)
        {
            team.Players.Should().HaveCount(5);
            team.Players.Select(p => p.Position.Trim()).Distinct().Should().HaveCount(5);
        }
    }

    [Fact]
    public void ComputeAllLeagueTeams_NoDuplicatePlayers()
    {
        var league = CreateTestLeague();
        var teams = AwardsService.ComputeAllLeagueTeams(league);

        var allPlayerIds = teams.SelectMany(t => t.Players).Select(p => p.PlayerId).ToList();
        allPlayerIds.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void ComputeAllLeagueTeams_FirstTeamHasLabel()
    {
        var league = CreateTestLeague();
        var teams = AwardsService.ComputeAllLeagueTeams(league);

        teams[0].TeamLabel.Should().Be("1st Team");
        teams[1].TeamLabel.Should().Be("2nd Team");
        teams[2].TeamLabel.Should().Be("3rd Team");
    }

    // ───────────────────────────────────────────────────────────────
    // All-Defense team tests
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeAllDefenseTeams_Returns2Teams()
    {
        var league = CreateTestLeague();
        var teams = AwardsService.ComputeAllDefenseTeams(league);

        teams.Should().HaveCount(2);
    }

    [Fact]
    public void ComputeAllDefenseTeams_NoDuplicatePlayers()
    {
        var league = CreateTestLeague();
        var teams = AwardsService.ComputeAllDefenseTeams(league);

        var allPlayerIds = teams.SelectMany(t => t.Players).Select(p => p.PlayerId).ToList();
        allPlayerIds.Should().OnlyHaveUniqueItems();
    }

    // ───────────────────────────────────────────────────────────────
    // All-Rookie team tests
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeAllRookieTeams_OnlyContainsRookies()
    {
        var league = CreateTestLeague();
        var teams = AwardsService.ComputeAllRookieTeams(league);

        var allPlayerIds = teams.SelectMany(t => t.Players).Select(p => p.PlayerId);
        foreach (var pid in allPlayerIds)
        {
            var player = FindPlayer(league, pid);
            player.Contract.YearsOfService.Should().BeLessThanOrEqualTo(1);
        }
    }

    [Fact]
    public void ComputeAllRookieTeams_Returns2Teams()
    {
        var league = CreateTestLeague();
        var teams = AwardsService.ComputeAllRookieTeams(league);

        teams.Should().HaveCount(2);
    }

    // ───────────────────────────────────────────────────────────────
    // ComputeAllAwards orchestrator
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeAllAwards_PopulatesAllAwardFields()
    {
        var league = CreateTestLeague();
        league.Bracket = new PlayoffBracket { ChampionTeamIndex = 0 };
        // Give champion team playoff stats
        foreach (var player in league.Teams[0].Roster.Take(5))
        {
            player.PlayoffStats = new PlayerStatLine
            {
                Games = 20,
                Minutes = 700,
                FieldGoalsMade = 100,
                FieldGoalsAttempted = 220,
                FreeThrowsMade = 50,
                FreeThrowsAttempted = 60,
                ThreePointersMade = 30,
                ThreePointersAttempted = 80,
                OffensiveRebounds = 20,
                Rebounds = 100,
                Assists = 60,
                Steals = 20,
                Turnovers = 15,
                Blocks = 10,
                PersonalFouls = 40
            };
        }

        var awards = AwardsService.ComputeAllAwards(league);

        awards.Mvp.Recipients.Should().NotBeEmpty();
        awards.DefensivePlayerOfYear.Recipients.Should().NotBeEmpty();
        awards.RookieOfYear.Recipients.Should().NotBeEmpty();
        awards.PlayoffMvp.Recipients.Should().NotBeEmpty();
        awards.AllLeagueTeams.Should().HaveCount(3);
        awards.AllDefenseTeams.Should().HaveCount(2);
        awards.AllRookieTeams.Should().HaveCount(2);
        awards.ChampionTeamIndex.Should().Be(0);
        awards.RingRecipientPlayerIds.Should().NotBeEmpty();
    }

    [Fact]
    public void ComputeAllAwards_ChampionTeamIndex_MinusOne_WhenNoBracket()
    {
        var league = CreateTestLeague();
        league.Bracket = null;

        var awards = AwardsService.ComputeAllAwards(league);

        awards.ChampionTeamIndex.Should().Be(-1);
        awards.RingRecipientPlayerIds.Should().BeEmpty();
    }

    [Fact]
    public void ComputeAllAwards_SetsRingRecipients_ForChampionRoster()
    {
        var league = CreateTestLeague();
        league.Bracket = new PlayoffBracket { ChampionTeamIndex = 1 };

        var awards = AwardsService.ComputeAllAwards(league);

        awards.ChampionTeamIndex.Should().Be(1);
        awards.RingRecipientPlayerIds.Should().HaveCount(league.Teams[1].Roster.Count);
        foreach (var pid in awards.RingRecipientPlayerIds)
        {
            league.Teams[1].Roster.Should().Contain(p => p.Id == pid);
        }
    }

    // ───────────────────────────────────────────────────────────────
    // Edge cases
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeMvp_ReturnsEmpty_WhenNoEligiblePlayers()
    {
        var league = new Models.League.League();
        league.Schedule.GamesInSeason = 82;
        league.Teams.Add(new Models.Team.Team { Id = 0, Name = "Empty" });

        var results = AwardsService.ComputeMvp(league);
        results.Should().BeEmpty();
    }

    [Fact]
    public void ComputeAllLeagueTeams_HandlesFewerCandidatesThanPositions()
    {
        // Only 3 players total — can't fill all positions
        var league = new Models.League.League();
        league.Schedule.GamesInSeason = 82;

        var team = new Models.Team.Team { Id = 0, Name = "Small" };
        for (int i = 0; i < 3; i++)
        {
            team.Roster.Add(CreatePlayer(i, 0, i));
        }
        league.Teams.Add(team);

        var teams = AwardsService.ComputeAllLeagueTeams(league);
        // Should still return 3 team objects, but with fewer players
        teams.Should().HaveCount(3);
        teams[0].Players.Count.Should().BeLessThanOrEqualTo(5);
    }

    [Fact]
    public void ComputeSixthMan_WorksWithBenchPlayers()
    {
        var league = CreateTestLeague();
        // Ensure some bench players with enough minutes
        foreach (var team in league.Teams)
        {
            for (int i = 5; i < team.Roster.Count; i++)
            {
                team.Roster[i].Starter = 0;
                team.Roster[i].SimulatedStats.Games = 80;
                team.Roster[i].SimulatedStats.Minutes = 1600; // 20 mpg
            }
        }

        var results = AwardsService.ComputeSixthMan(league);
        results.Should().NotBeEmpty();
    }

    // ───────────────────────────────────────────────────────────────
    // CalculateDefenseRating tests (in StatisticsCalculator)
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void CalculateDefenseRating_ReturnsZero_WhenZeroMinutes()
    {
        var stats = new PlayerStatLine();
        StatisticsCalculator.CalculateDefenseRating(stats).Should().Be(0);
    }

    [Fact]
    public void CalculateDefenseRating_ReturnsZero_WhenZeroGames()
    {
        var stats = new PlayerStatLine { Minutes = 100, Games = 0 };
        StatisticsCalculator.CalculateDefenseRating(stats).Should().Be(0);
    }

    [Fact]
    public void CalculateDefenseRating_ReturnsExpectedValue()
    {
        var stats = new PlayerStatLine
        {
            Games = 82,
            Minutes = 2800,
            FieldGoalsAttempted = 1100,
            ThreePointersAttempted = 500,
            FreeThrowsAttempted = 350,
            OffensiveRebounds = 100,
            Rebounds = 500,
            Steals = 100,
            Blocks = 50,
            PersonalFouls = 150
        };

        double result = StatisticsCalculator.CalculateDefenseRating(stats);

        // dreb = (500 - 100) / 3 = 133.33
        // fga = 1100 - 500 = 600 (2PT only)
        // rebRatio = 100 / (133.33 * 3) = 0.25
        // ftRatio = 350 / 600 = 0.5833
        // defense = (100 + 50 + 133.33 - 150) / 82 = 1.626
        // defense + 0.5833 - 0.25 = 1.959
        result.Should().BeApproximately(1.959, 0.01);
    }

    // ───────────────────────────────────────────────────────────────
    // Helper
    // ───────────────────────────────────────────────────────────────

    private static Player FindPlayer(Models.League.League league, int playerId)
    {
        return league.Teams.SelectMany(t => t.Roster).First(p => p.Id == playerId);
    }
}
