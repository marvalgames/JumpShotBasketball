using FluentAssertions;
using JumpShotBasketball.Core.Enums;
using JumpShotBasketball.Core.Models.Game;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class LeagueSimulationServiceTests
{
    // ── Helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a small league with proper two-conference structure and valid playoff settings.
    /// Reassigns half the teams to the Western Conference so AllStar/Playoffs work correctly.
    /// </summary>
    private static League CreateSmallLeague(int teams = 4, int seed = 42)
    {
        // With 4 teams, each game involves 2 of 4 teams, so each team plays ~50% of games.
        // Use a lower GamesPerSeason (40) so each team actually plays ~40 games,
        // meeting the awards 62.5% games fraction threshold (40/40 = 100%).
        var options = new LeagueCreationOptions { NumberOfTeams = teams, GamesPerSeason = 40 };
        var league = LeagueCreationService.CreateNewLeague(options, new Random(seed));

        // Fix conference assignments: split teams evenly across 2 conferences
        // (LeagueCreationService TeamData[0..3] are all Eastern Conference)
        int half = league.Teams.Count / 2;
        for (int i = half; i < league.Teams.Count; i++)
        {
            league.Teams[i].Record.Conference = "Western";
            league.Teams[i].Record.Division = "Southwest";
        }

        // Set valid playoff format for small leagues
        league.Settings.PlayoffFormat = "1 team per conference";
        league.Settings.Round1Format = "4 of 7";
        league.Settings.Round2Format = "None";
        league.Settings.Round3Format = "None";
        league.Settings.Round4Format = "None";

        return league;
    }

    // ── Validation Tests ────────────────────────────────────────────────

    [Fact]
    public void ValidateLeagueForSimulation_LessThan2Teams_Throws()
    {
        var league = new League();
        league.Teams.Add(new Models.Team.Team());
        league.Schedule.Games.Add(new ScheduledGame());

        var act = () => LeagueSimulationService.SimulateFullSeason(league);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*at least 2 teams*");
    }

    [Fact]
    public void ValidateLeagueForSimulation_NoSchedule_Throws()
    {
        var league = new League();
        league.Teams.Add(new Models.Team.Team());
        league.Teams.Add(new Models.Team.Team());
        // No games in schedule

        var act = () => LeagueSimulationService.SimulateFullSeason(league);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*no scheduled games*");
    }

    [Fact]
    public void ValidateLeagueForSimulation_SeasonAlreadyEnded_Throws()
    {
        var league = CreateSmallLeague();
        league.Schedule.RegularSeasonEnded = true;

        var act = () => LeagueSimulationService.SimulateFullSeason(league);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already ended*");
    }

    [Fact]
    public void SimulateMultipleSeasons_ZeroSeasons_Throws()
    {
        var league = CreateSmallLeague();

        var act = () => LeagueSimulationService.SimulateMultipleSeasons(league, 0);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*positive*");
    }

    // ── SimulateFirstHalf Tests ─────────────────────────────────────────

    [Fact]
    public void SimulateFirstHalf_StopsAtAllStarBreak()
    {
        var league = CreateSmallLeague();
        var random = new Random(42);

        var result = LeagueSimulationService.SimulateFirstHalf(league, random);

        // After first half, All-Star break should be reached
        LeagueSimulationService.IsAllStarBreakReached(league).Should().BeTrue();
        result.GamesSimulated.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SimulateFirstHalf_PlaysApproximatelyHalfTheGames()
    {
        var league = CreateSmallLeague();
        var random = new Random(42);
        int totalScheduledGames = league.Schedule.Games.Count(g => g.Type == GameType.League);

        var result = LeagueSimulationService.SimulateFirstHalf(league, random);

        result.GamesSimulated.Should().BeGreaterThan(0);
        result.GamesSimulated.Should().BeLessThan(totalScheduledGames);
    }

    [Fact]
    public void SimulateFirstHalf_SeasonNotComplete()
    {
        var league = CreateSmallLeague();
        var random = new Random(42);

        var result = LeagueSimulationService.SimulateFirstHalf(league, random);

        result.SeasonComplete.Should().BeFalse();
        league.Schedule.RegularSeasonEnded.Should().BeFalse();
    }

    [Fact]
    public void SimulateFirstHalf_GamesAreMarkedPlayed()
    {
        var league = CreateSmallLeague();
        var random = new Random(42);

        var result = LeagueSimulationService.SimulateFirstHalf(league, random);

        int playedGames = league.Schedule.Games.Count(g => g.Played && g.Type == GameType.League);
        playedGames.Should().Be(result.GamesSimulated);
    }

    // ── IsAllStarBreakReached Tests ─────────────────────────────────────

    [Fact]
    public void IsAllStarBreakReached_FreshLeague_ReturnsFalse()
    {
        var league = CreateSmallLeague();
        LeagueSimulationService.IsAllStarBreakReached(league).Should().BeFalse();
    }

    [Fact]
    public void IsAllStarBreakReached_BelowThreshold_ReturnsFalse()
    {
        var league = CreateSmallLeague();
        SeasonSimulationService.SimulateDay(league, new Random(42));
        LeagueSimulationService.IsAllStarBreakReached(league).Should().BeFalse();
    }

    [Fact]
    public void IsAllStarBreakReached_AtThreshold_ReturnsTrue()
    {
        var league = CreateSmallLeague();
        var random = new Random(42);

        while (!LeagueSimulationService.IsAllStarBreakReached(league))
        {
            bool hasGames = league.Schedule.Games.Any(g => !g.Played && g.Type == GameType.League);
            if (!hasGames) break;
            SeasonSimulationService.SimulateDay(league, random);
        }

        LeagueSimulationService.IsAllStarBreakReached(league).Should().BeTrue();
    }

    [Fact]
    public void IsAllStarBreakReached_AllGamesPlayed_ReturnsTrue()
    {
        var league = CreateSmallLeague();
        SeasonSimulationService.SimulateSeason(league, new Random(42));

        LeagueSimulationService.IsAllStarBreakReached(league).Should().BeTrue();
    }

    // ── All-Star Integration Tests ──────────────────────────────────────

    [Fact]
    public void SimulateFullSeason_AllStarWeekendIsPlayed()
    {
        var league = CreateSmallLeague();

        var result = LeagueSimulationService.SimulateFullSeason(league, new Random(42));

        result.AllStarWeekendPlayed.Should().BeTrue();
        result.AllStarWeekendResult.Should().NotBeNull();
    }

    [Fact]
    public void SimulateFullSeason_AllStarWeekendStoredOnLeague()
    {
        var league = CreateSmallLeague();

        LeagueSimulationService.SimulateFullSeason(league, new Random(42));

        league.AllStarWeekend.Should().NotBeNull();
    }

    [Fact]
    public void SimulateFullSeason_AllStarWeekendSkippedIfAlreadyPresent()
    {
        var league = CreateSmallLeague();
        var random = new Random(42);

        // Pre-populate All-Star weekend result
        var averages = LeagueAveragesCalculator.Calculate(league);
        var existingAsw = AllStarWeekendService.RunAllStarWeekend(league, random, averages);
        league.AllStarWeekend = existingAsw;

        var result = LeagueSimulationService.SimulateFullSeason(league, new Random(42));

        result.AllStarWeekendPlayed.Should().BeFalse();
        result.AllStarWeekendResult.Should().BeNull();
        league.AllStarWeekend.Should().BeSameAs(existingAsw);
    }

    [Fact]
    public void SimulateFullSeason_AllStarWeekendResultPopulated()
    {
        var league = CreateSmallLeague();

        var result = LeagueSimulationService.SimulateFullSeason(league, new Random(42));

        // Orchestrator called AllStarWeekendService and stored the result
        result.AllStarWeekendResult.Should().NotBeNull();
        result.AllStarWeekendResult!.Conference1Roster.Should().NotBeNull();
        result.AllStarWeekendResult!.Conference2Roster.Should().NotBeNull();
    }

    // ── Second Half + Completion Tests ──────────────────────────────────

    [Fact]
    public void SimulateFullSeason_AllRegularSeasonGamesPlayed()
    {
        var league = CreateSmallLeague();

        var result = LeagueSimulationService.SimulateFullSeason(league, new Random(42));

        int totalScheduledLeagueGames = league.Schedule.Games.Count(g => g.Type == GameType.League);
        int playedLeagueGames = league.Schedule.Games.Count(g => g.Played && g.Type == GameType.League);
        playedLeagueGames.Should().Be(totalScheduledLeagueGames);
    }

    [Fact]
    public void SimulateFullSeason_RegularSeasonEnded()
    {
        var league = CreateSmallLeague();

        LeagueSimulationService.SimulateFullSeason(league, new Random(42));

        league.Schedule.RegularSeasonEnded.Should().BeTrue();
    }

    [Fact]
    public void SimulateFullSeason_TeamsHaveRecords()
    {
        var league = CreateSmallLeague();

        LeagueSimulationService.SimulateFullSeason(league, new Random(42));

        foreach (var team in league.Teams)
        {
            // Every team should have played games (packed: wins*100 + losses > 0)
            team.Record.LeagueRecord.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public void SimulateFullSeason_TotalGamesMatchSum()
    {
        var league = CreateSmallLeague();

        var result = LeagueSimulationService.SimulateFullSeason(league, new Random(42));

        result.TotalRegularSeasonGames.Should().Be(
            result.FirstHalfResult.GamesSimulated + result.SecondHalfResult.GamesSimulated);
        result.TotalRegularSeasonDays.Should().Be(
            result.FirstHalfResult.DaysSimulated + result.SecondHalfResult.DaysSimulated);
    }

    // ── Playoff Integration Tests ───────────────────────────────────────

    [Fact]
    public void SimulateFullSeason_PlayoffsSimulated()
    {
        var league = CreateSmallLeague();

        var result = LeagueSimulationService.SimulateFullSeason(league, new Random(42));

        result.PlayoffResult.Should().NotBeNull();
        result.PlayoffResult!.PlayoffsComplete.Should().BeTrue();
        result.PlayoffResult.GamesSimulated.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SimulateFullSeason_ChampionDetermined()
    {
        var league = CreateSmallLeague();

        var result = LeagueSimulationService.SimulateFullSeason(league, new Random(42));

        result.ChampionTeamIndex.Should().NotBeNull();
        result.ChampionTeamIndex!.Value.Should().BeInRange(0, league.Teams.Count - 1);
    }

    [Fact]
    public void SimulateFullSeason_PlayoffResultMatchesChampion()
    {
        var league = CreateSmallLeague();

        var result = LeagueSimulationService.SimulateFullSeason(league, new Random(42));

        result.ChampionTeamIndex.Should().Be(result.PlayoffResult!.ChampionTeamIndex);
    }

    [Fact]
    public void SimulateFullSeason_BracketStoredOnLeague()
    {
        var league = CreateSmallLeague();

        LeagueSimulationService.SimulateFullSeason(league, new Random(42));

        league.Bracket.Should().NotBeNull();
        league.Schedule.PlayoffsStarted.Should().BeTrue();
    }

    [Fact]
    public void SimulateFullSeason_PlayoffGamesExist()
    {
        var league = CreateSmallLeague();

        var result = LeagueSimulationService.SimulateFullSeason(league, new Random(42));

        result.PlayoffResult!.GameResults.Should().NotBeEmpty();
    }

    // ── Awards Integration Tests ────────────────────────────────────────

    [Fact]
    public void SimulateFullSeason_AwardsComputed()
    {
        var league = CreateSmallLeague();

        var result = LeagueSimulationService.SimulateFullSeason(league, new Random(42));

        result.Awards.Should().NotBeNull();
    }

    [Fact]
    public void SimulateFullSeason_MvpComputed()
    {
        var league = CreateSmallLeague();

        var result = LeagueSimulationService.SimulateFullSeason(league, new Random(42));

        // Awards service was called and produced a valid MVP award
        result.Awards!.Mvp.Should().NotBeNull();
        result.Awards!.Mvp.AwardName.Should().Be("MVP");
        // With 4 teams × 15 players, starters get ~30+ MPG, meeting 24 MPG threshold
        result.Awards!.Mvp.Recipients.Should().NotBeEmpty();
    }

    [Fact]
    public void SimulateFullSeason_AwardsStoredOnLeague()
    {
        var league = CreateSmallLeague();

        LeagueSimulationService.SimulateFullSeason(league, new Random(42));

        league.Awards.Should().NotBeNull();
    }

    [Fact]
    public void SimulateFullSeason_AwardsYearMatchesLeague()
    {
        var league = CreateSmallLeague();
        int expectedYear = league.Settings.CurrentYear;

        var result = LeagueSimulationService.SimulateFullSeason(league, new Random(42));

        result.Awards!.Year.Should().Be(expectedYear);
        result.Year.Should().Be(expectedYear);
    }

    // ── SimulateFullCycle Tests ──────────────────────────────────────────

    [Fact]
    public void SimulateFullCycle_CompletesSuccessfully()
    {
        var league = CreateSmallLeague();

        var result = LeagueSimulationService.SimulateFullCycle(league, new Random(42));

        result.SeasonResult.Should().NotBeNull();
        result.OffSeasonResult.Should().NotBeNull();
    }

    [Fact]
    public void SimulateFullCycle_YearIncremented()
    {
        var league = CreateSmallLeague();
        int startYear = league.Settings.CurrentYear;

        LeagueSimulationService.SimulateFullCycle(league, new Random(42));

        league.Settings.CurrentYear.Should().Be(startYear + 1);
    }

    [Fact]
    public void SimulateFullCycle_ScheduleRegenerated()
    {
        var league = CreateSmallLeague();

        LeagueSimulationService.SimulateFullCycle(league, new Random(42));

        league.Schedule.Games.Should().NotBeEmpty();
        league.Schedule.Games.Where(g => g.Type == GameType.League)
            .Should().AllSatisfy(g => g.Played.Should().BeFalse());
    }

    [Fact]
    public void SimulateFullCycle_SeasonStateReset()
    {
        var league = CreateSmallLeague();

        LeagueSimulationService.SimulateFullCycle(league, new Random(42));

        league.Schedule.RegularSeasonEnded.Should().BeFalse();
        league.Schedule.PlayoffsStarted.Should().BeFalse();
    }

    [Fact]
    public void SimulateFullCycle_ReadyForNextSeason()
    {
        var league = CreateSmallLeague();

        LeagueSimulationService.SimulateFullCycle(league, new Random(42));

        // Should be able to simulate another full season without throwing
        var act = () => LeagueSimulationService.SimulateFullSeason(league, new Random(99));
        act.Should().NotThrow();
    }

    // ── SimulateMultipleSeasons Tests ───────────────────────────────────

    [Fact]
    public void SimulateMultipleSeasons_CorrectCount()
    {
        var league = CreateSmallLeague();

        var result = LeagueSimulationService.SimulateMultipleSeasons(league, 2, new Random(42));

        result.TotalSeasonsSimulated.Should().Be(2);
        result.Seasons.Should().HaveCount(2);
    }

    [Fact]
    public void SimulateMultipleSeasons_YearsAdvance()
    {
        var league = CreateSmallLeague();
        int startYear = league.Settings.CurrentYear;

        var result = LeagueSimulationService.SimulateMultipleSeasons(league, 3, new Random(42));

        league.Settings.CurrentYear.Should().Be(startYear + 3);
        result.Seasons[0].SeasonResult.Year.Should().Be(startYear);
        result.Seasons[1].SeasonResult.Year.Should().Be(startYear + 1);
        result.Seasons[2].SeasonResult.Year.Should().Be(startYear + 2);
    }

    [Fact]
    public void SimulateMultipleSeasons_AwardsHistoryGrows()
    {
        var league = CreateSmallLeague();

        var result = LeagueSimulationService.SimulateMultipleSeasons(league, 2, new Random(42));

        league.AwardsHistory.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void SimulateMultipleSeasons_TotalGamesAccumulated()
    {
        var league = CreateSmallLeague();

        var result = LeagueSimulationService.SimulateMultipleSeasons(league, 2, new Random(42));

        result.TotalGamesSimulated.Should().BeGreaterThan(0);

        int manualTotal = result.Seasons.Sum(s =>
            s.SeasonResult.TotalRegularSeasonGames +
            (s.SeasonResult.PlayoffResult?.GamesSimulated ?? 0));
        result.TotalGamesSimulated.Should().Be(manualTotal);
    }

    [Fact]
    public void SimulateMultipleSeasons_EachSeasonHasChampion()
    {
        var league = CreateSmallLeague();

        var result = LeagueSimulationService.SimulateMultipleSeasons(league, 2, new Random(42));

        foreach (var season in result.Seasons)
        {
            season.SeasonResult.ChampionTeamIndex.Should().NotBeNull();
        }
    }

    // ── Edge Case Tests ─────────────────────────────────────────────────

    [Fact]
    public void SimulateFullSeason_DeterministicWithSeed()
    {
        var league1 = CreateSmallLeague(seed: 100);
        var league2 = CreateSmallLeague(seed: 100);

        var result1 = LeagueSimulationService.SimulateFullSeason(league1, new Random(100));
        var result2 = LeagueSimulationService.SimulateFullSeason(league2, new Random(100));

        result1.TotalRegularSeasonGames.Should().Be(result2.TotalRegularSeasonGames);
        result1.ChampionTeamIndex.Should().Be(result2.ChampionTeamIndex);
    }

    [Fact]
    public void SimulateFullSeason_SmallLeagueWorks()
    {
        var league = CreateSmallLeague(teams: 4);

        var result = LeagueSimulationService.SimulateFullSeason(league, new Random(42));

        result.TotalRegularSeasonGames.Should().BeGreaterThan(0);
        result.PlayoffResult.Should().NotBeNull();
        result.PlayoffResult!.PlayoffsComplete.Should().BeTrue();
    }

    [Fact]
    public void SimulateMultipleSeasons_TwoSeasonsStable()
    {
        var league = CreateSmallLeague();

        var act = () => LeagueSimulationService.SimulateMultipleSeasons(league, 2, new Random(42));
        act.Should().NotThrow();
    }

    // ── Phase 21 Regression Tests ─────────────────────────────────────

    [Fact]
    public void SimulateFullCycle_SecondSeasonPlayersEligible()
    {
        var league = CreateSmallLeague();

        // Complete one full cycle (season + off-season)
        LeagueSimulationService.SimulateFullCycle(league, new Random(42));

        // After off-season, players should still have engine-eligible stats
        foreach (var team in league.Teams)
        {
            foreach (var player in team.Roster.Where(p => !string.IsNullOrEmpty(p.Name)))
            {
                player.SeasonStats.Minutes.Should().BeGreaterThan(0,
                    $"player {player.Name} should have SeasonStats for engine eligibility");
                player.Ratings.FieldGoalsAttemptedPer48Min.Should().BeGreaterThan(0,
                    $"player {player.Name} should have per-48 rates for the engine");
            }
        }
    }
}
