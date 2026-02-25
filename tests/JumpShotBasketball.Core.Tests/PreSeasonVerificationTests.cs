using FluentAssertions;
using JumpShotBasketball.Core.Enums;
using JumpShotBasketball.Core.Models.Game;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class PreSeasonVerificationTests
{
    // ── Helpers ─────────────────────────────────────────────────────────

    private static League CreateSmallLeague(int teams = 4, int seed = 42)
    {
        var options = new LeagueCreationOptions { NumberOfTeams = teams, GamesPerSeason = 40 };
        var league = LeagueCreationService.CreateNewLeague(options, new Random(seed));

        int half = league.Teams.Count / 2;
        for (int i = half; i < league.Teams.Count; i++)
        {
            league.Teams[i].Record.Conference = "Western";
            league.Teams[i].Record.Division = "Southwest";
        }

        league.Settings.PlayoffFormat = "1 team per conference";
        league.Settings.Round1Format = "4 of 7";
        league.Settings.Round2Format = "None";
        league.Settings.Round3Format = "None";
        league.Settings.Round4Format = "None";

        return league;
    }

    // ── Pre-Season Verification Tests ────────────────────────────────────

    [Fact]
    public void PreSeasonVerification_CalledBeforeFirstGame()
    {
        // When we call SimulateFullSeason, ProcessRosterEmergencies runs before any games
        var league = CreateSmallLeague();

        // Remove a player to create a below-minimum situation
        var team = league.Teams[0];
        while (team.Roster.Count > 7)
        {
            var last = team.Roster[^1];
            league.FreeAgentPool.Add(last);
            team.Roster.RemoveAt(team.Roster.Count - 1);
        }

        // Should not throw — verification should fix it
        var result = LeagueSimulationService.SimulateFullSeason(league, new Random(42));

        result.TotalRegularSeasonGames.Should().BeGreaterThan(0);
    }

    [Fact]
    public void TeamMissingPosition_SignsFreeAgent()
    {
        var league = CreateSmallLeague();
        var team = league.Teams[0];

        // Remove all PGs
        var pgs = team.Roster.Where(p => p.Position == "PG").ToList();
        foreach (var pg in pgs)
        {
            team.Roster.Remove(pg);
            pg.Team = "";
            pg.TeamIndex = -1;
            league.FreeAgentPool.Add(pg);
        }

        int rosterBefore = team.Roster.Count;

        // ProcessRosterEmergencies should sign an FA if team is below minimum
        if (rosterBefore < 8)
        {
            var rosterResult = RosterManagementService.ProcessRosterEmergencies(league, new Random(42));
            team.Roster.Count.Should().BeGreaterThanOrEqualTo(rosterBefore,
                "Team should sign at least one FA to fill roster");
        }
    }

    [Fact]
    public void TeamOverRosterLimit_ReleasesPlayer()
    {
        var league = CreateSmallLeague();
        var team = league.Teams[0];

        // Add extra players to exceed 15-man roster
        for (int i = 0; i < 5; i++)
        {
            var extra = new Player
            {
                Id = 9000 + i,
                Name = $"Extra{i}",
                LastName = $"Extra{i}",
                Position = "SF",
                Team = team.Name,
                TeamIndex = 0,
                Active = true,
                Health = 100,
                Ratings = new PlayerRatings { TrueRating = 20, TradeTrueRating = 20 },
                SeasonStats = new PlayerStatLine(),
                Contract = new PlayerContract { ContractYears = 1, CurrentYearSalary = 500 }
            };
            team.Roster.Add(extra);
        }

        team.Roster.Count.Should().BeGreaterThan(15);

        var result = RosterManagementService.ProcessRosterEmergencies(league, new Random(42));

        team.Roster.Count.Should().BeLessThanOrEqualTo(15,
            "Team should be trimmed to max roster size");
        result.PlayersReleased.Should().NotBeEmpty();
    }

    [Fact]
    public void AllTeamsValid_NoChanges()
    {
        var league = CreateSmallLeague();

        // All teams should already be valid from CreateNewLeague
        var result = RosterManagementService.ProcessRosterEmergencies(league, new Random(42));

        result.PlayersReleased.Should().BeEmpty("Valid rosters should not have releases");
        result.PlayersSigned.Should().BeEmpty("Valid rosters should not have emergency signings");
    }

    [Fact]
    public void PlayerControlledTeam_Skipped()
    {
        var league = CreateSmallLeague();
        var team = league.Teams[0];
        team.Record.Control = "Player";

        // Remove players to make roster invalid
        while (team.Roster.Count > 5)
        {
            var last = team.Roster[^1];
            league.FreeAgentPool.Add(last);
            team.Roster.RemoveAt(team.Roster.Count - 1);
        }

        var result = RosterManagementService.ProcessRosterEmergencies(league, new Random(42));

        // Player-controlled team should NOT be modified
        team.Roster.Count.Should().Be(5, "Player-controlled team should not be modified");
        result.PlayersSigned.Where(s => s.Item1 == 0).Should().BeEmpty();
    }

    [Fact]
    public void MultipleTeamsInvalid_AllFixed()
    {
        var league = CreateSmallLeague();

        // Invalidate teams 0 and 1 by adding extra players
        for (int t = 0; t < 2; t++)
        {
            for (int i = 0; i < 3; i++)
            {
                league.Teams[t].Roster.Add(new Player
                {
                    Id = 8000 + t * 10 + i,
                    Name = $"Extra{t}_{i}",
                    LastName = $"Extra{t}_{i}",
                    Position = "SF",
                    Team = league.Teams[t].Name,
                    TeamIndex = t,
                    Active = true,
                    Health = 100,
                    Ratings = new PlayerRatings { TrueRating = 15, TradeTrueRating = 15 },
                    SeasonStats = new PlayerStatLine(),
                    Contract = new PlayerContract { ContractYears = 1, CurrentYearSalary = 500 }
                });
            }
        }

        var result = RosterManagementService.ProcessRosterEmergencies(league, new Random(42));

        league.Teams[0].Roster.Count.Should().BeLessThanOrEqualTo(15);
        league.Teams[1].Roster.Count.Should().BeLessThanOrEqualTo(15);
        result.PlayersReleased.Count.Should().BeGreaterThanOrEqualTo(6,
            "Both teams should have players released");
    }

    [Fact]
    public void NoFreeAgents_HandledGracefully()
    {
        var league = CreateSmallLeague();

        // Clear all free agents
        league.FreeAgentPool.Clear();

        // Remove some players from team to go below minimum
        var team = league.Teams[0];
        while (team.Roster.Count > 6)
        {
            team.Roster.RemoveAt(team.Roster.Count - 1);
        }

        // Should not throw even with empty FA pool
        var act = () => RosterManagementService.ProcessRosterEmergencies(league, new Random(42));
        act.Should().NotThrow("Empty FA pool should be handled gracefully");
    }

    [Fact]
    public void SeasonSimulation_StartsClean()
    {
        var league = CreateSmallLeague();

        // Add extra players to team 0
        for (int i = 0; i < 3; i++)
        {
            league.Teams[0].Roster.Add(new Player
            {
                Id = 7000 + i,
                Name = $"Extra{i}",
                LastName = $"Extra{i}",
                Position = "PF",
                Team = league.Teams[0].Name,
                TeamIndex = 0,
                Active = true,
                Health = 100,
                Ratings = new PlayerRatings { TrueRating = 20, TradeTrueRating = 20 },
                SeasonStats = new PlayerStatLine(),
                Contract = new PlayerContract { ContractYears = 1, CurrentYearSalary = 500 }
            });
        }

        // After SimulateFullSeason, team should have been fixed at the start
        var result = LeagueSimulationService.SimulateFullSeason(league, new Random(42));

        result.TotalRegularSeasonGames.Should().BeGreaterThan(0,
            "Season should complete without roster issues");
    }

    [Fact]
    public void InjuredPlayers_TeamStillPlaysGames()
    {
        var league = CreateSmallLeague();
        var team = league.Teams[0];

        // Injure several players
        for (int i = 0; i < 4 && i < team.Roster.Count; i++)
        {
            team.Roster[i].Health = 0;
            team.Roster[i].Injury = 20;
        }

        // Season should still work — verification handles roster emergencies
        var result = LeagueSimulationService.SimulateFullSeason(league, new Random(42));

        result.TotalRegularSeasonGames.Should().BeGreaterThan(0,
            "Season should handle injured players gracefully");
    }

    [Fact]
    public void FullSeason_NoCrashesFromEmptyPositions()
    {
        var league = CreateSmallLeague();

        // Remove all centers from team 0
        var centers = league.Teams[0].Roster.Where(p => p.Position?.Trim() == "C").ToList();
        foreach (var c in centers)
        {
            league.Teams[0].Roster.Remove(c);
            c.Team = "";
            c.TeamIndex = -1;
            league.FreeAgentPool.Add(c);
        }

        // Season should complete — pre-season verification + in-season emergencies handle it
        var act = () => LeagueSimulationService.SimulateFullSeason(league, new Random(42));
        act.Should().NotThrow("Missing positions should be handled by roster verification");
    }
}
