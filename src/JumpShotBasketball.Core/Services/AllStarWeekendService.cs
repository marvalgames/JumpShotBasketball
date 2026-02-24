using System;
using JumpShotBasketball.Core.Enums;
using JumpShotBasketball.Core.Models.Game;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Team;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// All-Star Weekend: team selection, 3-Point/Dunk contest simulation, game orchestration.
/// Port of C++ CAllStarWeekend (~778 lines) + SetAswScores (~25 lines).
/// All methods are static and pure — no global state mutation.
/// </summary>
public static class AllStarWeekendService
{
    private static readonly string[] Positions = { "PG", "SG", "SF", "PF", "C" };

    // ─────────────────────────────────────────────────
    // Selection: All-Star Teams
    // ─────────────────────────────────────────────────

    /// <summary>
    /// Select All-Star teams by conference. Returns up to 12 per conference.
    /// Port of C++ SelectAllStarTeams (AllStarWeekend.cpp:41-152).
    /// </summary>
    public static (List<Player> Conference1, List<Player> Conference2) SelectAllStarTeams(
        Models.League.League league)
    {
        string conf1Name = league.Settings.ConferenceName1;
        string conf2Name = league.Settings.ConferenceName2;
        if (string.IsNullOrEmpty(conf2Name)) conf2Name = conf1Name;

        // Build scored+sorted candidate list
        var candidates = GetAllEligiblePlayers(league)
            .Select(p => (Player: p, Score: ComputeAllStarComposite(p)))
            .OrderByDescending(x => x.Score)
            .ToList();

        var conf1 = new List<Player>();
        var conf2 = new List<Player>();
        var selected = new HashSet<int>();

        // 6 passes: positions 1-5 (PG through C) then wildcard (pass 6)
        for (int pass = 1; pass <= 6; pass++)
        {
            foreach (var (player, _) in candidates)
            {
                if (selected.Contains(player.Id)) continue;

                string conference = GetPlayerConference(player, league);
                if (string.IsNullOrEmpty(conference)) continue;

                int posIndex = GetPositionIndex(player.Position);
                bool posMatch = pass == 6 || posIndex == pass;
                if (!posMatch) continue;

                if (conference == conf1Name && conf1.Count < pass * 2 && conf1.Count < 12)
                {
                    conf1.Add(player);
                    selected.Add(player.Id);
                }
                else if (conference == conf2Name && conf2.Count < pass * 2 && conf2.Count < 12)
                {
                    conf2.Add(player);
                    selected.Add(player.Id);
                }
            }
        }

        return (conf1, conf2);
    }

    /// <summary>
    /// Composite scoring for All-Star selection.
    /// sim_fgm*2 + sim_tfgm*3 + sim_ftm + sim_oreb + sim_reb + sim_ast + sim_stl + sim_blk,
    /// then multiplied by age factor (100 + age) / 128.
    /// </summary>
    internal static double ComputeAllStarComposite(Player player)
    {
        var s = player.SimulatedStats;
        double raw = s.FieldGoalsMade * 2 + s.ThreePointersMade * 3 + s.FreeThrowsMade
            + s.OffensiveRebounds + s.Rebounds + s.Assists + s.Steals + s.Blocks;
        double ageFactor = (100.0 + player.Age) / 128.0;
        double score = raw * ageFactor;
        if (score == 0) score = 0.001; // avoid zero ties
        return score;
    }

    // ─────────────────────────────────────────────────
    // Selection: Rookie/Sophomore Teams
    // ─────────────────────────────────────────────────

