using FluentAssertions;
using JumpShotBasketball.Core.Enums;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Playoff;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class PlayoffServiceTests
{
    // ── Format Parsing ─────────────────────────────────────────────

    [Theory]
    [InlineData("8 teams per conference", PlayoffMode.Conference, 8)]
    [InlineData("4 teams per conference", PlayoffMode.Conference, 4)]
    [InlineData("2 teams per conference", PlayoffMode.Conference, 2)]
    [InlineData("1 team per conference", PlayoffMode.Conference, 1)]
    [InlineData("4 teams per division", PlayoffMode.Division, 4)]
    [InlineData("2 teams per division", PlayoffMode.Division, 2)]
    public void ParsePlayoffFormat_ReturnsCorrectModeAndTeams(
        string format, PlayoffMode expectedMode, int expectedTeams)
    {
        var (mode, teamsPerGroup) = PlayoffService.ParsePlayoffFormat(format);

        mode.Should().Be(expectedMode);
        teamsPerGroup.Should().Be(expectedTeams);
    }

    [Theory]
    [InlineData("4 of 7", SeriesFormat.BestOf7)]
    [InlineData("3 of 5", SeriesFormat.BestOf5)]
    [InlineData("2 of 3", SeriesFormat.BestOf3)]
    [InlineData("1 of 1", SeriesFormat.SingleGame)]
    [InlineData("None", null)]
    [InlineData("", null)]
    public void ParseRoundFormat_ReturnsCorrectFormat(string input, SeriesFormat? expected)
    {
        PlayoffService.ParseRoundFormat(input).Should().Be(expected);
    }

    [Fact]
    public void CalculatePlayoffRounds_4Rounds()
    {
        PlayoffService.CalculatePlayoffRounds("4 of 7", "4 of 7", "4 of 7", "4 of 7")
            .Should().Be(4);
    }

    [Fact]
    public void CalculatePlayoffRounds_3Rounds()
    {
        PlayoffService.CalculatePlayoffRounds("4 of 7", "4 of 7", "4 of 7", "None")
            .Should().Be(3);
    }

    [Fact]
    public void CalculatePlayoffRounds_1Round()
    {
        PlayoffService.CalculatePlayoffRounds("4 of 7", "None", "None", "None")
            .Should().Be(1);
    }

    [Fact]
    public void CalculatePlayoffRounds_NoPlayoffs()
    {
        PlayoffService.CalculatePlayoffRounds("None", "None", "None", "None")
            .Should().Be(0);
    }

    [Fact]
    public void GetHomeAwayPattern_BestOf7_NonFinal_CorrectPattern()
    {
        // H-H-A-A-H-A-H
        var pattern = PlayoffService.GetHomeAwayPattern(SeriesFormat.BestOf7, false);
        pattern.Should().Equal(true, true, false, false, true, false, true);
    }

    [Fact]
    public void GetHomeAwayPattern_BestOf7_Final_CorrectPattern()
    {
        // H-H-A-A-A-H-H
        var pattern = PlayoffService.GetHomeAwayPattern(SeriesFormat.BestOf7, true);
        pattern.Should().Equal(true, true, false, false, false, true, true);
    }

    [Fact]
    public void GetHomeAwayPattern_BestOf5_CorrectPattern()
    {
        // H-H-A-A-H
        var pattern = PlayoffService.GetHomeAwayPattern(SeriesFormat.BestOf5, false);
        pattern.Should().Equal(true, true, false, false, true);
    }

    [Fact]
    public void GetHomeAwayPattern_BestOf3_CorrectPattern()
    {
        // H-A-H
        var pattern = PlayoffService.GetHomeAwayPattern(SeriesFormat.BestOf3, false);
        pattern.Should().Equal(true, false, true);
    }

    [Fact]
    public void GetHomeAwayPattern_SingleGame_HigherSeedHome()
    {
        var pattern = PlayoffService.GetHomeAwayPattern(SeriesFormat.SingleGame, false);
        pattern.Should().Equal(true);
    }

    // ── Round Description ──────────────────────────────────────────

    [Theory]
    [InlineData(4, 4, "Championship")]
    [InlineData(3, 4, "Semi-Finals")]
    [InlineData(2, 4, "Quarter-Finals")]
    [InlineData(1, 4, "First Round")]
    [InlineData(1, 1, "Championship")]
    [InlineData(2, 2, "Championship")]
    [InlineData(1, 2, "Semi-Finals")]
    public void GetRoundDescription_CorrectDescription(int round, int total, string expected)
    {
        PlayoffService.GetRoundDescription(round, total).Should().Be(expected);
    }

    // ── Seeding ────────────────────────────────────────────────────

    private static League Create32TeamLeague()
    {
        var league = new League
        {
            Settings = new LeagueSettings
            {
                NumberOfTeams = 32,
                PlayoffFormat = "8 teams per conference",
                Round1Format = "4 of 7",
                Round2Format = "4 of 7",
                Round3Format = "4 of 7",
                Round4Format = "4 of 7",
                ConferenceName1 = "Eastern",
                ConferenceName2 = "Western",
                DivisionName1 = "Atlantic",
                DivisionName2 = "Central",
                DivisionName3 = "Pacific",
                DivisionName4 = "Southwest"
            }
        };

        // Create 32 teams: 16 Eastern, 16 Western
        // Each conference has 2 divisions of 8
        var eastDivs = new[] { "Atlantic", "Central" };
        var westDivs = new[] { "Pacific", "Southwest" };

        for (int i = 0; i < 32; i++)
        {
            bool eastern = i < 16;
            string conf = eastern ? "Eastern" : "Western";
            string div;
            if (eastern)
                div = i < 8 ? "Atlantic" : "Central";
            else
                div = i < 24 ? "Pacific" : "Southwest";

            int wins = 60 - i * 2; // Team 0 = best, Team 31 = worst
            int losses = 82 - wins;

            league.Teams.Add(new Team
            {
                Id = i + 1,
                Name = $"Team{i + 1}",
                CityName = $"City{i + 1}",
                Record = new TeamRecord
                {
                    TeamName = $"Team{i + 1}",
                    Conference = conf,
                    Division = div,
                    Control = "Computer",
                    InitialNumber = i,
                    LeagueRecord = wins * 100 + losses,
                    Wins = wins,
                    Losses = losses,
                    LeaguePercentage = (double)wins / 82,
                    DivisionRecord = wins * 100 + losses,
                    DivisionPercentage = (double)wins / 82,
                    ConferenceRecord = wins * 100 + losses,
                    ConferencePercentage = (double)wins / 82
                }
            });
        }

        return league;
    }

    [Fact]
    public void SetDivisionChamps_MarksBestTeamPerDivision()
    {
        var league = Create32TeamLeague();

        PlayoffService.SetDivisionChamps(league);

        // Best team in each division should be champ
        // Atlantic: Team1 (i=0), Central: Team9 (i=8), Pacific: Team17 (i=16), Southwest: Team25 (i=24)
        league.Teams[0].Record.IsDivisionChamps.Should().BeTrue();
        league.Teams[8].Record.IsDivisionChamps.Should().BeTrue();
        league.Teams[16].Record.IsDivisionChamps.Should().BeTrue();
        league.Teams[24].Record.IsDivisionChamps.Should().BeTrue();

        // Non-champs should not be marked
        league.Teams[1].Record.IsDivisionChamps.Should().BeFalse();
        league.Teams[15].Record.IsDivisionChamps.Should().BeFalse();
    }

    [Fact]
    public void SortTeamsByStandings_SortsByWinPercentage()
    {
        var league = Create32TeamLeague();
        PlayoffService.SetDivisionChamps(league);

        var sorted = PlayoffService.SortTeamsByStandings(league, "Eastern");

        // Should be sorted best to worst within Eastern conference (indices 0-15)
        for (int i = 0; i < sorted.Count - 1; i++)
        {
            var recCurrent = league.Teams[sorted[i]].Record;
            var recNext = league.Teams[sorted[i + 1]].Record;
            // With division champ bonus, champs sort first at equal pct
            // But all have different pcts here, so just check descending
            double pctCurrent = recCurrent.LeaguePercentage + (recCurrent.IsDivisionChamps ? 1.0 : 0.0);
            double pctNext = recNext.LeaguePercentage + (recNext.IsDivisionChamps ? 1.0 : 0.0);
            pctCurrent.Should().BeGreaterThanOrEqualTo(pctNext);
        }
    }

    [Fact]
    public void SortTeamsByStandings_HeadToHeadTiebreaker()
    {
        var league = new League
        {
            Settings = new LeagueSettings
            {
                ConferenceName1 = "East",
                ConferenceName2 = "West",
                DivisionName1 = "Div1",
                DivisionName2 = "Div2"
            }
        };

        // Two teams with identical records
        league.Teams.Add(new Team
        {
            Id = 1, Name = "TeamA",
            Record = new TeamRecord
            {
                TeamName = "TeamA", Conference = "East", Division = "Div1",
                Wins = 50, Losses = 32, LeaguePercentage = 50.0 / 82.0,
                LeagueRecord = 5032, InitialNumber = 0
            }
        });
        league.Teams.Add(new Team
        {
            Id = 2, Name = "TeamB",
            Record = new TeamRecord
            {
                TeamName = "TeamB", Conference = "East", Division = "Div1",
                Wins = 50, Losses = 32, LeaguePercentage = 50.0 / 82.0,
                LeagueRecord = 5032, InitialNumber = 1,
                // TeamB has better H2H vs TeamA
                VsOpponentPercentage = new Dictionary<int, double> { { 0, 0.75 } }
            }
        });
        // TeamA's H2H is worse
        league.Teams[0].Record.VsOpponentPercentage = new Dictionary<int, double> { { 1, 0.25 } };

        var sorted = PlayoffService.SortTeamsByStandings(league, "East");

        sorted[0].Should().Be(1); // TeamB should be first (better H2H)
        sorted[1].Should().Be(0);
    }

    [Fact]
    public void MarkPlayoffTeams_Conference_MarksTopN()
    {
        var league = Create32TeamLeague();
        PlayoffService.SetDivisionChamps(league);

        PlayoffService.MarkPlayoffTeams(league, PlayoffMode.Conference, 8);

        var eastPlayoff = league.Teams
            .Where(t => t.Record.Conference == "Eastern" && t.Record.IsPlayoffTeam)
            .ToList();
        var westPlayoff = league.Teams
            .Where(t => t.Record.Conference == "Western" && t.Record.IsPlayoffTeam)
            .ToList();

        eastPlayoff.Should().HaveCount(8);
        westPlayoff.Should().HaveCount(8);
    }

    [Fact]
    public void MarkPlayoffTeams_AssignsSeeds()
    {
        var league = Create32TeamLeague();
        PlayoffService.SetDivisionChamps(league);

        PlayoffService.MarkPlayoffTeams(league, PlayoffMode.Conference, 8);

        // Seeds should be 1-8 in each conference
        var eastSeeds = league.Teams
            .Where(t => t.Record.Conference == "Eastern" && t.Record.IsPlayoffTeam)
            .Select(t => t.Record.PlayoffSeed)
            .OrderBy(s => s)
            .ToList();

        eastSeeds.Should().Equal(1, 2, 3, 4, 5, 6, 7, 8);
    }

    [Fact]
    public void MarkPlayoffTeams_NonPlayoffTeamsHaveNoSeed()
    {
        var league = Create32TeamLeague();
        PlayoffService.SetDivisionChamps(league);

        PlayoffService.MarkPlayoffTeams(league, PlayoffMode.Conference, 4);

        var nonPlayoff = league.Teams.Where(t => !t.Record.IsPlayoffTeam).ToList();
        nonPlayoff.Should().OnlyContain(t => t.Record.PlayoffSeed == 0);
    }

    // ── Bracket Generation ─────────────────────────────────────────

    [Fact]
    public void GenerateBracket_8PerConference_Creates8Series()
    {
        var league = Create32TeamLeague();

        var bracket = PlayoffService.GenerateBracket(league);

        bracket.TotalRounds.Should().Be(4);
        bracket.CurrentRound.Should().Be(1);
        bracket.PlayoffsStarted.Should().BeTrue();
        bracket.PlayoffsComplete.Should().BeFalse();
        bracket.Rounds.Should().HaveCount(1); // only first round created initially
        bracket.Rounds[0].Series.Should().HaveCount(8); // 8 first-round series
    }

    [Fact]
    public void GenerateBracket_Seeds_Has16Teams()
    {
        var league = Create32TeamLeague();

        var bracket = PlayoffService.GenerateBracket(league);

        bracket.Seeds.Should().HaveCount(16); // 8 per conference × 2
    }

    [Fact]
    public void GenerateBracket_SeriesHave7Games_ForBestOf7()
    {
        var league = Create32TeamLeague();

        var bracket = PlayoffService.GenerateBracket(league);

        foreach (var series in bracket.Rounds[0].Series)
        {
            series.Games.Should().HaveCount(7);
            series.WinsToAdvance.Should().Be(4);
            series.Format.Should().Be(SeriesFormat.BestOf7);
        }
    }

    [Fact]
    public void GenerateBracket_HigherSeedGetsHomeCourt_Games1And2()
    {
        var league = Create32TeamLeague();

        var bracket = PlayoffService.GenerateBracket(league);

        foreach (var series in bracket.Rounds[0].Series)
        {
            // Games 1 and 2 should have higher seed as home
            series.Games[0].HomeTeamIndex.Should().Be(series.HigherSeedTeamIndex);
            series.Games[1].HomeTeamIndex.Should().Be(series.HigherSeedTeamIndex);
            // Games 3 and 4 should have lower seed as home
            series.Games[2].HomeTeamIndex.Should().Be(series.LowerSeedTeamIndex);
            series.Games[3].HomeTeamIndex.Should().Be(series.LowerSeedTeamIndex);
        }
    }

    [Fact]
    public void GenerateBracket_NoPlayoffs_ReturnsEmptyBracket()
    {
        var league = Create32TeamLeague();
        league.Settings.Round1Format = "None";

        var bracket = PlayoffService.GenerateBracket(league);

        bracket.TotalRounds.Should().Be(0);
        bracket.Rounds.Should().BeEmpty();
    }

    [Fact]
    public void GenerateBracket_4PerConference_Creates4Series()
    {
        var league = Create32TeamLeague();
        league.Settings.PlayoffFormat = "4 teams per conference";
        league.Settings.Round4Format = "None";

        var bracket = PlayoffService.GenerateBracket(league);

        bracket.TotalRounds.Should().Be(3);
        bracket.Rounds[0].Series.Should().HaveCount(4); // 4 first-round series
    }

    [Fact]
    public void GenerateBracket_2PerConference_Creates2Series()
    {
        var league = Create32TeamLeague();
        league.Settings.PlayoffFormat = "2 teams per conference";
        league.Settings.Round3Format = "None";
        league.Settings.Round4Format = "None";

        var bracket = PlayoffService.GenerateBracket(league);

        bracket.TotalRounds.Should().Be(2);
        bracket.Rounds[0].Series.Should().HaveCount(2);
    }

    [Fact]
    public void GenerateBracket_1PerConference_Creates1Series()
    {
        var league = Create32TeamLeague();
        league.Settings.PlayoffFormat = "1 team per conference";
        league.Settings.Round2Format = "None";
        league.Settings.Round3Format = "None";
        league.Settings.Round4Format = "None";

        var bracket = PlayoffService.GenerateBracket(league);

        bracket.TotalRounds.Should().Be(1);
        bracket.Rounds[0].Series.Should().HaveCount(1);
    }

    // ── Game Management ────────────────────────────────────────────

    [Fact]
    public void GetNextPlayoffGame_ReturnsFirstUnplayedGame()
    {
        var league = Create32TeamLeague();
        var bracket = PlayoffService.GenerateBracket(league);

        var game = PlayoffService.GetNextPlayoffGame(bracket);

        game.Should().NotBeNull();
        game!.Played.Should().BeFalse();
        game.GameNumberInSeries.Should().Be(1);
    }

    [Fact]
    public void GetNextPlayoffGame_SkipsCompletedSeries()
    {
        var league = Create32TeamLeague();
        var bracket = PlayoffService.GenerateBracket(league);

        // Complete the first series (4-0 sweep)
        var series0 = bracket.Rounds[0].Series[0];
        series0.HigherSeedWins = 4;
        foreach (var g in series0.Games.Take(4))
            g.Played = true;

        var game = PlayoffService.GetNextPlayoffGame(bracket);

        game.Should().NotBeNull();
        // Should skip to second series
        var series1 = bracket.Rounds[0].Series[1];
        game!.HomeTeamIndex.Should().BeOneOf(
            series1.HigherSeedTeamIndex, series1.LowerSeedTeamIndex);
    }

    [Fact]
    public void GetNextPlayoffGame_AllComplete_ReturnsNull()
    {
        var bracket = new PlayoffBracket { PlayoffsComplete = true };

        var game = PlayoffService.GetNextPlayoffGame(bracket);

        game.Should().BeNull();
    }

    [Fact]
    public void RecordGameResult_IncrementsHigherSeedWins()
    {
        var series = new PlayoffSeries
        {
            HigherSeedTeamIndex = 0,
            LowerSeedTeamIndex = 1,
            WinsToAdvance = 4,
            Format = SeriesFormat.BestOf7,
            Games = new List<PlayoffGame>
            {
                new() { GameNumberInSeries = 1, HomeTeamIndex = 0, VisitorTeamIndex = 1 }
            }
        };

        PlayoffService.RecordGameResult(series, series.Games[0], 110, 95);

        series.HigherSeedWins.Should().Be(1);
        series.LowerSeedWins.Should().Be(0);
        series.Games[0].Played.Should().BeTrue();
        series.Games[0].HomeScore.Should().Be(110);
        series.Games[0].VisitorScore.Should().Be(95);
    }

    [Fact]
    public void RecordGameResult_IncrementsLowerSeedWins()
    {
        var series = new PlayoffSeries
        {
            HigherSeedTeamIndex = 0,
            LowerSeedTeamIndex = 1,
            WinsToAdvance = 4,
            Format = SeriesFormat.BestOf7,
            Games = new List<PlayoffGame>
            {
                new() { GameNumberInSeries = 1, HomeTeamIndex = 0, VisitorTeamIndex = 1 }
            }
        };

        // Visitor (lower seed) wins
        PlayoffService.RecordGameResult(series, series.Games[0], 90, 105);

        series.HigherSeedWins.Should().Be(0);
        series.LowerSeedWins.Should().Be(1);
    }

    // ── Series Completion ──────────────────────────────────────────

    [Fact]
    public void PlayoffSeries_IsComplete_WhenHigherSeedReachesWinsToAdvance()
    {
        var series = new PlayoffSeries
        {
            HigherSeedTeamIndex = 0,
            LowerSeedTeamIndex = 1,
            HigherSeedWins = 4,
            LowerSeedWins = 2,
            WinsToAdvance = 4,
            Format = SeriesFormat.BestOf7
        };

        series.IsComplete.Should().BeTrue();
        series.WinnerTeamIndex.Should().Be(0);
        series.LoserTeamIndex.Should().Be(1);
    }

    [Fact]
    public void PlayoffSeries_NotComplete_WhenNoTeamReachesWins()
    {
        var series = new PlayoffSeries
        {
            HigherSeedTeamIndex = 0,
            LowerSeedTeamIndex = 1,
            HigherSeedWins = 3,
            LowerSeedWins = 2,
            WinsToAdvance = 4,
            Format = SeriesFormat.BestOf7
        };

        series.IsComplete.Should().BeFalse();
        series.WinnerTeamIndex.Should().BeNull();
    }

    [Fact]
    public void PlayoffSeries_GamesPlayed_SumsWins()
    {
        var series = new PlayoffSeries
        {
            HigherSeedWins = 3,
            LowerSeedWins = 2,
            WinsToAdvance = 4
        };

        series.GamesPlayed.Should().Be(5);
    }

    // ── Round Advancement ──────────────────────────────────────────

    [Fact]
    public void TryAdvanceRound_RoundIncomplete_ReturnsFalse()
    {
        var league = Create32TeamLeague();
        var bracket = PlayoffService.GenerateBracket(league);
        league.Bracket = bracket;

        var result = PlayoffService.TryAdvanceRound(bracket, league);

        result.Should().BeFalse();
        bracket.CurrentRound.Should().Be(1);
    }

    [Fact]
    public void TryAdvanceRound_RoundComplete_AdvancesToNextRound()
    {
        var league = Create32TeamLeague();
        var bracket = PlayoffService.GenerateBracket(league);
        league.Bracket = bracket;

        // Complete all first round series (higher seed wins 4-0)
        foreach (var series in bracket.Rounds[0].Series)
        {
            series.HigherSeedWins = 4;
            foreach (var g in series.Games.Take(4))
                g.Played = true;
        }

        var result = PlayoffService.TryAdvanceRound(bracket, league);

        result.Should().BeTrue();
        bracket.CurrentRound.Should().Be(2);
        bracket.Rounds.Should().HaveCount(2);
        bracket.Rounds[1].Series.Should().HaveCount(4); // 4 second-round series
    }

    [Fact]
    public void TryAdvanceRound_FinalRoundComplete_SetsChampion()
    {
        var league = Create32TeamLeague();
        league.Settings.PlayoffFormat = "1 team per conference";
        league.Settings.Round2Format = "None";
        league.Settings.Round3Format = "None";
        league.Settings.Round4Format = "None";
        var bracket = PlayoffService.GenerateBracket(league);
        league.Bracket = bracket;

        // Complete the single final series
        var finalSeries = bracket.Rounds[0].Series[0];
        finalSeries.HigherSeedWins = 4;
        foreach (var g in finalSeries.Games.Take(4))
            g.Played = true;

        PlayoffService.TryAdvanceRound(bracket, league);

        bracket.PlayoffsComplete.Should().BeTrue();
        bracket.ChampionTeamIndex.Should().Be(finalSeries.HigherSeedTeamIndex);
    }

    [Fact]
    public void TryAdvanceRound_Winners_PairedInBracketOrder()
    {
        var league = Create32TeamLeague();
        league.Settings.PlayoffFormat = "4 teams per conference";
        league.Settings.Round4Format = "None";
        var bracket = PlayoffService.GenerateBracket(league);
        league.Bracket = bracket;

        // Complete all first round series (4 series)
        foreach (var series in bracket.Rounds[0].Series)
        {
            series.HigherSeedWins = 4;
            foreach (var g in series.Games.Take(4))
                g.Played = true;
        }

        PlayoffService.TryAdvanceRound(bracket, league);

        // Round 2 should have 2 series
        bracket.Rounds[1].Series.Should().HaveCount(2);

        // Winners from series 0 and 1 should meet, winners from 2 and 3 should meet
        var r1Winners = bracket.Rounds[0].Series.Select(s => s.WinnerTeamIndex!.Value).ToList();
        var r2Series = bracket.Rounds[1].Series;

        // Series 0: winner[0] vs winner[1]
        var s0Teams = new[] { r2Series[0].HigherSeedTeamIndex, r2Series[0].LowerSeedTeamIndex };
        s0Teams.Should().Contain(r1Winners[0]);
        s0Teams.Should().Contain(r1Winners[1]);

        // Series 1: winner[2] vs winner[3]
        var s1Teams = new[] { r2Series[1].HigherSeedTeamIndex, r2Series[1].LowerSeedTeamIndex };
        s1Teams.Should().Contain(r1Winners[2]);
        s1Teams.Should().Contain(r1Winners[3]);
    }

    // ── FinalizePlayoffs ───────────────────────────────────────────

    [Fact]
    public void FinalizePlayoffs_SetsHasRingOnChampion()
    {
        var league = Create32TeamLeague();
        var bracket = PlayoffService.GenerateBracket(league);
        league.Bracket = bracket;

        bracket.PlayoffsComplete = true;
        bracket.ChampionTeamIndex = 0;

        PlayoffService.FinalizePlayoffs(league);

        league.Teams[0].Record.HasRing.Should().BeTrue();
        league.Teams[1].Record.HasRing.Should().BeFalse();
    }

    [Fact]
    public void FinalizePlayoffs_MarksAllPlayoffTeamsInPlayoffs()
    {
        var league = Create32TeamLeague();
        var bracket = PlayoffService.GenerateBracket(league);
        league.Bracket = bracket;

        bracket.PlayoffsComplete = true;
        bracket.ChampionTeamIndex = 0;

        PlayoffService.FinalizePlayoffs(league);

        // All seeded teams should be marked InPlayoffs
        foreach (var seed in bracket.Seeds)
        {
            league.Teams[seed.TeamIndex].Record.InPlayoffs.Should().BeTrue();
        }

        // Non-playoff teams should not be marked
        var playoffIndices = bracket.Seeds.Select(s => s.TeamIndex).ToHashSet();
        for (int i = 0; i < league.Teams.Count; i++)
        {
            if (!playoffIndices.Contains(i))
                league.Teams[i].Record.InPlayoffs.Should().BeFalse();
        }
    }

    [Fact]
    public void FinalizePlayoffs_NoBracket_NoOp()
    {
        var league = Create32TeamLeague();
        league.Bracket = null;

        // Should not throw
        PlayoffService.FinalizePlayoffs(league);

        league.Teams[0].Record.HasRing.Should().BeFalse();
    }

    // ── PlayoffRound IsComplete ────────────────────────────────────

    [Fact]
    public void PlayoffRound_IsComplete_AllSeriesComplete()
    {
        var round = new PlayoffRound
        {
            RoundNumber = 1,
            Series = new List<PlayoffSeries>
            {
                new() { HigherSeedWins = 4, WinsToAdvance = 4 },
                new() { LowerSeedWins = 4, WinsToAdvance = 4 }
            }
        };

        round.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void PlayoffRound_NotComplete_OneSeriesIncomplete()
    {
        var round = new PlayoffRound
        {
            RoundNumber = 1,
            Series = new List<PlayoffSeries>
            {
                new() { HigherSeedWins = 4, WinsToAdvance = 4 },
                new() { HigherSeedWins = 2, LowerSeedWins = 1, WinsToAdvance = 4 }
            }
        };

        round.IsComplete.Should().BeFalse();
    }
}
