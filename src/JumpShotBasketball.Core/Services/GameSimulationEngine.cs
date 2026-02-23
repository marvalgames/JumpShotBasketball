using System;
using JumpShotBasketball.Core.Constants;
using JumpShotBasketball.Core.Enums;
using JumpShotBasketball.Core.Models.Game;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Team;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Possession-by-possession basketball game simulation engine.
/// Port of C++ CEngine (~7,400 lines) decomposed into focused methods.
/// Instance-based: holds mutable game state across ~100-150 possessions.
/// Takes a seeded Random for deterministic replay support.
/// </summary>
public class GameSimulationEngine
{
    private readonly Random _random;
    private GameState _state = null!;
    private LeagueAverages _leagueAvg = null!;
    private GameType _gameType;
    private bool _homeCourtEnabled;
    private double _hca = 0.20; // Home court advantage factor

    // Flat player lookup: all players from both teams, indexed by their original roster position.
    // Visitor: indices 1-30, Home: indices 31-60
    private Player[] _players = null!;
    private Player[] _teamRatings = null!; // Aggregated team-level stats [0]=unused, [1]=visitor, [2]=home

    // Team references
    private Team _visitorTeam = null!;
    private Team _homeTeam = null!;

    public GameSimulationEngine(Random? random = null)
    {
        _random = random ?? Random.Shared;
    }

    /// <summary>
    /// Simulates a complete game between two teams.
    /// Returns an immutable GameResult. The caller handles post-game side effects.
    /// </summary>
    public GameResult SimulateGame(
        Team visitorTeam, Team homeTeam,
        GameType gameType, LeagueAverages leagueAverages,
        double scoringFactor = 1.0, bool homeCourtAdvantage = true)
    {
        _visitorTeam = visitorTeam;
        _homeTeam = homeTeam;
        _gameType = gameType;
        _leagueAvg = leagueAverages;
        _homeCourtEnabled = homeCourtAdvantage;

        _state = new GameState
        {
            ScoringFactor = scoringFactor,
            InjuriesEnabled = true,
            VisitorIndex = 1,
            HomeIndex = 2
        };

        Initialize();
        SetupLineups();
        StartGame();

        // Main game loop — process possessions until game over
        // Safety limit prevents infinite loops (typical game: 100-200 possessions)
        int maxPossessions = 500;
        while (!_state.GameOver && maxPossessions-- > 0)
        {
            PlayPossession();
        }

        if (!_state.GameOver)
        {
            // Force game end if safety limit reached
            _state.GameOver = true;
        }

        return FinalizeGame();
    }

    #region Initialization

    private void Initialize()
    {
        // Build flat player array: index 1-30 = visitor, 31-60 = home
        _players = new Player[61];
        _teamRatings = new Player[3];
        _teamRatings[1] = new Player { Name = "VisitorTeam" };
        _teamRatings[2] = new Player { Name = "HomeTeam" };

        int co = 1;
        foreach (var p in _visitorTeam.Roster.Take(30))
        {
            _players[co] = p;
            p.GameState.Reset();

            if (p.Injury == 0 && !string.IsNullOrEmpty(p.Name) && p.Active
                && co <= 12 && p.SeasonStats.Minutes >= 72)
            {
                _state.PlayingPlayersList[co] = co;
                p.GameState.StatSlot = co;
            }
            co++;
        }
        // Fill remaining visitor slots
        while (co <= 30)
        {
            _players[co] = new Player(); // placeholder
            co++;
        }

        co = 31;
        int homeCo = 1;
        foreach (var p in _homeTeam.Roster.Take(30))
        {
            _players[co] = p;
            p.GameState.Reset();

            if (p.Injury == 0 && !string.IsNullOrEmpty(p.Name) && p.Active
                && homeCo <= 12 && p.SeasonStats.Minutes >= 72)
            {
                _state.PlayingPlayersList[co - 30 + 30] = co; // slots 31-60
                p.GameState.StatSlot = co - 30 + 30;
            }
            co++;
            homeCo++;
        }
        while (co <= 60)
        {
            _players[co] = new Player();
            co++;
        }

        // Rebuild PlayingPlayersList properly: visitor = slots 1-12, home = slots 31-42
        int vSlot = 1, hSlot = 31;
        for (int i = 1; i <= 30 && i <= _visitorTeam.Roster.Count; i++)
        {
            var p = _players[i];
            if (p.Injury == 0 && !string.IsNullOrEmpty(p.Name) && p.Active
                && vSlot <= 12 && p.SeasonStats.Minutes >= 72)
            {
                _state.PlayingPlayersList[vSlot] = i;
                p.GameState.StatSlot = vSlot;
                vSlot++;
            }
        }
        for (int i = 31; i <= 60 && i - 30 <= _homeTeam.Roster.Count; i++)
        {
            var p = _players[i];
            if (p.Injury == 0 && !string.IsNullOrEmpty(p.Name) && p.Active
                && hSlot <= 42 && p.SeasonStats.Minutes >= 72)
            {
                _state.PlayingPlayersList[hSlot] = i;
                p.GameState.StatSlot = hSlot;
                hSlot++;
            }
        }

        _state.Quarter = 1;
        _state.TimeRemainingSeconds = 720;
        _state.Score[1] = 0;
        _state.Score[2] = 0;
    }

    #endregion

    #region Lineup Setup

    private void SetupLineups()
    {
        // Build rotation depth chart for each team
        SetupTeamLineup(1, _visitorTeam);
        SetupTeamLineup(2, _homeTeam);

        // Build team-level aggregate ratings
        BuildTeamRatings(1);
        BuildTeamRatings(2);
    }

    private void SetupTeamLineup(int teamSide, Team team)
    {
        // Build eligible player list sorted by rating for each position
        var eligible = new List<(int index, Player player)>();
        int offset = teamSide == 1 ? 1 : 31;

        for (int i = 0; i < Math.Min(team.Roster.Count, 30); i++)
        {
            var p = _players[offset + i];
            if (p.Injury == 0 && !string.IsNullOrEmpty(p.Name) && p.Active
                && p.SeasonStats.Minutes >= 72)
            {
                // Apply per-game health adjustments
                double healthFactor = p.Health / 100.0;
                if (healthFactor < 0.5) healthFactor = 0.5;

                // Set per-game stamina
                p.GameState.CurrentStamina = p.Ratings.Stamina > 0 ? p.Ratings.Stamina : 80;

                eligible.Add((offset + i, p));
            }
        }

        if (eligible.Count == 0) return;

        // Assign players to positions using rotation eligibility
        for (int pos = 1; pos <= 5; pos++)
        {
            var positionPlayers = GetPlayersForPosition(pos, eligible);

            for (int slot = 0; slot < 10 && slot < positionPlayers.Count; slot++)
            {
                var (idx, player) = positionPlayers[slot];
                var entry = _state.Rotation[teamSide, pos, slot];
                entry.PlayerIndex = idx;
                entry.InRotation = slot < 5 || player.GameMinutes > 0;

                // Priority: starter gets highest, decreasing by slot
                entry.Priority = Math.Max(1, 100 - slot * 15);

                if (slot == 0)
                    entry.Minutes = Math.Max(20, player.GameMinutes > 0 ? player.GameMinutes : 28);
                else if (slot == 1)
                    entry.Minutes = Math.Max(10, player.GameMinutes > 0 ? player.GameMinutes : 20);
                else
                    entry.Minutes = Math.Max(5, player.GameMinutes > 0 ? player.GameMinutes : 10);
            }

            // Fill empty slots
            if (positionPlayers.Count > 0)
            {
                for (int slot = positionPlayers.Count; slot < 10; slot++)
                {
                    _state.Rotation[teamSide, pos, slot].PlayerIndex =
                        positionPlayers[positionPlayers.Count - 1].index;
                }
            }
        }
    }

    private List<(int index, Player player)> GetPlayersForPosition(int pos,
        List<(int index, Player player)> eligible)
    {
        // Filter players eligible for this position
        var candidates = eligible.Where(e => IsEligibleForPosition(e.player, pos)).ToList();

        // If no one is specifically eligible, use everyone
        if (candidates.Count == 0)
            candidates = eligible.ToList();

        // Sort by trade true rating (highest first)
        candidates.Sort((a, b) =>
        {
            double ratingA = a.player.Ratings.TradeTrueRating;
            double ratingB = b.player.Ratings.TradeTrueRating;

            // Apply positional depth penalty for mismatched positions
            int posIdxA = GetPositionIndex(a.player.Position);
            int posIdxB = GetPositionIndex(b.player.Position);
            if (posIdxA != pos) ratingA -= Math.Abs(posIdxA - pos) * 240;
            if (posIdxB != pos) ratingB -= Math.Abs(posIdxB - pos) * 240;

            return ratingB.CompareTo(ratingA);
        });

        return candidates;
    }

    private static bool IsEligibleForPosition(Player p, int pos)
    {
        return pos switch
        {
            1 => p.PgRotation || p.Position?.Trim().ToUpper() == "PG",
            2 => p.SgRotation || p.Position?.Trim().ToUpper() == "SG",
            3 => p.SfRotation || p.Position?.Trim().ToUpper() == "SF",
            4 => p.PfRotation || p.Position?.Trim().ToUpper() == "PF",
            5 => p.CRotation || p.Position?.Trim().ToUpper() is "C" or " C",
            _ => false
        };
    }