    /// <summary>
    /// Select Rookie vs Sophomore teams. Falls back to All-Defense game if not enough rookies/sophs.
    /// Port of C++ SelectRookieTeams (AllStarWeekend.cpp:154-340).
    /// </summary>
    public static (List<Player> Team1, List<Player> Team2, bool IsAllDefenseGame) SelectRookieTeams(
        Models.League.League league)
    {
        string conf1Name = league.Settings.ConferenceName1;
        string conf2Name = league.Settings.ConferenceName2;
        if (string.IsNullOrEmpty(conf2Name)) conf2Name = conf1Name;

        var eligible = GetAllEligiblePlayers(league);

        // First pass: score with rookie/soph bonus, count rookies and sophomores
        int rookieCount = eligible.Count(p => p.Contract.YearsOfService == 1);
        int sophCount = eligible.Count(p => p.Contract.YearsOfService == 2);

        bool useDefenseFallback = rookieCount < 9 || sophCount < 9;

        if (!useDefenseFallback)
        {
            // Normal mode: rookie/soph scoring with YOS bonus
            var scored = eligible
                .Select(p => (Player: p, Score: ComputeRookieComposite(p)))
                .OrderByDescending(x => x.Score)
                .ToList();

            var rookies = new List<Player>();
            var sophomores = new List<Player>();

            // 5 positional passes (PG through C)
            for (int pass = 1; pass <= 5; pass++)
            {
                foreach (var (player, _) in scored)
                {
                    int posIndex = GetPositionIndex(player.Position);
                    if (posIndex != pass) continue;

                    if (player.Contract.YearsOfService == 1 && rookies.Count < pass * 2)
                    {
                        rookies.Add(player);
                    }
                    else if (player.Contract.YearsOfService == 2 && sophomores.Count < pass * 2)
                    {
                        sophomores.Add(player);
                    }
                }
            }

            return (rookies, sophomores, false);
        }
        else
        {
            // Fallback: All-Defense scoring, conference-based split
            var scored = eligible
                .Select(p => (Player: p, Score: ComputeDefenseComposite(p)))
                .OrderByDescending(x => x.Score)
                .ToList();

            var team1 = new List<Player>();
            var team2 = new List<Player>();

            for (int pass = 1; pass <= 5; pass++)
            {
                foreach (var (player, _) in scored)
                {
                    string conference = GetPlayerConference(player, league);
                    int posIndex = GetPositionIndex(player.Position);
                    if (posIndex != pass) continue;

                    if (conference == conf1Name && team1.Count < pass * 2)
                    {
                        team1.Add(player);
                    }
                    else if (conference == conf2Name && team2.Count < pass * 2)
                    {
                        team2.Add(player);
                    }
                }
            }

            return (team1, team2, true);
        }
    }

    /// <summary>
    /// Rookie/Soph composite: base stats + 20000*(3-yrs) bonus for 1st/2nd year players.
    /// </summary>
    internal static double ComputeRookieComposite(Player player)
    {
        var s = player.SimulatedStats;
        double raw = s.FieldGoalsMade * 2 + s.ThreePointersMade * 3 + s.FreeThrowsMade
            + s.OffensiveRebounds + s.Rebounds + s.Assists + s.Steals + s.Blocks;
        int yrs = player.Contract.YearsOfService;
        if (yrs == 1 || yrs == 2) raw += 20000.0 * (3 - yrs);
        return raw;
    }

    /// <summary>
    /// All-Defense fallback scoring: (stl+blk)*4 + penDef + movDef + postDef + movDef/2.
    /// </summary>
    internal static double ComputeDefenseComposite(Player player)
    {
        var s = player.SimulatedStats;
        var r = player.Ratings;
        return (s.Steals + s.Blocks) * 4.0
            + r.PenetrationDefenseRaw + r.MovementDefenseRaw + r.PostDefenseRaw
            + r.MovementDefenseRaw / 2.0;
    }

    // ─────────────────────────────────────────────────
    // Selection: Contest Scoring
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 3-Point selection score. Port of C++ SetAswScores m_3ptscore (Player.cpp:2188-2193).
    /// Returns -1 if ineligible.
    /// </summary>
    public static int CalculateThreePointScore(Player player)
    {
        if (player.Injury > 0 || !player.Active || player.SimulatedStats.Games == 0
            || player.SimulatedStats.Minutes < 72 || string.IsNullOrEmpty(player.Name))
            return -1;

        double n1 = player.SimulatedStats.ThreePointersMade;
        double n2 = n1 > 0 ? n1 / (double)player.SimulatedStats.ThreePointersAttempted : 0;
        double score = n1 * n2 * (1.0 + player.Ratings.ProjectionThreePointersAttempted / 100.0) * 100;
        return (int)score;
    }

