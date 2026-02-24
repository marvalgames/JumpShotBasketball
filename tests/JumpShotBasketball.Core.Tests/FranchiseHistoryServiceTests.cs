using FluentAssertions;
using JumpShotBasketball.Core.Models.History;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Staff;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class FranchiseHistoryServiceTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static League CreateTestLeague(int teamCount = 2)
    {
        var league = new League
        {
            Settings = new LeagueSettings
            {
                CurrentYear = 2024,
                NumberOfTeams = teamCount
            }
        };

        for (int i = 0; i < teamCount; i++)
        {
            var team = new Team
            {
                Id = i,
                Name = $"Team{i}",
                Record = new TeamRecord
                {
                    TeamName = $"Team{i}",
                    Wins = 40 + i * 5,
                    Losses = 42 - i * 5,
                    IsPlayoffTeam = i == 0,
                    HasRing = i == 0
                },
                Coach = new StaffMember { Name = $"Coach{i}" }
            };

            // Add a player for MVP tracking
            team.Roster.Add(new Player
            {
                Name = $"Star{i}",
                Id = i * 10,
                Ratings = new PlayerRatings { TradeTrueRating = 8.0 + i }
            });

            league.Teams.Add(team);
        }

        return league;
    }

    // ── ArchiveSeasonRecords Tests ──────────────────────────────────────────

    [Fact]
    public void ArchiveSeasonRecords_CreatesHistoryForEachTeam()
    {
        var league = CreateTestLeague(3);

        FranchiseHistoryService.ArchiveSeasonRecords(league, 2024);

        league.FranchiseHistories.Should().HaveCount(3);
        league.FranchiseHistories[0].Seasons.Should().HaveCount(1);
        league.FranchiseHistories[1].Seasons.Should().HaveCount(1);
        league.FranchiseHistories[2].Seasons.Should().HaveCount(1);
    }

    [Fact]
    public void ArchiveSeasonRecords_CapturesWinsAndLosses()
    {
        var league = CreateTestLeague();

        FranchiseHistoryService.ArchiveSeasonRecords(league, 2024);

        var season = league.FranchiseHistories[0].Seasons[0];
        season.Wins.Should().Be(40);
        season.Losses.Should().Be(42);
        season.Year.Should().Be(2024);
    }

    [Fact]
    public void ArchiveSeasonRecords_CapturesPlayoffAndChampionship()
    {
        var league = CreateTestLeague();

        FranchiseHistoryService.ArchiveSeasonRecords(league, 2024);

        league.FranchiseHistories[0].Seasons[0].MadePlayoffs.Should().BeTrue();
        league.FranchiseHistories[0].Seasons[0].WonChampionship.Should().BeTrue();
        league.FranchiseHistories[1].Seasons[0].MadePlayoffs.Should().BeFalse();
        league.FranchiseHistories[1].Seasons[0].WonChampionship.Should().BeFalse();
    }

    [Fact]
    public void ArchiveSeasonRecords_CapturesCoachAndMvp()
    {
        var league = CreateTestLeague();

        FranchiseHistoryService.ArchiveSeasonRecords(league, 2024);

        var season = league.FranchiseHistories[0].Seasons[0];
        season.CoachName.Should().Be("Coach0");
        season.MvpName.Should().Be("Star0");
        season.MvpValue.Should().Be(8.0);
    }

    [Fact]
    public void ArchiveSeasonRecords_AccumulatesMultipleSeasons()
    {
        var league = CreateTestLeague();

        FranchiseHistoryService.ArchiveSeasonRecords(league, 2024);
        FranchiseHistoryService.ArchiveSeasonRecords(league, 2025);

        league.FranchiseHistories[0].Seasons.Should().HaveCount(2);
        league.FranchiseHistories[0].Seasons[0].Year.Should().Be(2024);
        league.FranchiseHistories[0].Seasons[1].Year.Should().Be(2025);
    }

    // ── GetChampionshipCount Tests ──────────────────────────────────────────

    [Fact]
    public void GetChampionshipCount_ReturnsCorrectCount()
    {
        var history = new FranchiseHistory
        {
            Seasons = new List<FranchiseSeasonRecord>
            {
                new() { Year = 2020, WonChampionship = true },
                new() { Year = 2021, WonChampionship = false },
                new() { Year = 2022, WonChampionship = true },
            }
        };

        FranchiseHistoryService.GetChampionshipCount(history).Should().Be(2);
    }

    // ── GetBestSeason Tests ─────────────────────────────────────────────────

    [Fact]
    public void GetBestSeason_ReturnsBestByWins()
    {
        var history = new FranchiseHistory
        {
            Seasons = new List<FranchiseSeasonRecord>
            {
                new() { Year = 2020, Wins = 45, Losses = 37 },
                new() { Year = 2021, Wins = 60, Losses = 22 },
                new() { Year = 2022, Wins = 50, Losses = 32 },
            }
        };

        var best = FranchiseHistoryService.GetBestSeason(history);
        best.Should().NotBeNull();
        best!.Year.Should().Be(2021);
        best.Wins.Should().Be(60);
    }

    [Fact]
    public void GetBestSeason_ReturnsNullForEmptyHistory()
    {
        var history = new FranchiseHistory();
        FranchiseHistoryService.GetBestSeason(history).Should().BeNull();
    }
}
