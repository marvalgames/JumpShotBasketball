using JumpShotBasketball.Core.Constants;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// AI trade generation, execution, and trading block management.
/// Port of Computer.cpp Trade() (41-853), WriteTransaction() (933-1041),
/// SetOwnDraftPicks() (1292-1327), and TradeBlockDlg.cpp (256-320).
/// </summary>
public static class TradeService
{
    /// <summary>
    /// Runs an AI trading period: generates random trade proposals and executes accepted ones.
    /// Port of CComputer::Trade() — Computer.cpp:41-853.
    /// </summary>
    public static TradeResult RunTradingPeriod(
        League league, int loops = 100, int maxOffers = 3, Random? random = null)
    {
        random ??= Random.Shared;
        var result = new TradeResult();

        if (!league.Settings.ComputerTradesEnabled) return result;

        int numTeams = league.Teams.Count;
        if (numTeams < 2) return result;

        int offers = 0;

        for (int times = 0; times < loops; times++)
        {
            if (offers >= maxOffers) break;

            // Pick two random teams
            int team1Idx, team2Idx;
            do
            {
                team1Idx = random.Next(numTeams);
                team2Idx = random.Next(numTeams);
            } while (team1Idx == team2Idx);

            // Skip player-vs-player trades
            if (league.Teams[team1Idx].Record.Control == "Player" &&
                league.Teams[team2Idx].Record.Control == "Player")
                continue;

            var proposal = GenerateTradeProposal(league, team1Idx, team2Idx, false, random);
            if (proposal == null)
            {
                result.ProposalsGenerated++;
                result.ProposalsRejected++;
                continue;
            }

            result.ProposalsGenerated++;

            // Check salary matching
            if (league.Settings.SalaryMatchingEnabled)
            {
                proposal.SalaryFit = TradeEvaluationService.CheckSalaryMatching(
                    proposal.Team1Salary, proposal.Team2Salary);
            }
            else
            {
                proposal.SalaryFit = true;
            }

            if (!proposal.SalaryFit)
            {
                result.ProposalsRejected++;
                continue;
            }

            // Check acceptance
            string team1Control = league.Teams[team1Idx].Record.Control;
            string team2Control = league.Teams[team2Idx].Record.Control;

            bool accepted = TradeEvaluationService.EvaluateAcceptance(
                proposal.Team1OldTrue, proposal.Team1NewTrue,
                proposal.Team2OldTrue, proposal.Team2NewTrue,
                team1Control, team2Control);

            if (!accepted)
            {
                result.ProposalsRejected++;
                continue;
            }

            // Execute the trade
            ExecuteTrade(league, proposal, random);
            result.TradesMade++;
            result.AcceptedTrades.Add(proposal);
            offers++;
        }

        return result;
    }

