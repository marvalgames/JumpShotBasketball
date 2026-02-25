using FluentAssertions;
using JumpShotBasketball.Core.Models.Awards;
using JumpShotBasketball.Core.Models.Game;
using JumpShotBasketball.Core.Models.History;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class HallOfFameServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────

    private static SeasonAwards CreateEmptyAwards(int year = 1)
    {
        return new SeasonAwards { Year = year };
    }

    private static AwardRecipient MakeRecipient(int playerId, int rank, string name = "Player")
    {
        return new AwardRecipient { PlayerId = playerId, Rank = rank, PlayerName = name };
    }

    private static AllStarWeekendResult CreateEmptyWeekend()
    {
        return new AllStarWeekendResult();
    }

    private static Player CreateCareerPlayer(int id, int games, int fgm, int tfgm, int ftm,
        int reb, int ast, int stl, int blk, int yos)
    {
        return new Player
        {
            Id = id,
            Name = $"Player{id}",
            Position = "SG",
            CareerStats = new PlayerStatLine
            {
                Games = games,
                FieldGoalsMade = fgm,
                ThreePointersMade = tfgm,
                FreeThrowsMade = ftm,
                Rebounds = reb,
                Assists = ast,
                Steals = stl,
                Blocks = blk
            },
            Contract = new PlayerContract { YearsOfService = yos }
        };
    }

    // ── CalculateAwardPoints: MVP ───────────────────────────────────

    [Fact]
    public void Mvp1stPlace_15Points_10Major()
    {
        var awards = CreateEmptyAwards();
        awards.Mvp.Recipients.Add(MakeRecipient(100, 1));

        var (pts, major) = HallOfFameService.CalculateAwardPoints(
            100, new List<SeasonAwards> { awards }, new List<AllStarWeekendResult>());

        pts.Should().Be(15); // (6-1)*3
        major.Should().Be(10);
    }

    [Fact]
    public void Mvp3rdPlace_9Points_0Major()
    {
        var awards = CreateEmptyAwards();
        awards.Mvp.Recipients.Add(MakeRecipient(100, 3));

        var (pts, major) = HallOfFameService.CalculateAwardPoints(
            100, new List<SeasonAwards> { awards }, new List<AllStarWeekendResult>());

        pts.Should().Be(9); // (6-3)*3
        major.Should().Be(0);
    }

    // ── CalculateAwardPoints: DPOY ──────────────────────────────────

    [Fact]
    public void Dpoy1stPlace_7Points_10Major()
    {
        var awards = CreateEmptyAwards();
        awards.DefensivePlayerOfYear.Recipients.Add(MakeRecipient(100, 1));

        var (pts, major) = HallOfFameService.CalculateAwardPoints(
            100, new List<SeasonAwards> { awards }, new List<AllStarWeekendResult>());

        pts.Should().Be(7); // 8-1
        major.Should().Be(10); // (6-1)*1 + 5
    }

    [Fact]
    public void Dpoy3rdPlace_5Points_3Major()
    {
        var awards = CreateEmptyAwards();
        awards.DefensivePlayerOfYear.Recipients.Add(MakeRecipient(100, 3));

        var (pts, major) = HallOfFameService.CalculateAwardPoints(
            100, new List<SeasonAwards> { awards }, new List<AllStarWeekendResult>());

        pts.Should().Be(5); // 8-3
        major.Should().Be(3); // (6-3)*1
    }

    // ── CalculateAwardPoints: ROY ───────────────────────────────────

    [Fact]
    public void RookieOfYear_3Points()
    {
        var awards = CreateEmptyAwards();
        awards.RookieOfYear.Recipients.Add(MakeRecipient(100, 1));

        var (pts, _) = HallOfFameService.CalculateAwardPoints(
            100, new List<SeasonAwards> { awards }, new List<AllStarWeekendResult>());

        pts.Should().Be(3); // flat
    }

    // ── CalculateAwardPoints: Scoring Leader ────────────────────────

    [Fact]
    public void ScoringLeader1st_7Points()
    {
        var awards = CreateEmptyAwards();
        awards.ScoringLeader.Recipients.Add(MakeRecipient(100, 1));

        var (pts, _) = HallOfFameService.CalculateAwardPoints(
            100, new List<SeasonAwards> { awards }, new List<AllStarWeekendResult>());

        pts.Should().Be(7); // 8-1
    }

    // ── CalculateAwardPoints: Rebounding Leader ─────────────────────

    [Fact]
    public void ReboundingLeader2nd_5Points()
    {
        var awards = CreateEmptyAwards();
        awards.ReboundingLeader.Recipients.Add(MakeRecipient(100, 2));

        var (pts, _) = HallOfFameService.CalculateAwardPoints(
            100, new List<SeasonAwards> { awards }, new List<AllStarWeekendResult>());

        pts.Should().Be(5); // 7-2
    }

    // ── CalculateAwardPoints: Championship Ring ─────────────────────

    [Fact]
    public void ChampionshipRing_5Points()
    {
        var awards = CreateEmptyAwards();
        awards.RingRecipientPlayerIds.Add(100);

        var (pts, _) = HallOfFameService.CalculateAwardPoints(
            100, new List<SeasonAwards> { awards }, new List<AllStarWeekendResult>());

        pts.Should().Be(5);
    }

    // ── CalculateAwardPoints: Playoff MVP ───────────────────────────

    [Fact]
    public void PlayoffMvp_10Points()
    {
        var awards = CreateEmptyAwards();
        awards.PlayoffMvp.Recipients.Add(MakeRecipient(100, 1));

        var (pts, _) = HallOfFameService.CalculateAwardPoints(
            100, new List<SeasonAwards> { awards }, new List<AllStarWeekendResult>());

        pts.Should().Be(10);
    }

    // ── CalculateAwardPoints: 6th Man ───────────────────────────────

    [Fact]
    public void SixthMan_4Points()
    {
        var awards = CreateEmptyAwards();
        awards.SixthMan.Recipients.Add(MakeRecipient(100, 1));

        var (pts, _) = HallOfFameService.CalculateAwardPoints(
            100, new List<SeasonAwards> { awards }, new List<AllStarWeekendResult>());

        pts.Should().Be(4); // 3+1 flat
    }

    // ── CalculateAwardPoints: All-League Teams ──────────────────────

    [Fact]
    public void AllLeague1stTeam_6Points_5Major()
    {
        var awards = CreateEmptyAwards();
        awards.AllLeagueTeams.Add(new AllTeamSelection
        {
            TeamNumber = 1,
            TeamLabel = "1st Team",
            Players = new List<AwardRecipient> { MakeRecipient(100, 1) }
        });

        var (pts, major) = HallOfFameService.CalculateAwardPoints(
            100, new List<SeasonAwards> { awards }, new List<AllStarWeekendResult>());

        pts.Should().Be(6); // 7-1
        major.Should().Be(5);
    }

    // ── CalculateAwardPoints: All-Defense Teams ─────────────────────

    [Fact]
    public void AllDefense2ndTeam_4Points()
    {
        var awards = CreateEmptyAwards();
        awards.AllDefenseTeams.Add(new AllTeamSelection
        {
            TeamNumber = 2,
            TeamLabel = "2nd Team",
            Players = new List<AwardRecipient> { MakeRecipient(100, 1) }
        });

        var (pts, _) = HallOfFameService.CalculateAwardPoints(
            100, new List<SeasonAwards> { awards }, new List<AllStarWeekendResult>());

        pts.Should().Be(4); // 6-2
    }

    // ── CalculateAwardPoints: All-Rookie Teams ──────────────────────

    [Fact]
    public void AllRookieTeam_0Points()
    {
        var awards = CreateEmptyAwards();
        awards.AllRookieTeams.Add(new AllTeamSelection
        {
            TeamNumber = 1,
            TeamLabel = "1st Team",
            Players = new List<AwardRecipient> { MakeRecipient(100, 1) }
        });

        var (pts, major) = HallOfFameService.CalculateAwardPoints(
            100, new List<SeasonAwards> { awards }, new List<AllStarWeekendResult>());

        pts.Should().Be(0);
        major.Should().Be(0);
    }

    // ── CalculateAwardPoints: All-Star Selection ────────────────────

    [Fact]
    public void AllStarSelection_3Points_5Major()
    {
        var weekend = CreateEmptyWeekend();
        weekend.Conference1Roster.Add(100);

        var (pts, major) = HallOfFameService.CalculateAwardPoints(
            100, new List<SeasonAwards>(), new List<AllStarWeekendResult> { weekend });

        pts.Should().Be(3);
        major.Should().Be(5);
    }

    [Fact]
    public void AllStarSelection_Conference2Roster_3Points_5Major()
    {
        var weekend = CreateEmptyWeekend();
        weekend.Conference2Roster.Add(100);

        var (pts, major) = HallOfFameService.CalculateAwardPoints(
            100, new List<SeasonAwards>(), new List<AllStarWeekendResult> { weekend });

        pts.Should().Be(3);
        major.Should().Be(5);
    }

    // ── CalculateAwardPoints: Contest Winners ───────────────────────

    [Fact]
    public void ContestWinner_1PointEach()
    {
        var weekend = CreateEmptyWeekend();
        weekend.ThreePointWinnerId = 100;
        weekend.DunkWinnerId = 100;

        var (pts, _) = HallOfFameService.CalculateAwardPoints(
            100, new List<SeasonAwards>(), new List<AllStarWeekendResult> { weekend });

        pts.Should().Be(2); // 1+1
    }

    // ── CalculateAwardPoints: Multiple Seasons ──────────────────────

    [Fact]
    public void MultipleSeasons_Accumulates()
    {
        var awards1 = CreateEmptyAwards(1);
        awards1.Mvp.Recipients.Add(MakeRecipient(100, 1)); // 15 pts + 10 major

        var awards2 = CreateEmptyAwards(2);
        awards2.Mvp.Recipients.Add(MakeRecipient(100, 2)); // 12 pts

        var weekend1 = CreateEmptyWeekend();
        weekend1.Conference1Roster.Add(100); // 3 pts + 5 major

        var (pts, major) = HallOfFameService.CalculateAwardPoints(
            100,
            new List<SeasonAwards> { awards1, awards2 },
            new List<AllStarWeekendResult> { weekend1 });

        pts.Should().Be(15 + 12 + 3); // 30
        major.Should().Be(10 + 5); // 15
    }

    // ── CalculateAwardPoints: Not Found ─────────────────────────────

    [Fact]
    public void PlayerNotInAwards_ZeroPoints()
    {
        var awards = CreateEmptyAwards();
        awards.Mvp.Recipients.Add(MakeRecipient(999, 1)); // different player

        var (pts, major) = HallOfFameService.CalculateAwardPoints(
            100, new List<SeasonAwards> { awards }, new List<AllStarWeekendResult>());

        pts.Should().Be(0);
        major.Should().Be(0);
    }

    // ── CalculateAwardPoints: All-Star MVP ──────────────────────────

    [Fact]
    public void AllStarMvp_4Points_HighestScorer()
    {
        var weekend = CreateEmptyWeekend();
        weekend.Conference1Roster.Add(100);
        var mvpBox = new PlayerGameState
        {
            FieldGoalsMade = 10,
            ThreePointersMade = 3,
            FreeThrowsMade = 5,
            PlayerPointer = 100
        };
        var otherBox = new PlayerGameState
        {
            FieldGoalsMade = 5,
            ThreePointersMade = 1,
            FreeThrowsMade = 2,
            PlayerPointer = 200
        };
        weekend.AllStarGameResult = new GameResult
        {
            VisitorBoxScore = new List<PlayerGameState> { mvpBox },
            HomeBoxScore = new List<PlayerGameState> { otherBox }
        };

        var (pts, _) = HallOfFameService.CalculateAwardPoints(
            100, new List<SeasonAwards>(), new List<AllStarWeekendResult> { weekend });

        // 3 (all-star) + 4 (all-star MVP) = 7
        pts.Should().Be(7);
    }

    // ── IsHallOfFamer ───────────────────────────────────────────────

    [Fact]
    public void Path1_TotalPts200_Yos7_Major20()
    {
        // Need (ppg+rpg+apg+spg+bpg)*5 + awardPts >= 200, yos>=7, major>=20
        // ppg=25 (fgm*2+tfgm*3+ftm=25*g), rpg=10, apg=5, spg=2, bpg=1 => stats=43*5=215
        // totalPts = 215 + 0 = 215 >= 200 ✓
        var player = CreateCareerPlayer(1, 100, 1000, 100, 200, 1000, 500, 200, 100, 8);
        // ppg = (1000*2+100*3+200)/100 = 2500/100 = 25
        // rpg = 1000/100 = 10, apg = 500/100 = 5, spg = 200/100 = 2, bpg = 100/100 = 1
        // stats = (25+10+5+2+1)*5 = 215

        HallOfFameService.IsHallOfFamer(player, 0, 20).Should().BeTrue();
    }

    [Fact]
    public void Path2_Stats150_Total180_Yos7_Major20()
    {
        // stats >= 150, total >= 180, yos >= 7, major >= 20
        // ppg=20, rpg=8, apg=3, spg=1, bpg=1 => stats=(20+8+3+1+1)*5=165
        // awardPts=20, total=185 >= 180 ✓
        var player = CreateCareerPlayer(1, 100, 800, 50, 150, 800, 300, 100, 100, 8);
        // ppg = (800*2+50*3+150)/100 = (1600+150+150)/100 = 19
        // rpg = 800/100 = 8, apg = 300/100 = 3, spg = 100/100 = 1, bpg = 100/100 = 1
        // stats = (19+8+3+1+1)*5 = 160
        // total = 160 + 20 = 180
        // Path 2 requires stats >= 150 ✓ and total >= 180 ✓

        HallOfFameService.IsHallOfFamer(player, 20, 20).Should().BeTrue();
    }

    [Fact]
    public void Path3_AwaPts121_Major20()
    {
        // awardPts > 120, major >= 20 (no YOS or stats requirement)
        var player = CreateCareerPlayer(1, 100, 100, 10, 50, 200, 100, 50, 20, 3);

        HallOfFameService.IsHallOfFamer(player, 121, 20).Should().BeTrue();
    }

    [Fact]
    public void BelowAllThresholds_False()
    {
        var player = CreateCareerPlayer(1, 100, 200, 20, 50, 200, 100, 50, 20, 5);
        // ppg = (400+60+50)/100 = 5.1, rpg=2, apg=1, spg=0.5, bpg=0.2
        // stats = (5.1+2+1+0.5+0.2)*5 = 44

        HallOfFameService.IsHallOfFamer(player, 10, 5).Should().BeFalse();
    }

    [Fact]
    public void Yos6_HighStats_False()
    {
        // High stats but YOS < 7 → paths 1 and 2 fail
        var player = CreateCareerPlayer(1, 100, 1000, 100, 200, 1000, 500, 200, 100, 6);

        HallOfFameService.IsHallOfFamer(player, 0, 20).Should().BeFalse();
    }

    [Fact]
    public void Major19_AllPaths_False()
    {
        // All paths require major >= 20
        var player = CreateCareerPlayer(1, 100, 1000, 100, 200, 1000, 500, 200, 100, 8);

        HallOfFameService.IsHallOfFamer(player, 200, 19).Should().BeFalse();
    }

    [Fact]
    public void ZeroCareerGames_False()
    {
        var player = CreateCareerPlayer(1, 0, 0, 0, 0, 0, 0, 0, 0, 10);

        HallOfFameService.IsHallOfFamer(player, 200, 50).Should().BeFalse();
    }

    [Fact]
    public void ExactlyAtThresholds_True()
    {
        // Path 1: totalPts exactly 200, yos exactly 7, major exactly 20
        // Need stats + awards = 200. Use stats=180 + awards=20
        // stats = (ppg+rpg+apg+spg+bpg)*5 = 180 → sum = 36
        // ppg=25, rpg=7, apg=3, spg=0.5, bpg=0.5 → sum=36
        var player = CreateCareerPlayer(1, 100, 1000, 100, 200, 700, 300, 50, 50, 7);
        // ppg = (2000+300+200)/100 = 25, rpg=7, apg=3, spg=0.5, bpg=0.5
        // stats = (25+7+3+0.5+0.5)*5 = 180
        // total = 180+20 = 200

        HallOfFameService.IsHallOfFamer(player, 20, 20).Should().BeTrue();
    }

    // ── Integration: EvaluateRetiredPlayersForHallOfFame ─────────────

    [Fact]
    public void EvaluateRetired_NoRetired_Empty()
    {
        var league = CreateLeagueWithTeam();
        league.Teams[0].Roster[0].Retired = false;

        var result = HallOfFameService.EvaluateRetiredPlayersForHallOfFame(league);

        result.Should().BeEmpty();
    }

    [Fact]
    public void EvaluateRetired_HofWorthy_Inducted()
    {
        var league = CreateLeagueWithTeam();
        var player = league.Teams[0].Roster[0];
        player.Retired = true;
        player.Id = 100;
        player.Name = "HOF Star";
        player.Position = "PG";
        player.Contract.YearsOfService = 10;
        player.CareerStats = new PlayerStatLine
        {
            Games = 800,
            FieldGoalsMade = 8000,
            ThreePointersMade = 800,
            FreeThrowsMade = 2000,
            Rebounds = 4000,
            Assists = 4000,
            Steals = 1200,
            Blocks = 400
        };

        // Add enough awards for major points
        for (int i = 0; i < 5; i++)
        {
            var awards = CreateEmptyAwards(i + 1);
            awards.Mvp.Recipients.Add(MakeRecipient(100, 1)); // 15 pts + 10 major per season
            league.AwardsHistory.Add(awards);
        }

        var result = HallOfFameService.EvaluateRetiredPlayersForHallOfFame(league);

        result.Should().HaveCount(1);
        result[0].PlayerName.Should().Be("HOF Star");
        result[0].AwardPoints.Should().Be(75); // 15*5
        result[0].MajorPoints.Should().Be(50); // 10*5
    }

    [Fact]
    public void EvaluateRetired_NotWorthy_Skipped()
    {
        var league = CreateLeagueWithTeam();
        var player = league.Teams[0].Roster[0];
        player.Retired = true;
        player.Id = 100;
        player.Name = "Average Joe";
        player.Contract.YearsOfService = 3;
        player.CareerStats = new PlayerStatLine
        {
            Games = 200,
            FieldGoalsMade = 400,
            ThreePointersMade = 40,
            FreeThrowsMade = 100,
            Rebounds = 300,
            Assists = 200,
            Steals = 60,
            Blocks = 20
        };

        var result = HallOfFameService.EvaluateRetiredPlayersForHallOfFame(league);

        result.Should().BeEmpty();
    }

    [Fact]
    public void AllStarArchived_BeforeClearing()
    {
        var league = CreateLeagueWithTeam();
        league.AllStarWeekend = new AllStarWeekendResult
        {
            Conference1Roster = new List<int> { 100, 200 },
            Conference2Roster = new List<int> { 300, 400 }
        };

        OffSeasonService.ResetSeasonState(league);

        league.AllStarWeekendHistory.Should().HaveCount(1);
        league.AllStarWeekendHistory[0].Conference1Roster.Should().Contain(100);
        league.AllStarWeekend.Should().BeNull();
    }

    [Fact]
    public void HofAccumulatesAcrossSeasons()
    {
        var league = CreateLeagueWithTeam();
        var player1 = CreateHofPlayer(100, "Star1");
        var player2 = CreateHofPlayer(200, "Star2");
        league.Teams[0].Roster.Add(player1);
        league.Teams[0].Roster.Add(player2);

        // Add awards for player1
        for (int i = 0; i < 5; i++)
        {
            var awards = CreateEmptyAwards(i + 1);
            awards.Mvp.Recipients.Add(MakeRecipient(100, 1));
            league.AwardsHistory.Add(awards);
        }

        // Retire player1 first
        player1.Retired = true;
        var batch1 = HallOfFameService.EvaluateRetiredPlayersForHallOfFame(league);
        league.HallOfFame.AddRange(batch1);

        // Add awards for player2
        for (int i = 0; i < 5; i++)
        {
            var awards = CreateEmptyAwards(i + 6);
            awards.Mvp.Recipients.Add(MakeRecipient(200, 1));
            league.AwardsHistory.Add(awards);
        }

        // Retire player2
        player2.Retired = true;
        var batch2 = HallOfFameService.EvaluateRetiredPlayersForHallOfFame(league);
        league.HallOfFame.AddRange(batch2);

        league.HallOfFame.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    // ── Helper: FindPlayerRank ───────────────────────────────────────

    [Fact]
    public void FindPlayerRank_Found_ReturnsRank()
    {
        var award = new AwardResult { AwardName = "MVP" };
        award.Recipients.Add(MakeRecipient(100, 2));

        HallOfFameService.FindPlayerRank(award, 100).Should().Be(2);
    }

    [Fact]
    public void FindPlayerRank_NotFound_Returns0()
    {
        var award = new AwardResult { AwardName = "MVP" };
        award.Recipients.Add(MakeRecipient(999, 1));

        HallOfFameService.FindPlayerRank(award, 100).Should().Be(0);
    }

    [Fact]
    public void FindPlayerRank_NullAward_Returns0()
    {
        HallOfFameService.FindPlayerRank(null!, 100).Should().Be(0);
    }

    // ── Helper: FindPlayerTeamNumber ─────────────────────────────────

    [Fact]
    public void FindPlayerTeamNumber_Found_ReturnsTeamNum()
    {
        var teams = new List<AllTeamSelection>
        {
            new AllTeamSelection
            {
                TeamNumber = 2,
                Players = new List<AwardRecipient> { MakeRecipient(100, 1) }
            }
        };

        HallOfFameService.FindPlayerTeamNumber(teams, 100).Should().Be(2);
    }

    [Fact]
    public void FindPlayerTeamNumber_NotFound_Returns0()
    {
        var teams = new List<AllTeamSelection>
        {
            new AllTeamSelection
            {
                TeamNumber = 1,
                Players = new List<AwardRecipient> { MakeRecipient(999, 1) }
            }
        };

        HallOfFameService.FindPlayerTeamNumber(teams, 100).Should().Be(0);
    }

    // ── Helper: FindGameHighestScorerPointer ─────────────────────────

    [Fact]
    public void FindGameHighestScorerPointer_ReturnsHighestScorer()
    {
        var result = new GameResult
        {
            VisitorBoxScore = new List<PlayerGameState>
            {
                new PlayerGameState { FieldGoalsMade = 5, ThreePointersMade = 2, FreeThrowsMade = 3, PlayerPointer = 10 }
            },
            HomeBoxScore = new List<PlayerGameState>
            {
                new PlayerGameState { FieldGoalsMade = 10, ThreePointersMade = 3, FreeThrowsMade = 5, PlayerPointer = 20 }
            }
        };

        // Visitor: 5*2+2+3=15, Home: 10*2+3+5=28
        HallOfFameService.FindGameHighestScorerPointer(result).Should().Be(20);
    }

    [Fact]
    public void FindGameHighestScorerPointer_EmptyBoxScores_Returns0()
    {
        var result = new GameResult
        {
            VisitorBoxScore = new List<PlayerGameState>(),
            HomeBoxScore = new List<PlayerGameState>()
        };

        HallOfFameService.FindGameHighestScorerPointer(result).Should().Be(0);
    }

    // ── Helpers for league setup ─────────────────────────────────────

    private static League CreateLeagueWithTeam()
    {
        var league = new League();
        league.Settings = new LeagueSettings { CurrentYear = 2025 };
        var team = new Team { Name = "Test Team" };
        team.Roster.Add(new Player { Id = 1, Name = "Default", CareerStats = new PlayerStatLine() });
        league.Teams.Add(team);
        return league;
    }

    private static Player CreateHofPlayer(int id, string name)
    {
        return new Player
        {
            Id = id,
            Name = name,
            Position = "SF",
            Retired = false,
            Contract = new PlayerContract { YearsOfService = 12 },
            CareerStats = new PlayerStatLine
            {
                Games = 900,
                FieldGoalsMade = 9000,
                ThreePointersMade = 900,
                FreeThrowsMade = 2500,
                Rebounds = 5000,
                Assists = 3500,
                Steals = 1000,
                Blocks = 500
            }
        };
    }
}
