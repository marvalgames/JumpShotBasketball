using FluentAssertions;
using JumpShotBasketball.Core.Enums;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class ScheduleGenerationServiceTests
{
    // ── Helper ──────────────────────────────────────────────────────

    private static League CreateLeague32()
    {
        var league = new League();
        league.Settings.ConferenceName1 = "Eastern";
        league.Settings.ConferenceName2 = "Western";
        league.Settings.DivisionName1 = "Atlantic";
        league.Settings.DivisionName2 = "Central";
        league.Settings.DivisionName3 = "Midwest";
        league.Settings.DivisionName4 = "Pacific";
        league.Settings.NumberOfTeams = 32;

        string[] divNames = { "Atlantic", "Central", "Midwest", "Pacific" };
        string[] confNames = { "Eastern", "Eastern", "Western", "Western" };

        for (int i = 0; i < 32; i++)
        {
            int divIdx = i / 8;
            var team = new Models.Team.Team
            {
                Id = i,
                Name = $"Team{i}",
                Record = new TeamRecord
                {
                    TeamName = $"Team{i}",
                    Division = divNames[divIdx],
                    Conference = confNames[divIdx]
                }
            };
            team.Roster.Add(new Player { Name = $"Player{i}" });
            league.Teams.Add(team);
        }

        return league;
    }

    private static League CreateSmallLeague(int teamsPerDiv)
    {
        var league = new League();
        league.Settings.ConferenceName1 = "East";
        league.Settings.ConferenceName2 = "West";
        league.Settings.DivisionName1 = "Div1";
        league.Settings.DivisionName2 = "Div2";
        league.Settings.DivisionName3 = "Div3";
        league.Settings.DivisionName4 = "Div4";

        string[] divNames = { "Div1", "Div2", "Div3", "Div4" };
        string[] confNames = { "East", "East", "West", "West" };

        int total = teamsPerDiv * 4;
        league.Settings.NumberOfTeams = total;

        for (int i = 0; i < total; i++)
        {
            int divIdx = i / teamsPerDiv;
            league.Teams.Add(new Models.Team.Team
            {
                Id = i,
                Name = $"T{i}",
                Record = new TeamRecord
                {
                    TeamName = $"T{i}",
                    Division = divNames[divIdx],
                    Conference = confNames[divIdx]
                }
            });
        }

        return league;
    }

    // ── BuildDivisionTables ─────────────────────────────────────────

    [Fact]
    public void BuildDivisionTables_32Teams_4Divisions8Each()
    {
        var league = CreateLeague32();
        var (divisions, conferences) = ScheduleGenerationService.BuildDivisionTables(league);

        divisions.Should().HaveCount(4);
        foreach (var div in divisions.Values)
            div.Should().HaveCount(8);

        conferences.Should().HaveCount(2);
        foreach (var conf in conferences.Values)
            conf.Should().HaveCount(16);
    }

    [Fact]
    public void BuildDivisionTables_SmallLeague_CorrectGrouping()
    {
        var league = CreateSmallLeague(2); // 8 teams total
        var (divisions, conferences) = ScheduleGenerationService.BuildDivisionTables(league);

        divisions.Should().HaveCount(4);
        foreach (var div in divisions.Values)
            div.Should().HaveCount(2);

        conferences.Should().HaveCount(2);
        foreach (var conf in conferences.Values)
            conf.Should().HaveCount(4);
    }

    // ── Full Schedule: Game Count ───────────────────────────────────

    [Fact]
    public void GenerateSchedule_32Teams82Games_EachTeamPlays82()
    {
        var league = CreateLeague32();
        var rng = new Random(42);

        ScheduleGenerationService.GenerateSchedule(league, gamesPerTeam: 82, random: rng);

        var games = league.Schedule.Games;
        games.Should().NotBeEmpty();

        var teamGameCounts = new int[32];
        foreach (var g in games)
        {
            teamGameCounts[g.HomeTeamIndex]++;
            teamGameCounts[g.VisitorTeamIndex]++;
        }

        for (int i = 0; i < 32; i++)
            teamGameCounts[i].Should().Be(82, $"Team {i} should play 82 games");
    }

    [Fact]
    public void GenerateSchedule_TotalGamesCorrect()
    {
        var league = CreateLeague32();
        var rng = new Random(42);

        ScheduleGenerationService.GenerateSchedule(league, gamesPerTeam: 82, random: rng);

        // 32 teams * 82 games / 2 = 1312 total games
        league.Schedule.Games.Should().HaveCount(1312);
    }

    // ── Home/Away Balance ───────────────────────────────────────────

    [Fact]
    public void GenerateSchedule_HomeAwayBalance_Reasonable()
    {
        var league = CreateLeague32();
        var rng = new Random(42);

        ScheduleGenerationService.GenerateSchedule(league, gamesPerTeam: 82, random: rng);

        var homeCount = new int[32];
        var awayCount = new int[32];
        foreach (var g in league.Schedule.Games)
        {
            homeCount[g.HomeTeamIndex]++;
            awayCount[g.VisitorTeamIndex]++;
        }

        for (int i = 0; i < 32; i++)
        {
            int total = homeCount[i] + awayCount[i];
            total.Should().Be(82, $"Team {i} total games");
            // Home/away should be roughly balanced (within ~5 games of 41)
            homeCount[i].Should().BeInRange(36, 46, $"Team {i} home games should be roughly balanced");
        }
    }

    // ── Division Matchups ───────────────────────────────────────────

    [Fact]
    public void GenerateSchedule_DivisionOpponents_PlayDivisionTimesTimes()
    {
        var league = CreateLeague32();
        var rng = new Random(42);
        int divisionTimes = 4;

        ScheduleGenerationService.GenerateSchedule(league, gamesPerTeam: 82,
            divisionTimes: divisionTimes, random: rng);

        // Count matchups between pairs
        var matchupCount = new Dictionary<(int, int), int>();
        foreach (var g in league.Schedule.Games)
        {
            int a = Math.Min(g.HomeTeamIndex, g.VisitorTeamIndex);
            int b = Math.Max(g.HomeTeamIndex, g.VisitorTeamIndex);
            var key = (a, b);
            matchupCount.TryAdd(key, 0);
            matchupCount[key]++;
        }

        // Division opponents (team 0 in Atlantic, indices 0-7)
        // Should play each other at least divisionTimes (4) times.
        // May be slightly more due to fill algorithm adding same-conference games.
        for (int j = 1; j < 8; j++)
        {
            var key = (0, j);
            matchupCount.Should().ContainKey(key);
            matchupCount[key].Should().BeGreaterThanOrEqualTo(divisionTimes,
                $"Team 0 vs Team {j} (same division) should play at least {divisionTimes} times");
        }
    }

    // ── Conference Cross-Division Matchups ──────────────────────────

    [Fact]
    public void GenerateSchedule_ConferenceOpponents_PlayConferenceTimesTimes()
    {
        var league = CreateLeague32();
        var rng = new Random(42);
        int conferenceTimes = 2;

        ScheduleGenerationService.GenerateSchedule(league, gamesPerTeam: 82,
            conferenceTimes: conferenceTimes, random: rng);

        var matchupCount = new Dictionary<(int, int), int>();
        foreach (var g in league.Schedule.Games)
        {
            int a = Math.Min(g.HomeTeamIndex, g.VisitorTeamIndex);
            int b = Math.Max(g.HomeTeamIndex, g.VisitorTeamIndex);
            var key = (a, b);
            matchupCount.TryAdd(key, 0);
            matchupCount[key]++;
        }

        // Team 0 (Atlantic/Eastern) vs Team 8 (Central/Eastern) — cross-division, same conf
        // Should play at least conferenceTimes games (may have fill games too)
        var key08 = (0, 8);
        matchupCount.Should().ContainKey(key08);
        matchupCount[key08].Should().BeGreaterThanOrEqualTo(conferenceTimes,
            $"Team 0 vs Team 8 (same conf, diff div) should play at least {conferenceTimes} times");
    }

    // ── Inter-Conference Matchups ───────────────────────────────────

    [Fact]
    public void GenerateSchedule_InterConference_PlayInterConferenceTimesTimes()
    {
        var league = CreateLeague32();
        var rng = new Random(42);
        int interConferenceTimes = 2;

        ScheduleGenerationService.GenerateSchedule(league, gamesPerTeam: 82,
            interConferenceTimes: interConferenceTimes, random: rng);

        var matchupCount = new Dictionary<(int, int), int>();
        foreach (var g in league.Schedule.Games)
        {
            int a = Math.Min(g.HomeTeamIndex, g.VisitorTeamIndex);
            int b = Math.Max(g.HomeTeamIndex, g.VisitorTeamIndex);
            var key = (a, b);
            matchupCount.TryAdd(key, 0);
            matchupCount[key]++;
        }

        // Team 0 (Eastern) vs Team 16 (Western) — inter-conference
        var key016 = (0, 16);
        matchupCount.Should().ContainKey(key016);
        matchupCount[key016].Should().Be(interConferenceTimes,
            $"Team 0 vs Team 16 (opposite conf) should play {interConferenceTimes} times");
    }

    // ── Day Assignment: No Same-Day Conflicts ───────────────────────

    [Fact]
    public void GenerateSchedule_NoTeamPlaysTwiceSameDay()
    {
        var league = CreateLeague32();
        var rng = new Random(42);

        ScheduleGenerationService.GenerateSchedule(league, gamesPerTeam: 82, random: rng);

        var dayGroups = league.Schedule.Games.GroupBy(g => g.Day);
        foreach (var dayGroup in dayGroups)
        {
            var teamsOnDay = new HashSet<int>();
            foreach (var game in dayGroup)
            {
                teamsOnDay.Should().NotContain(game.HomeTeamIndex,
                    $"Home team {game.HomeTeamIndex} plays twice on day {dayGroup.Key}");
                teamsOnDay.Should().NotContain(game.VisitorTeamIndex,
                    $"Visitor team {game.VisitorTeamIndex} plays twice on day {dayGroup.Key}");
                teamsOnDay.Add(game.HomeTeamIndex);
                teamsOnDay.Add(game.VisitorTeamIndex);
            }
        }
    }

    // ── Games Per Day Limit ─────────────────────────────────────────

    [Fact]
    public void GenerateSchedule_MaxGamesPerDay_Respected()
    {
        var league = CreateLeague32();
        var rng = new Random(42);

        ScheduleGenerationService.GenerateSchedule(league, gamesPerTeam: 82, random: rng);

        int maxPerDay = 32 / 4; // = 8
        var dayGroups = league.Schedule.Games.GroupBy(g => g.Day);
        foreach (var dayGroup in dayGroups)
        {
            dayGroup.Count().Should().BeLessThanOrEqualTo(maxPerDay,
                $"Day {dayGroup.Key} exceeds max {maxPerDay} games");
        }
    }

    // ── All-Star Break ──────────────────────────────────────────────

    [Fact]
    public void GenerateSchedule_AllStarBreak_3DayGap()
    {
        var league = CreateLeague32();
        var rng = new Random(42);

        ScheduleGenerationService.GenerateSchedule(league, gamesPerTeam: 82, random: rng);

        // Calculate expected all-star break day
        int gamesPerDay = 8; // 32/4
        int totalGames = 1312;
        int breakDay = (int)(45.0 / 82.0 * totalGames / gamesPerDay);

        // The 3 break days should have no games
        var gameDays = league.Schedule.Games.Select(g => g.Day).Distinct().ToHashSet();

        gameDays.Should().NotContain(breakDay, $"Break day {breakDay}");
        gameDays.Should().NotContain(breakDay + 1, $"Break day {breakDay + 1}");
        gameDays.Should().NotContain(breakDay + 2, $"Break day {breakDay + 2}");
    }

    // ── Determinism ─────────────────────────────────────────────────

    [Fact]
    public void GenerateSchedule_SameSeed_SameSchedule()
    {
        var league1 = CreateLeague32();
        var league2 = CreateLeague32();

        ScheduleGenerationService.GenerateSchedule(league1, gamesPerTeam: 82, random: new Random(123));
        ScheduleGenerationService.GenerateSchedule(league2, gamesPerTeam: 82, random: new Random(123));

        league1.Schedule.Games.Should().HaveCount(league2.Schedule.Games.Count);

        for (int i = 0; i < league1.Schedule.Games.Count; i++)
        {
            var g1 = league1.Schedule.Games[i];
            var g2 = league2.Schedule.Games[i];
            g1.Day.Should().Be(g2.Day);
            g1.HomeTeamIndex.Should().Be(g2.HomeTeamIndex);
            g1.VisitorTeamIndex.Should().Be(g2.VisitorTeamIndex);
            g1.GameNumber.Should().Be(g2.GameNumber);
        }
    }

    // ── GameNumber Sequential ───────────────────────────────────────

    [Fact]
    public void GenerateSchedule_GameNumbers_Sequential()
    {
        var league = CreateLeague32();
        ScheduleGenerationService.GenerateSchedule(league, gamesPerTeam: 82, random: new Random(42));

        for (int i = 0; i < league.Schedule.Games.Count; i++)
        {
            league.Schedule.Games[i].GameNumber.Should().Be(i + 1);
        }
    }

    // ── All Games Are League Type ───────────────────────────────────

    [Fact]
    public void GenerateSchedule_AllGamesAreLeagueType()
    {
        var league = CreateLeague32();
        ScheduleGenerationService.GenerateSchedule(league, gamesPerTeam: 82, random: new Random(42));

        league.Schedule.Games.Should().AllSatisfy(g =>
        {
            g.Type.Should().Be(GameType.League);
            g.Played.Should().BeFalse();
        });
    }

    // ── Schedule State Flags ────────────────────────────────────────

    [Fact]
    public void GenerateSchedule_SetsScheduleProperties()
    {
        var league = CreateLeague32();
        ScheduleGenerationService.GenerateSchedule(league, gamesPerTeam: 82, random: new Random(42));

        league.Schedule.GamesInSeason.Should().Be(82);
        league.Schedule.SeasonStarted.Should().BeFalse();
        league.Schedule.RegularSeasonEnded.Should().BeFalse();
        league.Schedule.PlayoffsStarted.Should().BeFalse();
    }

    // ── Small League ────────────────────────────────────────────────

    [Fact]
    public void GenerateSchedule_SmallLeague_8Teams_AllPlayCorrectGames()
    {
        var league = CreateSmallLeague(2); // 8 teams, 4 divisions of 2
        var rng = new Random(42);
        int gamesPerTeam = 20;

        ScheduleGenerationService.GenerateSchedule(league, gamesPerTeam: gamesPerTeam,
            divisionTimes: 2, conferenceTimes: 1, interConferenceTimes: 1, random: rng);

        var teamGameCounts = new int[8];
        foreach (var g in league.Schedule.Games)
        {
            teamGameCounts[g.HomeTeamIndex]++;
            teamGameCounts[g.VisitorTeamIndex]++;
        }

        for (int i = 0; i < 8; i++)
            teamGameCounts[i].Should().Be(gamesPerTeam, $"Team {i} should play {gamesPerTeam} games");
    }

    [Fact]
    public void GenerateSchedule_SmallLeague_NoSameDayConflicts()
    {
        var league = CreateSmallLeague(2);
        var rng = new Random(42);

        ScheduleGenerationService.GenerateSchedule(league, gamesPerTeam: 20,
            divisionTimes: 2, conferenceTimes: 1, interConferenceTimes: 1, random: rng);

        var dayGroups = league.Schedule.Games.GroupBy(g => g.Day);
        foreach (var dayGroup in dayGroups)
        {
            var teams = new HashSet<int>();
            foreach (var game in dayGroup)
            {
                teams.Should().NotContain(game.HomeTeamIndex);
                teams.Should().NotContain(game.VisitorTeamIndex);
                teams.Add(game.HomeTeamIndex);
                teams.Add(game.VisitorTeamIndex);
            }
        }
    }

    // ── Edge: 1 team ────────────────────────────────────────────────

    [Fact]
    public void GenerateSchedule_SingleTeam_NoGames()
    {
        var league = new League();
        league.Teams.Add(new Models.Team.Team
        {
            Id = 0,
            Name = "Solo",
            Record = new TeamRecord { Division = "D1", Conference = "C1" }
        });

        ScheduleGenerationService.GenerateSchedule(league, random: new Random(42));

        league.Schedule.Games.Should().BeEmpty();
    }

    // ── ShuffleMatchups ─────────────────────────────────────────────

    [Fact]
    public void ShuffleMatchups_ChangesOrder()
    {
        var matchups = new List<(int, int)>();
        for (int i = 0; i < 100; i++)
            matchups.Add((i, i + 1));

        var original = matchups.ToList();
        ScheduleGenerationService.ShuffleMatchups(matchups, new Random(42));

        // After shuffle, order should differ (extremely unlikely to be identical with 100 items)
        matchups.Should().NotEqual(original);
        // But should contain the same elements
        matchups.Should().BeEquivalentTo(original);
    }

    // ── Days Are Monotonically Non-Decreasing ───────────────────────

    [Fact]
    public void GenerateSchedule_DaysAreOrdered()
    {
        var league = CreateLeague32();
        ScheduleGenerationService.GenerateSchedule(league, gamesPerTeam: 82, random: new Random(42));

        var days = league.Schedule.Games.Select(g => g.Day).ToList();
        for (int i = 1; i < days.Count; i++)
        {
            days[i].Should().BeGreaterThanOrEqualTo(days[i - 1],
                $"Game {i} day {days[i]} should be >= game {i - 1} day {days[i - 1]}");
        }
    }

    // ── Inter-Conference Games Exist ────────────────────────────────

    [Fact]
    public void GenerateSchedule_InterConferenceGamesExist()
    {
        var league = CreateLeague32();
        ScheduleGenerationService.GenerateSchedule(league, gamesPerTeam: 82, random: new Random(42));

        // Teams 0-15 are Eastern, 16-31 are Western
        var interConf = league.Schedule.Games
            .Where(g =>
                (g.HomeTeamIndex < 16 && g.VisitorTeamIndex >= 16) ||
                (g.HomeTeamIndex >= 16 && g.VisitorTeamIndex < 16))
            .ToList();

        // 16*16 pairs * 2 games = 512 inter-conference games
        interConf.Should().HaveCount(512);
    }

    // ── Different Seeds Produce Different Schedules ──────────────────

    [Fact]
    public void GenerateSchedule_DifferentSeeds_DifferentOrder()
    {
        var league1 = CreateLeague32();
        var league2 = CreateLeague32();

        ScheduleGenerationService.GenerateSchedule(league1, gamesPerTeam: 82, random: new Random(1));
        ScheduleGenerationService.GenerateSchedule(league2, gamesPerTeam: 82, random: new Random(999));

        league1.Schedule.Games.Should().HaveCount(league2.Schedule.Games.Count);

        // Check first 10 games — different seeds should yield different ordering
        bool allSame = true;
        for (int i = 0; i < 10; i++)
        {
            var g1 = league1.Schedule.Games[i];
            var g2 = league2.Schedule.Games[i];
            if (g1.HomeTeamIndex != g2.HomeTeamIndex || g1.VisitorTeamIndex != g2.VisitorTeamIndex)
            {
                allSame = false;
                break;
            }
        }

        allSame.Should().BeFalse("Different seeds should produce different schedules");
    }

    // ── All Games Placed (no stalls) ────────────────────────────────

    [Fact]
    public void GenerateSchedule_AllGamesPlaced()
    {
        var league = CreateLeague32();
        ScheduleGenerationService.GenerateSchedule(league, gamesPerTeam: 82, random: new Random(42));

        // Verify all 1312 games were placed onto days
        league.Schedule.Games.Should().HaveCount(1312);
        league.Schedule.Games.Should().AllSatisfy(g =>
            g.Day.Should().BeGreaterThan(0));
    }
}