    /// <summary>
    /// Generates a random trade proposal between two teams.
    /// Port of Computer.cpp:102-332 (team selection, player picking, value calculation).
    /// Returns null if not enough eligible players.
    /// </summary>
    public static TradeProposal? GenerateTradeProposal(
        League league, int team1Idx, int team2Idx,
        bool useTradingBlock, Random random)
    {
        var team1 = league.Teams[team1Idx];
        var team2 = league.Teams[team2Idx];

        // Build eligible player pools (active, not injured, has minutes/games, max 15)
        var eligible1 = BuildEligiblePool(team1.Roster);
        var eligible2 = BuildEligiblePool(team2.Roster);

        if (eligible1.Count < 8 || eligible2.Count < 8)
            return null;

        // Determine number of players per side (1-4, biased toward fewer)
        int players1 = random.Next(1, 3); // 1 or 2
        if (players1 == 2) players1 = random.Next(1, 5); // 1-4
        int players2 = random.Next(1, 3);
        if (players2 == 2) players2 = random.Next(1, 5);

        // Count trading block players if applicable
        int playersOnBlock = 0;
        if (useTradingBlock)
        {
            playersOnBlock = eligible1.Count(p => p.OnBlock);
            if (playersOnBlock > 0 && players1 > playersOnBlock)
            {
                players1 = random.Next(1, playersOnBlock + 1);
                if (players1 > 2) players1 = random.Next(1, playersOnBlock + 1);
            }
        }

        // Select random players from each team
        var selected1 = SelectRandomPlayers(eligible1, players1, useTradingBlock && playersOnBlock > 0, random);
        var selected2 = SelectRandomPlayers(eligible2, players2, false, random);

        if (selected1.Count == 0 || selected2.Count == 0)
            return null;

        // Calculate old roster values
        double oldTrue1 = TradeEvaluationService.CalculateRosterValue(team1.Roster);
        double oldTrue2 = TradeEvaluationService.CalculateRosterValue(team2.Roster);

        // Calculate trade values and salaries for selected players
        int salary1 = 0, salary2 = 0;
        double totalTrue1InTrade = 0, totalSalary1InTrade = 0;
        double totalTrue2InTrade = 0, totalSalary2InTrade = 0;

        var team1Indices = new List<int>();
        var team2Indices = new List<int>();

        foreach (var p in selected1)
        {
            int idx = team1.Roster.IndexOf(p);
            if (idx < 0) continue;
            team1Indices.Add(idx);
            salary1 += p.Contract.CurrentYearSalary;

            var (trueC, cappedSal) = TradeEvaluationService.CalculatePlayerTradeWorth(p);
            totalTrue1InTrade += trueC;
            totalSalary1InTrade += cappedSal;
        }

        foreach (var p in selected2)
        {
            int idx = team2.Roster.IndexOf(p);
            if (idx < 0) continue;
            team2Indices.Add(idx);
            salary2 += p.Contract.CurrentYearSalary;

            var (trueC, cappedSal) = TradeEvaluationService.CalculatePlayerTradeWorth(p);
            totalTrue2InTrade += trueC;
            totalSalary2InTrade += cappedSal;
        }

        // Draft picks (1-in-4 chance each side gets a pick)
        int pick1Yrpp = 0, pick2Yrpp = 0;
        double pick1Value = 0, pick2Value = 0;
        int numTeams = league.Teams.Count;

        if (league.DraftBoard != null)
        {
            // Team 1 offering a pick
            if (random.Next(4) == 0)
            {
                var ownedPicks = TradeEvaluationService.CollectTeamOwnedPicks(
                    league.DraftBoard, team1Idx + 1, numTeams);
                if (ownedPicks.Count > 0)
                {
                    int pickIdx = random.Next(ownedPicks.Count);
                    int candidatePick = ownedPicks[pickIdx];

                    // Validate receiving team won't exceed 6 picks in that year
                    if (TradeEvaluationService.ValidateDraftPickLimits(
                        league.DraftBoard, team2Idx + 1, candidatePick, numTeams))
                    {
                        pick1Yrpp = candidatePick;
                        var (pY, pR, pT) = DraftService.DecodeYrpp(candidatePick);
                        int pWins = 0, pLosses = 0;
                        if (pT > 0 && pT <= numTeams)
                        {
                            pWins = league.Teams[pT - 1].Record.Wins;
                            pLosses = league.Teams[pT - 1].Record.Losses;
                        }
                        double avgTru = CalculateLeagueAvgTru(league);
                        double teamAvgTru = CalculateTeamAvgTru(team1);
                        int gamesInSeason = league.Schedule.GamesInSeason > 0
                            ? league.Schedule.GamesInSeason : 82;
                        pick1Value = TradeEvaluationService.CalculateDraftPickTradeValue(
                            league.DraftBoard, candidatePick, avgTru, teamAvgTru,
                            pWins, pLosses, gamesInSeason);
                    }
                }
            }

            // Team 2 offering a pick
            if (random.Next(4) == 0)
            {
                var ownedPicks = TradeEvaluationService.CollectTeamOwnedPicks(
                    league.DraftBoard, team2Idx + 1, numTeams);
                if (ownedPicks.Count > 0)
                {
                    int pickIdx = random.Next(ownedPicks.Count);
                    int candidatePick = ownedPicks[pickIdx];

                    if (TradeEvaluationService.ValidateDraftPickLimits(
                        league.DraftBoard, team1Idx + 1, candidatePick, numTeams))
                    {
                        pick2Yrpp = candidatePick;
                        var (pY, pR, pT) = DraftService.DecodeYrpp(candidatePick);
                        int pWins = 0, pLosses = 0;
                        if (pT > 0 && pT <= numTeams)
                        {
                            pWins = league.Teams[pT - 1].Record.Wins;
                            pLosses = league.Teams[pT - 1].Record.Losses;
                        }
                        double avgTru = CalculateLeagueAvgTru(league);
                        double teamAvgTru = CalculateTeamAvgTru(team2);
                        int gamesInSeason = league.Schedule.GamesInSeason > 0
                            ? league.Schedule.GamesInSeason : 82;
                        pick2Value = TradeEvaluationService.CalculateDraftPickTradeValue(
                            league.DraftBoard, candidatePick, avgTru, teamAvgTru,
                            pWins, pLosses, gamesInSeason);
                    }
                }
            }
        }

        // Calculate value factors
        double teamValue1 = team1.Financial.CurrentValue > 0
            ? (double)team1.Financial.CurrentValue : LeagueConstants.DefaultCurrentValue;
        double teamValue2 = team2.Financial.CurrentValue > 0
            ? (double)team2.Financial.CurrentValue : LeagueConstants.DefaultCurrentValue;

        double valueFactor1 = TradeEvaluationService.CalculateValueFactor(
            totalTrue1InTrade, (int)totalSalary1InTrade, teamValue1);
        double valueFactor2 = TradeEvaluationService.CalculateValueFactor(
            totalTrue2InTrade, (int)totalSalary2InTrade, teamValue2);

        // Calculate new roster values (simulate the swap)
        double newTrue1 = CalculatePostTradeRosterValue(team1, team2, team1Indices, team2Indices);
        double newTrue2 = CalculatePostTradeRosterValue(team2, team1, team2Indices, team1Indices);

        // Apply pick difference and value factor adjustments (Computer.cpp:720-725)
        double pickDiff = pick2Value - pick1Value;
        double valueDiff = (valueFactor1 - valueFactor2) / 2.0;

        newTrue1 += pickDiff / 2.0 + valueDiff;
        newTrue2 -= pickDiff / 2.0 + valueDiff;

        return new TradeProposal
        {
            Team1Index = team1Idx,
            Team2Index = team2Idx,
            Team1PlayerIndices = team1Indices,
            Team2PlayerIndices = team2Indices,
            Team1DraftPick = pick1Yrpp,
            Team2DraftPick = pick2Yrpp,
            Team1OldTrue = oldTrue1,
            Team2OldTrue = oldTrue2,
            Team1NewTrue = newTrue1,
            Team2NewTrue = newTrue2,
            Team1Salary = salary1,
            Team2Salary = salary2,
            Team1PickValue = pick1Value,
            Team2PickValue = pick2Value,
            Team1ValueFactor = valueFactor1,
            Team2ValueFactor = valueFactor2,
            SalaryFit = true // checked later in RunTradingPeriod
        };
    }