    private void BuildTeamRatings(int teamSide)
    {
        // Sum up per-48 ratings for the team's playing roster
        var tr = _teamRatings[teamSide];
        tr.Ratings = new PlayerRatings();

        double totalFga = 0, totalTfga = 0, totalTo = 0, totalFd = 0;
        double totalOreb = 0, totalDreb = 0, totalStl = 0, totalBlk = 0, totalPf = 0;

        int startIdx = teamSide == 1 ? 1 : 31;
        int endIdx = startIdx + 11;

        for (int i = startIdx; i <= Math.Min(endIdx, 60); i++)
        {
            if (_state.PlayingPlayersList[i] == 0 && i > startIdx) continue;
            int pIdx = teamSide == 1 ? _state.PlayingPlayersList[i] : _state.PlayingPlayersList[i];
            if (pIdx <= 0 || pIdx > 60) continue;
            var p = _players[pIdx];
            if (p == null || string.IsNullOrEmpty(p.Name)) continue;

            totalFga += p.Ratings.AdjustedFieldGoalsAttemptedPer48Min > 0
                ? p.Ratings.AdjustedFieldGoalsAttemptedPer48Min : p.Ratings.FieldGoalsAttemptedPer48Min;
            totalTfga += p.Ratings.AdjustedThreePointersAttemptedPer48Min > 0
                ? p.Ratings.AdjustedThreePointersAttemptedPer48Min : p.Ratings.ThreePointersAttemptedPer48Min;
            totalTo += p.Ratings.AdjustedTurnoversPer48Min > 0
                ? p.Ratings.AdjustedTurnoversPer48Min : p.Ratings.TurnoversPer48Min;
            totalFd += p.Ratings.AdjustedFoulsDrawnPer48Min > 0
                ? p.Ratings.AdjustedFoulsDrawnPer48Min : p.Ratings.FoulsDrawnPer48Min;
            totalOreb += p.Ratings.OffensiveReboundsPer48Min;
            totalDreb += p.Ratings.DefensiveReboundsPer48Min;
            totalStl += p.Ratings.StealsPer48Min;
            totalBlk += p.Ratings.BlocksPer48Min;
            totalPf += p.Ratings.PersonalFoulsPer48Min;
        }

        // Store as team totals (per 5 players lineup)
        int count = Math.Max(1, CountPlayingPlayers(teamSide));
        double factor = 5.0 / count; // Normalize to 5-player lineup

        tr.Ratings.FieldGoalsAttemptedPer48Min = totalFga * factor;
        tr.Ratings.ThreePointersAttemptedPer48Min = totalTfga * factor;
        tr.Ratings.TurnoversPer48Min = totalTo * factor;
        tr.Ratings.FoulsDrawnPer48Min = totalFd * factor;
        tr.Ratings.OffensiveReboundsPer48Min = totalOreb * factor;
        tr.Ratings.DefensiveReboundsPer48Min = totalDreb * factor;
        tr.Ratings.StealsPer48Min = totalStl * factor;
        tr.Ratings.BlocksPer48Min = totalBlk * factor;
        tr.Ratings.PersonalFoulsPer48Min = totalPf * factor;
    }

    private int CountPlayingPlayers(int teamSide)
    {
        int count = 0;
        int start = teamSide == 1 ? 1 : 31;
        int end = start + 11;
        for (int i = start; i <= Math.Min(end, 60); i++)
        {
            if (_state.PlayingPlayersList[i] > 0) count++;
        }
        return count;
    }

    #endregion

    #region Game Start

    private void StartGame()
    {
        double poss = _leagueAvg.PersonalFouls * 5 + _leagueAvg.Turnovers * 5 * 0.9
                      + _leagueAvg.ThreePointersAttempted * 5;
        SetAverageTime(poss, _leagueAvg.OffensiveRebounds * 5);
        SetStarters();
        _state.GameStarted = true;
        _state.Possession = JumpBall();
        _state.FirstPossession = _state.Possession;
        _state.LineupSet = true;
    }

    private void SetStarters()
    {
        for (int team = 1; team <= 2; team++)
        {
            int otherTeam = team == 1 ? 2 : 1;
            for (int pos = 1; pos <= 5; pos++)
            {
                if (_state.Lineup[team, pos] == 0)
                    _state.Lineup[team, pos] = _state.Rotation[team, pos, 0].PlayerIndex;
                _state.Defense[team, pos] = _state.Rotation[otherTeam, pos, 0].PlayerIndex;
                _state.Starters[(team - 1) * 5 + pos] = _state.Rotation[team, pos, 0].PlayerIndex;
            }
        }

        // Verify no zero lineups — fallback to slot 1 if needed
        for (int team = 1; team <= 2; team++)
        {
            bool ok = true;
            for (int pos = 1; pos <= 5; pos++)
            {
                if (_state.Lineup[team, pos] == 0) ok = false;
            }
            if (!ok)
            {
                for (int pos = 1; pos <= 5; pos++)
                {
                    _state.Lineup[team, pos] = _state.Rotation[team, pos, 1].PlayerIndex;
                    _state.Rotation[team, pos, 0] = _state.Rotation[team, pos, 1];
                }
            }
        }
    }

    private int JumpBall() => IntRandom(2);

    #endregion

    #region Main Possession Loop

    private void PlayPossession()
    {
        _state.QuarterEnded = false;
        bool forced3 = false;
        int typeShot = (int)ShotType.Auto;
        int originalShot = (int)ShotType.Auto;

        int adjForCoach = IntRandom(2);
        int currentTeam = _state.Possession;

        // Fastbreak check
        if (_state.FastbreakChance && !forced3)
        {
            _state.FastbreakChance = false;
            int p = IntRandom(5);
            int pl = _state.Lineup[_state.Possession, p];
            if (pl > 0 && pl <= 60)
            {
                int n = IntRandom(18);
                int fb = _players[pl].Ratings.TransitionOffenseRaw - (_gameType == GameType.Playoff ? 1 : 0);

                // Coach fastbreak adjustment
                var coach = GetCoach(currentTeam);
                int add = GetCoachAdjustment(coach?.CoachFastbreak ?? 3, adjForCoach);
                fb += add;

                if (n <= fb)
                {
                    originalShot = (int)ShotType.Fastbreak;
                    _state.BallHandler = pl;
                    _state.PositionBallHandler = p;
                }
            }
        }

        int scoreDiff = _state.Score[1] - _state.Score[2];

        // Select ball handler
        bool shotOk = false;
        if (originalShot != (int)ShotType.Fastbreak)
            _state.BallHandler = SelectBallHandler(_state.Possession, ref shotOk, ref typeShot, ref forced3);

        // Intentional foul logic (late game)
        bool intFoul = false;
        if (_state.TimeRemainingSeconds <= 24 && _state.Quarter >= 4)
        {
            if ((_state.Possession == 1 && scoreDiff <= 3 && scoreDiff >= 1) ||
                (_state.Possession == 2 && scoreDiff >= -3 && scoreDiff <= -1))
            {
                int avoidFoulFactor = _state.BallHandler > 0 && _state.BallHandler <= 60
                    ? _players[_state.BallHandler].Ratings.PenetrationOffenseRaw : 5;
                if (IntRandom(10) > avoidFoulFactor)
                    intFoul = true;
            }
        }

        // Forced 3 when trailing by 3 with time running out
        if (_state.TimeRemainingSeconds <= 24 && _state.Quarter >= 4)
        {
            if ((_state.Possession == 1 && scoreDiff == -3) ||
                (_state.Possession == 2 && scoreDiff == 3))
                forced3 = true;
        }

        // Milk clock when leading with time running out
        _state.MilkClock = false;
        if (_state.TimeRemainingSeconds <= 24 && _state.Quarter >= 4 && !intFoul)
        {
            if ((_state.Possession == 1 && scoreDiff > 0) ||
                (_state.Possession == 2 && scoreDiff < 0))
                _state.MilkClock = true;
        }

        // Forced 3 with 3 seconds or less
        if (_state.TimeRemainingSeconds <= 3)
            forced3 = true;

        bool putBack = false;
        bool threePointer = false;
        bool block = false;

        do // Main play loop (repeats for putbacks)
        {
            int player = _state.BallHandler;
            if (player <= 0 || player > 60) break;
            var playerObj = _players[player];

            int mov = playerObj.Ratings.MovementOffenseRaw;
            int pen = playerObj.Ratings.PenetrationOffenseRaw;
            int post = playerObj.Ratings.PostOffenseRaw;
            int trans = playerObj.Ratings.TransitionOffenseRaw;

            AdjustOffenseForGamePlan(playerObj, ref mov, ref pen, ref post, ref trans);

            int dMov = mov, dPen = pen, dPost = post;
            // Apply coaching adjustments to offense
            ApplyCoachOffenseAdjustments(currentTeam, adjForCoach, ref dMov, ref dPen, ref dPost);

            // Shot type selection
            if (!shotOk || putBack || typeShot == (int)ShotType.Auto)
            {
                typeShot = SelectShotType(player, dMov, dPen, dPost);
            }

            // Defensive adjustments
            int defTeam = _state.Possession == 1 ? 2 : 1;
            int d = _state.PositionBallHandler;
            if (_state.Possession == 1) d += 5;
            ApplyDefensiveAdjustments(d, ref dMov, ref dPen, ref dPost, typeShot);

            // Select assister
            int astPlayer = SelectAssister();

            // Calculate lineup quality factor
            double better = CalculateLineupBetter(player, astPlayer);

            // Determine play result
            if (putBack) forced3 = false;
            PossessionResult result;
            do
            {
                if (typeShot == (int)ShotType.Three && shotOk)
                    result = PossessionResult.ThreePointAttempt;
                else
                    result = DetermineResult(player, putBack, better, typeShot, forced3);
            } while (!_state.InjuriesEnabled && result == PossessionResult.Injury);

            if (intFoul) result = PossessionResult.Foul;

            // Get defender info for FG% calculation
            int defenderPos = _state.PositionBallHandler;
            int defender = _state.Defense[_state.Possession, defenderPos];
            if (defender <= 0 || defender > 60) defender = _state.Lineup[defTeam, defenderPos];

            // Calculate defensive differential
            double defDiff = CalculateDefensiveDifferential(
                player, defender, mov, pen, post, dMov, dPen, dPost,
                typeShot, originalShot, forced3);

            // Process result
            bool andOne = false;

            if (result == PossessionResult.TwoPointAttempt)
            {
                ProcessTwoPointAttempt(player, ref putBack, ref block, ref threePointer,
                    astPlayer, defDiff, better, typeShot, originalShot, ref andOne);
            }
            else if (result == PossessionResult.ThreePointAttempt && !putBack)
            {
                ProcessThreePointAttempt(player, ref putBack, ref block,
                    astPlayer, defDiff, better, typeShot, ref andOne);
                threePointer = true;
            }
            else if (result == PossessionResult.Foul)
            {
                ProcessFoul(player, astPlayer, andOne, threePointer, intFoul);
                putBack = false;
            }
            else if (result == PossessionResult.Turnover && !putBack)
            {
                ProcessTurnover(player);
                putBack = false;
            }
            else if (result == PossessionResult.Injury && !putBack)
            {
                ProcessInjury(player);
                putBack = false;
            }

        } while (putBack);

        // Advance clock
        AdvanceClock(putBack, originalShot);

        // Handle quarter/game end
        // Note: AdvanceClock calls AdvanceQuarter() which increments Quarter
        // and resets TimeRemainingSeconds. So Quarter > 4 means we just
        // finished Q4 (or an OT period).
        if (_state.QuarterEnded)
        {
            if (_state.Quarter > 4 && _state.Score[1] != _state.Score[2])
            {
                // Game over — regulation or OT ended with different scores
                HandleGameEnd();
                return;
            }

            if (_state.Quarter > 1)
            {
                HandleQuarterEnd();
            }
        }

        // Auto-substitute
        AutoSubstitute();
    }

    #endregion

    #region Ball Handler Selection

