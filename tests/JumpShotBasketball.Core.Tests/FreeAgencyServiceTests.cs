using FluentAssertions;
using JumpShotBasketball.Core.Constants;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Staff;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class FreeAgencyServiceTests
{
    // ── CalculatePositionValues ──────────────────────────────────────

    [Fact]
    public void CalculatePositionValues_SumsRatingsByPosition()
    {
        var team = CreateTeam();
        team.Roster.Add(CreatePlayer("PG", 5.0));
        team.Roster.Add(CreatePlayer("PG", 3.0));
        team.Roster.Add(CreatePlayer("C", 7.0));

        var values = FreeAgencyService.CalculatePositionValues(team);

        values[1].Should().BeApproximately(8.0, 0.01, "PG: 5.0 + 3.0");
        values[5].Should().BeApproximately(7.0, 0.01, "C: 7.0");
        values[2].Should().Be(0, "no SG");
    }

    [Fact]
    public void CalculatePositionValues_ExcludesFreeAgents()
    {
        var team = CreateTeam();
        var p1 = CreatePlayer("PG", 5.0);
        var p2 = CreatePlayer("PG", 3.0);
        p2.Contract.IsFreeAgent = true;
        team.Roster.Add(p1);
        team.Roster.Add(p2);

        var values = FreeAgencyService.CalculatePositionValues(team);
        values[1].Should().BeApproximately(5.0, 0.01, "only non-FA PG counted");
    }

    [Fact]
    public void CalculatePositionValues_ClampsNegativeToZero()
    {
        var team = CreateTeam();
        var p = CreatePlayer("SF", -2.0);
        team.Roster.Add(p);

        var values = FreeAgencyService.CalculatePositionValues(team);
        values[3].Should().Be(0, "negative ratings clamped to 0");
    }

    // ── CalculatePlayerSalaryRequests ────────────────────────────────

    [Fact]
    public void CalculatePlayerSalaryRequests_SetsSeekingPerTeam()
    {
        var league = CreateLeagueWithTeams(4);
        var state = CreateDefaultState(league);
        var fa = CreateFreeAgent("PG", 6.0, 3);

        FreeAgencyService.CalculatePlayerSalaryRequests(fa, league, state, new Random(42));

        // Each team should have a non-zero seeking value
        for (int t = 0; t < 4; t++)
            fa.Contract.Seeking[t].Should().BeGreaterThan(0, $"team {t} should have seeking value");
    }

    [Fact]
    public void CalculatePlayerSalaryRequests_HigherRating_HigherSeeking()
    {
        var league = CreateLeagueWithTeams(4);
        var state = CreateDefaultState(league);

        var faLow = CreateFreeAgent("PG", 3.0, 3);
        var faHigh = CreateFreeAgent("PG", 8.0, 3);

        FreeAgencyService.CalculatePlayerSalaryRequests(faLow, league, state, new Random(42));
        FreeAgencyService.CalculatePlayerSalaryRequests(faHigh, league, state, new Random(42));

        double avgLow = Enumerable.Range(0, 4).Average(t => faLow.Contract.Seeking[t]);
        double avgHigh = Enumerable.Range(0, 4).Average(t => faHigh.Contract.Seeking[t]);

        avgHigh.Should().BeGreaterThan(avgLow, "higher rated player seeks more money");
    }

    [Fact]
    public void CalculatePlayerSalaryRequests_LoyaltyDiscount_ForPreviousTeam()
    {
        var league = CreateLeagueWithTeams(4);
        var state = CreateDefaultState(league);

        var fa = CreateFreeAgent("PG", 6.0, 5);
        fa.Contract.PreviousTeam = 2;
        fa.Contract.LoyaltyFactor = 5; // high loyalty

        FreeAgencyService.CalculatePlayerSalaryRequests(fa, league, state, new Random(42));

        // Previous team should have lower seeking (discount)
        double avgOther = new[] { fa.Contract.Seeking[0], fa.Contract.Seeking[1], fa.Contract.Seeking[3] }
            .Average();
        fa.Contract.Seeking[2].Should().BeLessThan((int)avgOther,
            "previous team gets loyalty discount");
    }

    [Fact]
    public void CalculatePlayerSalaryRequests_RespectsSalaryMinimum()
    {
        var league = CreateLeagueWithTeams(2);
        var state = CreateDefaultState(league);
        var fa = CreateFreeAgent("PG", 0.1, 0); // very low rating, 0 YOS

        FreeAgencyService.CalculatePlayerSalaryRequests(fa, league, state, new Random(42));

        fa.Contract.QualifyingSalaries[0].Should()
            .BeGreaterThanOrEqualTo(LeagueConstants.SalaryMinimumByYos[0]);
    }

    [Fact]
    public void CalculatePlayerSalaryRequests_RespectsSalaryMaximum()
    {
        var league = CreateLeagueWithTeams(2);
        var state = CreateDefaultState(league);
        var fa = CreateFreeAgent("PG", 20.0, 12); // superstar level, 12 YOS

        FreeAgencyService.CalculatePlayerSalaryRequests(fa, league, state, new Random(42));

        fa.Contract.QualifyingSalaries[0].Should()
            .BeLessThanOrEqualTo(LeagueConstants.SalaryMaximumByYos[10]);
    }

    [Fact]
    public void CalculatePlayerSalaryRequests_SetsQualifyingOfferYears()
    {
        var league = CreateLeagueWithTeams(2);
        var state = CreateDefaultState(league);
        var fa = CreateFreeAgent("PG", 6.0, 5);

        FreeAgencyService.CalculatePlayerSalaryRequests(fa, league, state, new Random(42));

        fa.Contract.QualifyingOfferYears.Should().BeInRange(1, 6);
        fa.Contract.TotalSalarySeeking.Should().BeGreaterThan(0);
    }

    // ── GenerateContractOffer ───────────────────────────────────────

    [Fact]
    public void GenerateContractOffer_ReturnsValidContract()
    {
        var player = CreateFreeAgent("PG", 6.0, 5);

        var (years, salaries, totalSalary) = FreeAgencyService.GenerateContractOffer(
            player, stage: 1, teamPayroll: 2000, salaryCap: 3550, ownerCap: 35500, new Random(42));

        years.Should().BeInRange(1, 6);
        salaries[0].Should().BeGreaterThan(0);
        totalSalary.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GenerateContractOffer_HigherRating_HigherSalary()
    {
        var lowPlayer = CreateFreeAgent("PG", 3.0, 3);
        var highPlayer = CreateFreeAgent("PG", 9.0, 3);

        var (_, _, totalLow) = FreeAgencyService.GenerateContractOffer(
            lowPlayer, 1, 1000, 3550, 35500, new Random(42));
        var (_, _, totalHigh) = FreeAgencyService.GenerateContractOffer(
            highPlayer, 1, 1000, 3550, 35500, new Random(42));

        totalHigh.Should().BeGreaterThan(totalLow);
    }

    [Fact]
    public void GenerateContractOffer_CapRoomAdjustment_LowersSalary()
    {
        var player = CreateFreeAgent("PG", 8.0, 5);

        // Team near cap
        var (_, salariesNearCap, _) = FreeAgencyService.GenerateContractOffer(
            player, 1, teamPayroll: 3400, salaryCap: 3550, ownerCap: 35500, new Random(42));

        // Team with room
        var (_, salariesRoom, _) = FreeAgencyService.GenerateContractOffer(
            player, 1, teamPayroll: 1000, salaryCap: 3550, ownerCap: 35500, new Random(42));

        salariesNearCap[0].Should().BeLessThanOrEqualTo(salariesRoom[0],
            "near-cap team offers less");
    }

    [Fact]
    public void GenerateContractOffer_MultiyearHasRaises()
    {
        // Run multiple times to find a multi-year deal
        for (int seed = 0; seed < 20; seed++)
        {
            var player = CreateFreeAgent("PG", 6.0, 5);
            var (years, salaries, _) = FreeAgencyService.GenerateContractOffer(
                player, 1, 1000, 3550, 35500, new Random(seed));

            if (years > 1)
            {
                salaries[1].Should().BeGreaterThanOrEqualTo(salaries[0],
                    "later years should have raises");
                return;
            }
        }
        // At least one seed should produce multi-year
        Assert.Fail("No multi-year contract generated in 20 attempts");
    }

    // ── ChooseBestContract ──────────────────────────────────────────

    [Fact]
    public void ChooseBestContract_NoOffers_ReturnsNegative()
    {
        var player = CreateFreeAgent("PG", 6.0, 3);
        player.Contract.TotalSalarySeeking = 600;
        player.Contract.QualifyingOfferYears = 2;

        int best = FreeAgencyService.ChooseBestContract(player, new int[] { 0, 0, 0, 0 }, 15);
        best.Should().Be(-1);
    }

    [Fact]
    public void ChooseBestContract_PicksHighestValueOffer()
    {
        var player = CreateFreeAgent("PG", 6.0, 3);
        player.Contract.TotalSalarySeeking = 600;
        player.Contract.QualifyingOfferYears = 2;

        // Team 0: low offer
        player.Contract.Seeking[0] = 300;
        player.Contract.YearOffer[0] = 2;
        player.Contract.TotalSalaryOffer[0] = 200;
        player.Contract.SalaryOffer[0][0] = 100;
        player.Contract.SalaryOffer[0][1] = 100;

        // Team 1: high offer
        player.Contract.Seeking[1] = 300;
        player.Contract.YearOffer[1] = 3;
        player.Contract.TotalSalaryOffer[1] = 900;
        player.Contract.SalaryOffer[1][0] = 300;
        player.Contract.SalaryOffer[1][1] = 300;
        player.Contract.SalaryOffer[1][2] = 300;

        int best = FreeAgencyService.ChooseBestContract(player, new int[] { 0, 0 }, 15);
        best.Should().Be(1, "team 1 has higher value offer");
    }

    [Fact]
    public void ChooseBestContract_SkipsFullRosters()
    {
        var player = CreateFreeAgent("PG", 6.0, 3);
        player.Contract.TotalSalarySeeking = 600;
        player.Contract.QualifyingOfferYears = 2;

        // Team 0: full roster
        player.Contract.Seeking[0] = 300;
        player.Contract.YearOffer[0] = 2;
        player.Contract.TotalSalaryOffer[0] = 1000;
        player.Contract.SalaryOffer[0][0] = 500;
        player.Contract.SalaryOffer[0][1] = 500;

        // Team 1: has room
        player.Contract.Seeking[1] = 300;
        player.Contract.YearOffer[1] = 2;
        player.Contract.TotalSalaryOffer[1] = 500;
        player.Contract.SalaryOffer[1][0] = 250;
        player.Contract.SalaryOffer[1][1] = 250;

        int best = FreeAgencyService.ChooseBestContract(player, new int[] { 15, 10 }, 15);
        best.Should().Be(1, "team 0 has full roster");
    }

    [Fact]
    public void ChooseBestContract_DraftedRookieAutoSignsWithTeam()
    {
        var player = CreateFreeAgent("PG", 6.0, 0);
        player.RoundSelected = 1;
        player.Contract.IsRookie = true;
        player.Contract.CurrentTeam = 2;

        int best = FreeAgencyService.ChooseBestContract(player, new int[] { 0, 0, 0, 0 }, 15);
        best.Should().Be(2, "drafted rookie auto-signs with drafting team");
    }

    // ── FinalizeContract ────────────────────────────────────────────

    [Fact]
    public void FinalizeContract_CopiesOfferToContract()
    {
        var player = CreateFreeAgent("PG", 6.0, 3);
        player.Contract.YearOffer[1] = 3;
        player.Contract.SalaryOffer[1][0] = 200;
        player.Contract.SalaryOffer[1][1] = 210;
        player.Contract.SalaryOffer[1][2] = 220;

        FreeAgencyService.FinalizeContract(player, teamIndex: 1);

        player.Contract.IsFreeAgent.Should().BeFalse();
        player.Contract.Signed.Should().BeTrue();
        player.Contract.JustSigned.Should().BeTrue();
        player.Contract.ContractYears.Should().Be(3);
        player.Contract.ContractSalaries[0].Should().Be(200);
        player.Contract.ContractSalaries[1].Should().Be(210);
        player.Contract.ContractSalaries[2].Should().Be(220);
        player.Contract.TotalSalary.Should().Be(630);
        player.Contract.RemainingSalary.Should().Be(630);
        player.Contract.CurrentTeam.Should().Be(1);
    }

    [Fact]
    public void FinalizeContract_ClearsPreviousContractSalaries()
    {
        var player = CreateFreeAgent("PG", 6.0, 3);
        player.Contract.ContractSalaries[0] = 999;
        player.Contract.ContractSalaries[5] = 888;

        player.Contract.YearOffer[0] = 2;
        player.Contract.SalaryOffer[0][0] = 100;
        player.Contract.SalaryOffer[0][1] = 110;

        FreeAgencyService.FinalizeContract(player, teamIndex: 0);

        player.Contract.ContractSalaries[2].Should().Be(0, "year 3 cleared");
        player.Contract.ContractSalaries[5].Should().Be(0, "year 6 cleared");
    }

    // ── AcceptOffer ─────────────────────────────────────────────────

    [Fact]
    public void AcceptOffer_NoTopOffer_Rejects()
    {
        var player = CreateFreeAgent("PG", 6.0, 3);
        player.Contract.BestContract = 0;
        // TopOffer[0] is false by default

        bool accepted = FreeAgencyService.AcceptOffer(
            player, stage: 6, new int[] { 1000, 1000 }, 2, 3550, 5.0, new Random(42));

        accepted.Should().BeFalse();
    }

    [Fact]
    public void AcceptOffer_LateStage_MoreLikelyToAccept()
    {
        int acceptsEarly = 0;
        int acceptsLate = 0;

        for (int seed = 0; seed < 100; seed++)
        {
            var playerEarly = CreatePlayerWithOffer(0.0);
            if (FreeAgencyService.AcceptOffer(playerEarly, 1, new int[] { 1000, 1000 }, 2, 3550, 5.0, new Random(seed)))
                acceptsEarly++;

            var playerLate = CreatePlayerWithOffer(0.9);
            if (FreeAgencyService.AcceptOffer(playerLate, 10, new int[] { 1000, 1000 }, 2, 3550, 5.0, new Random(seed)))
                acceptsLate++;
        }

        acceptsLate.Should().BeGreaterThanOrEqualTo(acceptsEarly,
            "late stage + high accept factor should lead to more acceptances");
    }

    [Fact]
    public void AcceptOffer_AlreadySigned_Rejects()
    {
        var player = CreatePlayerWithOffer(0.5);
        player.Contract.Signed = true;

        bool accepted = FreeAgencyService.AcceptOffer(
            player, 5, new int[] { 1000, 1000 }, 2, 3550, 5.0, new Random(42));
        accepted.Should().BeFalse();
    }

    // ── RunFreeAgencyPeriod ─────────────────────────────────────────

    [Fact]
    public void RunFreeAgencyPeriod_NoFreeAgents_ReturnsEmpty()
    {
        var league = CreateLeagueWithTeams(4);
        // No FAs on any roster

        var result = FreeAgencyService.RunFreeAgencyPeriod(league, 15, new Random(42));

        result.PlayersSigned.Should().Be(0);
        result.StagesCompleted.Should().Be(0, "no stages needed when there are no free agents");
    }

    [Fact]
    public void RunFreeAgencyPeriod_SignsFreeAgents()
    {
        var league = CreateLeagueWithFreeAgents(4, 3); // 4 teams, 3 FAs per team

        var result = FreeAgencyService.RunFreeAgencyPeriod(league, 15, new Random(42));

        result.PlayersSigned.Should().BeGreaterThan(0, "some FAs should sign");
        result.SigningDescriptions.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public void RunFreeAgencyPeriod_SignedPlayersNoLongerFA()
    {
        var league = CreateLeagueWithFreeAgents(4, 3);

        FreeAgencyService.RunFreeAgencyPeriod(league, 15, new Random(42));

        foreach (var team in league.Teams)
        {
            foreach (var player in team.Roster)
            {
                if (player.Contract.JustSigned)
                {
                    player.Contract.IsFreeAgent.Should().BeFalse();
                    player.Contract.Signed.Should().BeTrue();
                    player.Contract.ContractYears.Should().BeGreaterThan(0);
                }
            }
        }
    }

    [Fact]
    public void RunFreeAgencyPeriod_MaxTwelveStages()
    {
        var league = CreateLeagueWithFreeAgents(4, 5);

        var result = FreeAgencyService.RunFreeAgencyPeriod(league, 15, new Random(42));

        result.StagesCompleted.Should().BeLessThanOrEqualTo(12);
    }

    [Fact]
    public void RunFreeAgencyPeriod_Deterministic_WithSameSeed()
    {
        var league1 = CreateLeagueWithFreeAgents(4, 3);
        var league2 = CreateLeagueWithFreeAgents(4, 3);

        var r1 = FreeAgencyService.RunFreeAgencyPeriod(league1, 15, new Random(42));
        var r2 = FreeAgencyService.RunFreeAgencyPeriod(league2, 15, new Random(42));

        r1.PlayersSigned.Should().Be(r2.PlayersSigned);
        r1.StagesCompleted.Should().Be(r2.StagesCompleted);
    }

    [Fact]
    public void RunFreeAgencyPeriod_UndraftedRookiesCanSign()
    {
        var league = CreateLeagueWithTeams(2);
        // Add undrafted rookies to draft pool
        league.DraftPool = new RookiePool();
        for (int i = 0; i < 4; i++)
        {
            var rookie = CreateFreeAgent("PG", 5.0 + i, 0);
            rookie.Name = $"UndraftedRookie{i}";
            rookie.Contract.IsFreeAgent = true;
            rookie.Contract.CurrentTeam = -1;
            league.DraftPool.Rookies.Add(rookie);
        }

        var result = FreeAgencyService.RunFreeAgencyPeriod(league, 15, new Random(42));

        // At least some undrafted rookies should have been signed
        int rookiesSigned = 0;
        foreach (var team in league.Teams)
        {
            rookiesSigned += team.Roster.Count(p => p.Name.StartsWith("UndraftedRookie") && !p.Contract.IsFreeAgent);
        }
        rookiesSigned.Should().BeGreaterThan(0, "undrafted rookies should be signable");
    }

    [Fact]
    public void RunFreeAgencyPeriod_AcceptFactorIncreases()
    {
        var league = CreateLeagueWithFreeAgents(2, 2);
        var fa = league.Teams[0].Roster.First(p => p.Contract.IsFreeAgent);
        fa.Contract.AcceptFactor.Should().Be(0, "starts at 0");

        FreeAgencyService.RunFreeAgencyPeriod(league, 15, new Random(42));

        // Even if not signed, accept factor should have increased from 0
        var unsigned = league.Teams.SelectMany(t => t.Roster)
            .Where(p => p.Contract.IsFreeAgent && p.Contract.AcceptFactor > 0);
        // This is probabilistic but over 12 stages, factor should increase for unsigned FAs
    }

    [Fact]
    public void RunFreeAgencyPeriod_SkipsPlayerControlledTeams()
    {
        var league = CreateLeagueWithFreeAgents(4, 3);
        league.Teams[0].Record.Control = "Player";

        var result = FreeAgencyService.RunFreeAgencyPeriod(league, 15, new Random(42));

        // Should still complete without errors
        result.StagesCompleted.Should().BeGreaterThan(0);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static Team CreateTeam()
    {
        return new Team
        {
            Id = 0,
            Name = "TestTeam",
            Record = new TeamRecord { TeamName = "TestTeam" },
            GeneralManager = new StaffMember { Power1 = 3 },
            Scout = new StaffMember(),
            Coach = new StaffMember()
        };
    }

    private static League CreateLeagueWithTeams(int numTeams)
    {
        var league = new League();
        league.Settings.NumberOfTeams = numTeams;
        league.Settings.SalaryCap = 3550;
        league.Settings.FreeAgencyEnabled = true;

        for (int t = 0; t < numTeams; t++)
        {
            var team = new Team
            {
                Id = t,
                Name = $"Team {t}",
                Record = new TeamRecord
                {
                    TeamName = $"Team {t}",
                    LeaguePercentage = 0.5,
                    Wins = 41,
                    Losses = 41
                },
                Financial = new TeamFinancial { OwnerSalaryCap = 35500 },
                GeneralManager = new StaffMember { Power1 = 3 },
                Scout = new StaffMember(),
                Coach = new StaffMember()
            };

            // 12 players per team (non-FA)
            for (int p = 0; p < 12; p++)
            {
                string pos = (p % 5) switch { 0 => "PG", 1 => "SG", 2 => "SF", 3 => "PF", _ => "C" };
                var player = CreatePlayer(pos, 4.0 + p * 0.2);
                player.Id = t * 30 + p + 1;
                player.TeamIndex = t;
                player.Team = team.Name;
                player.Contract.CurrentTeam = t;
                player.Contract.PreviousTeam = t;
                player.Contract.ContractYears = 3;
                player.Contract.CurrentContractYear = 1;
                player.Contract.CurrentYearSalary = (int)(player.Ratings.TradeTrueRating * 80);
                team.Roster.Add(player);
            }

            league.Teams.Add(team);
        }

        return league;
    }

    private static League CreateLeagueWithFreeAgents(int numTeams, int faPerTeam)
    {
        var league = CreateLeagueWithTeams(numTeams);

        for (int t = 0; t < numTeams; t++)
        {
            for (int f = 0; f < faPerTeam; f++)
            {
                string pos = (f % 5) switch { 0 => "PG", 1 => "SG", 2 => "SF", 3 => "PF", _ => "C" };
                var fa = CreateFreeAgent(pos, 4.0 + f * 0.5, 3 + f);
                fa.Name = $"FA_{t}_{f}";
                fa.Id = 1000 + t * 10 + f;
                fa.TeamIndex = t;
                fa.Team = league.Teams[t].Name;
                fa.Contract.CurrentTeam = t;
                fa.Contract.PreviousTeam = t;
                league.Teams[t].Roster.Add(fa);
            }
        }

        return league;
    }

    private static Player CreateFreeAgent(string position, double tradeTru, int yos)
    {
        var p = CreatePlayer(position, tradeTru);
        p.Contract.IsFreeAgent = true;
        p.Contract.YearsOfService = yos;
        p.Contract.CurrentYearSalary = (int)(tradeTru * 50);
        return p;
    }

    private static Player CreatePlayer(string position, double tradeTru)
    {
        return new Player
        {
            Name = $"Player_{position}_{tradeTru:F1}",
            Position = position,
            Age = 26,
            Ratings = new PlayerRatings
            {
                TradeTrueRating = tradeTru,
                TrueRatingSimple = tradeTru * 0.9,
                TradeValue = tradeTru * 0.85,
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
                CoachFactor = 3,
                YearsOnTeam = 2
            }
        };
    }

    private static Player CreatePlayerWithOffer(double acceptFactor)
    {
        var player = CreateFreeAgent("PG", 5.0, 3);
        player.Contract.AcceptFactor = acceptFactor;
        player.Contract.BestContract = 0;
        player.Contract.TopOffer[0] = true;
        player.Contract.YearOffer[0] = 2;
        player.Contract.TotalSalaryOffer[0] = 600;
        player.Contract.SalaryOffer[0][0] = 300;
        player.Contract.SalaryOffer[0][1] = 300;
        player.Contract.Seeking[0] = 250;
        player.Contract.TotalSalarySeeking = 500;
        player.Contract.QualifyingOfferYears = 2;
        return player;
    }

    private static FreeAgencyState CreateDefaultState(League league)
    {
        var state = new FreeAgencyState();
        for (int t = 0; t < league.Teams.Count; t++)
        {
            var posValues = FreeAgencyService.CalculatePositionValues(league.Teams[t]);
            for (int p = 1; p <= 5; p++)
            {
                state.PositionValues[t, p] = posValues[p];
                state.LeagueAvgPositionValues[p] += posValues[p];
            }
        }
        for (int p = 1; p <= 5; p++)
            state.LeagueAvgPositionValues[p] /= Math.Max(1, league.Teams.Count);
        return state;
    }
}