    /// <summary>
    /// Executes an accepted trade: swaps players between rosters, transfers picks,
    /// fills positional needs from within roster, and records the transaction.
    /// Port of Computer.cpp:780-832.
    /// </summary>
    public static void ExecuteTrade(League league, TradeProposal proposal, Random? random = null)
    {
        random ??= Random.Shared;

        var team1 = league.Teams[proposal.Team1Index];
        var team2 = league.Teams[proposal.Team2Index];

        // Collect players to swap (snapshot before modifying)
        var playersFromTeam1 = proposal.Team1PlayerIndices
            .Where(i => i >= 0 && i < team1.Roster.Count)
            .Select(i => team1.Roster[i])
            .ToList();

        var playersFromTeam2 = proposal.Team2PlayerIndices
            .Where(i => i >= 0 && i < team2.Roster.Count)
            .Select(i => team2.Roster[i])
            .ToList();

        // Remove traded players from their teams
        foreach (var p in playersFromTeam1)
            team1.Roster.Remove(p);
        foreach (var p in playersFromTeam2)
            team2.Roster.Remove(p);

        // Add received players to new teams
        foreach (var p in playersFromTeam2)
        {
            p.Contract.PreviousTeam = p.TeamIndex;
            p.TeamIndex = proposal.Team1Index;
            p.Team = team1.Name;
            p.Contract.YearsOnTeam = 0;
            p.Active = true;
            team1.Roster.Add(p);
        }

        foreach (var p in playersFromTeam1)
        {
            p.Contract.PreviousTeam = p.TeamIndex;
            p.TeamIndex = proposal.Team2Index;
            p.Team = team2.Name;
            p.Contract.YearsOnTeam = 0;
            p.Active = true;
            team2.Roster.Add(p);
        }

        // Transfer draft picks
        if (proposal.Team1DraftPick != 0 && league.DraftBoard != null)
        {
            var (y, r, p) = DraftService.DecodeYrpp(proposal.Team1DraftPick);
            DraftService.TransferPick(league.DraftBoard, proposal.Team1Index + 1,
                proposal.Team2Index + 1, y, r);
        }
        if (proposal.Team2DraftPick != 0 && league.DraftBoard != null)
        {
            var (y, r, p) = DraftService.DecodeYrpp(proposal.Team2DraftPick);
            DraftService.TransferPick(league.DraftBoard, proposal.Team2Index + 1,
                proposal.Team1Index + 1, y, r);
        }

        // Ensure both teams have minimum roster requirements
        // Trim excess players (cap at 15 active)
        TrimRoster(team1, 15);
        TrimRoster(team2, 15);

        // Record the transaction
        RecordTradeTransaction(league, proposal, playersFromTeam1, playersFromTeam2);

        // Reset rotations for affected teams
        RecalculateTeamRatings(team1);
        RecalculateTeamRatings(team2);
    }