    private int SelectBallHandler(int possession, ref bool shotOk, ref int typeShot, ref bool forced3)
    {
        double[] touches = new double[6];
        int ballHandler = 0;

        // Calculate touches distribution
        int totMov = 0, totPen = 0, totPost = 0;
        double totalShots = 0;

        for (int i = 1; i <= 5; i++)
        {
            int player = _state.Lineup[possession, i];
            if (player <= 0 || player > 60) continue;
            totMov += _players[player].Ratings.MovementOffenseRaw;
            totPen += _players[player].Ratings.PenetrationOffenseRaw;
            totPost += _players[player].Ratings.PostOffenseRaw;
            totalShots += GetAdjustedFgaPer48(player);
        }

        for (int i = 1; i <= 5; i++)
        {
            int player = _state.Lineup[possession, i];
            if (player <= 0 || player > 60)
            {
                touches[i] = touches[i - 1];
                continue;
            }

            var p = _players[player];
            double foulsDrawn = GetAdjustedFoulsDrawnPer48(player);
            double to = GetAdjustedToPer48(player);

            // Position-based touch weight
            int hi, set;
            GetPositionWeight(i, p, totMov, totPen, totPost, out hi, out set);

            double adj = set > 0 && hi > 0
                ? ((double)hi / ((double)set - hi + 0.001)) / ((double)hi / 20.0)
                  * (p.Ratings.ProjectionFieldGoalPercentage > 0 ? p.Ratings.ProjectionFieldGoalPercentage : 450.0)
                  / (_leagueAvg.FieldGoalPercentageByPosition[i] > 0 ? _leagueAvg.FieldGoalPercentageByPosition[i] : 450.0)
                : 1.0;
            adj = (adj + 1) / 2;

            double attempts = GetAdjustedFgaPer48(player);

            // Cap dominant scorer
            if (totalShots > 0 && (attempts / totalShots) > 0.375)
                attempts = totalShots * 0.375;

            // Home court advantage on fouls/turnovers
            if (_homeCourtEnabled)
            {
                foulsDrawn += (possession * 2 - 3) * _hca;
                to -= (possession * 2 - 3) * _hca;
            }

            if (forced3)
            {
                double tfga = GetAdjustedTfgaPer48(player);
                double factor = attempts > 0 ? tfga / attempts : 0;
                touches[i] = touches[i - 1] + tfga + to * factor;
            }
            else
            {
                touches[i] = touches[i - 1] + attempts * adj
                             + GetAdjustedTfgaPer48(player)
                             + foulsDrawn * adj + to * adj;
            }
        }

        if (touches[5] <= 0)
        {
            // Fallback: pick random starter
            int pos = IntRandom(5);
            return _state.Lineup[possession, pos];
        }

        // Select ball handler via weighted random
        int maxAttempts = 20;
        do
        {
            double number = RandomDouble(touches[5]);
            for (int i = 1; i <= 5; i++)
            {
                if (number <= touches[i] && number > touches[i - 1] && ballHandler == 0)
                {
                    ballHandler = _state.Lineup[possession, i];
                    _state.PositionBallHandler = i;
                }
            }
            maxAttempts--;
        } while (ballHandler == 0 && maxAttempts > 0);

        if (ballHandler == 0)
        {
            ballHandler = _state.Lineup[possession, IntRandom(5)];
            _state.PositionBallHandler = 1;
        }

        return ballHandler;
    }

    private void GetPositionWeight(int positionSlot, Player p, int totMov, int totPen, int totPost,
        out int hi, out int set)
    {
        hi = 0; set = 0;
        switch (positionSlot)
        {
            case 1: hi = p.Ratings.PenetrationOffenseRaw; set = totPen; break;
            case 2: hi = p.Ratings.MovementOffenseRaw; set = totMov; break;
            case 3: hi = p.Ratings.MovementOffenseRaw; set = totMov; break;
            case 4: hi = p.Ratings.PostOffenseRaw; set = totPost; break;
            case 5: hi = p.Ratings.PostOffenseRaw; set = totPost; break;
        }
        if (hi <= 0) hi = 1;
        if (set <= 0) set = hi + 1;
    }

    #endregion

    #region Shot Type Selection

    private int SelectShotType(int player, int dMov, int dPen, int dPost)
    {
        // Position-based shot type weighting table (pun array from C++)
        int[,] pun = {
            { 9, 1, 9 }, // PG
            { 8, 3, 8 }, // SG
            { 7, 5, 7 }, // SF
            { 5, 7, 5 }, // PF
            { 4, 8, 4 }  // C
        };

        var p = _players[player];
        int po = GetPositionIndex(p.Position) - 1;
        if (po < 0) po = 0;
        if (po > 4) po = 4;

        int lineupPos = _state.PositionBallHandler - 1;
        if (lineupPos < 0) lineupPos = 0;
        if (lineupPos > 4) lineupPos = 4;

        int tMov = dMov - (pun[po, 0] - pun[lineupPos, 0]);
        int tPost = dPost - (pun[po, 1] - pun[lineupPos, 1]);
        int tPen = dPen - (pun[po, 2] - pun[lineupPos, 2]);

        if (tMov < 1) tMov = 1;
        if (tPost < 1) tPost = 1;
        if (tPen < 1) tPen = 1;

        int total = tMov + tPen + tPost;
        int shot = IntRandom(total);

        if (shot > tPen + tMov)
            return (int)ShotType.Inside;
        else if (shot > tMov)
            return (int)ShotType.Penetration;
        else
            return (int)ShotType.Outside;
    }

    #endregion

    #region Result Determination

    private PossessionResult DetermineResult(int player, bool putBack, double better,
        int typeShot, bool forced3)
    {
        var p = _players[player];
        double fatigueFactor = GetFatigueFactor(player);

        double playerTo = GetAdjustedToPer48(player) * (2 - fatigueFactor);
        double lineupStl = CalculateLineupSteals();
        double lineupTo = CalculateLineupTurnovers();
        double avgStl = _leagueAvg.Steals * 5;
        double avgTo = _leagueAvg.Turnovers * 5;

        // Calculate fouls drawn with team foul rate adjustment
        double fds = GetAdjustedFoulsDrawnPer48(player);
        double foulsDrawn = fds * fatigueFactor;

        if (_homeCourtEnabled)
        {
            foulsDrawn += (_state.Possession * 2 - 3) * _hca;
            playerTo -= (_state.Possession * 2 - 3) * _hca;
        }

        // Adjust turnovers based on opponent steals
        if (lineupTo > 0)
            playerTo += playerTo / lineupTo * (lineupStl - avgStl * 5.0 / 6.0);

        // Better factor adjusts turnovers down and fouls drawn up
        if (avgTo > 0)
            playerTo -= better / 4 / (avgTo / 5) * playerTo;
        foulsDrawn += better / 4;

        if (foulsDrawn <= 0) foulsDrawn = RandomDouble(0.030);
        if (playerTo <= 0) playerTo = RandomDouble(0.60);

        if (forced3 && GetAdjustedTfgaPer48(player) > 0)
        {
            double factor = GetAdjustedTfgaPer48(player) / (GetAdjustedFgaPer48(player) + 0.001);
            playerTo *= factor;
        }

        double fga = GetAdjustedFgaPer48(player);
        double tfga = GetAdjustedTfgaPer48(player);
        double totalTouches = fga + tfga + foulsDrawn + playerTo;

        PossessionResult result;
        int safetyCounter = 50;
        do
        {
            double number = RandomDouble(totalTouches);
            result = (PossessionResult)IntRandom(5); // random default

            double limit1 = fga;
            double limit2 = limit1 + tfga;
            if (number <= limit1) result = PossessionResult.TwoPointAttempt;
            else if (number <= limit2) result = PossessionResult.ThreePointAttempt;
            else
            {
                double limit3 = limit2 + foulsDrawn;
                if (number <= limit3) result = PossessionResult.Foul;
                else result = PossessionResult.Turnover;
            }
            safetyCounter--;
        }
        while (safetyCounter > 0 &&
               ((putBack && (result == PossessionResult.ThreePointAttempt || result == PossessionResult.Turnover))
                || (typeShot == (int)ShotType.Three && result == PossessionResult.ThreePointAttempt)
                || (forced3 && result != PossessionResult.ThreePointAttempt && result != PossessionResult.Turnover)));

        // Injury check
        int injuryRating = p.Ratings.InjuryRating;
        double adjInjury = Math.Sqrt(injuryRating);
        int injuryRoll = IntRandom(1793);
        if (injuryRoll <= adjInjury && !putBack && _state.InjuriesEnabled)
        {
            result = PossessionResult.Injury;
        }

        return result;
    }

    #endregion

    #region Field Goal Processing

    private void ProcessTwoPointAttempt(int player, ref bool putBack, ref bool block,
        ref bool threePointer, int astPlayer, double defDiff, double better,
        int typeShot, int originalShot, ref bool andOne)
    {
        var p = _players[player];
        int fgPct = p.Ratings.AdjustedFieldGoalPercentage > 0
            ? p.Ratings.AdjustedFieldGoalPercentage : p.Ratings.FieldGoalPercentage;

        // Home court adjustment
        if (_homeCourtEnabled)
            fgPct += (int)(_hca * 10 * 8.0 / 3.0) * (_state.Possession * 2 - 3);

        double tempPct;
        if (putBack)
        {
            tempPct = p.Ratings.FieldGoalPercentage * 4.0 / 3.0;
        }
        else
        {
            double avgFga = _leagueAvg.FieldGoalsAttempted * 5;
            if (avgFga <= 0) avgFga = 90;
            tempPct = fgPct + (defDiff / 2 / (avgFga / 5) * 1000);
        }

        // Clutch adjustment
        if (_state.Quarter >= 4 && Math.Abs(_state.Score[1] - _state.Score[2]) <= 5)
            tempPct *= (0.98 + p.Ratings.Clutch / 100.0);

        fgPct = (int)tempPct;

        // Blocks adjustment
        double blocks = CalculateLineupBlocks();
        double avgBlk5 = _leagueAvg.Blocks * 5;
        double avgFga5 = _leagueAvg.FieldGoalsAttempted * 5;
        if (avgFga5 <= 0) avgFga5 = 90;

        double adjustedFgPct = fgPct / 1000.0 + (avgBlk5 - blocks) / 2 / avgFga5;

        // Better/teammate quality adjustment
        double fgaPer48 = p.Ratings.FieldGoalsAttemptedPer48Min;
        if (fgaPer48 <= 0) fgaPer48 = 1;
        adjustedFgPct += better / 4 / fgaPer48;

        fgPct = (int)(adjustedFgPct * 1000);

        // Consistency grid + fatigue = make/miss
        int fga = (int)(p.Ratings.FieldGoalsAttemptedPer48Min + GetAdjustedTfgaPer48(player));
        bool made = AttemptFieldGoal(fgPct, fga, player);

        if (made)
        {
            // And-one check: 5% on 2PT
            if (RandomDouble(1) < 0.05) andOne = true;

            p.GameState.FieldGoalsMade++;
            p.GameState.FieldGoalsAttempted++;
            _state.Score[_state.Possession] += 2;

            // Assist
            if (!putBack && astPlayer > 0)
                RecordAssist(astPlayer);

            AddPbp(PlayByPlayGenerator.FieldGoalMade(
                p.LastName, GetTeamName(_state.Possession), (ShotType)typeShot, putBack, andOne));

            putBack = false;
            if (!andOne) PossessionChange();
        }
        else
        {
            p.GameState.FieldGoalsAttempted++;

            // Block check
            block = CheckBlock(player, false);

            if (!block)
            {
                AddPbp(PlayByPlayGenerator.FieldGoalMissed(
                    p.LastName, (ShotType)typeShot, putBack));
            }

            // Rebound
            threePointer = false;
            putBack = ProcessRebound(player, ref block);
        }

        if (andOne)
        {
            ProcessAndOneFreeThrow(player);
            PossessionChange();
        }
    }

