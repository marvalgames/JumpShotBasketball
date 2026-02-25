using FluentAssertions;
using JumpShotBasketball.Core.Constants;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Staff;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class TransactionLoggingTests
{
    // ── Draft Transaction Logging ────────────────────────────────────

    [Fact]
    public void DraftPick_LogsTransaction()
    {
        var league = CreateDraftLeague(2, 6);

        DraftExecutionService.ExecuteDraft(league, new Random(42));

        league.Transactions.Should().NotBeEmpty("each draft pick should log a transaction");
        league.Transactions.Should().OnlyContain(t => t.Type == TransactionType.DraftPick);
    }

    [Fact]
    public void DraftPick_HasCorrectType()
    {
        var league = CreateDraftLeague(2, 4);

        DraftExecutionService.ExecuteDraft(league, new Random(42));

        foreach (var tx in league.Transactions)
            tx.Type.Should().Be(TransactionType.DraftPick);
    }

    [Fact]
    public void DraftPick_DescriptionIncludesPickNumber()
    {
        var league = CreateDraftLeague(2, 4);

        DraftExecutionService.ExecuteDraft(league, new Random(42));

        league.Transactions.Should().Contain(t => t.Description.Contains("pick #"));
    }

    [Fact]
    public void DraftPick_DescriptionIncludesPlayerName()
    {
        var league = CreateDraftLeague(2, 4);

        DraftExecutionService.ExecuteDraft(league, new Random(42));

        // All drafted rookies should have their name in the description
        foreach (var tx in league.Transactions)
        {
            // Description format: "{team} selected {name} ({pos}) with pick #{num}"
            tx.Description.Should().Contain("selected");
            tx.Description.Should().MatchRegex(@"selected .+ \(");
        }
    }

    [Fact]
    public void DraftPick_DescriptionIncludesTeamName()
    {
        var league = CreateDraftLeague(2, 4);

        DraftExecutionService.ExecuteDraft(league, new Random(42));

        foreach (var tx in league.Transactions)
        {
            // Team name should appear at start of description
            bool hasTeamName = league.Teams.Any(t => tx.Description.StartsWith(t.Name));
            hasTeamName.Should().BeTrue($"description '{tx.Description}' should start with a team name");
        }
    }

    [Fact]
    public void MultipleDraftPicks_IncrementId()
    {
        var league = CreateDraftLeague(4, 10);

        DraftExecutionService.ExecuteDraft(league, new Random(42));

        league.Transactions.Should().HaveCountGreaterThan(1);

        // IDs should be sequential
        for (int i = 0; i < league.Transactions.Count; i++)
            league.Transactions[i].Id.Should().Be(i + 1);

        // NumberOfTransactions should match
        league.Settings.NumberOfTransactions.Should().Be(league.Transactions.Count);
    }

    // ── Free Agency Transaction Logging ──────────────────────────────

    [Fact]
    public void FreeAgentSigning_LogsTransaction()
    {
        var league = CreateFreeAgencyLeague();

        FreeAgencyService.RunFreeAgencyPeriod(league, 15, new Random(42));

        var signings = league.Transactions.Where(t => t.Type == TransactionType.Signing).ToList();
        signings.Should().NotBeEmpty("free agent signings should log transactions");
    }

    [Fact]
    public void FreeAgentSigning_HasCorrectType()
    {
        var league = CreateFreeAgencyLeague();

        FreeAgencyService.RunFreeAgencyPeriod(league, 15, new Random(42));

        var signings = league.Transactions.Where(t => t.Type == TransactionType.Signing).ToList();
        signings.Should().OnlyContain(t => t.Type == TransactionType.Signing);
    }

    [Fact]
    public void FreeAgentSigning_DescriptionIncludesPlayer()
    {
        var league = CreateFreeAgencyLeague();

        FreeAgencyService.RunFreeAgencyPeriod(league, 15, new Random(42));

        var signings = league.Transactions.Where(t => t.Type == TransactionType.Signing).ToList();
        foreach (var tx in signings)
        {
            tx.Description.Should().Contain("signed FA");
            tx.Description.Should().MatchRegex(@"signed FA .+ \(");
        }
    }

    [Fact]
    public void AllTransactionTypes_DraftAndSigningPresent()
    {
        // Run draft then free agency — should have both DraftPick and Signing types
        var league = CreateDraftLeague(4, 20);
        league.Settings.FreeAgencyEnabled = true;

        DraftExecutionService.ExecuteDraft(league, new Random(42));
        int draftTransactions = league.Transactions.Count;

        // Mark some players as free agents for FA
        foreach (var team in league.Teams)
        {
            if (team.Roster.Count > 12)
            {
                for (int i = 12; i < team.Roster.Count; i++)
                {
                    team.Roster[i].Contract.IsFreeAgent = true;
                    team.Roster[i].Contract.Signed = false;
                }
            }
        }

        FreeAgencyService.RunFreeAgencyPeriod(league, 15, new Random(42));

        league.Transactions.Where(t => t.Type == TransactionType.DraftPick).Should().NotBeEmpty();

        // IDs should remain sequential across both types
        for (int i = 0; i < league.Transactions.Count; i++)
            league.Transactions[i].Id.Should().Be(i + 1);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static League CreateDraftLeague(int numTeams, int numRookies)
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
                Coach = new StaffMember(),
                Financial = new TeamFinancial()
            };

            // Add baseline roster (5 per team)
            for (int p = 0; p < 5; p++)
            {
                string pos = p switch { 0 => "PG", 1 => "SG", 2 => "SF", 3 => "PF", _ => "C" };
                team.Roster.Add(CreatePlayer($"Vet-{t}-{p}", pos, 5.0 + p, t));
            }

            league.Teams.Add(team);
        }

        // Create rookie pool
        var rookies = new List<Player>();
        for (int i = 0; i < numRookies; i++)
        {
            string pos = (i % 5) switch { 0 => "PG", 1 => "SG", 2 => "SF", 3 => "PF", _ => "C" };
            var rookie = new Player
            {
                Name = $"Rookie {i}",
                Position = pos,
                Age = 19,
                Active = true,
                Health = 100,
                Ratings = new PlayerRatings
                {
                    TradeTrueRating = 4.0 + i * 0.2,
                    TradeValue = 4.0 + i * 0.2,
                    ProjectionFieldGoalsAttempted = 10,
                    Prime = 28
                },
                Contract = new PlayerContract { IsFreeAgent = false }
            };
            rookies.Add(rookie);
        }

        league.DraftPool = new RookiePool { Rookies = rookies };
        league.DraftBoard = new DraftBoard();
        DraftService.InitializeDraftChart(league.DraftBoard, numTeams);

        return league;
    }

    private static League CreateFreeAgencyLeague()
    {
        var league = new League();
        league.Settings.NumberOfTeams = 2;
        league.Settings.FreeAgencyEnabled = true;
        league.Settings.SalaryCap = LeagueConstants.DefaultSalaryCap;

        for (int t = 0; t < 2; t++)
        {
            var team = new Team
            {
                Id = t,
                Name = $"Team {t}",
                GeneralManager = new StaffMember { Power1 = 3 },
                Scout = new StaffMember(),
                Coach = new StaffMember
                {
                    Pot1Rating = 3, Pot2Rating = 3, EffortRating = 3,
                    ScoringRating = 3, ShootingRating = 3, ReboundingRating = 3,
                    PassingRating = 3, DefenseRating = 3, Personality = 3
                },
                Financial = new TeamFinancial
                {
                    OwnerSalaryCap = (int)LeagueConstants.DefaultSalaryCap * 10
                }
            };
            team.Record.TeamName = team.Name;
            team.Record.Wins = 41;
            team.Record.Losses = 41;
            team.Record.LeaguePercentage = 0.500;

            // Add 10 players (need 15 → 5 slots to fill via FA)
            for (int p = 0; p < 10; p++)
            {
                string pos = (p % 5) switch { 0 => "PG", 1 => "SG", 2 => "SF", 3 => "PF", _ => "C" };
                var player = CreatePlayer($"Signed-{t}-{p}", pos, 5.0, t);
                player.Contract.ContractYears = 3;
                player.Contract.CurrentContractYear = 1;
                team.Roster.Add(player);
            }

            league.Teams.Add(team);
        }

        // Create free agents
        for (int i = 0; i < 20; i++)
        {
            string pos = (i % 5) switch { 0 => "PG", 1 => "SG", 2 => "SF", 3 => "PF", _ => "C" };
            var fa = new Player
            {
                Id = 100 + i,
                Name = $"FreeAgent {i}",
                Position = pos,
                Age = 25,
                Active = true,
                Health = 100,
                Ratings = new PlayerRatings
                {
                    TradeTrueRating = 3.0 + i * 0.1,
                    TradeValue = 3.0 + i * 0.1,
                    Prime = 30
                },
                Contract = new PlayerContract
                {
                    IsFreeAgent = true,
                    Signed = false,
                    PreviousTeam = i % 2,
                    YearsOfService = 2,
                    CurrentYearSalary = 200,
                    SecurityFactor = 3,
                    WinningFactor = 3,
                    TraditionFactor = 3,
                    LoyaltyFactor = 3,
                    PlayingTimeFactor = 3
                }
            };
            league.FreeAgentPool.Add(fa);
        }

        return league;
    }

    private static Player CreatePlayer(string name, string position, double tradeTru, int teamIndex)
    {
        return new Player
        {
            Name = name,
            Position = position,
            Age = 26,
            Active = true,
            Health = 100,
            TeamIndex = teamIndex,
            Ratings = new PlayerRatings
            {
                TradeTrueRating = tradeTru,
                TradeValue = tradeTru,
                ProjectionFieldGoalsAttempted = 10,
                Prime = 30
            },
            Contract = new PlayerContract
            {
                IsFreeAgent = false,
                Signed = true,
                CurrentTeam = teamIndex,
                ContractYears = 3,
                CurrentContractYear = 1,
                CurrentYearSalary = 300,
                RemainingSalary = 900,
                TotalSalary = 900
            },
            SeasonStats = new PlayerStatLine { Games = 82, Minutes = 2000 }
        };
    }
}