    /// <summary>
    /// Runs trading block mode: generates trade proposals only for a specific team's
    /// blocked players. Port of TradeBlockDlg.cpp:256-320.
    /// </summary>
    public static TradeResult RunTradingBlock(
        League league, int teamIndex, int maxOffers = 3, Random? random = null)
    {
        random ??= Random.Shared;
        var result = new TradeResult();

        if (teamIndex < 0 || teamIndex >= league.Teams.Count) return result;

        int numTeams = league.Teams.Count;
        int offers = 0;

        // Check if any players are on the trading block
        var team = league.Teams[teamIndex];
        bool hasBlockedPlayers = team.Roster.Any(p =>
            !string.IsNullOrEmpty(p.Name) && p.OnBlock && p.Injury == 0 && p.Active);

        if (!hasBlockedPlayers) return result;

        for (int times = 0; times < 200; times++) // more iterations for block mode
        {
            if (offers >= maxOffers) break;

            // Pick a random opponent
            int opponentIdx;
            do
            {
                opponentIdx = random.Next(numTeams);
            } while (opponentIdx == teamIndex);

            // Skip if opponent is player-controlled
            if (league.Teams[opponentIdx].Record.Control == "Player") continue;

            var proposal = GenerateTradeProposal(league, teamIndex, opponentIdx, true, random);
            if (proposal == null)
            {
                result.ProposalsGenerated++;
                result.ProposalsRejected++;
                continue;
            }

            result.ProposalsGenerated++;

            // Check salary matching
            if (league.Settings.SalaryMatchingEnabled)
            {
                proposal.SalaryFit = TradeEvaluationService.CheckSalaryMatching(
                    proposal.Team1Salary, proposal.Team2Salary);
            }
            else
            {
                proposal.SalaryFit = true;
            }

            if (!proposal.SalaryFit)
            {
                result.ProposalsRejected++;
                continue;
            }

            // For trading block, use the player-to-computer acceptance thresholds
            string team1Control = league.Teams[teamIndex].Record.Control;
            string team2Control = league.Teams[opponentIdx].Record.Control;

            bool accepted = TradeEvaluationService.EvaluateAcceptance(
                proposal.Team1OldTrue, proposal.Team1NewTrue,
                proposal.Team2OldTrue, proposal.Team2NewTrue,
                team1Control, team2Control);

            if (!accepted)
            {
                result.ProposalsRejected++;
                continue;
            }

            result.AcceptedTrades.Add(proposal);
            offers++;
            // Don't auto-execute in block mode — return proposals for player review
        }

        return result;
    }

    /// <summary>
    /// Sets a player on the trading block.
    /// </summary>
    public static void SetPlayerOnBlock(Player player)
    {
        player.OnBlock = true;
    }

    /// <summary>
    /// Removes a player from the trading block.
    /// </summary>
    public static void RemovePlayerFromBlock(Player player)
    {
        player.OnBlock = false;
    }

    // ── Private helpers ──────────────────────────────────────────

    private static List<Player> BuildEligiblePool(List<Player> roster)
    {
        return roster
            .Where(p => !string.IsNullOrEmpty(p.Name)
                     && p.Active
                     && p.Injury == 0
                     && p.SeasonStats.Minutes > 0
                     && p.SeasonStats.Games > 0)
            .OrderByDescending(p => p.Ratings.TradeTrueRating)
            .Take(15)
            .ToList();
    }

    private static List<Player> SelectRandomPlayers(
        List<Player> eligible, int count, bool onlyBlocked, Random random)
    {
        var pool = onlyBlocked
            ? eligible.Where(p => p.OnBlock).ToList()
            : eligible;

        if (pool.Count == 0) return new List<Player>();

        count = Math.Min(count, pool.Count);
        var selected = new List<Player>();
        var used = new HashSet<int>();

        for (int i = 0; i < count; i++)
        {
            int attempts = 0;
            int idx;
            do
            {
                idx = random.Next(pool.Count);
                attempts++;
            } while (used.Contains(idx) && attempts < 50);

            if (used.Contains(idx)) continue;

            used.Add(idx);
            selected.Add(pool[idx]);
        }

        return selected;
    }

