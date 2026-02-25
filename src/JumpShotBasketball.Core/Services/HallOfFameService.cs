using JumpShotBasketball.Core.Models.Awards;
using JumpShotBasketball.Core.Models.Game;
using JumpShotBasketball.Core.Models.History;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Hall of Fame evaluation service.
/// Ported from CPlayer::HallofFamer() + CPlayer::GetAwardPoints() — Player.cpp:1708-2089.
/// Calculates lifetime award points and determines HOF eligibility for retired players.
/// </summary>
public static class HallOfFameService
{
    /// <summary>
    /// Calculates total award points and major points for a player across all seasons.
    /// Ported from CPlayer::GetAwardPoints() — Player.cpp:1735-2089.
    /// </summary>
    public static (int awardPoints, int majorPoints) CalculateAwardPoints(
        int playerId,
        List<SeasonAwards> awardsHistory,
        List<AllStarWeekendResult> allStarHistory)
    {
        int awardPoints = 0;
        int majorPoints = 0;

        // Process each season's awards
        foreach (var season in awardsHistory)
        {
            // MVP (rank 1-5): (6-rank)*3 points; 1st place: +10 major
            int mvpRank = FindPlayerRank(season.Mvp, playerId);
            if (mvpRank > 0)
            {
                awardPoints += (6 - mvpRank) * 3;
                if (mvpRank == 1) majorPoints += 10;
            }

            // DPOY (rank 1-5): (8-rank) points; (6-rank)*1 major; 1st: +5 extra major
            int dpoyRank = FindPlayerRank(season.DefensivePlayerOfYear, playerId);
            if (dpoyRank > 0)
            {
                awardPoints += 8 - dpoyRank;
                majorPoints += 6 - dpoyRank;
                if (dpoyRank == 1) majorPoints += 5;
            }

            // ROY (rank 1-5): 3 points flat
            int royRank = FindPlayerRank(season.RookieOfYear, playerId);
            if (royRank > 0)
            {
                awardPoints += 3;
            }

            // Scoring leader (rank 1-5): (8-rank) points
            int scoringRank = FindPlayerRank(season.ScoringLeader, playerId);
            if (scoringRank > 0)
            {
                awardPoints += 8 - scoringRank;
            }

            // Rebounding leader (rank 1-5): (7-rank) points
            int rebRank = FindPlayerRank(season.ReboundingLeader, playerId);
            if (rebRank > 0)
            {
                awardPoints += 7 - rebRank;
            }

            // Assists leader (rank 1-5): (7-rank) points
            int astRank = FindPlayerRank(season.AssistsLeader, playerId);
            if (astRank > 0)
            {
                awardPoints += 7 - astRank;
            }

            // Steals leader (rank 1-5): (7-rank) points
            int stlRank = FindPlayerRank(season.StealsLeader, playerId);
            if (stlRank > 0)
            {
                awardPoints += 7 - stlRank;
            }

            // Blocks leader (rank 1-5): (7-rank) points
            int blkRank = FindPlayerRank(season.BlocksLeader, playerId);
            if (blkRank > 0)
            {
                awardPoints += 7 - blkRank;
            }

            // Championship ring: 5 points
            if (season.RingRecipientPlayerIds.Contains(playerId))
            {
                awardPoints += 5;
            }

            // Playoff MVP (rank 1 only): 10 points
            int playoffMvpRank = FindPlayerRank(season.PlayoffMvp, playerId);
            if (playoffMvpRank == 1)
            {
                awardPoints += 10;
            }

            // 6th Man (rank 1-5): 4 points flat (3+1 per C++)
            int sixthManRank = FindPlayerRank(season.SixthMan, playerId);
            if (sixthManRank > 0)
            {
                awardPoints += 4;
            }

            // All-League teams: (7-teamNumber) points; 1st team: +5 major
            int allLeagueTeamNum = FindPlayerTeamNumber(season.AllLeagueTeams, playerId);
            if (allLeagueTeamNum > 0)
            {
                awardPoints += 7 - allLeagueTeamNum;
                if (allLeagueTeamNum == 1) majorPoints += 5;
            }

            // All-Defense teams: (6-teamNumber) points
            int allDefenseTeamNum = FindPlayerTeamNumber(season.AllDefenseTeams, playerId);
            if (allDefenseTeamNum > 0)
            {
                awardPoints += 6 - allDefenseTeamNum;
            }

            // All-Rookie teams: 0 points (no points in C++)
            // Intentionally no points awarded
        }

        // Process All-Star Weekend history
        foreach (var weekend in allStarHistory)
        {
            // All-Star selection: 3 points + 5 major
            bool isAllStar = weekend.Conference1Roster.Contains(playerId) ||
                             weekend.Conference2Roster.Contains(playerId);
            if (isAllStar)
            {
                awardPoints += 3;
                majorPoints += 5;
            }

            // All-Star MVP: 4 points (highest scorer)
            // GameResult box scores use PlayerPointer as the player identifier.
            if (weekend.AllStarGameResult != null)
            {
                int allStarMvpId = FindGameHighestScorerPointer(weekend.AllStarGameResult);
                if (allStarMvpId == playerId)
                {
                    awardPoints += 4;
                }
            }

            // 3-Point contest champion: 1 point
            if (weekend.ThreePointWinnerId == playerId)
            {
                awardPoints += 1;
            }

            // Dunk contest champion: 1 point
            if (weekend.DunkWinnerId == playerId)
            {
                awardPoints += 1;
            }
        }

        return (awardPoints, majorPoints);
    }

