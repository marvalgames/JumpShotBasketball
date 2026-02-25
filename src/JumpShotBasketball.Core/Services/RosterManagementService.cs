using JumpShotBasketball.Core.Constants;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Team;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// In-season roster management: emergency releases and free agent signings.
/// Port of CComputer::SignFreeAgent() / CComputer::ReleasePlayer() (Computer.cpp:1044-1290)
/// and CAverage::VerifyRosters() (Average.cpp:1764-1815).
/// </summary>
public static class RosterManagementService
{
    private const int MaxRosterSize = 15;
    private const int MinActivePlayers = 8;

    /// <summary>
    /// Processes roster emergencies for all computer-controlled teams.
    /// For teams over max roster: releases the worst player.
    /// For teams under min active: signs the best available free agent.
    /// Port of the VerifyRosters → SignFreeAgent/ReleasePlayer pipeline.
    /// </summary>
    public static RosterManagementResult ProcessRosterEmergencies(League league, Random random)
    {
        var result = new RosterManagementResult();

        for (int t = 0; t < league.Teams.Count; t++)
        {
            var team = league.Teams[t];

            // Skip player-controlled teams
            if (team.Record.Control == "Player") continue;

            // Over max roster: release worst player(s)
            while (team.Roster.Count > MaxRosterSize)
            {
                string? released = ReleaseWorstPlayer(league, team, t);
                if (released != null)
                {
                    result.PlayersReleased.Add((t, released));

                    // Record transaction
                    league.Transactions.Add(new Transaction
                    {
                        Id = league.Transactions.Count + 1,
                        Type = TransactionType.Waiver,
                        Description = $"{team.Name} released {released}",
                        TeamIndex1 = t,
                        Team1Name = team.Name
                    });
                }
                else
                {
                    break; // No valid candidate to release
                }
            }

            // Under min active players: sign free agents
            int activePlayers = CountActivePlayers(team);
            while (activePlayers < MinActivePlayers && HasAvailableFreeAgents(league))
            {
                int neededPosition = PositionNeeded(team);
                var freeAgent = FindBestFreeAgent(league, team, neededPosition);

                if (freeAgent == null) break;

                SignEmergencyFreeAgent(league, team, t, freeAgent);
                result.PlayersSigned.Add((t, freeAgent.Name));

                // Record transaction
                league.Transactions.Add(new Transaction
                {
                    Id = league.Transactions.Count + 1,
                    Type = TransactionType.Signing,
                    Description = $"{team.Name} signed FA {freeAgent.Name}",
                    TeamIndex1 = t,
                    Team1Name = team.Name
                });

                activePlayers = CountActivePlayers(team);
            }
        }

        return result;
    }

    /// <summary>
    /// Releases the worst player on the team.
    /// Port of CComputer::ReleasePlayer() (Computer.cpp:1245-1275).
    /// Finds the player with lowest TradeTrueRating among non-injured, non-just-signed players.
    /// </summary>
    public static string? ReleaseWorstPlayer(League league, Team team, int teamIndex)
    {
        Player? worst = null;
        double lowestRating = double.MaxValue;

        foreach (var player in team.Roster)
        {
            if (string.IsNullOrEmpty(player.Name)) continue;
            if (player.Injury > 0) continue; // Don't release injured players
            if (player.Contract.JustSigned) continue; // Don't release just-signed players

            double rating = player.Ratings.TradeTrueRating;
            if (rating < lowestRating)
            {
                lowestRating = rating;
                worst = player;
            }
        }

        if (worst == null) return null;

        string releasedName = worst.Name;

        // Remove from roster
        team.Roster.Remove(worst);

        // Clear contract — mark as free agent
        worst.Contract.IsFreeAgent = true;
        worst.Contract.ContractYears = 0;
        worst.Contract.CurrentContractYear = 0;
        worst.Contract.CurrentYearSalary = 0;

        // Add to free agent pool
        league.FreeAgentPool.Add(worst);

        return releasedName;
    }

    /// <summary>
    /// Finds the best free agent at the needed position (or any position as fallback).
    /// Port of CComputer::BestFreeAgent() (Computer.cpp:1100-1145).
    /// Bug fix: C++ never updates the 'high' tracking variable — always picks the last
    /// matching FA instead of the best. Fixed to track actual best rating.
    /// </summary>
    public static Player? FindBestFreeAgent(League league, Team team, int neededPosition)
    {
        if (league.FreeAgentPool.Count == 0) return null;

        Player? bestPositionMatch = null;
        double bestPositionRating = -1;

        Player? bestAnyPosition = null;
        double bestAnyRating = -1;

        foreach (var fa in league.FreeAgentPool)
        {
            if (string.IsNullOrEmpty(fa.Name)) continue;
            if (fa.Retired) continue;

            double rating = fa.Ratings.TradeTrueRating;

            // Track best at any position (fallback)
            if (rating > bestAnyRating)
            {
                bestAnyRating = rating;
                bestAnyPosition = fa;
            }

            // Track best at needed position
            int faPosition = PositionToIndex(fa.Position);
            if (faPosition == neededPosition && rating > bestPositionRating)
            {
                bestPositionRating = rating;
                bestPositionMatch = fa;
            }
        }

        // Prefer position match, fall back to any position
        return bestPositionMatch ?? bestAnyPosition;
    }