    private void ProcessThreePointAttempt(int player, ref bool putBack, ref bool block,
        int astPlayer, double defDiff, double better, int typeShot, ref bool andOne)
    {
        var p = _players[player];
        int tfgPct = p.Ratings.ThreePointPercentage;

        double avgFga = _leagueAvg.FieldGoalsAttempted * 5;
        if (avgFga <= 0) avgFga = 90;
        double f = avgFga / 5.0 * 3.0 / 2.0;
        double tempPct = tfgPct + (defDiff / 2 / f * 1000);
        tfgPct = (int)tempPct;

        if (_homeCourtEnabled)
            tfgPct += (int)(_hca * 10 * 8.0 / 3.0) * (_state.Possession * 2 - 3);

        // Blocks adjustment for 3PT
        double blocks = CalculateLineupBlocks();
        double avgBlk5 = _leagueAvg.Blocks * 5;
        double adjustedPct = tfgPct / 1000.0 + (avgBlk5 - blocks) / 2 / (f * 5);
        tfgPct = (int)(adjustedPct * 1000);

        int fga = (int)(GetAdjustedFgaPer48(player) + GetAdjustedTfgaPer48(player));
        bool made = AttemptFieldGoal(tfgPct, fga, player);

        if (made)
        {
            // And-one on 3PT: 1%
            if (RandomDouble(1) < 0.01) andOne = true;

            p.GameState.FieldGoalsMade++;
            p.GameState.FieldGoalsAttempted++;
            p.GameState.ThreePointersMade++;
            p.GameState.ThreePointersAttempted++;
            _state.Score[_state.Possession] += 3;

            if (astPlayer > 0)
                RecordAssist(astPlayer);

            AddPbp(PlayByPlayGenerator.ThreePointMade(
                p.LastName, GetTeamName(_state.Possession), andOne));

            putBack = false;
            if (!andOne) PossessionChange();
        }
        else
        {
            p.GameState.FieldGoalsAttempted++;
            p.GameState.ThreePointersAttempted++;

            block = CheckBlock(player, true);

            if (!block)
            {
                AddPbp(PlayByPlayGenerator.ThreePointMissed(p.LastName));
            }

            putBack = ProcessRebound(player, ref block);
        }

        if (andOne)
        {
            ProcessAndOneFreeThrow(player);
            PossessionChange();
        }
    }

    private void ProcessFoul(int player, int astPlayer, bool andOne, bool threePointer, bool intFoul)
    {
        PossessionChange();
        int pfPosition = SelectFouler();
        int pfPlayer = _state.Lineup[_state.Possession, pfPosition];
        if (pfPlayer <= 0 || pfPlayer > 60) return;

        _players[pfPlayer].GameState.PersonalFouls++;
        _state.TeamFouls[_state.Possession]++;

        AddPbp(PlayByPlayGenerator.PersonalFoul(
            _players[pfPlayer].LastName, _players[pfPlayer].GameState.PersonalFouls,
            andOne, !intFoul, false));

        // Check foul out
        if (_players[pfPlayer].GameState.PersonalFouls >= 6)
        {
            _state.MustSub[_state.Possession] = pfPlayer;
            AddPbp(PlayByPlayGenerator.FouledOut(_players[pfPlayer].LastName));
        }

        PossessionChange();

        // Free throws
        bool inTheAct = !intFoul && (RandomDouble(1) > 0.5 || andOne || threePointer);

        if (inTheAct || _state.TeamFouls[_state.Possession == 1 ? 2 : 1] >= 5 || andOne || threePointer)
        {
            int ftCount = andOne ? 1 : (threePointer ? 3 : 2);
            ShootFreeThrows(player, ftCount, andOne);
        }
    }

    private void ShootFreeThrows(int player, int count, bool andOne)
    {
        var p = _players[player];
        if (player <= 0 || player > 60) return;

        int ftPct = p.Ratings.FreeThrowPercentage;

        for (int i = 0; i < count; i++)
        {
            bool made = AttemptFreeThrow(ftPct, player);
            string ordinal = (i + 1) switch
            {
                1 => andOne ? "bonus" : "1st",
                2 => "2nd",
                3 => "3rd",
                _ => $"{i + 1}th"
            };

            AddPbp(PlayByPlayGenerator.FreeThrow(p.LastName, made, ordinal));

            if (made)
            {
                p.GameState.FreeThrowsMade++;
                p.GameState.FreeThrowsAttempted++;
                _state.Score[_state.Possession]++;
            }
            else
            {
                p.GameState.FreeThrowsAttempted++;

                // Last free throw miss: rebound
                if (i == count - 1 && !andOne)
                {
                    bool blk = false;
                    ProcessRebound(player, ref blk);
                    return;
                }
            }
        }

        PossessionChange();
    }

    private void ProcessAndOneFreeThrow(int player)
    {
        var p = _players[player];
        if (player <= 0 || player > 60) return;

        int ftPct = p.Ratings.FreeThrowPercentage;
        bool made = AttemptFreeThrow(ftPct, player);

        AddPbp(PlayByPlayGenerator.FreeThrow(p.LastName, made, "bonus"));

        if (made)
        {
            p.GameState.FreeThrowsMade++;
            p.GameState.FreeThrowsAttempted++;
            _state.Score[_state.Possession]++;
        }
        else
        {
            p.GameState.FreeThrowsAttempted++;
            bool blk = false;
            ProcessRebound(player, ref blk);
        }
    }

    private void ProcessTurnover(int player)
    {
        var p = _players[player];
        if (player <= 0 || player > 60) return;

        bool offFoul = RandomDouble(1) < 0.1;
        p.GameState.Turnovers++;
        if (offFoul) p.GameState.PersonalFouls++;

        // Check for steal
        bool stolen = false;
        if (!offFoul)
        {
            int stealerPos = SelectStealer();
            if (stealerPos > 0)
            {
                int defTeam = _state.Possession == 1 ? 2 : 1;
                int stealerPlayer = _state.Lineup[defTeam, stealerPos];
                if (stealerPlayer > 0 && stealerPlayer <= 60)
                {
                    _players[stealerPlayer].GameState.Steals++;
                    AddPbp(PlayByPlayGenerator.Steal(
                        _players[stealerPlayer].LastName, p.LastName));
                    _state.FastbreakChance = true;
                    stolen = true;
                }
            }
        }

        if (!stolen)
        {
            AddPbp(PlayByPlayGenerator.Turnover(p.LastName, offFoul));
        }

        PossessionChange();
    }

    private void ProcessInjury(int player)
    {
        var p = _players[player];
        if (player <= 0 || player > 60) return;

        int injuryRating = p.Ratings.InjuryRating;
        double adjInjury = Math.Sqrt(injuryRating);

        double factor1 = adjInjury * 2.0 / 9.0;
        double factor2 = factor1 + adjInjury * 2.0 / 9.0 * 2.0 / 3.0;
        double factor3 = factor2 + adjInjury * 2.0 / 9.0 * 2.0 / 3.0 * 2.0 / 6.0;
        double factor4 = factor3 + adjInjury * 2.0 / 9.0 * 2.0 / 3.0 * 2.0 / 9.0 * 2.0 / 6.0;

        double i = RandomDouble(adjInjury);
        if (i <= factor1) i *= 3;
        else if (i <= factor2) i *= 9;
        else if (i <= factor3) i *= 27;
        else if (i <= factor4) i *= 81;

        int gamesOut = Math.Clamp((int)i + 1, 1, 160);

        if (gamesOut > 1)
        {
            p.GameState.GameInjury = gamesOut;
            _state.MustSub[_state.Possession] = player;
        }

        AddPbp(PlayByPlayGenerator.Injury(p.LastName, gamesOut));
        _state.TimeRemainingSeconds--;
        AutoSubstitute();
    }

    #endregion

    #region Shot Attempt Methods

    private bool AttemptFieldGoal(int fgPct, int fga, int player)
    {
        double fatigueFactor = GetFatigueFactor(player);
        if (fatigueFactor > 1) fatigueFactor = 1;

        int cons = 4 - (_players[player].Ratings.Consistency - 1);
        if (cons < 0) cons = 0;
        int factor = fga * (cons / 2) - 1;
        if (factor < 3) factor = 3;
        if (factor > 99) factor = 99;
        double cells = 1000.0 / (factor + 1);

        int statSlot = _players[player].GameState.StatSlot;
        if (statSlot <= 0) statSlot = 1;
        if (statSlot > 60) statSlot = 60;

        // Check if any cells available
        bool anyOpen = false;
        for (int j = 0; j <= factor; j++)
        {
            if (!_state.ConsistencyGrid[statSlot, j]) { anyOpen = true; break; }
        }
        if (!anyOpen)
        {
            for (int j = 0; j <= factor; j++)
                _state.ConsistencyGrid[statSlot, j] = false;
        }

        // Find open cell
        int roll, cellIndex;
        int maxTries = 100;
        do
        {
            roll = IntRandom(1000);
            cellIndex = (int)((roll - 1) / cells);
            if (cellIndex > factor) cellIndex = factor;
            if (cellIndex < 0) cellIndex = 0;
            maxTries--;
        } while (_state.ConsistencyGrid[statSlot, cellIndex] && maxTries > 0);

        _state.ConsistencyGrid[statSlot, cellIndex] = true;

        return roll <= (fgPct * fatigueFactor);
    }

    private bool AttemptFreeThrow(int ftPct, int player)
    {
        double fatigueFactor = GetFatigueFactor(player);
        if (fatigueFactor > 1) fatigueFactor = 1;

        int roll = IntRandom(1000);
        return roll <= (ftPct * fatigueFactor);
    }

    #endregion

    #region Rebound Processing

