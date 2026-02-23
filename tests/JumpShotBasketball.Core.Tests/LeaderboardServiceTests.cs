using FluentAssertions;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class LeaderboardServiceTests
{
    // ───────────────────────────────────────────────────────────────
    // Test helpers
    // ───────────────────────────────────────────────────────────────

    private static Models.League.League CreateTestLeague()
    {
        var league = new Models.League.League();
        league.Schedule.GamesInSeason = 82;

        for (int t = 0; t < 4; t++)
        {
            var team = new Models.Team.Team { Id = t, Name = $"Team {t}" };
            for (int p = 0; p < 15; p++)
            {
                double factor = 1.0 - (p * 0.05);
                if (factor < 0.2) factor = 0.2;

                team.Roster.Add(new Player
                {
                    Id = t * 100 + p,
                    Name = $"Player {t * 100 + p}",
                    Position = new[] { "PG", "SG", "SF", "PF", "C" }[p % 5],
                    TeamIndex = t,
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
                    }
                });
            }
            league.Teams.Add(team);
        }

        return league;
    }

    // ───────────────────────────────────────────────────────────────
    // ComputeLeaderboard tests
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeLeaderboard_PopulatesAllCategories()
    {
        var league = CreateTestLeague();
        var board = LeaderboardService.ComputeLeaderboard(league);

        board.PointsLeaders.Should().NotBeEmpty();
        board.ReboundsLeaders.Should().NotBeEmpty();
        board.AssistsLeaders.Should().NotBeEmpty();
        board.StealsLeaders.Should().NotBeEmpty();
        board.BlocksLeaders.Should().NotBeEmpty();
        board.FieldGoalPctLeaders.Should().NotBeEmpty();
        board.FreeThrowPctLeaders.Should().NotBeEmpty();
        board.ThreePointPctLeaders.Should().NotBeEmpty();
    }

    [Fact]
    public void RankByPointsPerGame_ReturnsOrderedDescending()
    {
        var league = CreateTestLeague();
        var leaders = LeaderboardService.RankByPointsPerGame(league);

        for (int i = 1; i < leaders.Count; i++)
            leaders[i - 1].PerGameAverage.Should().BeGreaterThanOrEqualTo(leaders[i].PerGameAverage);
    }

    [Fact]
    public void RankByPointsPerGame_RespectsCountParameter()
    {
        var league = CreateTestLeague();
        var leaders = LeaderboardService.RankByPointsPerGame(league, count: 5);

        leaders.Count.Should().BeLessThanOrEqualTo(5);
    }

    [Fact]
    public void RankByReboundsPerGame_ReturnsOrderedDescending()
    {
        var league = CreateTestLeague();
        var leaders = LeaderboardService.RankByReboundsPerGame(league);

        for (int i = 1; i < leaders.Count; i++)
            leaders[i - 1].PerGameAverage.Should().BeGreaterThanOrEqualTo(leaders[i].PerGameAverage);
    }

    [Fact]
    public void RankByAssistsPerGame_ReturnsOrderedDescending()
    {
        var league = CreateTestLeague();
        var leaders = LeaderboardService.RankByAssistsPerGame(league);

        leaders.Should().NotBeEmpty();
        for (int i = 1; i < leaders.Count; i++)
            leaders[i - 1].PerGameAverage.Should().BeGreaterThanOrEqualTo(leaders[i].PerGameAverage);
    }

    [Fact]
    public void RankByFieldGoalPct_ReturnsOrderedDescending()
    {
        var league = CreateTestLeague();
        var leaders = LeaderboardService.RankByFieldGoalPct(league);

        leaders.Should().NotBeEmpty();
        for (int i = 1; i < leaders.Count; i++)
            leaders[i - 1].PerGameAverage.Should().BeGreaterThanOrEqualTo(leaders[i].PerGameAverage);
    }

    [Fact]
    public void RankByFreeThrowPct_ReturnsOrderedDescending()
    {
        var league = CreateTestLeague();
        var leaders = LeaderboardService.RankByFreeThrowPct(league);

        leaders.Should().NotBeEmpty();
        for (int i = 1; i < leaders.Count; i++)
            leaders[i - 1].PerGameAverage.Should().BeGreaterThanOrEqualTo(leaders[i].PerGameAverage);
    }

    [Fact]
    public void RankByThreePointPct_ReturnsOrderedDescending()
    {
        var league = CreateTestLeague();
        var leaders = LeaderboardService.RankByThreePointPct(league);

        leaders.Should().NotBeEmpty();
        for (int i = 1; i < leaders.Count; i++)
            leaders[i - 1].PerGameAverage.Should().BeGreaterThanOrEqualTo(leaders[i].PerGameAverage);
    }

    // ───────────────────────────────────────────────────────────────
    // Edge cases
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void RankByPointsPerGame_ReturnsEmpty_WhenNoEligiblePlayers()
    {
        var league = new Models.League.League();
        league.Schedule.GamesInSeason = 82;
        league.Teams.Add(new Models.Team.Team { Id = 0, Name = "Empty" });

        var leaders = LeaderboardService.RankByPointsPerGame(league);
        leaders.Should().BeEmpty();
    }

    [Fact]
    public void ComputeLeaderboard_PopulatesPlayerInfo()
    {
        var league = CreateTestLeague();
        var board = LeaderboardService.ComputeLeaderboard(league);

        var leader = board.PointsLeaders[0];
        leader.PlayerName.Should().NotBeEmpty();
        leader.GamesPlayed.Should().BeGreaterThan(0);
        leader.PerGameAverage.Should().BeGreaterThan(0);
    }

    [Fact]
    public void RankByStealsPerGame_ReturnsOrderedDescending()
    {
        var league = CreateTestLeague();
        var leaders = LeaderboardService.RankByStealsPerGame(league);

        leaders.Should().NotBeEmpty();
        for (int i = 1; i < leaders.Count; i++)
            leaders[i - 1].PerGameAverage.Should().BeGreaterThanOrEqualTo(leaders[i].PerGameAverage);
    }

    [Fact]
    public void RankByBlocksPerGame_ReturnsOrderedDescending()
    {
        var league = CreateTestLeague();
        var leaders = LeaderboardService.RankByBlocksPerGame(league);

        leaders.Should().NotBeEmpty();
        for (int i = 1; i < leaders.Count; i++)
            leaders[i - 1].PerGameAverage.Should().BeGreaterThanOrEqualTo(leaders[i].PerGameAverage);
    }
}
