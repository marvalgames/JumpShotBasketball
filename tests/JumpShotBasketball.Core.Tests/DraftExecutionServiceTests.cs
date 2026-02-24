using FluentAssertions;
using JumpShotBasketball.Core.Constants;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Staff;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class DraftExecutionServiceTests
{
    private static readonly Random Seed42 = new(42);

    // ── BuildDraftOrder ─────────────────────────────────────────────

    [Fact]
    public void BuildDraftOrder_EmptyLeague_ReturnsEmptyList()
    {
        var league = new League();
        var order = DraftExecutionService.BuildDraftOrder(league);
        order.Should().BeEmpty();
    }

    [Fact]
    public void BuildDraftOrder_SortsByWorstRecordFirst()
    {
        var league = CreateLeagueWithTeams(4);
        // Team 0: best record, Team 3: worst
        league.Teams[0].Record.LeaguePercentage = 0.750;
        league.Teams[1].Record.LeaguePercentage = 0.500;
        league.Teams[2].Record.LeaguePercentage = 0.250;
        league.Teams[3].Record.LeaguePercentage = 0.125;

        var order = DraftExecutionService.BuildDraftOrder(league);

        // Round 1: worst team picks first
        order[0].PickNumber.Should().Be(1);
        order[0].PickingTeamIndex.Should().Be(3, "worst team (0.125) picks #1");
        order[1].PickingTeamIndex.Should().Be(2, "second worst (0.250) picks #2");
        order[2].PickingTeamIndex.Should().Be(1, "third worst (0.500) picks #3");
        order[3].PickingTeamIndex.Should().Be(0, "best team (0.750) picks #4");
    }

    [Fact]
    public void BuildDraftOrder_PlayoffTeamsPickLater()
    {
        var league = CreateLeagueWithTeams(2);
        league.Teams[0].Record.LeaguePercentage = 0.500;
        league.Teams[0].Record.IsPlayoffTeam = true; // 0.5 + 1.0 = 1.5
        league.Teams[1].Record.LeaguePercentage = 0.500;
        league.Teams[1].Record.IsPlayoffTeam = false; // 0.5 + 0.0 = 0.5

        var order = DraftExecutionService.BuildDraftOrder(league);

        // Non-playoff team picks before playoff team at same record
        order[0].PickingTeamIndex.Should().Be(1, "non-playoff team picks first");
    }

    [Fact]
    public void BuildDraftOrder_ProducesTwoRounds()
    {
        var league = CreateLeagueWithTeams(4);
        var order = DraftExecutionService.BuildDraftOrder(league);

        order.Count.Should().Be(8, "4 teams × 2 rounds");
        order.Count(o => o.Round == 0).Should().Be(4);
        order.Count(o => o.Round == 1).Should().Be(4);
    }

    [Fact]
    public void BuildDraftOrder_RespectsDraftBoardOwnership()
    {
        var league = CreateLeagueWithTeams(4);
        league.Teams[0].Record.LeaguePercentage = 0.750; // best record = last pick
        league.Teams[1].Record.LeaguePercentage = 0.500;
        league.Teams[2].Record.LeaguePercentage = 0.250;
        league.Teams[3].Record.LeaguePercentage = 0.125; // worst record = pick #1

        var board = new DraftBoard();
        DraftService.InitializeDraftChart(board, 4);
        // Team 3 traded their pick to team 0
        DraftService.TransferPick(board, fromTeam: 3, toTeam: 0, year: 0, round: 0);
        league.DraftBoard = board;

        var order = DraftExecutionService.BuildDraftOrder(league);

        // Pick #1 is team 3's slot, but owned by team 0
        order[0].OriginalTeamIndex.Should().Be(3);
        order[0].PickingTeamIndex.Should().Be(0, "team 0 owns team 3's pick");
    }

    // ── GetComputerSelection ────────────────────────────────────────

    [Fact]
    public void GetComputerSelection_EmptyPool_ReturnsNegative()
    {
        var team = CreateTeamWithPlayers(10);
        int result = DraftExecutionService.GetComputerSelection(new List<Player>(), team, 5.0, new Random(1));
        result.Should().Be(-1);
    }

    [Fact]
    public void GetComputerSelection_FillsMissingPosition()
    {
        // Team has no PG on roster, all other positions have eligible players
        var team = new Team { Id = 0, Name = "Test" };
        var sg = CreatePlayer("SG", 6.0); sg.Ratings.ProjectionFieldGoalsAttempted = 10;
        var sf = CreatePlayer("SF", 5.0); sf.Ratings.ProjectionFieldGoalsAttempted = 10;
        var pf = CreatePlayer("PF", 5.0); pf.Ratings.ProjectionFieldGoalsAttempted = 10;
        var c = CreatePlayer("C", 5.0);   c.Ratings.ProjectionFieldGoalsAttempted = 10;
        team.Roster.Add(sg);
        team.Roster.Add(sf);
        team.Roster.Add(pf);
        team.Roster.Add(c);
        team.GeneralManager = new StaffMember { Power1 = 3 };

        var available = new List<Player>
        {
            CreatePlayer("SG", 8.0), // index 0: SG (not needed)
            CreatePlayer("PG", 5.0), // index 1: PG (needed!)
            CreatePlayer("C", 7.0),  // index 2: C (not needed)
        };

        int idx = DraftExecutionService.GetComputerSelection(available, team, 5.0, new Random(1));
        idx.Should().Be(1, "should fill missing PG position");
    }

    [Fact]
    public void GetComputerSelection_FallsBackToBPA()
    {
        // Team has all positions covered with good players
        var team = new Team { Id = 0, Name = "Test" };
        for (int i = 0; i < 10; i++)
        {
            string pos = (i % 5) switch { 0 => "PG", 1 => "SG", 2 => "SF", 3 => "PF", _ => "C" };
            var p = CreatePlayer(pos, 7.0);
            p.Ratings.ProjectionFieldGoalsAttempted = 10;
            team.Roster.Add(p);
        }
        team.GeneralManager = new StaffMember { Power1 = 1 };

        var available = new List<Player>
        {
            CreatePlayer("PG", 9.0),
            CreatePlayer("SG", 6.0),
        };

        int idx = DraftExecutionService.GetComputerSelection(available, team, 5.0, new Random(99));
        idx.Should().BeInRange(0, 1, "should pick someone from available pool");
    }

    [Fact]
    public void GetComputerSelection_Deterministic_WithSameSeed()
    {
        var team = CreateTeamWithPlayers(5);
        team.GeneralManager = new StaffMember { Power1 = 3 };
        var available = CreateRookiePool(10);

        int r1 = DraftExecutionService.GetComputerSelection(available, team, 5.0, new Random(42));
        int r2 = DraftExecutionService.GetComputerSelection(available, team, 5.0, new Random(42));
        r1.Should().Be(r2);
    }

    // ── ExecuteDraft ────────────────────────────────────────────────

    [Fact]
    public void ExecuteDraft_NoDraftPool_ReturnsEmptyResult()
    {
        var league = CreateLeagueWithTeams(4);
        league.DraftPool = null;

        var result = DraftExecutionService.ExecuteDraft(league, new Random(1));
        result.TotalPicks.Should().Be(0);
        result.Selections.Should().BeEmpty();
    }

    [Fact]
    public void ExecuteDraft_PlacesRookiesOnRosters()
    {
        var league = CreateLeagueWithTeams(4);
        var rookies = CreateRookiePool(24); // 6 per team, 2 rounds of 4
        league.DraftPool = new RookiePool { Rookies = rookies };
        league.DraftBoard = new DraftBoard();
        DraftService.InitializeDraftChart(league.DraftBoard, 4);

        int totalPlayersBefore = league.Teams.Sum(t => t.Roster.Count);

        var result = DraftExecutionService.ExecuteDraft(league, new Random(42));

        result.TotalPicks.Should().Be(8, "4 teams × 2 rounds = 8 picks");
        int totalPlayersAfter = league.Teams.Sum(t => t.Roster.Count);
        totalPlayersAfter.Should().Be(totalPlayersBefore + 8, "8 rookies added to rosters");
    }

    [Fact]
    public void ExecuteDraft_DraftedRookiesHaveCorrectFlags()
    {
        var league = CreateLeagueWithTeams(2);
        var rookies = CreateRookiePool(10);
        league.DraftPool = new RookiePool { Rookies = rookies };
        league.DraftBoard = new DraftBoard();
        DraftService.InitializeDraftChart(league.DraftBoard, 2);

        var result = DraftExecutionService.ExecuteDraft(league, new Random(42));

        foreach (var selection in result.Selections)
        {
            var team = league.Teams[selection.TeamIndex];
            var drafted = team.Roster.FirstOrDefault(p => p.Name == selection.PlayerName);
            drafted.Should().NotBeNull($"player {selection.PlayerName} should be on team roster");
            drafted!.Contract.IsRookie.Should().BeTrue();
            drafted.Contract.IsFreeAgent.Should().BeFalse();
            drafted.Contract.Signed.Should().BeTrue();
            drafted.Contract.ContractYears.Should().Be(3, "rookie contract is 3 years");
            drafted.RoundSelected.Should().BeGreaterThan(0);
            drafted.Content.Should().Be(5);
            drafted.Active.Should().BeTrue();
        }
    }

    [Fact]
    public void ExecuteDraft_UndraftedRookiesBecomeFA()
    {
        var league = CreateLeagueWithTeams(2);
        var rookies = CreateRookiePool(20); // 20 rookies, 4 picks → 16 undrafted
        league.DraftPool = new RookiePool { Rookies = rookies };
        league.DraftBoard = new DraftBoard();
        DraftService.InitializeDraftChart(league.DraftBoard, 2);

        var result = DraftExecutionService.ExecuteDraft(league, new Random(42));

        result.TotalPicks.Should().Be(4, "2 teams × 2 rounds");
        result.UndraftedCount.Should().Be(16);

        // Undrafted rookies remain in DraftPool
        league.DraftPool.Rookies.Count.Should().Be(16);
        league.DraftPool.Rookies.Should().OnlyContain(r => r.Contract.IsFreeAgent);
        league.DraftPool.Rookies.Should().OnlyContain(r => r.Contract.CurrentTeam == -1);
    }

    [Fact]
    public void ExecuteDraft_AssignsUniqueIds()
    {
        var league = CreateLeagueWithTeams(2);
        league.Teams[0].Roster[0].Id = 100;
        var rookies = CreateRookiePool(10);
        league.DraftPool = new RookiePool { Rookies = rookies };
        league.DraftBoard = new DraftBoard();
        DraftService.InitializeDraftChart(league.DraftBoard, 2);

        DraftExecutionService.ExecuteDraft(league, new Random(42));

        // All drafted + undrafted rookies should have unique IDs > 100
        var allRookieIds = new HashSet<int>();
        foreach (var team in league.Teams)
        {
            foreach (var p in team.Roster.Where(p => p.Contract.IsRookie))
            {
                p.Id.Should().BeGreaterThan(100);
                allRookieIds.Add(p.Id).Should().BeTrue($"ID {p.Id} should be unique");
            }
        }
        foreach (var undrafted in league.DraftPool.Rookies)
        {
            undrafted.Id.Should().BeGreaterThan(100);
            allRookieIds.Add(undrafted.Id).Should().BeTrue($"ID {undrafted.Id} should be unique");
        }
    }

    [Fact]
    public void ExecuteDraft_Deterministic_WithSameSeed()
    {
        var league1 = CreateLeagueWithTeams(4);
        league1.DraftPool = new RookiePool { Rookies = CreateRookiePool(24) };
        league1.DraftBoard = new DraftBoard();
        DraftService.InitializeDraftChart(league1.DraftBoard, 4);

        var league2 = CreateLeagueWithTeams(4);
        league2.DraftPool = new RookiePool { Rookies = CreateRookiePool(24) };
        league2.DraftBoard = new DraftBoard();
        DraftService.InitializeDraftChart(league2.DraftBoard, 4);

        var r1 = DraftExecutionService.ExecuteDraft(league1, new Random(42));
        var r2 = DraftExecutionService.ExecuteDraft(league2, new Random(42));

        r1.TotalPicks.Should().Be(r2.TotalPicks);
        for (int i = 0; i < r1.Selections.Count; i++)
        {
            r1.Selections[i].PlayerName.Should().Be(r2.Selections[i].PlayerName);
            r1.Selections[i].TeamIndex.Should().Be(r2.Selections[i].TeamIndex);
        }
    }

    [Fact]
    public void ExecuteDraft_LotteryDeterminesFirstPicks()
    {
        var league = CreateLeagueWithTeams(4);
        league.Teams[0].Record.LeaguePercentage = 0.750;
        league.Teams[1].Record.LeaguePercentage = 0.500;
        league.Teams[2].Record.LeaguePercentage = 0.250;
        league.Teams[3].Record.LeaguePercentage = 0.100;

        league.DraftPool = new RookiePool { Rookies = CreateRookiePool(24) };
        league.DraftBoard = new DraftBoard();
        DraftService.InitializeDraftChart(league.DraftBoard, 4);

        var result = DraftExecutionService.ExecuteDraft(league, new Random(42));

        // Lottery draws 3 winners from 4 non-playoff teams
        result.LotteryWinners.Should().HaveCount(3);

        // First 3 picks should match lottery winners
        for (int i = 0; i < 3; i++)
        {
            result.Selections[i].TeamIndex.Should().Be(result.LotteryWinners[i].TeamIndex,
                $"pick {i + 1} should go to lottery winner {i + 1}");
        }
    }

    [Fact]
    public void ExecuteDraft_StoresResultOnLeague()
    {
        var league = CreateLeagueWithTeams(2);
        league.DraftPool = new RookiePool { Rookies = CreateRookiePool(10) };
        league.DraftBoard = new DraftBoard();
        DraftService.InitializeDraftChart(league.DraftBoard, 2);

        DraftExecutionService.ExecuteDraft(league, new Random(42));

        league.LastDraftResult.Should().NotBeNull();
        league.LastDraftResult!.TotalPicks.Should().Be(4);
    }

    [Fact]
    public void ExecuteDraft_SelectionHasRoundAndPickInfo()
    {
        var league = CreateLeagueWithTeams(2);
        league.DraftPool = new RookiePool { Rookies = CreateRookiePool(10) };
        league.DraftBoard = new DraftBoard();
        DraftService.InitializeDraftChart(league.DraftBoard, 2);

        var result = DraftExecutionService.ExecuteDraft(league, new Random(42));

        // Round 1 picks
        result.Selections[0].Round.Should().Be(1);
        result.Selections[0].Pick.Should().Be(1);
        result.Selections[1].Round.Should().Be(1);
        result.Selections[1].Pick.Should().Be(2);
        // Round 2 picks
        result.Selections[2].Round.Should().Be(2);
        result.Selections[2].Pick.Should().Be(1);
    }

    [Fact]
    public void ExecuteDraft_RookieContractHasSalaries()
    {
        var league = CreateLeagueWithTeams(2);
        var rookies = CreateRookiePool(10);
        league.DraftPool = new RookiePool { Rookies = rookies };
        league.DraftBoard = new DraftBoard();
        DraftService.InitializeDraftChart(league.DraftBoard, 2);

        DraftExecutionService.ExecuteDraft(league, new Random(42));

        foreach (var team in league.Teams)
        {
            foreach (var p in team.Roster.Where(p => p.Contract.IsRookie))
            {
                p.Contract.ContractSalaries[0].Should().BeGreaterThan(0, "should have year 1 salary");
                p.Contract.TotalSalary.Should().BeGreaterThan(0);
                p.Contract.RemainingSalary.Should().BeGreaterThan(0);
            }
        }
    }

    // ── ApplyLottery ─────────────────────────────────────────────────

    [Fact]
    public void ApplyLottery_AllNonPlayoff_ThreeWinners()
    {
        var league = CreateLeagueWithTeams(8);
        for (int i = 0; i < 8; i++)
            league.Teams[i].Record.LeaguePercentage = (i + 1) * 0.1;

        var order = DraftExecutionService.BuildDraftOrder(league);

        var winners = DraftExecutionService.ApplyLottery(order, league, new Random(42));

        winners.Should().HaveCount(3, "3 lottery winners drawn from 8 non-playoff teams");
        winners[0].Pick.Should().Be(1);
        winners[1].Pick.Should().Be(2);
        winners[2].Pick.Should().Be(3);
    }

    [Fact]
    public void ApplyLottery_SomePlayoffTeams_OnlyNonPlayoffInLottery()
    {
        var league = CreateLeagueWithTeams(8);
        // 4 non-playoff (worst records), 4 playoff (best records)
        for (int i = 0; i < 8; i++)
        {
            league.Teams[i].Record.LeaguePercentage = (i + 1) * 0.1;
            league.Teams[i].Record.IsPlayoffTeam = i >= 4;
        }

        var order = DraftExecutionService.BuildDraftOrder(league);

        var winners = DraftExecutionService.ApplyLottery(order, league, new Random(42));

        winners.Should().HaveCount(3, "3 lottery winners from 4 non-playoff teams");

        // Winners should all be non-playoff teams (indices 0-3)
        foreach (var w in winners)
        {
            league.Teams[w.TeamIndex].Record.IsPlayoffTeam.Should().BeFalse(
                $"lottery winner team {w.TeamIndex} should be non-playoff");
        }
    }

    [Fact]
    public void ApplyLottery_TwoNonPlayoffTeams_TwoWinners()
    {
        var league = CreateLeagueWithTeams(4);
        league.Teams[0].Record.LeaguePercentage = 0.250;
        league.Teams[1].Record.LeaguePercentage = 0.500;
        league.Teams[2].Record.LeaguePercentage = 0.600;
        league.Teams[2].Record.IsPlayoffTeam = true;
        league.Teams[3].Record.LeaguePercentage = 0.750;
        league.Teams[3].Record.IsPlayoffTeam = true;

        var order = DraftExecutionService.BuildDraftOrder(league);

        var winners = DraftExecutionService.ApplyLottery(order, league, new Random(42));

        winners.Should().HaveCount(2, "only 2 non-playoff teams → min(3, 2) = 2 winners");
    }

    [Fact]
    public void ApplyLottery_OneNonPlayoffTeam_NoLottery()
    {
        var league = CreateLeagueWithTeams(4);
        league.Teams[0].Record.LeaguePercentage = 0.250;
        for (int i = 1; i < 4; i++)
        {
            league.Teams[i].Record.LeaguePercentage = 0.500 + i * 0.1;
            league.Teams[i].Record.IsPlayoffTeam = true;
        }

        var order = DraftExecutionService.BuildDraftOrder(league);
        var orderBefore = order.Select(o => o.PickingTeamIndex).ToList();

        var winners = DraftExecutionService.ApplyLottery(order, league, new Random(42));

        winners.Should().BeEmpty("need at least 2 non-playoff teams for lottery");
        order.Select(o => o.PickingTeamIndex).Should().Equal(orderBefore, "order should be unchanged");
    }

    [Fact]
    public void ApplyLottery_OnlyAffectsRound1()
    {
        var league = CreateLeagueWithTeams(4);
        for (int i = 0; i < 4; i++)
            league.Teams[i].Record.LeaguePercentage = (i + 1) * 0.2;

        var order = DraftExecutionService.BuildDraftOrder(league);

        // Capture round 2 order before lottery
        var round2Before = order.Where(o => o.Round == 1)
            .Select(o => o.PickingTeamIndex).ToList();

        DraftExecutionService.ApplyLottery(order, league, new Random(42));

        // Round 2 should be unchanged
        var round2After = order.Where(o => o.Round == 1)
            .Select(o => o.PickingTeamIndex).ToList();
        round2After.Should().Equal(round2Before, "lottery only affects round 1");
    }

    [Fact]
    public void ApplyLottery_Deterministic_WithSameSeed()
    {
        var league = CreateLeagueWithTeams(8);
        for (int i = 0; i < 8; i++)
            league.Teams[i].Record.LeaguePercentage = (i + 1) * 0.1;

        var order1 = DraftExecutionService.BuildDraftOrder(league);
        var order2 = DraftExecutionService.BuildDraftOrder(league);

        var w1 = DraftExecutionService.ApplyLottery(order1, league, new Random(99));
        var w2 = DraftExecutionService.ApplyLottery(order2, league, new Random(99));

        w1.Should().HaveCount(w2.Count);
        for (int i = 0; i < w1.Count; i++)
        {
            w1[i].TeamIndex.Should().Be(w2[i].TeamIndex);
            w1[i].Pick.Should().Be(w2[i].Pick);
        }
    }

    [Fact]
    public void ApplyLottery_WinnersAreUnique()
    {
        var league = CreateLeagueWithTeams(8);
        for (int i = 0; i < 8; i++)
            league.Teams[i].Record.LeaguePercentage = (i + 1) * 0.1;

        var order = DraftExecutionService.BuildDraftOrder(league);
        var winners = DraftExecutionService.ApplyLottery(order, league, new Random(42));

        var teamIndices = winners.Select(w => w.TeamIndex).ToList();
        teamIndices.Should().OnlyHaveUniqueItems("each team can only win once");
    }

    [Fact]
    public void ExecuteDraft_LotteryPopulatesResult()
    {
        var league = CreateLeagueWithTeams(8);
        for (int i = 0; i < 8; i++)
            league.Teams[i].Record.LeaguePercentage = (i + 1) * 0.1;

        league.DraftPool = new RookiePool { Rookies = CreateRookiePool(30) };
        league.DraftBoard = new DraftBoard();
        DraftService.InitializeDraftChart(league.DraftBoard, 8);

        var result = DraftExecutionService.ExecuteDraft(league, new Random(42));

        result.LotteryWinners.Should().HaveCount(3);
        result.LotteryWinners.Should().OnlyContain(w => w.Pick >= 1 && w.Pick <= 3);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static League CreateLeagueWithTeams(int numTeams)
    {
        var league = new League();
        league.Settings.NumberOfTeams = numTeams;

        for (int t = 0; t < numTeams; t++)
        {
            var team = new Team
            {
                Id = t,
                Name = $"Team {t}",
                GeneralManager = new StaffMember { Power1 = 3 },
                Scout = new StaffMember(),
                Coach = new StaffMember()
            };

            // Add some existing players (5 per team)
            for (int p = 0; p < 5; p++)
            {
                string pos = p switch { 0 => "PG", 1 => "SG", 2 => "SF", 3 => "PF", _ => "C" };
                var player = CreatePlayer(pos, 4.0 + p * 0.5);
                player.Id = t * 30 + p + 1;
                player.TeamIndex = t;
                player.Team = team.Name;
                player.Contract.CurrentTeam = t;
                team.Roster.Add(player);
            }

            league.Teams.Add(team);
        }

        return league;
    }

    private static Team CreateTeamWithPlayers(int count)
    {
        var team = new Team
        {
            Id = 0,
            Name = "TestTeam",
            GeneralManager = new StaffMember { Power1 = 3 },
            Scout = new StaffMember(),
            Coach = new StaffMember()
        };

        for (int i = 0; i < count; i++)
        {
            string pos = (i % 5) switch { 0 => "PG", 1 => "SG", 2 => "SF", 3 => "PF", _ => "C" };
            var p = CreatePlayer(pos, 4.0 + i * 0.3);
            p.TeamIndex = 0;
            p.Contract.CurrentTeam = 0;
            team.Roster.Add(p);
        }

        return team;
    }

    private static List<Player> CreateRookiePool(int count)
    {
        var rookies = new List<Player>();
        string[] positions = { "PG", "SG", "SF", "PF", "C" };

        for (int i = 0; i < count; i++)
        {
            var p = CreatePlayer(positions[i % 5], 3.0 + (count - i) * 0.2);
            p.Name = $"Rookie{i + 1}";
            p.Age = 20 + i % 4;
            p.Ratings.Prime = 28;
            p.Contract.IsRookie = true;
            p.Contract.IsFreeAgent = false;
            rookies.Add(p);
        }

        return rookies;
    }

    private static Player CreatePlayer(string position, double tradeTru)
    {
        return new Player
        {
            Name = $"Player_{position}_{tradeTru:F1}",
            Position = position,
            Age = 25,
            Ratings = new PlayerRatings
            {
                TradeTrueRating = tradeTru,
                TradeValue = tradeTru * 0.9,
                Prime = 28,
                Potential1 = 3,
                Potential2 = 3,
                Effort = 3
            },
            Contract = new PlayerContract
            {
                SecurityFactor = 3,
                LoyaltyFactor = 3,
                WinningFactor = 3,
                TraditionFactor = 3,
                PlayingTimeFactor = 3,
                CoachFactor = 3
            }
        };
    }
}