    private bool ProcessRebound(int player, ref bool block)
    {
        int reboundTeam = DetermineReboundTeam();
        int reboundPosition = SelectRebounder(reboundTeam);
        int rebounder = _state.Lineup[reboundTeam, reboundPosition];
        if (rebounder <= 0 || rebounder > 60) return false;

        double teamRebRoll = RandomDouble(1);

        if (reboundTeam != _state.Possession) // Defensive rebound
        {
            PossessionChange();
            if (teamRebRoll > 0.06)
            {
                _players[rebounder].GameState.DefensiveRebounds++;
                _state.FastbreakChance = true;
                AddPbp(PlayByPlayGenerator.DefensiveRebound(_players[rebounder].LastName));
            }
            else
            {
                AddPbp(PlayByPlayGenerator.TeamRebound(GetTeamName(_state.Possession)));
            }
            return false;
        }
        else // Offensive rebound
        {
            bool putBack = false;
            if (teamRebRoll > 0.06)
            {
                _players[rebounder].GameState.OffensiveRebounds++;

                // Putback chance
                double pbRoll = RandomDouble(1);
                var rebPlayer = _players[rebounder];
                double fgaPer48 = rebPlayer.Ratings.FieldGoalsAttemptedPer48Min;
                double orebPer48 = rebPlayer.Ratings.OffensiveReboundsPer48Min;
                double putbackFactor = 0.25 + (fgaPer48 / (fgaPer48 + orebPer48 + 0.001)) / 2;

                if (pbRoll < putbackFactor && !block)
                {
                    putBack = true;
                    _state.BallHandler = rebounder;
                    _state.PositionBallHandler = reboundPosition;
                    AddPbp(PlayByPlayGenerator.OffensiveRebound(_players[rebounder].LastName, true));
                }
                else
                {
                    AddPbp(PlayByPlayGenerator.OffensiveRebound(_players[rebounder].LastName, false));
                }
            }
            else
            {
                AddPbp(PlayByPlayGenerator.TeamRebound(GetTeamName(_state.Possession)));
            }

            if (putBack)
                AdvanceClock(true, (int)ShotType.Auto);

            return putBack;
        }
    }

    private int DetermineReboundTeam()
    {
        int offTeam = _state.Possession;
        int defTeam = offTeam == 1 ? 2 : 1;

        double offReb = 0, defReb = 0;

        for (int i = 1; i <= 5; i++)
        {
            int player = _state.Lineup[offTeam, i];
            if (player <= 0 || player > 60) continue;
            double ff = GetFatigueFactor(player);
            offReb += _players[player].Ratings.OffensiveReboundsPer48Min * ff;
        }

        for (int i = 1; i <= 5; i++)
        {
            int player = _state.Lineup[defTeam, i];
            if (player <= 0 || player > 60) continue;
            double ff = GetFatigueFactor(player);
            defReb += _players[player].Ratings.DefensiveReboundsPer48Min * ff;
        }

        // Cap offensive rebounds
        if (offReb > defReb) offReb = defReb;

        // Dampening factor (from C++)
        double normalRate = _leagueAvg.OffensiveRebounds / (_leagueAvg.OffensiveRebounds + _leagueAvg.DefensiveRebounds + 0.001) * 100;
        double rate = offReb / (offReb + defReb + 0.001) * 100;
        double diff = (rate - normalRate) / 2;

        double adjustedOffReb;
        if (diff > 0)
            adjustedOffReb = normalRate + diff + Math.Sqrt(Math.Abs(diff));
        else
            adjustedOffReb = normalRate + diff - Math.Sqrt(Math.Abs(diff));

        return RandomDouble(100) <= adjustedOffReb ? offTeam : defTeam;
    }

    private int SelectRebounder(int reboundTeam)
    {
        double[] touches = new double[6];
        bool isOffensive = reboundTeam == _state.Possession;

        for (int i = 1; i <= 5; i++)
        {
            int player = _state.Lineup[reboundTeam, i];
            if (player <= 0 || player > 60)
            {
                touches[i] = touches[i - 1];
                continue;
            }
            double ff = GetFatigueFactor(player);
            if (isOffensive)
                touches[i] = touches[i - 1] + _players[player].Ratings.OffensiveReboundsPer48Min * ff;
            else
                touches[i] = touches[i - 1] + _players[player].Ratings.DefensiveReboundsPer48Min * ff;
        }

        if (touches[5] <= 0) return IntRandom(5);

        double number = RandomDouble(touches[5]);
        for (int i = 1; i <= 5; i++)
        {
            if (number <= touches[i] && number > touches[i - 1])
                return i;
        }
        return IntRandom(5);
    }

    #endregion

    #region Player Selection Methods

    private int SelectFouler()
    {
        int defTeam = _state.Possession;
        double[] touches = new double[6];

        for (int i = 1; i <= 5; i++)
        {
            int player = _state.Lineup[defTeam, i];
            if (player <= 0 || player > 60)
            {
                touches[i] = touches[i - 1];
                continue;
            }
            double ff = GetFatigueFactor(player);
            double foulRating = _players[player].Ratings.PersonalFoulsPer48Min;
            if (IsInFoulTrouble(player)) foulRating *= 2.0 / 3.0;
            touches[i] = touches[i - 1] + foulRating * (2 - ff);
        }

        if (touches[5] <= 0) return IntRandom(5);

        double number = RandomDouble(touches[5]);
        for (int i = 1; i <= 5; i++)
        {
            if (number <= touches[i] && number > touches[i - 1])
                return i;
        }
        return IntRandom(5);
    }

    private int SelectAssister()
    {
        double[] astRating = new double[6];
        int[] positions = new int[6];
        int p = 0;

        // Calculate team FGM for ratio
        double totalFgm = 0;
        for (int i = 1; i <= 5; i++)
        {
            int player = _state.Lineup[_state.Possession, i];
            if (player <= 0 || player > 60) continue;
            var pl = _players[player];
            double fgm = GetAdjustedFgaPer48(player) * pl.Ratings.AdjustedFieldGoalPercentage / 1000.0;
            double tfgm = GetAdjustedTfgaPer48(player) * pl.Ratings.ThreePointPercentage / 1000.0;
            totalFgm += fgm + tfgm;
        }

        for (int i = 1; i <= 5; i++)
        {
            if (i == _state.PositionBallHandler) continue;
            p++;
            int player = _state.Lineup[_state.Possession, i];
            if (player <= 0 || player > 60) continue;

            double ff = GetFatigueFactor(player);
            var pl = _players[player];
            double playerFgm = GetAdjustedFgaPer48(player) * pl.Ratings.AdjustedFieldGoalPercentage / 1000.0
                               + GetAdjustedTfgaPer48(player) * pl.Ratings.ThreePointPercentage / 1000.0;
            double denom = totalFgm - playerFgm;
            if (denom <= 0) denom = 1;
            double fg = pl.Ratings.AssistsPer48Min * ff / denom;
            astRating[p] = astRating[p - 1] + fg;
            positions[p] = i;
        }

        if (p == 0 || astRating[p] <= 0) return 0;

        // Limit assists if too high
        double total = astRating[p];
        double factor = 1;
        if (total > 9.0 / 11.0) factor = total * 11.0 / 9.0;

        double number = RandomDouble(factor);
        for (int i = 1; i <= p; i++)
        {
            if (number <= astRating[i] && number > astRating[i - 1])
                return positions[i];
        }
        return 0;
    }

    private int SelectStealer()
    {
        int defTeam = _state.Possession == 1 ? 2 : 1;
        double avgStl = _leagueAvg.Steals * 5;

        double[] steals = new double[6];
        for (int i = 1; i <= 5; i++)
        {
            int player = _state.Lineup[defTeam, i];
            if (player <= 0 || player > 60)
            {
                steals[i] = steals[i - 1];
                continue;
            }
            double ff = GetFatigueFactor(player);
            steals[i] = steals[i - 1] + _players[player].Ratings.StealsPer48Min * ff;
        }

        // Cap at 1.5x league average
        if (steals[5] > avgStl * 1.5)
        {
            double f = avgStl * 1.5 / steals[5];
            for (int i = 1; i <= 5; i++) steals[i] *= f;
        }

        // Sum offensive turnovers
        double[] turnovers = new double[6];
        for (int i = 1; i <= 5; i++)
        {
            int player = _state.Lineup[_state.Possession, i];
            if (player <= 0 || player > 60)
            {
                turnovers[i] = turnovers[i - 1];
                continue;
            }
            double ff = GetFatigueFactor(player);
            turnovers[i] = turnovers[i - 1] + GetAdjustedToPer48(player) * (2 - ff);
        }

        if (turnovers[5] <= 0) return 0;

        double number = RandomDouble(turnovers[5]);
        if (number >= steals[5]) return 0; // No steal

        for (int i = 1; i <= 5; i++)
        {
            if (number <= steals[i] && number > steals[i - 1])
                return i;
        }
        return 0;
    }

    private bool CheckBlock(int player, bool threePointer)
    {
        int defTeam = _state.Possession == 1 ? 2 : 1;
        double avgBlk = _leagueAvg.Blocks * 5;
        double tptBlkPct = threePointer ? 0.04 : 0.96;

        double[] blocks = new double[6];
        for (int i = 1; i <= 5; i++)
        {
            int defPlayer = _state.Lineup[defTeam, i];
            if (defPlayer <= 0 || defPlayer > 60)
            {
                blocks[i] = blocks[i - 1];
                continue;
            }
            double ff = GetFatigueFactor(defPlayer);
            blocks[i] = blocks[i - 1] + ff * _players[defPlayer].Ratings.BlocksPer48Min * tptBlkPct;
        }

        if (blocks[5] > avgBlk * 1.5)
        {
            double f = avgBlk * 1.5 / blocks[5];
            for (int i = 1; i <= 5; i++) blocks[i] *= f;
        }

        // Calculate opponent shots missed
        double[] shotsMissed = new double[6];
        for (int i = 1; i <= 5; i++)
        {
            int offPlayer = _state.Lineup[defTeam, i];
            if (offPlayer <= 0 || offPlayer > 60)
            {
                shotsMissed[i] = shotsMissed[i - 1];
                continue;
            }
            var op = _players[offPlayer];
            double fga, fgm;
            if (threePointer)
            {
                fga = GetAdjustedTfgaPer48(offPlayer);
                fgm = fga * op.Ratings.ThreePointPercentage / 1000.0;
            }
            else
            {
                fga = op.Ratings.FieldGoalsAttemptedPer48Min;
                fgm = fga * op.Ratings.FieldGoalPercentage / 1000.0;
            }
            shotsMissed[i] = shotsMissed[i - 1] + (fga - fgm);
        }

        if (shotsMissed[5] <= 0) return false;

        double number = RandomDouble(shotsMissed[5]);
        if (number > blocks[5]) return false;

        // Find blocker
        for (int i = 1; i <= 5; i++)
        {
            if (number <= blocks[i] && number > blocks[i - 1])
            {
                int blockerPlayer = _state.Lineup[defTeam, i];
                if (blockerPlayer > 0 && blockerPlayer <= 60)
                {
                    _players[blockerPlayer].GameState.Blocks++;
                    AddPbp(PlayByPlayGenerator.Block(
                        _players[blockerPlayer].LastName, _players[player].LastName));
                }
                return true;
            }
        }
        return false;
    }