    /// <summary>
    /// Determines which position the team needs most.
    /// Port of CComputer::PositionNeeded() (Computer.cpp:1060-1098).
    /// Counts active (non-injured) players at each position.
    /// Returns the position index (1=PG, 2=SG, 3=SF, 4=PF, 5=C) with fewest active players.
    /// Tie-break: prefer C > PF > PG > SF > SG (matches C++ order).
    /// </summary>
    public static int PositionNeeded(Team team)
    {
        int[] positionCounts = new int[6]; // [1]=PG, [2]=SG, [3]=SF, [4]=PF, [5]=C

        foreach (var player in team.Roster)
        {
            if (string.IsNullOrEmpty(player.Name)) continue;
            if (player.Injury > 0) continue; // Skip injured players

            int posIdx = PositionToIndex(player.Position);
            if (posIdx >= 1 && posIdx <= 5)
                positionCounts[posIdx]++;
        }

        // Find position with fewest active players
        // Tie-break order: C(5) > PF(4) > PG(1) > SF(3) > SG(2)
        int[] tiebreakOrder = { 5, 4, 1, 3, 2 };
        int bestPosition = 5; // Default to center
        int lowestCount = int.MaxValue;

        foreach (int pos in tiebreakOrder)
        {
            if (positionCounts[pos] < lowestCount)
            {
                lowestCount = positionCounts[pos];
                bestPosition = pos;
            }
        }

        return bestPosition;
    }

    /// <summary>
    /// Signs a free agent to the team with an emergency minimum contract.
    /// Port of CComputer::AddFreeAgentToTeam() (Computer.cpp:1160-1200).
    /// </summary>
    public static void SignEmergencyFreeAgent(League league, Team team, int teamIndex, Player freeAgent)
    {
        // Remove from free agent pool
        league.FreeAgentPool.Remove(freeAgent);

        // Add to team roster
        team.Roster.Add(freeAgent);

        // Assign emergency contract
        AssignEmergencyContract(freeAgent, teamIndex, team.Name);

        // Set JustSigned to prevent immediate re-release
        freeAgent.Contract.JustSigned = true;

        // Update player team info
        freeAgent.TeamIndex = teamIndex;
        freeAgent.Team = team.Name;
        freeAgent.Active = true;
    }

    /// <summary>
    /// Assigns a 1-year minimum salary emergency contract.
    /// </summary>
    public static void AssignEmergencyContract(Player player, int teamId, string teamName)
    {
        var contract = player.Contract;
        int yosIdx = Math.Min(contract.YearsOfService, 10);
        int minimumSalary = LeagueConstants.SalaryMinimumByYos[yosIdx];

        contract.IsFreeAgent = false;
        contract.ContractYears = 1;
        contract.CurrentContractYear = 1;
        contract.CurrentYearSalary = minimumSalary;
        contract.TotalSalary = minimumSalary;
        contract.RemainingSalary = minimumSalary;
        contract.CurrentTeam = teamId;
        contract.PreviousTeam = teamId;
        contract.TeamPaying = teamId;
        contract.TeamPayingName = teamName;
        contract.Signed = true;

        // Set contract salary array
        for (int i = 0; i < contract.ContractSalaries.Length; i++)
            contract.ContractSalaries[i] = 0;
        contract.ContractSalaries[0] = minimumSalary;
    }

    /// <summary>
    /// Returns count of non-injured players on a team.
    /// </summary>
    public static int CountActivePlayers(Team team)
    {
        int count = 0;
        foreach (var p in team.Roster)
        {
            if (string.IsNullOrEmpty(p.Name)) continue;
            if (p.Injury <= 0)
                count++;
        }
        return count;
    }

    /// <summary>
    /// Returns true if the league's free agent pool has any available players.
    /// </summary>
    public static bool HasAvailableFreeAgents(League league)
    {
        return league.FreeAgentPool.Count > 0;
    }

    private static int PositionToIndex(string? position)
    {
        return position?.Trim() switch
        {
            "PG" => 1,
            "SG" => 2,
            "SF" => 3,
            "PF" => 4,
            "C" => 5,
            _ => 0
        };
    }
}