    /// <summary>
    /// Determines if a player qualifies for the Hall of Fame.
    /// Ported from CPlayer::HallofFamer() — Player.cpp:1708-1732.
    /// Three paths to induction:
    /// 1. total_pts >= 200 AND YOS >= 7 AND major >= 20
    /// 2. stats_pts >= 150 AND total_pts >= 180 AND YOS >= 7 AND major >= 20
    /// 3. awa_pts > 120 AND major >= 20
    /// </summary>
    public static bool IsHallOfFamer(Player player, int awardPoints, int majorPoints)
    {
        if (player.CareerStats.Games <= 0) return false;

        double games = player.CareerStats.Games;
        double ppg = (player.CareerStats.FieldGoalsMade * 2 +
                      player.CareerStats.ThreePointersMade * 3 +
                      player.CareerStats.FreeThrowsMade) / games;
        double rpg = player.CareerStats.Rebounds / games;
        double apg = player.CareerStats.Assists / games;
        double spg = player.CareerStats.Steals / games;
        double bpg = player.CareerStats.Blocks / games;

        double statsPts = (ppg + rpg + apg + spg + bpg) * 5;
        double totalPts = statsPts + awardPoints;

        int yos = player.Contract.YearsOfService;

        // Path 1: dominant total
        if (totalPts >= 200 && yos >= 7 && majorPoints >= 20)
            return true;

        // Path 2: strong stats + good awards
        if (statsPts >= 150 && totalPts >= 180 && yos >= 7 && majorPoints >= 20)
            return true;

        // Path 3: award-driven (no YOS requirement)
        if (awardPoints > 120 && majorPoints >= 20)
            return true;

        return false;
    }

    /// <summary>
    /// Evaluates all retired players across the league for Hall of Fame induction.
    /// Returns a list of new inductees.
    /// </summary>
    public static List<HallOfFameEntry> EvaluateRetiredPlayersForHallOfFame(League league)
    {
        var inductees = new List<HallOfFameEntry>();

        foreach (var team in league.Teams)
        {
            foreach (var player in team.Roster)
            {
                if (!player.Retired) continue;
                if (string.IsNullOrEmpty(player.Name)) continue;

                var (awardPts, majorPts) = CalculateAwardPoints(
                    player.Id, league.AwardsHistory, league.AllStarWeekendHistory);

                if (IsHallOfFamer(player, awardPts, majorPts))
                {
                    double games = player.CareerStats.Games > 0 ? player.CareerStats.Games : 1;
                    double ppg = (player.CareerStats.FieldGoalsMade * 2 +
                                  player.CareerStats.ThreePointersMade * 3 +
                                  player.CareerStats.FreeThrowsMade) / games;

                    inductees.Add(new HallOfFameEntry
                    {
                        PlayerId = player.Id,
                        PlayerName = player.Name,
                        Position = player.Position,
                        AwardPoints = awardPts,
                        MajorPoints = majorPts,
                        YearsOfService = player.Contract.YearsOfService,
                        CareerPpg = ppg,
                        CareerRpg = player.CareerStats.Rebounds / games,
                        CareerApg = player.CareerStats.Assists / games,
                        InductionYear = league.Settings.CurrentYear
                    });
                }
            }
        }

        return inductees;
    }

    /// <summary>
    /// Finds a player's rank (1-5) in an award result. Returns 0 if not found.
    /// </summary>
    internal static int FindPlayerRank(AwardResult award, int playerId)
    {
        if (award?.Recipients == null) return 0;

        foreach (var recipient in award.Recipients)
        {
            if (recipient.PlayerId == playerId)
                return recipient.Rank;
        }

        return 0;
    }

    /// <summary>
    /// Finds which team number (1-5) a player was selected to. Returns 0 if not found.
    /// </summary>
    internal static int FindPlayerTeamNumber(List<AllTeamSelection> teams, int playerId)
    {
        if (teams == null) return 0;

        foreach (var team in teams)
        {
            foreach (var player in team.Players)
            {
                if (player.PlayerId == playerId)
                    return team.TeamNumber;
            }
        }

        return 0;
    }

    /// <summary>
    /// Finds the highest scorer in a game result by checking both box score lists.
    /// Returns the PlayerPointer of the highest scorer, or 0 if not determinable.
    /// PlayerPointer maps to the player's Id when set by the engine.
    /// </summary>
    internal static int FindGameHighestScorerPointer(GameResult gameResult)
    {
        int highestPoints = 0;
        int mvpPointer = 0;

        foreach (var box in gameResult.VisitorBoxScore)
        {
            if (box.Points > highestPoints)
            {
                highestPoints = box.Points;
                mvpPointer = box.PlayerPointer;
            }
        }

        foreach (var box in gameResult.HomeBoxScore)
        {
            if (box.Points > highestPoints)
            {
                highestPoints = box.Points;
                mvpPointer = box.PlayerPointer;
            }
        }

        return mvpPointer;
    }
}