    private void RecordAssist(int astPosition)
    {
        if (astPosition <= 0 || astPosition > 5) return;
        int player = _state.Lineup[_state.Possession, astPosition];
        if (player > 0 && player <= 60)
        {
            _players[player].GameState.Assists++;
            AddPbp(PlayByPlayGenerator.Assist(_players[player].LastName));
        }
    }

    #endregion

    #region Defensive Adjustments

    private void ApplyDefensiveAdjustments(int d, ref int mov, ref int pen, ref int post, int typeShot)
    {
        _state.DefensiveAdjustment = 0;

        // Determine defensive team context
        int t = d <= 5 ? 2 : 1;
        int startP = d <= 5 ? 1 : 6;

        int matchType = d < _state.MatchupType.Length ? _state.MatchupType[d] : 0;

        // Apply matchup type adjustments
        switch (matchType)
        {
            case 1: mov -= 4; pen += 2; post += 2; break;
            case 2: mov += 2; pen -= 4; post += 2; break;
            case 3: mov += 2; pen += 2; post -= 4; break;
            case 4: mov -= 2; pen -= 2; post -= 2; break;
        }

        // Non-doubled: add defensive teammate bonus
        if (matchType != 4)
        {
            for (int q = startP; q <= startP + 4; q++)
            {
                int idx = q > 5 ? q - 5 : q;
                int player = _state.Lineup[t, idx];
                if (player <= 0 || player > 60) continue;
                double adj = _players[player].Ratings.TeammatesBetterRating;
                if (q < _state.MatchupType.Length && _state.MatchupType[q] == 4 && adj > 0)
                    _state.DefensiveAdjustment += adj;
            }
        }

        // Penalty for too many double teams or defensive specializations
        for (int q = startP; q <= startP + 4; q++)
        {
            int qMatch = q < _state.MatchupType.Length ? _state.MatchupType[q] : 0;
            if (q == d) continue;
            switch (qMatch)
            {
                case 4: mov++; pen++; post++; break;
                case 3: mov++; pen++; break;
                case 2: mov++; post++; break;
                case 1: pen++; post++; break;
            }
        }
    }

    private double CalculateDefensiveDifferential(int player, int defender,
        int mov, int pen, int post, int dMov, int dPen, int dPost,
        int typeShot, int originalShot, bool forced3)
    {
        if (player <= 0 || player > 60) return 0;
        if (defender <= 0 || defender > 60) return 0;

        var def = _players[defender];
        int movDef = def.Ratings.MovementDefenseRaw;
        int penDef = def.Ratings.PenetrationDefenseRaw;
        int postDef = def.Ratings.PostDefenseRaw;
        int transDef = def.Ratings.TransitionDefenseRaw;

        // Coach defense adjustments
        int defTeam = _state.Possession == 1 ? 2 : 1;
        int adjForCoach = IntRandom(2);
        var coach = GetCoach(defTeam);
        if (coach != null)
        {
            movDef += GetCoachAdjustment(coach.CoachOutsideDefense, adjForCoach);
            penDef += GetCoachAdjustment(coach.CoachPenetrationDefense, adjForCoach);
            postDef += GetCoachAdjustment(coach.CoachInsideDefense, adjForCoach);
            transDef += GetCoachAdjustment(coach.CoachFastbreakDefense, adjForCoach);
        }

        // Position-based expected ratings
        double factor = 1 + _players[player].Ratings.MinutesPerGame / 96.0;
        int pos = _state.PositionBallHandler;
        double fo = 5, fd = 5, fp = 5;
        if (pos == 1) { fo = 1.5 * factor; fd = 4.5 * factor; fp = 4.5 * factor; }
        else if (pos == 2) { fo = 3 * factor; fd = 6 * factor; fp = 3 * factor; }
        else if (pos == 3) { fo = 3 * factor; fd = 6 * factor; fp = 3 * factor; }
        else if (pos == 4) { fo = 3 * factor; fd = 4 * factor; fp = 5 * factor; }
        else if (pos == 5) { fo = 3 * factor; fd = 3 * factor; fp = 6 * factor; }

        int ratingsTotal = mov + pen + post;
        if (ratingsTotal <= 0) ratingsTotal = 1;

        double difference = ((mov - fo) * mov + (pen - fd) * pen + (post - fp) * post);
        double diff = difference / ratingsTotal;

        double defDiff;
        if (originalShot == (int)ShotType.Fastbreak)
            defDiff = 5 - transDef;
        else if (typeShot == (int)ShotType.Outside || typeShot == (int)ShotType.Three)
            defDiff = dMov - diff - movDef;
        else if (typeShot == (int)ShotType.Penetration)
            defDiff = dPen - diff - penDef;
        else
            defDiff = dPost - diff - postDef;

        if (forced3)
            defDiff -= 4;

        // Playoff defense boost (25%)
        if (_gameType == GameType.Playoff)
            defDiff += defDiff * 0.25;

        return defDiff;
    }

    #endregion

    #region Lineup Calculation Methods

    private double CalculateLineupBetter(int player, int passer)
    {
        if (player <= 0 || player > 60) return 0;

        double better = 0;
        var ballHandlerPlayer = _players[player];
        double usual = ballHandlerPlayer.Better * 2.0 / 10.0 - 9.9;

        double posFactor = _state.PositionBallHandler == 1 ? 2.0 / 3.0 : 1.0;

        for (int i = 1; i <= 5; i++)
        {
            int lineupPlayer = _state.Lineup[_state.Possession, i];
            if (lineupPlayer == player || lineupPlayer <= 0 || lineupPlayer > 60) continue;

            double ff = GetFatigueFactor(lineupPlayer);
            if (i != passer)
                better += _players[lineupPlayer].Ratings.TeammatesBetterRating * ff * posFactor;
            else
            {
                double avgAst = _leagueAvg.AssistsByPosition[i];
                if (avgAst <= 0) avgAst = 3;
                double adjustedBetter = _players[lineupPlayer].Ratings.TeammatesBetterRating * (1 + avgAst / 10);
                better += adjustedBetter * ff;
            }
        }

        better += _state.DefensiveAdjustment;
        return (better - usual) / 5.0;
    }

    private double CalculateLineupBlocks()
    {
        int defTeam = _state.Possession == 1 ? 2 : 1;
        double avgBlk = _leagueAvg.Blocks * 5;

        double[] blocks = new double[6];
        for (int i = 1; i <= 5; i++)
        {
            int player = _state.Lineup[defTeam, i];
            if (player <= 0 || player > 60)
            {
                blocks[i] = blocks[i - 1];
                continue;
            }
            blocks[i] = blocks[i - 1] + _players[player].Ratings.BlocksPer48Min;
        }

        if (blocks[5] > avgBlk * 1.5)
        {
            double f = avgBlk * 1.5 / blocks[5];
            for (int i = 1; i <= 5; i++) blocks[i] *= f;
        }

        return blocks[5];
    }

    private double CalculateLineupSteals()
    {
        int defTeam = _state.Possession == 1 ? 2 : 1;
        double avgStl = _leagueAvg.Steals * 5;

        double[] steals = new double[6];
        for (int i = 1; i <= 5; i++)
        {
            int player = _state.Lineup[defTeam, i];
            if (player <= 0 || player > 60)
            {
                steals[i] = steals[i - 1];
                continue;
            }
            double ff = GetFatigueFactor(player);
            steals[i] = steals[i - 1] + _players[player].Ratings.StealsPer48Min;
        }

        if (steals[5] > avgStl * 1.5)
        {
            double f = avgStl * 1.5 / steals[5];
            for (int i = 1; i <= 5; i++) steals[i] *= f;
        }

        return steals[5];
    }

    private double CalculateLineupTurnovers()
    {
        double[] turnovers = new double[6];

        for (int i = 1; i <= 5; i++)
        {
            int player = _state.Lineup[_state.Possession, i];
            if (player <= 0 || player > 60)
            {
                turnovers[i] = turnovers[i - 1];
                continue;
            }
            double ff = GetFatigueFactor(player);
            double playerTo = GetAdjustedToPer48(player);
            if (_homeCourtEnabled)
                playerTo -= (_state.Possession * 2 - 3) * _hca;
            turnovers[i] = turnovers[i - 1] + playerTo;
        }

        return turnovers[5];
    }

    #endregion

    #region Substitution

