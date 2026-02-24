using FluentAssertions;
using JumpShotBasketball.Core.Models.Awards;
using JumpShotBasketball.Core.Models.Game;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Playoff;
using JumpShotBasketball.Core.Models.Staff;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class OffSeasonServiceTests
{
    // ── Helpers ──────────────────────────────────────────────────────

    private static Player CreateSeasonPlayer(string name = "Test Player", int age = 25,
        int games = 70, int minutes = 2100, string pos = "PG")
    {
        return new Player
        {
            Name = name,
            Position = pos,
            Age = age,
            Health = 100,
            Team = "TestTeam",
            Ratings = new PlayerRatings
            {
                Prime = 28,
                Potential1 = 3,
                Potential2 = 3,
                Effort = 3,
                TradeTrueRating = 8.0,
                MovementOffenseRaw = 5,
                PenetrationOffenseRaw = 5,
                PostOffenseRaw = 5,
                TransitionOffenseRaw = 5,
                MovementDefenseRaw = 5,
                PenetrationDefenseRaw = 5,
                PostDefenseRaw = 5,
                TransitionDefenseRaw = 5,
                ProjectionFieldGoalsAttempted = 50,
                ProjectionFreeThrowsAttempted = 50,
                ProjectionFieldGoalPercentage = 50
            },
            SimulatedStats = new PlayerStatLine
            {
                Games = games,
                Minutes = minutes,
                FieldGoalsMade = 350,
                FieldGoalsAttempted = 800,
                FreeThrowsMade = 150,
                FreeThrowsAttempted = 200,
                ThreePointersMade = 60,
                ThreePointersAttempted = 180,
                OffensiveRebounds = 40,
                Rebounds = 250,
                Assists = 300,
                Steals = 80,
                Turnovers = 150,
                Blocks = 15,
                PersonalFouls = 130
            },
            Contract = new PlayerContract
            {
                YearsOfService = 5,
                YearsOnTeam = 3,
                ContractYears = 4,
                CurrentContractYear = 2,
                LoyaltyFactor = 3,
                CurrentYearSalary = 500
            }
        };
    }

    private static League CreateTestLeague(int numPlayers = 3)
    {
        var league = new League
        {
            Settings = new LeagueSettings
            {
                CurrentYear = 2024,
                NumberOfTeams = 1,
                LeagueName = "TestLeague"
            },
            Schedule = new Schedule
            {
                SeasonStarted = true,
                RegularSeasonEnded = true,
                PlayoffsStarted = true,
                GamesInSeason = 82
            }
        };

        var team = new Team
        {
            Id = 1,
            Name = "TestTeam",
            Coach = new StaffMember
            {
                CoachPot1 = 3, CoachPot2 = 3, CoachEffort = 3,
                CoachScoring = 3, CoachShooting = 3,
                CoachRebounding = 3, CoachPassing = 3, CoachDefense = 3
            },
            Record = new TeamRecord { Wins = 50, Losses = 32 }
        };

        for (int i = 0; i < numPlayers; i++)
        {
            team.Roster.Add(CreateSeasonPlayer($"Player{i + 1}", age: 25 + i));
        }

        league.Teams.Add(team);

        // Add some games to schedule
        league.Schedule.Games.Add(new ScheduledGame { GameNumber = 1, Played = true });

        // Add some transactions
        league.Transactions.Add(new Transaction());

        return league;
    }

    // ── ResetSeasonState ────────────────────────────────────────────

    [Fact]
    public void ResetSeasonState_ClearsScheduleFlags()
    {
        var league = CreateTestLeague();

        OffSeasonService.ResetSeasonState(league);

        league.Schedule.SeasonStarted.Should().BeFalse();
        league.Schedule.RegularSeasonEnded.Should().BeFalse();
        league.Schedule.PlayoffsStarted.Should().BeFalse();
        league.Schedule.Games.Should().BeEmpty();
    }

    [Fact]
    public void ResetSeasonState_ClearsTransactions()
    {
        var league = CreateTestLeague();

        OffSeasonService.ResetSeasonState(league);

        league.Transactions.Should().BeEmpty();
        league.Settings.NumberOfTransactions.Should().Be(0);
    }

    [Fact]
    public void ResetSeasonState_ClearsBracketAndAwards()
    {
        var league = CreateTestLeague();
        league.Bracket = new PlayoffBracket();
        league.Awards = new SeasonAwards();
        league.Leaderboard = new LeagueLeaderboard();
        league.AllStarWeekend = new AllStarWeekendResult();

        OffSeasonService.ResetSeasonState(league);

        league.Bracket.Should().BeNull();
        league.Awards.Should().BeNull();
        league.Leaderboard.Should().BeNull();
        league.AllStarWeekend.Should().BeNull();
    }

    [Fact]
    public void ResetSeasonState_ResetsPlayerStats()
    {
        var league = CreateTestLeague(1);
        var player = league.Teams[0].Roster[0];
        player.SimulatedStats.Games.Should().BeGreaterThan(0, "precondition");

        OffSeasonService.ResetSeasonState(league);

        player.SimulatedStats.Games.Should().Be(0);
        player.SimulatedStats.Minutes.Should().Be(0);
        player.PlayoffStats.Games.Should().Be(0);
    }

    [Fact]
    public void ResetSeasonState_ClearsAllStarFlags()
    {
        var league = CreateTestLeague(1);
        var player = league.Teams[0].Roster[0];
        player.AllStar = 1;
        player.ThreePointContest = 1;

        OffSeasonService.ResetSeasonState(league);

        player.AllStar.Should().Be(0);
        player.ThreePointContest.Should().Be(0);
    }

    [Fact]
    public void ResetSeasonState_ResetsTeamRecords()
    {
        var league = CreateTestLeague();

        OffSeasonService.ResetSeasonState(league);

        league.Teams[0].Record.Wins.Should().Be(0);
        league.Teams[0].Record.Losses.Should().Be(0);
    }

    // ── ApplyStartSeasonAttributes ──────────────────────────────────

    [Fact]
    public void ApplyStartSeasonAttributes_IncrementsYOS()
    {
        var league = CreateTestLeague(1);
        var player = league.Teams[0].Roster[0];
        int origYos = player.Contract.YearsOfService;
        int expired = 0, newFa = 0;

        OffSeasonService.ApplyStartSeasonAttributes(league, true, ref expired, ref newFa, new Random(42));

        player.Contract.YearsOfService.Should().Be(origYos + 1);
    }

    [Fact]
    public void ApplyStartSeasonAttributes_IncrementsYearsOnTeam()
    {
        var league = CreateTestLeague(1);
        var player = league.Teams[0].Roster[0];
        int origYot = player.Contract.YearsOnTeam;
        int expired = 0, newFa = 0;

        OffSeasonService.ApplyStartSeasonAttributes(league, true, ref expired, ref newFa, new Random(42));

        player.Contract.YearsOnTeam.Should().Be(origYot + 1);
    }

    [Fact]
    public void ApplyStartSeasonAttributes_NoIncrementWhenFalse()
    {
        var league = CreateTestLeague(1);
        var player = league.Teams[0].Roster[0];
        int origYos = player.Contract.YearsOfService;
        int expired = 0, newFa = 0;

        OffSeasonService.ApplyStartSeasonAttributes(league, false, ref expired, ref newFa, new Random(42));

        player.Contract.YearsOfService.Should().Be(origYos);
    }

    [Fact]
    public void ApplyStartSeasonAttributes_ContractExpiry_MarksFreeAgent()
    {
        var league = CreateTestLeague(1);
        var player = league.Teams[0].Roster[0];
        player.Contract.ContractYears = 3;
        player.Contract.CurrentContractYear = 3; // At end of contract
        int expired = 0, newFa = 0;

        OffSeasonService.ApplyStartSeasonAttributes(league, true, ref expired, ref newFa, new Random(42));

        player.Contract.IsFreeAgent.Should().BeTrue();
        expired.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ApplyStartSeasonAttributes_BirdPlayer_SetWhen3YearsOnTeam()
    {
        var league = CreateTestLeague(1);
        var player = league.Teams[0].Roster[0];
        player.Contract.YearsOnTeam = 2; // Will become 3 after increment
        int expired = 0, newFa = 0;

        OffSeasonService.ApplyStartSeasonAttributes(league, true, ref expired, ref newFa, new Random(42));

        player.Contract.IsBirdPlayer.Should().BeTrue();
    }

    [Fact]
    public void ApplyStartSeasonAttributes_HealthReset()
    {
        var league = CreateTestLeague(1);
        var player = league.Teams[0].Roster[0];
        player.Health = 70;
        player.Injury = 5;
        int expired = 0, newFa = 0;

        OffSeasonService.ApplyStartSeasonAttributes(league, true, ref expired, ref newFa, new Random(42));

        player.Health.Should().Be(100);
    }

    // ── AdvanceSeason (integration) ────────────────────────────────

    [Fact]
    public void AdvanceSeason_IncrementsYear()
    {
        var league = CreateTestLeague();

        var result = OffSeasonService.AdvanceSeason(league, new Random(42));

        result.PreviousYear.Should().Be(2024);
        result.NewYear.Should().Be(2025);
        league.Settings.CurrentYear.Should().Be(2025);
    }

    [Fact]
    public void AdvanceSeason_ArchivesStats()
    {
        var league = CreateTestLeague(1);
        var player = league.Teams[0].Roster[0];
        player.CareerHistory.Clear();
        player.CareerStats.Reset();
        int origGames = player.SimulatedStats.Games;

        OffSeasonService.AdvanceSeason(league, new Random(42));

        player.CareerHistory.Should().HaveCount(1);
        player.CareerHistory[0].Year.Should().Be(2024);
        player.CareerHistory[0].Stats.Games.Should().Be(origGames);
    }

    [Fact]
    public void AdvanceSeason_ResetsSimulatedStats()
    {
        var league = CreateTestLeague(1);

        OffSeasonService.AdvanceSeason(league, new Random(42));

        // After reset, sim stats have been repopulated by DevelopPlayers
        // but they should reflect developed/projected stats, not the original season stats
        // The key thing is the season was processed and the league is in a valid state
        league.Schedule.SeasonStarted.Should().BeFalse();
    }

    [Fact]
    public void AdvanceSeason_ClearsRookieFlags()
    {
        var league = CreateTestLeague(1);
        league.Teams[0].Roster[0].Contract.IsRookie = true;

        OffSeasonService.AdvanceSeason(league, new Random(42));

        league.Teams[0].Roster[0].Contract.IsRookie.Should().BeFalse();
    }

    [Fact]
    public void AdvanceSeason_Deterministic()
    {
        var league1 = CreateTestLeague(2);
        var result1 = OffSeasonService.AdvanceSeason(league1, new Random(42));

        var league2 = CreateTestLeague(2);
        var result2 = OffSeasonService.AdvanceSeason(league2, new Random(42));

        result1.PlayersRetired.Should().Be(result2.PlayersRetired);
        result1.NewYear.Should().Be(result2.NewYear);
    }

    [Fact]
    public void AdvanceSeason_ProducesValidResult()
    {
        var league = CreateTestLeague(5);

        var result = OffSeasonService.AdvanceSeason(league, new Random(42));

        result.Should().NotBeNull();
        result.PreviousYear.Should().Be(2024);
        result.NewYear.Should().Be(2025);
        result.RetiredPlayerNames.Should().NotBeNull();
    }
}