    /// <summary>
    /// Dunk selection score. Port of C++ SetAswScores m_dunkscore (Player.cpp:2196-2209).
    /// Returns -1 if ineligible.
    /// </summary>
    public static int CalculateDunkScore(Player player)
    {
        if (player.Injury > 0 || !player.Active || player.SimulatedStats.Games == 0
            || player.SimulatedStats.Minutes < 72 || string.IsNullOrEmpty(player.Name))
            return -1;

        var r = player.Ratings;
        if (r.ProjectionFieldGoalsAttempted == 0 || r.ProjectionDefensiveRebounds == 0)
            return -1;

        double f = (double)r.ProjectionFreeThrowsAttempted / r.ProjectionFieldGoalsAttempted
            + (double)r.ProjectionOffensiveRebounds / r.ProjectionDefensiveRebounds
            + (r.TransitionOffenseRaw + r.TransitionDefenseRaw) / 10.0;

        double bonus = 1.0;
        string pos = NormalizePosition(player.Position);
        if (pos == "SG" || pos == "SF") bonus = 1.2;
        else if (pos == "PF" || pos == "C") bonus = 0.8;

        return (int)(f * 100 * bonus);
    }

    /// <summary>Select top 8 three-point contestants.</summary>
    public static List<Player> SelectThreePointContestants(Models.League.League league)
    {
        return GetAllEligiblePlayers(league)
            .Select(p => (Player: p, Score: CalculateThreePointScore(p)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .DistinctBy(x => x.Player.Id)
            .Take(8)
            .Select(x => x.Player)
            .ToList();
    }

    /// <summary>Select top 8 dunk contestants.</summary>
    public static List<Player> SelectDunkContestants(Models.League.League league)
    {
        return GetAllEligiblePlayers(league)
            .Select(p => (Player: p, Score: CalculateDunkScore(p)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .DistinctBy(x => x.Player.Id)
            .Take(8)
            .Select(x => x.Player)
            .ToList();
    }

    // ─────────────────────────────────────────────────
    // Contest Simulation: 3-Point Contest
    // ─────────────────────────────────────────────────

    /// <summary>
    /// Simulate 3-Point Contest: 3 rounds with elimination (8→4→2→1).
    /// Port of C++ SimThreePointContestRoundOne/Two/Three (AllStarWeekend.cpp:418-561).
    /// </summary>
    public static List<ContestParticipant> SimulateThreePointContest(List<Player> contestants, Random random)
    {
        var participants = contestants.Take(8).Select(p => new ContestParticipant
        {
            PlayerId = p.Id,
            PlayerName = p.Name,
            RoundScores = new int[4]
            {
                CalculateThreePointScore(p), 0, 0, 0
            },
            HighestRoundReached = 1
        }).ToList();

        // Map contestant index to their Player for stat access (handle potential duplicate IDs)
        var playerLookup = contestants.Take(8)
            .GroupBy(p => p.Id)
            .ToDictionary(g => g.Key, g => g.First());

        // Round 1: all 8 shoot
        SimulateThreePointRound(participants, playerLookup, 1, random);
        participants = participants.OrderByDescending(p => p.RoundScores[1]).ToList();

        // Top 4 advance to Round 2
        var round2 = participants.Take(4).ToList();
        foreach (var p in round2) p.HighestRoundReached = 2;
        SimulateThreePointRound(round2, playerLookup, 2, random);
        round2 = round2.OrderByDescending(p => p.RoundScores[2]).ToList();

        // Top 2 advance to Round 3 (Finals)
        var round3 = round2.Take(2).ToList();
        foreach (var p in round3) p.HighestRoundReached = 3;
        SimulateThreePointRound(round3, playerLookup, 3, random);
        round3 = round3.OrderByDescending(p => p.RoundScores[3]).ToList();

        // Rebuild full list: R3 finalists first (sorted), then R2 eliminated, then R1 eliminated
        var r2Eliminated = round2.Skip(2).ToList();
        var r1Eliminated = participants.Skip(4).ToList();
        var finalOrder = round3.Concat(r2Eliminated).Concat(r1Eliminated).ToList();

        return finalOrder;
    }

    private static void SimulateThreePointRound(
        List<ContestParticipant> participants, Dictionary<int, Player> lookup, int round, Random random)
    {
        foreach (var p in participants)
        {
            if (!lookup.TryGetValue(p.PlayerId, out var player)) continue;
            var r = player.Ratings;

            // Probability per shot: (prTga * (prTgp / 50)) / 120
            double probability = (r.ProjectionThreePointersAttempted * (r.ProjectionThreePointPercentage / 50.0)) / 120.0;

            int score = 0;
            for (int shot = 1; shot <= 25; shot++)
            {
                double roll = random.NextDouble();
                if (roll <= probability)
                {
                    score++;
                    if (shot >= 21) score++; // Money ball: +2 total instead of +1
                }
            }

            p.RoundScores[round] = score;
        }
    }

    // ─────────────────────────────────────────────────
    // Contest Simulation: Dunk Contest
    // ─────────────────────────────────────────────────

    /// <summary>
    /// Simulate Dunk Contest: 2 rounds with elimination (8→3→1).
    /// Port of C++ SimSlamDunkContestRoundOne/Two (AllStarWeekend.cpp:563-672).
    /// </summary>
    public static List<ContestParticipant> SimulateDunkContest(List<Player> contestants, Random random)
    {
        var participants = contestants.Take(8).Select(p => new ContestParticipant
        {
            PlayerId = p.Id,
            PlayerName = p.Name,
            RoundScores = new int[4]
            {
                CalculateDunkScore(p), 0, 0, 0
            },
            HighestRoundReached = 1
        }).ToList();

        var playerLookup = contestants.Take(8)
            .GroupBy(p => p.Id)
            .ToDictionary(g => g.Key, g => g.First());

        // Round 1: all 8 dunk
        SimulateDunkRound(participants, playerLookup, 1, random);
        participants = participants.OrderByDescending(p => p.RoundScores[1]).ToList();

        // Top 3 advance to Round 2
        var round2 = participants.Take(3).ToList();
        foreach (var p in round2) p.HighestRoundReached = 2;
        SimulateDunkRound(round2, playerLookup, 2, random);
        round2 = round2.OrderByDescending(p => p.RoundScores[2]).ToList();

        // Rebuild: R2 finalists sorted, then R1 eliminated
        var r1Eliminated = participants.Skip(3).ToList();
        var finalOrder = round2.Concat(r1Eliminated).ToList();

        return finalOrder;
    }

    private static void SimulateDunkRound(
        List<ContestParticipant> participants, Dictionary<int, Player> lookup, int round, Random random)
    {
        foreach (var p in participants)
        {
            if (!lookup.TryGetValue(p.PlayerId, out var player)) continue;
            var r = player.Ratings;

            if (r.ProjectionFieldGoalsAttempted == 0 || r.ProjectionDefensiveRebounds == 0)
            {
                p.RoundScores[round] = 600; // minimum
                continue;
            }

            // Factor: prFta/prFga + prOrb/prDrb + (transOff+transDef)/10
            double f = (double)r.ProjectionFreeThrowsAttempted / r.ProjectionFieldGoalsAttempted
                + (double)r.ProjectionOffensiveRebounds / r.ProjectionDefensiveRebounds
                + (r.TransitionOffenseRaw + r.TransitionDefenseRaw) / 10.0;

            if (f > 3.6) f = 3.6 + f / 10.0;

            int hits = 60; // base
            for (int attempt = 1; attempt <= 50; attempt++)
            {
                double roll = random.NextDouble() * 6.0;
                if (roll <= f) hits++;
            }

            int score = hits * 10 + random.Next(10);
            if (score > 1000) score = 1000;

            p.RoundScores[round] = score;
        }
    }

    // ─────────────────────────────────────────────────
    // Game Simulation
    // ─────────────────────────────────────────────────

    /// <summary>
    /// Simulate the All-Star Game using temporary team objects.
    /// </summary>
    public static GameResult SimulateAllStarGame(
        Models.League.League league, List<Player> conf1, List<Player> conf2,
        Random random, LeagueAverages averages)
    {
        var team1 = BuildTemporaryTeam(conf1, league.Settings.ConferenceName1, 0);
        var team2 = BuildTemporaryTeam(conf2, league.Settings.ConferenceName2, 1);

        var engine = new GameSimulationEngine(random);
        return engine.SimulateGame(team1, team2, GameType.AllStar, averages,
            scoringFactor: league.Settings.ScoringFactor, homeCourtAdvantage: false);
    }

    /// <summary>
    /// Simulate the Rookie/Sophomore (or All-Defense) Game.
    /// </summary>
    public static GameResult SimulateRookieGame(
        Models.League.League league, List<Player> team1Players, List<Player> team2Players,
        bool isAllDefense, Random random, LeagueAverages averages)
    {
        string name1, name2;
        if (isAllDefense)
        {
            name1 = league.Settings.ConferenceName1;
            name2 = league.Settings.ConferenceName2;
        }
        else
        {
            name1 = "Rookies";
            name2 = "Sophomores";
        }

        var team1 = BuildTemporaryTeam(team1Players, name1, 0);
        var team2 = BuildTemporaryTeam(team2Players, name2, 1);

        var engine = new GameSimulationEngine(random);
        return engine.SimulateGame(team1, team2, GameType.Rookie, averages,
            scoringFactor: league.Settings.ScoringFactor, homeCourtAdvantage: false);
    }

    // ─────────────────────────────────────────────────
    // Orchestrator
    // ─────────────────────────────────────────────────

    /// <summary>
    /// Run the complete All-Star Weekend: selections, contests, games.
    /// </summary>
    public static AllStarWeekendResult RunAllStarWeekend(
        Models.League.League league, Random random, LeagueAverages averages)
    {
        var result = new AllStarWeekendResult
        {
            Conference1Name = league.Settings.ConferenceName1,
            Conference2Name = league.Settings.ConferenceName2
        };

        // 1. Select All-Star teams
        var (conf1, conf2) = SelectAllStarTeams(league);
        result.Conference1Roster = conf1.Select(p => p.Id).ToList();
        result.Conference2Roster = conf2.Select(p => p.Id).ToList();

        // 2. Select Rookie/Soph teams
        var (team1, team2, isAllDefense) = SelectRookieTeams(league);
        result.RookieRoster = team1.Select(p => p.Id).ToList();
        result.SophomoreRoster = team2.Select(p => p.Id).ToList();
        result.IsAllDefenseGame = isAllDefense;

        // 3. Select and simulate 3-Point Contest
        var threePointContestants = SelectThreePointContestants(league);
        if (threePointContestants.Count >= 2)
        {
            result.ThreePointContestants = SimulateThreePointContest(threePointContestants, random);
            result.ThreePointWinnerId = result.ThreePointContestants.First().PlayerId;
        }

        // 4. Select and simulate Dunk Contest
        var dunkContestants = SelectDunkContestants(league);
        if (dunkContestants.Count >= 2)
        {
            result.DunkContestants = SimulateDunkContest(dunkContestants, random);
            result.DunkWinnerId = result.DunkContestants.First().PlayerId;
        }

        // 5. Simulate Rookie/Soph game
        if (team1.Count > 0 && team2.Count > 0)
        {
            result.RookieGameResult = SimulateRookieGame(league, team1, team2, isAllDefense, random, averages);
        }

        // 6. Simulate All-Star game
        if (conf1.Count > 0 && conf2.Count > 0)
        {
            result.AllStarGameResult = SimulateAllStarGame(league, conf1, conf2, random, averages);
        }

        // 7. Mark participating players
        MarkParticipants(league, result, conf1, conf2, team1, team2, threePointContestants, dunkContestants);

        // 8. Store contest scores on players
        StoreContestScores(league, result);

        return result;
    }

    // ─────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────

    private static List<Player> GetAllEligiblePlayers(Models.League.League league)
    {
        return league.Teams
            .SelectMany(t => t.Roster)
            .Where(p => !string.IsNullOrEmpty(p.Name) && p.Active && p.Injury == 0
                && p.SimulatedStats.Games > 0)
            .ToList();
    }

    private static string GetPlayerConference(Player player, Models.League.League league)
    {
        int teamIdx = player.TeamIndex;
        if (teamIdx < 0 || teamIdx >= league.Teams.Count) return string.Empty;
        return league.Teams[teamIdx].Record.Conference;
    }

    private static int GetPositionIndex(string? position)
    {
        return NormalizePosition(position) switch
        {
            "PG" => 1,
            "SG" => 2,
            "SF" => 3,
            "PF" => 4,
            "C" => 5,
            _ => 0
        };
    }

    private static string NormalizePosition(string? position)
    {
        return position?.Trim().ToUpper() switch
        {
            "PG" => "PG",
            "SG" => "SG",
            "SF" => "SF",
            "PF" => "PF",
            "C" => "C",
            _ => position?.Trim() ?? ""
        };
    }

    private static Team BuildTemporaryTeam(List<Player> players, string name, int id)
    {
        var team = new Team
        {
            Id = id,
            Name = name,
            CityName = name,
            Record = new TeamRecord { TeamName = name }
        };

        // Add players to the roster (up to 12)
        foreach (var p in players.Take(12))
        {
            team.Roster.Add(p);
        }

        // Pad to at least 12 with empty placeholders if needed
        while (team.Roster.Count < 12)
        {
            team.Roster.Add(new Player { Name = string.Empty });
        }

        return team;
    }

    private static void MarkParticipants(
        Models.League.League league, AllStarWeekendResult result,
        List<Player> conf1, List<Player> conf2,
        List<Player> rookieTeam1, List<Player> rookieTeam2,
        List<Player> threePointContestants, List<Player> dunkContestants)
    {
        // All-Star flags
        for (int i = 0; i < conf1.Count; i++)
        {
            conf1[i].AllStar = 1;
            conf1[i].AllStarIndex = i;
        }
        for (int i = 0; i < conf2.Count; i++)
        {
            conf2[i].AllStar = 1;
            conf2[i].AllStarIndex = i;
        }

        // Rookie/Soph flags
        for (int i = 0; i < rookieTeam1.Count; i++)
        {
            rookieTeam1[i].RookieStar = 1;
            rookieTeam1[i].RookieStarIndex = i;
        }
        for (int i = 0; i < rookieTeam2.Count; i++)
        {
            rookieTeam2[i].RookieStar = 1;
            rookieTeam2[i].RookieStarIndex = i;
        }

        // 3-Point Contest flags
        for (int i = 0; i < threePointContestants.Count; i++)
        {
            threePointContestants[i].ThreePointContest = 1;
            threePointContestants[i].ThreePointContestIndex = i;
        }

        // Dunk Contest flags
        for (int i = 0; i < dunkContestants.Count; i++)
        {
            dunkContestants[i].DunkContest = 1;
            dunkContestants[i].DunkContestIndex = i;
        }
    }

    private static void StoreContestScores(Models.League.League league, AllStarWeekendResult result)
    {
        // Find players in the league and store their contest scores
        var playerLookup = league.Teams.SelectMany(t => t.Roster)
            .GroupBy(p => p.Id)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var cp in result.ThreePointContestants)
        {
            if (playerLookup.TryGetValue(cp.PlayerId, out var player))
            {
                Array.Copy(cp.RoundScores, player.ThreePointScores, Math.Min(cp.RoundScores.Length, player.ThreePointScores.Length));
            }
        }

        foreach (var cp in result.DunkContestants)
        {
            if (playerLookup.TryGetValue(cp.PlayerId, out var player))
            {
                Array.Copy(cp.RoundScores, player.DunkScores, Math.Min(cp.RoundScores.Length, player.DunkScores.Length));
            }
        }
    }
}