    private void AutoSubstitute()
    {
        UpdateMinutesAndStamina();

        int time = GetElapsedTime();
        int margin = Math.Abs(_state.Score[1] - _state.Score[2]);

        // Garbage time check
        if ((margin > 48 && _state.Quarter < 4) ||
            (_state.Quarter == 4 && margin > ((2880 - time) / 60 + 1) * 2 + 12))
            _state.GarbageTime = true;
        else
            _state.GarbageTime = false;

        for (int t = 1; t <= 2; t++)
        {
            if (!_state.AutoSubEnabled[t]) continue;

            bool[] inLineup = new bool[61];

            for (int p = 1; p <= 5; p++)
            {
                _state.Lineup[t, p] = 0;

                // Get rotation players
                int firstString = _state.Rotation[t, p, 0].PlayerIndex;
                int topString = _state.Rotation[t, p, 1].PlayerIndex;
                int secondString = _state.Rotation[t, p, 2].PlayerIndex;
                int thirdString = _state.Rotation[t, p, 3].PlayerIndex;
                int fourthString = _state.Rotation[t, p, 4].PlayerIndex;
                int fifthString = _state.Rotation[t, p, 9].PlayerIndex;

                if (secondString <= 0) secondString = firstString;
                if (thirdString <= 0) thirdString = secondString;
                if (fourthString <= 0) fourthString = thirdString;

                // Garbage time: use non-starters
                if (_state.GarbageTime)
                {
                    if (fifthString > 0 && !IsStarter(fifthString)) firstString = fifthString;
                    else if (fourthString > 0 && !IsStarter(fourthString)) firstString = fourthString;
                    else if (thirdString > 0 && !IsStarter(thirdString)) firstString = thirdString;
                }
                if (firstString <= 0) firstString = secondString;

                // Check availability (fouls, injury, fatigue)
                bool ft1 = IsUnavailable(firstString);
                bool ftTop = IsUnavailable(topString);
                bool ft2 = IsUnavailable(secondString);
                bool ft3 = IsUnavailable(thirdString);
                bool ft4 = IsUnavailable(fourthString);
                bool ft5 = IsUnavailable(fifthString);

                bool placed = false;

                // Early game or start of 3rd quarter: use first string
                int slot = GetStatSlot(firstString);
                if ((time <= 240 || (time >= 1380 && time <= 1680))
                    && !ft1 && slot > 0 && !inLineup[slot])
                {
                    _state.Lineup[t, p] = firstString;
                    placed = true;
                }

                // End of 2nd quarter or 4th quarter: use top finisher
                slot = GetStatSlot(topString);
                if (!placed && ((time >= 1200 && time <= 1440) || time >= 2580)
                    && !ftTop && slot > 0 && !inLineup[slot] && !_state.GarbageTime)
                {
                    _state.Lineup[t, p] = topString;
                    placed = true;
                }

                // Priority-based rotation
                if (!placed)
                {
                    double total = _state.Rotation[t, p, 0].Priority + _state.Rotation[t, p, 2].Priority;
                    if (total <= 0) total = 100;
                    double n = RandomDouble(total);
                    double compare1 = _state.Rotation[t, p, 0].Priority;
                    if (compare1 < 0) compare1 = 0;

                    slot = GetStatSlot(firstString);
                    if (n < compare1 && !ft1 && slot > 0 && !inLineup[slot])
                    {
                        _state.Lineup[t, p] = firstString;
                        placed = true;
                    }

                    if (!placed)
                    {
                        slot = GetStatSlot(secondString);
                        if (!ft2 && slot > 0 && !inLineup[slot])
                        {
                            _state.Lineup[t, p] = secondString;
                            placed = true;
                        }
                    }
                    if (!placed)
                    {
                        slot = GetStatSlot(firstString);
                        if (!ft1 && slot > 0 && !inLineup[slot])
                        {
                            _state.Lineup[t, p] = firstString;
                            placed = true;
                        }
                    }
                    if (!placed)
                    {
                        slot = GetStatSlot(thirdString);
                        if (!ft3 && slot > 0 && !inLineup[slot])
                        {
                            _state.Lineup[t, p] = thirdString;
                            placed = true;
                        }
                    }
                    if (!placed)
                    {
                        slot = GetStatSlot(fourthString);
                        if (!ft4 && slot > 0 && !inLineup[slot])
                        {
                            _state.Lineup[t, p] = fourthString;
                            placed = true;
                        }
                    }
                    if (!placed)
                    {
                        slot = GetStatSlot(fifthString);
                        if (!ft5 && slot > 0 && !inLineup[slot])
                        {
                            _state.Lineup[t, p] = fifthString;
                            placed = true;
                        }
                    }
                }

                // Emergency: find ANY eligible player
                if (!placed || _state.Lineup[t, p] <= 0)
                {
                    int offset = t == 1 ? 1 : 31;
                    int rosterSize = t == 1
                        ? Math.Min(_visitorTeam.Roster.Count, 30)
                        : Math.Min(_homeTeam.Roster.Count, 30);

                    for (int i = 0; i < rosterSize; i++)
                    {
                        int idx = offset + i;
                        if (idx > 60) break;
                        var pl = _players[idx];
                        if (string.IsNullOrEmpty(pl.Name)) continue;
                        if (pl.GameState.PersonalFouls >= 6) continue;
                        if (pl.GameState.GameInjury > 3) continue;
                        int s = pl.GameState.StatSlot;
                        if (s > 0 && s <= 60 && !inLineup[s])
                        {
                            _state.Lineup[t, p] = idx;
                            placed = true;
                            break;
                        }
                    }
                }

                // Mark as in lineup
                if (_state.Lineup[t, p] > 0 && _state.Lineup[t, p] <= 60)
                {
                    int s = _players[_state.Lineup[t, p]].GameState.StatSlot;
                    if (s > 0 && s <= 60) inLineup[s] = true;
                }
            }

            // Position swap logic for mismatched positions
            if (_state.GarbageTime || HasEmptySlot(t))
            {
                SwapPositionsIfNeeded(t);
            }

            // Update defensive matchups
            int otherTeam = t == 1 ? 2 : 1;
            for (int p = 1; p <= 5; p++)
            {
                _state.Defense[otherTeam, p] = _state.Lineup[t, p];
            }
        }

        _state.MustSub[1] = 0;
        _state.MustSub[2] = 0;
    }

    private void SwapPositionsIfNeeded(int team)
    {
        // PG/SG swap: higher AST/TO ratio should be PG
        int pg = _state.Lineup[team, 1];
        int sg = _state.Lineup[team, 2];
        if (pg > 0 && pg <= 60 && sg > 0 && sg <= 60)
        {
            double pgAstTo = _players[pg].Ratings.TurnoversPer48Min > 0
                ? _players[pg].Ratings.AssistsPer48Min / _players[pg].Ratings.TurnoversPer48Min : 0;
            double sgAstTo = _players[sg].Ratings.TurnoversPer48Min > 0
                ? _players[sg].Ratings.AssistsPer48Min / _players[sg].Ratings.TurnoversPer48Min : 0;
            if (sgAstTo > pgAstTo)
            {
                _state.Lineup[team, 1] = sg;
                _state.Lineup[team, 2] = pg;
            }
        }

        // PF/C swap: higher BLK should be C
        int pf = _state.Lineup[team, 4];
        int c = _state.Lineup[team, 5];
        if (pf > 0 && pf <= 60 && c > 0 && c <= 60)
        {
            if (_players[c].Ratings.DefensiveReboundsPer48Min < _players[pf].Ratings.DefensiveReboundsPer48Min)
            {
                _state.Lineup[team, 4] = c;
                _state.Lineup[team, 5] = pf;
            }
        }
    }

    private bool HasEmptySlot(int team)
    {
        for (int p = 1; p <= 5; p++)
            if (_state.Lineup[team, p] <= 0) return true;
        return false;
    }

    private bool IsStarter(int playerIndex)
    {
        for (int i = 1; i <= 10; i++)
            if (_state.Starters[i] == playerIndex) return true;
        return false;
    }

    private bool IsUnavailable(int playerIndex)
    {
        if (playerIndex <= 0 || playerIndex > 60) return true;
        var p = _players[playerIndex];
        if (p.GameState.PersonalFouls >= 6) return true;
        if (p.GameState.GameInjury > 0) return true;
        if (p.GameState.CurrentStamina < 0) return true;
        if (IsInFoulTrouble(playerIndex) && RandomDouble(1) > 1.0 / 15.0) return true;
        return false;
    }

    private int GetStatSlot(int playerIndex)
    {
        if (playerIndex <= 0 || playerIndex > 60) return 0;
        return _players[playerIndex].GameState.StatSlot;
    }

    #endregion

    #region Clock and Timing

    private void AdvanceClock(bool putBack, int originalShot)
    {
        int seconds;
        if (putBack)
        {
            seconds = IntRandom(3);
        }
        else if (originalShot == (int)ShotType.Fastbreak)
        {
            seconds = IntRandom(3) + 2;
        }
        else
        {
            double avgTime = _state.AverageTime;
            if (avgTime < 1) avgTime = 14;
            double f = avgTime / 2;
            double dSeconds = RandomDouble(avgTime) + (avgTime - f);
            double r = dSeconds - (int)dSeconds;
            seconds = r < 0.5 ? (int)dSeconds : (int)dSeconds + 1;
            if (seconds == (int)avgTime) seconds = IntRandom(21) + 3;
        }

        if (_state.MilkClock)
        {
            seconds = IntRandom(21) + 3;
            _state.MilkClock = false;
        }

        _state.TimeRemainingSeconds -= seconds;

        if (_state.TimeRemainingSeconds <= 0)
        {
            AdvanceQuarter();
            _state.QuarterEnded = true;
        }

        // Prevent putback from ending OT game
        if (putBack && _state.Quarter >= 5 && _state.TimeRemainingSeconds == 300)
        {
            _state.Quarter--;
            _state.QuarterEnded = false;
            _state.TimeRemainingSeconds = 1;
        }
    }

    private void AdvanceQuarter()
    {
        _state.Quarter++;
        _state.TimeRemainingSeconds = _state.Quarter < 5 ? 720 : 300;
    }

    private void SetAverageTime(double avgPoss, double avgOreb)
    {
        double factor = _state.AveragePlayerMinFactor;
        if (factor > 1) factor = 1;

        double vFga = _teamRatings[1].Ratings.FieldGoalsAttemptedPer48Min * factor;
        double vTfga = _teamRatings[1].Ratings.ThreePointersAttemptedPer48Min * factor;
        double vTo = _teamRatings[1].Ratings.TurnoversPer48Min * factor;
        double vFd = _teamRatings[1].Ratings.FoulsDrawnPer48Min * factor;

        double hFga = _teamRatings[2].Ratings.FieldGoalsAttemptedPer48Min * factor;
        double hTfga = _teamRatings[2].Ratings.ThreePointersAttemptedPer48Min * factor;
        double hTo = _teamRatings[2].Ratings.TurnoversPer48Min * factor;
        double hFd = _teamRatings[2].Ratings.FoulsDrawnPer48Min * factor;

        double visitorPoss = (vFga + vTfga + vTo + vFd) * factor;
        double homePoss = (hFga + hTfga + hTo + hFd) * factor;

        double pb = (_teamRatings[1].Ratings.OffensiveReboundsPer48Min * factor +
                     _teamRatings[2].Ratings.OffensiveReboundsPer48Min * factor) * 5.0 / 8.0;
        double fbv = (_teamRatings[1].Ratings.DefensiveReboundsPer48Min * factor +
                      _teamRatings[1].Ratings.StealsPer48Min * factor) / 5.0;
        double fbh = (_teamRatings[2].Ratings.DefensiveReboundsPer48Min * factor +
                      _teamRatings[2].Ratings.StealsPer48Min * factor) / 5.0;
        double fb = (fbv + fbh) * 7.0 / 5.0;
        double avgFbChances = avgPoss * factor * 2 / 10;
        avgOreb = avgOreb * factor * 2 * 5.0 / 8.0;
        double poss = avgPoss * factor * 2 - pb - fb;
        double possessions = (visitorPoss + homePoss - avgOreb - avgFbChances) * 2 - poss;

        if (possessions <= 0) possessions = 200;

        _state.AverageTime = (2880 - pb * 2 - fb * 4) / possessions;
        _state.AverageTime = Math.Clamp(_state.AverageTime, 13, 16);
        _state.AverageTime *= (2 - _state.ScoringFactor);
        if (_state.AverageTime < 1 || _state.AverageTime > 24) _state.AverageTime = 24;
    }

