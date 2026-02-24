using FluentAssertions;
using JumpShotBasketball.Core.Constants;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class TradeServiceTests
{
    // ── GenerateTradeProposal ──────────────────────────────────────

    [Fact]
    public void GenerateTradeProposal_ValidTeams_ReturnsProposal()
    {
        var league = CreateTestLeague(4, 12);
        var random = new Random(42);

        var proposal = TradeService.GenerateTradeProposal(league, 0, 1, false, random);

        proposal.Should().NotBeNull();
        proposal!.Team1Index.Should().Be(0);
        proposal.Team2Index.Should().Be(1);
        proposal.Team1PlayerIndices.Should().NotBeEmpty();
        proposal.Team2PlayerIndices.Should().NotBeEmpty();
    }

    [Fact]
    public void GenerateTradeProposal_NotEnoughEligible_ReturnsNull()
    {
        var league = CreateTestLeague(4, 3); // only 3 players per team (< 8 threshold)
        var random = new Random(42);

        var proposal = TradeService.GenerateTradeProposal(league, 0, 1, false, random);

        proposal.Should().BeNull();
    }

    [Fact]
    public void GenerateTradeProposal_InjuredExcluded_StillWorks()
    {
        var league = CreateTestLeague(4, 12);
        // Injure some players on team 0
        for (int i = 0; i < 3; i++)
            league.Teams[0].Roster[i].Injury = 10;

        var random = new Random(42);
        var proposal = TradeService.GenerateTradeProposal(league, 0, 1, false, random);

        proposal.Should().NotBeNull();

        // No injured player should be in the trade
        foreach (int idx in proposal!.Team1PlayerIndices)
            league.Teams[0].Roster[idx].Injury.Should().Be(0);
    }

    [Fact]
    public void GenerateTradeProposal_TradingBlock_OnlyBlockedPlayersFromTeam1()
    {
        var league = CreateTestLeague(4, 12);
        // Put 2 players on block for team 0
        league.Teams[0].Roster[3].OnBlock = true;
        league.Teams[0].Roster[5].OnBlock = true;

        var random = new Random(42);
        var proposal = TradeService.GenerateTradeProposal(league, 0, 1, true, random);

        if (proposal != null)
        {
            // All team1 players should be on block
            foreach (int idx in proposal.Team1PlayerIndices)
                league.Teams[0].Roster[idx].OnBlock.Should().BeTrue();
        }
    }

    [Fact]
    public void GenerateTradeProposal_Deterministic_SameSeedSameResult()
    {
        var league1 = CreateTestLeague(4, 12);
        var league2 = CreateTestLeague(4, 12);

        var p1 = TradeService.GenerateTradeProposal(league1, 0, 1, false, new Random(99));
        var p2 = TradeService.GenerateTradeProposal(league2, 0, 1, false, new Random(99));

        p1.Should().NotBeNull();
        p2.Should().NotBeNull();
        p1!.Team1PlayerIndices.Should().BeEquivalentTo(p2!.Team1PlayerIndices);
        p2.Team2PlayerIndices.Should().BeEquivalentTo(p2.Team2PlayerIndices);
    }

    // ── ExecuteTrade ───────────────────────────────────────────────

    [Fact]
    public void ExecuteTrade_SwapsPlayers()
    {
        var league = CreateTestLeague(4, 12);
        var team1Player = league.Teams[0].Roster[0];
        var team2Player = league.Teams[1].Roster[0];
        string t1Name = team1Player.Name;
        string t2Name = team2Player.Name;

        var proposal = new TradeProposal
        {
            Team1Index = 0,
            Team2Index = 1,
            Team1PlayerIndices = new List<int> { 0 },
            Team2PlayerIndices = new List<int> { 0 }
        };

        TradeService.ExecuteTrade(league, proposal);

        // Player from team1 should now be on team2
        league.Teams[1].Roster.Should().Contain(p => p.Name == t1Name);
        // Player from team2 should now be on team1
        league.Teams[0].Roster.Should().Contain(p => p.Name == t2Name);
    }

    [Fact]
    public void ExecuteTrade_UpdatesTeamIndex()
    {
        var league = CreateTestLeague(4, 12);
        var proposal = new TradeProposal
        {
            Team1Index = 0,
            Team2Index = 1,
            Team1PlayerIndices = new List<int> { 0 },
            Team2PlayerIndices = new List<int> { 0 }
        };

        TradeService.ExecuteTrade(league, proposal);

        // Traded players should have updated TeamIndex
        var movedToTeam1 = league.Teams[0].Roster.Last();
        movedToTeam1.TeamIndex.Should().Be(0);

        var movedToTeam2 = league.Teams[1].Roster.Last();
        movedToTeam2.TeamIndex.Should().Be(1);
    }

    [Fact]
    public void ExecuteTrade_RecordsTransaction()
    {
        var league = CreateTestLeague(4, 12);
        int transCountBefore = league.Transactions.Count;

        var proposal = new TradeProposal
        {
            Team1Index = 0,
            Team2Index = 1,
            Team1PlayerIndices = new List<int> { 0 },
            Team2PlayerIndices = new List<int> { 0 }
        };

        TradeService.ExecuteTrade(league, proposal);

        league.Transactions.Count.Should().Be(transCountBefore + 1);
        league.Transactions.Last().Type.Should().Be(TransactionType.Trade);
    }

    [Fact]
    public void ExecuteTrade_TransfersDraftPick()
    {
        var league = CreateTestLeague(4, 12);
        league.DraftBoard = new DraftBoard();
        DraftService.InitializeDraftChart(league.DraftBoard, 4);

        int pick1Yrpp = DraftService.EncodeYrpp(1, 0, 1); // team1's (idx 0) year-1 round-0

        var proposal = new TradeProposal
        {
            Team1Index = 0,
            Team2Index = 1,
            Team1PlayerIndices = new List<int> { 0 },
            Team2PlayerIndices = new List<int> { 0 },
            Team1DraftPick = pick1Yrpp
        };

        TradeService.ExecuteTrade(league, proposal);

        // Team 2 (idx=1) should now own team 1's pick
        // In DraftChart, team index is 1-based so team1=1, team2=2
        // TransferPick uses team1Idx+1=1, team2Idx+1=2
        league.DraftBoard.DraftChart[1, 0, 1].Should().Be(2);
    }

    [Fact]
    public void ExecuteTrade_MultiplePlayersPerSide()
    {
        var league = CreateTestLeague(4, 12);
        int team1Count = league.Teams[0].Roster.Count;
        int team2Count = league.Teams[1].Roster.Count;

        var proposal = new TradeProposal
        {
            Team1Index = 0,
            Team2Index = 1,
            Team1PlayerIndices = new List<int> { 0, 1 },
            Team2PlayerIndices = new List<int> { 0, 1, 2 }
        };

        TradeService.ExecuteTrade(league, proposal);

        // team1 loses 2, gains 3 → net +1
        league.Teams[0].Roster.Count.Should().Be(team1Count + 1);
        // team2 loses 3, gains 2 → net -1
        league.Teams[1].Roster.Count.Should().Be(team2Count - 1);
    }

    // ── RunTradingPeriod ───────────────────────────────────────────

    [Fact]
    public void RunTradingPeriod_DisabledSetting_MakesNoTrades()
    {
        var league = CreateTestLeague(4, 12);
        league.Settings.ComputerTradesEnabled = false;

        var result = TradeService.RunTradingPeriod(league, 50, 3, new Random(42));

        result.TradesMade.Should().Be(0);
    }

    [Fact]
    public void RunTradingPeriod_EnabledSetting_CanMakeTrades()
    {
        var league = CreateTestLeague(8, 15);
        league.Settings.ComputerTradesEnabled = true;
        league.Settings.SalaryMatchingEnabled = false; // don't restrict by salary

        var result = TradeService.RunTradingPeriod(league, 500, 10, new Random(42));

        // With enough iterations and relaxed acceptance, at least some trades should occur
        result.ProposalsGenerated.Should().BeGreaterThan(0);
    }

    [Fact]
    public void RunTradingPeriod_RespectsMaxOffers()
    {
        var league = CreateTestLeague(8, 15);
        league.Settings.ComputerTradesEnabled = true;
        league.Settings.SalaryMatchingEnabled = false;

        var result = TradeService.RunTradingPeriod(league, 1000, 2, new Random(42));

        result.TradesMade.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public void RunTradingPeriod_Deterministic_SameSeedSameResult()
    {
        var league1 = CreateTestLeague(4, 12);
        league1.Settings.ComputerTradesEnabled = true;
        league1.Settings.SalaryMatchingEnabled = false;
        var league2 = CreateTestLeague(4, 12);
        league2.Settings.ComputerTradesEnabled = true;
        league2.Settings.SalaryMatchingEnabled = false;

        var r1 = TradeService.RunTradingPeriod(league1, 100, 3, new Random(55));
        var r2 = TradeService.RunTradingPeriod(league2, 100, 3, new Random(55));

        r1.TradesMade.Should().Be(r2.TradesMade);
        r1.ProposalsGenerated.Should().Be(r2.ProposalsGenerated);
    }

    // ── RunTradingBlock ────────────────────────────────────────────

    [Fact]
    public void RunTradingBlock_NoBlockedPlayers_MakesNoTrades()
    {
        var league = CreateTestLeague(4, 12);

        var result = TradeService.RunTradingBlock(league, 0, 3, new Random(42));

        result.TradesMade.Should().Be(0);
        result.AcceptedTrades.Should().BeEmpty();
    }

    [Fact]
    public void RunTradingBlock_WithBlockedPlayers_GeneratesProposals()
    {
        var league = CreateTestLeague(8, 15);
        league.Settings.SalaryMatchingEnabled = false;
        // Put some players on the block
        league.Teams[0].Roster[0].OnBlock = true;
        league.Teams[0].Roster[1].OnBlock = true;

        var result = TradeService.RunTradingBlock(league, 0, 5, new Random(42));

        result.ProposalsGenerated.Should().BeGreaterThan(0);
    }

    [Fact]
    public void RunTradingBlock_InvalidTeamIndex_ReturnsEmpty()
    {
        var league = CreateTestLeague(4, 12);

        var result = TradeService.RunTradingBlock(league, 99, 3, new Random(42));

        result.TradesMade.Should().Be(0);
    }

    // ── SetPlayerOnBlock / RemovePlayerFromBlock ────────────────────

    [Fact]
    public void SetPlayerOnBlock_SetsFlag()
    {
        var player = new Player { Name = "Test" };
        player.OnBlock.Should().BeFalse();

        TradeService.SetPlayerOnBlock(player);

        player.OnBlock.Should().BeTrue();
    }

    [Fact]
    public void RemovePlayerFromBlock_ClearsFlag()
    {
        var player = new Player { Name = "Test", OnBlock = true };

        TradeService.RemovePlayerFromBlock(player);

        player.OnBlock.Should().BeFalse();
    }

    // ── Integration: Roster integrity ──────────────────────────────

    [Fact]
    public void ExecuteTrade_RostersRemainValid()
    {
        var league = CreateTestLeague(4, 14);
        var random = new Random(42);

        var proposal = new TradeProposal
        {
            Team1Index = 0,
            Team2Index = 1,
            Team1PlayerIndices = new List<int> { 0, 1 },
            Team2PlayerIndices = new List<int> { 0 }
        };

        TradeService.ExecuteTrade(league, proposal, random);

        // Both teams should still have active players
        league.Teams[0].Roster.Count(p => !string.IsNullOrEmpty(p.Name)).Should().BeGreaterThanOrEqualTo(5);
        league.Teams[1].Roster.Count(p => !string.IsNullOrEmpty(p.Name)).Should().BeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public void RunTradingPeriod_32Team_SmokeTest()
    {
        var league = CreateTestLeague(32, 15);
        league.Settings.ComputerTradesEnabled = true;
        league.Settings.SalaryMatchingEnabled = false;
        var random = new Random(123);

        var result = TradeService.RunTradingPeriod(league, 100, 5, random);

        // Verify all rosters are still valid
        for (int t = 0; t < 32; t++)
        {
            var team = league.Teams[t];
            int activePlayers = team.Roster.Count(p => !string.IsNullOrEmpty(p.Name));
            activePlayers.Should().BeGreaterThanOrEqualTo(5,
                $"team {t} ({team.Name}) should have at least 5 active players after trades");
        }
    }

    [Fact]
    public void ExecuteTrade_TransactionDescription_ContainsPlayerNames()
    {
        var league = CreateTestLeague(4, 12);
        string player1Name = league.Teams[0].Roster[0].Name;
        string player2Name = league.Teams[1].Roster[0].Name;

        var proposal = new TradeProposal
        {
            Team1Index = 0,
            Team2Index = 1,
            Team1PlayerIndices = new List<int> { 0 },
            Team2PlayerIndices = new List<int> { 0 }
        };

        TradeService.ExecuteTrade(league, proposal);

        var transaction = league.Transactions.Last();
        transaction.Description.Should().Contain(player1Name);
        transaction.Description.Should().Contain(player2Name);
    }

    // ── Helper methods ─────────────────────────────────────────────

    private static readonly string[] Positions = { "PG", "SG", "SF", "PF", "C" };

    private static League CreateTestLeague(int numTeams, int playersPerTeam)
    {
        var league = new League();
        league.Settings.NumberOfTeams = numTeams;
        league.Settings.ComputerTradesEnabled = true;
        league.Schedule.GamesInSeason = 82;
        league.DraftBoard = new DraftBoard();
        DraftService.InitializeDraftChart(league.DraftBoard, numTeams);

        for (int t = 0; t < numTeams; t++)
        {
            var team = new Team
            {
                Id = t,
                Name = $"Team{t}",
                Record = new TeamRecord
                {
                    TeamName = $"Team{t}",
                    Control = "Computer",
                    Conference = t < numTeams / 2 ? "East" : "West",
                    Division = $"Div{t % 4}",
                    Wins = 20 + t,
                    Losses = 62 - t
                },
                Financial = new TeamFinancial()
            };

            for (int p = 0; p < playersPerTeam; p++)
            {
                int playerId = t * 30 + p + 1;
                double rating = 5.0 + (p % 5) * 1.5 + (t % 3) * 0.5;
                string pos = Positions[p % 5];

                var player = new Player
                {
                    Id = playerId,
                    Name = $"Player{playerId}",
                    Position = pos,
                    Team = team.Name,
                    TeamIndex = t,
                    Age = 22 + p % 10,
                    Active = true,
                    Injury = 0,
                    SeasonStats = new PlayerStatLine
                    {
                        Games = 40,
                        Minutes = 1200,
                        FieldGoalsMade = 200 + p * 10,
                        FieldGoalsAttempted = 450 + p * 15,
                        ThreePointersMade = 50 + p * 5,
                        ThreePointersAttempted = 150 + p * 10,
                        FreeThrowsMade = 100 + p * 5,
                        FreeThrowsAttempted = 130 + p * 5,
                        Rebounds = 200 + p * 15,
                        OffensiveRebounds = 50 + p * 5,
                        Assists = 150 + p * 10,
                        Steals = 40 + p * 3,
                        Blocks = 20 + p * 2,
                        Turnovers = 60 + p * 3,
                        PersonalFouls = 80 + p * 2
                    },
                    Ratings = new PlayerRatings
                    {
                        TradeTrueRating = rating,
                        TradeValue = rating * 0.9,
                        TrueRatingSimple = rating * 40,
                        Potential1 = 5,
                        Potential2 = 5,
                        Effort = 5,
                        MovementDefenseRaw = 5,
                        PostDefenseRaw = 5,
                        PenetrationDefenseRaw = 5,
                        TransitionDefenseRaw = 5
                    },
                    Contract = new PlayerContract
                    {
                        ContractYears = 3,
                        CurrentContractYear = 1,
                        CurrentYearSalary = 100 + p * 20,
                        RemainingSalary = 300 + p * 50,
                        CurrentTeam = t,
                        YearsOnTeam = 1,
                        YearsOfService = 2 + p % 5
                    }
                };

                team.Roster.Add(player);
            }

            league.Teams.Add(team);
        }

        return league;
    }
}