    /// <summary>
    /// Calculates what a team's roster value would be after trading away
    /// outgoing players and receiving incoming players.
    /// </summary>
    private static double CalculatePostTradeRosterValue(
        Models.Team.Team team, Models.Team.Team otherTeam,
        List<int> outgoingIndices, List<int> incomingIndices)
    {
        // Build a virtual roster: current minus outgoing plus incoming
        var outgoing = new HashSet<int>(outgoingIndices);
        var virtualRoster = new List<Player>();

        for (int i = 0; i < team.Roster.Count; i++)
        {
            if (!outgoing.Contains(i))
                virtualRoster.Add(team.Roster[i]);
        }

        foreach (int idx in incomingIndices)
        {
            if (idx >= 0 && idx < otherTeam.Roster.Count)
                virtualRoster.Add(otherTeam.Roster[idx]);
        }

        return TradeEvaluationService.CalculateRosterValue(virtualRoster);
    }

    private static void TrimRoster(Models.Team.Team team, int maxActive)
    {
        var active = team.Roster
            .Where(p => !string.IsNullOrEmpty(p.Name))
            .OrderByDescending(p => p.Ratings.TradeTrueRating)
            .ToList();

        if (active.Count > maxActive)
        {
            // Remove worst players beyond the limit
            var toRemove = active.Skip(maxActive).ToList();
            foreach (var p in toRemove)
            {
                p.Active = false;
                team.Roster.Remove(p);
            }
        }
    }

    private static void RecordTradeTransaction(
        League league, TradeProposal proposal,
        List<Player> fromTeam1, List<Player> fromTeam2)
    {
        var playersInvolved = new List<int>();
        playersInvolved.AddRange(fromTeam1.Select(p => p.Id));
        playersInvolved.AddRange(fromTeam2.Select(p => p.Id));

        var picksInvolved = new List<int>();
        if (proposal.Team1DraftPick != 0) picksInvolved.Add(proposal.Team1DraftPick);
        if (proposal.Team2DraftPick != 0) picksInvolved.Add(proposal.Team2DraftPick);

        string team1Name = league.Teams[proposal.Team1Index].Name;
        string team2Name = league.Teams[proposal.Team2Index].Name;

        // Build description
        var desc = new System.Text.StringBuilder();
        desc.Append($"{team1Name} traded ");
        desc.Append(string.Join(", ", fromTeam1.Select(p => $"{p.Position} {p.Name}")));
        if (proposal.Team1DraftPick != 0)
        {
            var (y, r, _) = DraftService.DecodeYrpp(proposal.Team1DraftPick);
            desc.Append($", Year {y} Round {r + 1} pick");
        }
        desc.Append($" to {team2Name} for ");
        desc.Append(string.Join(", ", fromTeam2.Select(p => $"{p.Position} {p.Name}")));
        if (proposal.Team2DraftPick != 0)
        {
            var (y, r, _) = DraftService.DecodeYrpp(proposal.Team2DraftPick);
            desc.Append($", Year {y} Round {r + 1} pick");
        }

        league.Settings.NumberOfTransactions++;

        league.Transactions.Add(new Transaction
        {
            Id = league.Settings.NumberOfTransactions,
            Type = TransactionType.Trade,
            Description = desc.ToString(),
            TeamIndex1 = proposal.Team1Index,
            TeamIndex2 = proposal.Team2Index,
            Team1Name = team1Name,
            Team2Name = team2Name,
            PlayersInvolved = playersInvolved,
            DraftPicksInvolved = picksInvolved
        });
    }

    private static void RecalculateTeamRatings(Models.Team.Team team)
    {
        foreach (var p in team.Roster)
        {
            if (string.IsNullOrEmpty(p.Name)) continue;
            if (p.SeasonStats.Games > 0)
                StatisticsCalculator.CalculateAllRatings(p);
        }
    }

    private static double CalculateLeagueAvgTru(League league)
    {
        var allPlayers = league.Teams
            .SelectMany(t => t.Roster)
            .Where(p => !string.IsNullOrEmpty(p.Name) && p.Active && p.SeasonStats.Games > 0)
            .ToList();

        if (allPlayers.Count == 0) return 5.0;
        return allPlayers.Average(p => p.Ratings.TradeTrueRating);
    }

    private static double CalculateTeamAvgTru(Models.Team.Team team)
    {
        var players = team.Roster
            .Where(p => !string.IsNullOrEmpty(p.Name) && p.Active && p.SeasonStats.Games > 0)
            .ToList();

        if (players.Count == 0) return 5.0;
        return players.Average(p => p.Ratings.TradeTrueRating);
    }
}