    private void UpdateMinutesAndStamina()
    {
        int currentTime = GetElapsedTime();
        int timePlayed = currentTime - _state.PreviousTimeElapsed;
        if (timePlayed <= 0) return;

        bool[] inLineup = new bool[61];

        for (int t = 1; t <= 2; t++)
        {
            for (int j = 1; j <= 5; j++)
            {
                int player = _state.Lineup[t, j];
                if (player <= 0 || player > 60) continue;
                _players[player].GameState.Minutes += timePlayed;

                int slot = _players[player].GameState.StatSlot;
                if (slot > 0 && slot <= 60) inLineup[slot] = true;
            }
        }

        // Update stamina for all playing players
        for (int i = 1; i <= 60; i++)
        {
            int pl = _state.PlayingPlayersList[i];
            if (pl <= 0 || pl > 60) continue;

            int staminaRating = _players[pl].Ratings.Stamina;
            if (staminaRating <= 0) staminaRating = 80;
            int currentStamina = _players[pl].GameState.CurrentStamina;

            if (inLineup[i])
            {
                _players[pl].GameState.CurrentStamina = currentStamina - timePlayed;
            }
            else
            {
                _players[pl].GameState.CurrentStamina = currentStamina + timePlayed * 3;
                if (_players[pl].GameState.CurrentStamina > staminaRating)
                    _players[pl].GameState.CurrentStamina = staminaRating;
            }
        }

        _state.PreviousTimeElapsed = currentTime;
    }

    private int GetElapsedTime()
    {
        if (_state.Quarter <= 4)
            return 720 - _state.TimeRemainingSeconds + (_state.Quarter - 1) * 720;
        else
            return 2880 + (300 - _state.TimeRemainingSeconds) + (_state.Quarter - 5) * 300;
    }

    #endregion

    #region Quarter/Game End

    private void HandleQuarterEnd()
    {
        int q = _state.Quarter;

        // Swap possession
        if (_state.FirstPossession == 1) _state.FirstPossession = 2;
        else _state.FirstPossession = 1;
        _state.Possession = _state.FirstPossession;

        _state.FastbreakChance = false;

        // Calculate quarter scores (q-1 because quarter was already advanced)
        CalculateQuarterScore(q - 1);

        AddPbp(PlayByPlayGenerator.EndOfQuarter(q - 1));

        // Entering overtime
        if (q == 5)
            AddPbp(PlayByPlayGenerator.Overtime());

        // Reset team fouls for new quarter
        _state.TeamFouls[1] = 0;
        _state.TeamFouls[2] = 0;

        // Half-time stamina reset
        if (q == 3)
        {
            for (int i = 1; i <= 60; i++)
            {
                int pl = _state.PlayingPlayersList[i];
                if (pl <= 0 || pl > 60) continue;
                int staminaRating = _players[pl].Ratings.Stamina;
                if (staminaRating <= 0) staminaRating = 80;
                _players[pl].GameState.CurrentStamina = staminaRating;
            }
        }

        AutoSubstitute();
    }

    private void CalculateQuarterScore(int quarter)
    {
        if (quarter <= 0 || quarter > 5) return;

        int prevVisitor = 0, prevHome = 0;
        for (int q = 1; q < quarter; q++)
        {
            prevVisitor += _state.QuarterScores[1, q];
            prevHome += _state.QuarterScores[2, q];
        }

        if (_state.QuarterScores[1, quarter] == 0)
        {
            _state.QuarterScores[1, quarter] = _state.Score[1] - prevVisitor;
            _state.QuarterScores[2, quarter] = _state.Score[2] - prevHome;
        }
    }

    private void HandleGameEnd()
    {
        // Quarter was already advanced by AdvanceClock, so the quarter
        // that just ended is Quarter - 1.
        int finishedQuarter = _state.Quarter - 1;

        AddPbp(PlayByPlayGenerator.EndOfQuarter(finishedQuarter));
        AddPbp(PlayByPlayGenerator.EndOfGame());

        // Calculate the score for the quarter that just ended
        CalculateQuarterScore(finishedQuarter);

        UpdateMinutesAndStamina();
        _state.GameOver = true;
    }

    #endregion

    #region Finalization

    private GameResult FinalizeGame()
    {
        // Build box scores
        var visitorBox = new List<PlayerGameState>();
        var homeBox = new List<PlayerGameState>();

        for (int i = 1; i <= 30 && i <= _visitorTeam.Roster.Count; i++)
        {
            if (_players[i] != null && _players[i].GameState.Minutes > 0)
                visitorBox.Add(_players[i].GameState);
        }
        for (int i = 31; i <= 60 && i - 30 <= _homeTeam.Roster.Count; i++)
        {
            if (_players[i] != null && _players[i].GameState.Minutes > 0)
                homeBox.Add(_players[i].GameState);
        }

        int mvpIndex = DetermineGameMvp();

        return new GameResult
        {
            VisitorScore = _state.Score[1],
            HomeScore = _state.Score[2],
            QuarterScores = _state.QuarterScores,
            QuartersPlayed = _state.Quarter > 4 ? _state.Quarter - 1 : 4,
            VisitorBoxScore = visitorBox,
            HomeBoxScore = homeBox,
            MvpPlayerIndex = mvpIndex,
            PlayByPlay = _state.PlayByPlay,
            VisitorTeamIndex = _state.VisitorIndex,
            HomeTeamIndex = _state.HomeIndex
        };
    }

    private int DetermineGameMvp()
    {
        int bestPlayer = -1;
        double bestRating = -1;

        for (int i = 1; i <= 60; i++)
        {
            if (_players[i] == null || string.IsNullOrEmpty(_players[i].Name)) continue;
            var gs = _players[i].GameState;
            if (gs.Minutes <= 0) continue;

            // Simple MVP formula: points + rebounds*1.2 + assists*1.5 + steals*2 + blocks*2 - turnovers
            double rating = gs.Points + gs.TotalRebounds * 1.2 + gs.Assists * 1.5
                            + gs.Steals * 2 + gs.Blocks * 2 - gs.Turnovers;

            if (rating > bestRating)
            {
                bestRating = rating;
                bestPlayer = i;
            }
        }

        return bestPlayer;
    }

    #endregion

    #region Helper Methods

    private bool IsInFoulTrouble(int playerIndex)
    {
        if (playerIndex <= 0 || playerIndex > 60) return false;
        var p = _players[playerIndex];
        int fouls = p.GameState.PersonalFouls;
        int time = GetElapsedTime();

        if ((_state.Quarter == 1 && fouls >= 1) ||
            (_state.Quarter == 2 && fouls >= 2) ||
            (_state.Quarter == 3 && fouls >= 3) ||
            (_state.Quarter >= 4 && time < 2580 && fouls >= 4) ||
            fouls >= 5)
            return true;

        return false;
    }

    private double GetFatigueFactor(int playerIndex)
    {
        if (playerIndex <= 0 || playerIndex > 60) return 1.0;
        double stamina = _players[playerIndex].GameState.CurrentStamina;
        double factor = (100 + stamina / 30.0) / 100.0;
        return Math.Min(1.0, factor);
    }

    private double GetAdjustedFgaPer48(int playerIndex)
    {
        if (playerIndex <= 0 || playerIndex > 60) return 5;
        var r = _players[playerIndex].Ratings;
        return r.AdjustedFieldGoalsAttemptedPer48Min > 0
            ? r.AdjustedFieldGoalsAttemptedPer48Min : r.FieldGoalsAttemptedPer48Min;
    }

    private double GetAdjustedTfgaPer48(int playerIndex)
    {
        if (playerIndex <= 0 || playerIndex > 60) return 1;
        var r = _players[playerIndex].Ratings;
        return r.AdjustedThreePointersAttemptedPer48Min > 0
            ? r.AdjustedThreePointersAttemptedPer48Min : r.ThreePointersAttemptedPer48Min;
    }

    private double GetAdjustedFoulsDrawnPer48(int playerIndex)
    {
        if (playerIndex <= 0 || playerIndex > 60) return 1;
        var r = _players[playerIndex].Ratings;
        return r.AdjustedFoulsDrawnPer48Min > 0
            ? r.AdjustedFoulsDrawnPer48Min : r.FoulsDrawnPer48Min;
    }

    private double GetAdjustedToPer48(int playerIndex)
    {
        if (playerIndex <= 0 || playerIndex > 60) return 1;
        var r = _players[playerIndex].Ratings;
        return r.AdjustedTurnoversPer48Min > 0
            ? r.AdjustedTurnoversPer48Min : r.TurnoversPer48Min;
    }

    private void AdjustOffenseForGamePlan(Player p, ref int mov, ref int pen, ref int post, ref int trans)
    {
        switch (p.OffensiveFocus)
        {
            case 1:
                mov++;
                if (RandomDouble(1) <= 0.5) pen--; else post--;
                break;
            case 2:
                pen++;
                if (RandomDouble(1) <= 0.5) mov--; else post--;
                break;
            case 3:
                post++;
                if (RandomDouble(1) <= 0.5) mov--; else pen--;
                break;
        }
    }

    private void ApplyCoachOffenseAdjustments(int team, int adjForCoach,
        ref int dMov, ref int dPen, ref int dPost)
    {
        var coach = GetCoach(team);
        if (coach == null) return;

        dMov += GetCoachAdjustment(coach.CoachOutside, adjForCoach);
        dPen += GetCoachAdjustment(coach.CoachPenetration, adjForCoach);
        dPost += GetCoachAdjustment(coach.CoachInside, adjForCoach);
    }

    private static int GetCoachAdjustment(int coachRating, int adjForCoach)
    {
        if ((coachRating == 4 && adjForCoach == 1) || coachRating == 5) return 1;
        if ((coachRating == 2 && adjForCoach == 1) || coachRating == 1) return -1;
        return 0;
    }

    private Models.Staff.StaffMember? GetCoach(int teamSide)
    {
        return teamSide == 1 ? _visitorTeam.Coach : _homeTeam.Coach;
    }

    private string GetTeamName(int side)
    {
        return side == 1 ? _visitorTeam.Name : _homeTeam.Name;
    }

    private void PossessionChange()
    {
        _state.Possession = _state.Possession == 1 ? 2 : 1;
    }

    private void AddPbp(string text)
    {
        if (!string.IsNullOrEmpty(text))
            _state.PlayByPlay.Add(text);
    }

    private static int GetPositionIndex(string? position)
    {
        return position?.Trim().ToUpper() switch
        {
            "PG" => 1,
            "SG" => 2,
            "SF" => 3,
            "PF" => 4,
            "C" => 5,
            _ => 1
        };
    }

    // Random number helpers matching C++ IntRandom/Random behavior
    private int IntRandom(int n)
    {
        if (n <= 0) return 1;
        return _random.Next(0, n) + 1; // 1 to n inclusive
    }

    private double RandomDouble(double n)
    {
        if (n <= 0) return 0;
        return _random.NextDouble() * n; // 0 to n exclusive
    }

    #endregion
}
