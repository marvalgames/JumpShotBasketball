using JumpShotBasketball.Core.Enums;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Playoff;
using JumpShotBasketball.Core.Models.Team;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Manages playoff seeding, bracket generation, series tracking, and round advancement.
/// Port of C++ CPlayoff class (~2050 lines) with file I/O replaced by in-memory models.
/// </summary>
public static class PlayoffService
{
    // ── Format Parsing ─────────────────────────────────────────────

    /// <summary>
    /// Parses playoff format string into mode and teams-per-group.
    /// Port of C++ ConferencePlayoff().
    /// </summary>
    public static (PlayoffMode mode, int teamsPerGroup) ParsePlayoffFormat(string playoffFormat)
    {
        return playoffFormat switch
        {
            "1 team per conference" => (PlayoffMode.Conference, 1),
            "2 teams per conference" => (PlayoffMode.Conference, 2),
            "4 teams per conference" => (PlayoffMode.Conference, 4),
            "8 teams per conference" => (PlayoffMode.Conference, 8),
            "2 teams per division" => (PlayoffMode.Division, 2),
            "4 teams per division" => (PlayoffMode.Division, 4),
            _ => (PlayoffMode.Conference, 8) // default
        };
    }

    /// <summary>
    /// Parses a round format string into SeriesFormat.
    /// </summary>
    public static SeriesFormat? ParseRoundFormat(string roundFormat)
    {
        return roundFormat switch
        {
            "4 of 7" => SeriesFormat.BestOf7,
            "3 of 5" => SeriesFormat.BestOf5,
            "2 of 3" => SeriesFormat.BestOf3,
            "1 of 1" => SeriesFormat.SingleGame,
            "None" or "" => null,
            _ => null
        };
    }

    /// <summary>
    /// Calculates total playoff rounds from round format strings.
    /// Port of C++ SetPlayoffRounds().
    /// </summary>
    public static int CalculatePlayoffRounds(string rd1, string rd2, string rd3, string rd4)
    {
        if (rd1 == "None" || string.IsNullOrEmpty(rd1)) return 0;
        if (rd2 == "None" || string.IsNullOrEmpty(rd2)) return 1;
        if (rd3 == "None" || string.IsNullOrEmpty(rd3)) return 2;
        if (rd4 == "None" || string.IsNullOrEmpty(rd4)) return 3;
        return 4;
    }

    /// <summary>
    /// Gets the home/away pattern for a series.
    /// true = higher seed is home, false = lower seed is home.
    /// Port of C++ SetSeriesFormat() encoding.
    /// </summary>
    public static bool[] GetHomeAwayPattern(SeriesFormat format, bool isFinalRound)
    {
        return format switch
        {
            // BestOf7 final: H-H-A-A-A-H-H ("471100011")
            SeriesFormat.BestOf7 when isFinalRound => new[] { true, true, false, false, false, true, true },
            // BestOf7 non-final: H-H-A-A-H-A-H ("471100101")
            SeriesFormat.BestOf7 => new[] { true, true, false, false, true, false, true },
            // BestOf5: H-H-A-A-H ("3511001")
            SeriesFormat.BestOf5 => new[] { true, true, false, false, true },
            // BestOf3: H-A-H ("23011")
            SeriesFormat.BestOf3 => new[] { true, false, true },
            // Single: H ("111")
            SeriesFormat.SingleGame => new[] { true },
            _ => new[] { true }
        };
    }

    /// <summary>
    /// Gets a human-readable round description relative to the final round.
    /// </summary>
    public static string GetRoundDescription(int round, int totalRounds)
    {
        int fromFinal = totalRounds - round;
        return fromFinal switch
        {
            0 => "Championship",
            1 => "Semi-Finals",
            2 => "Quarter-Finals",
            _ => "First Round"
        };
    }

    // ── Seeding ────────────────────────────────────────────────────

    /// <summary>
    /// Sorts teams by standings within a group (conference or division name).
    /// Returns team indices sorted best-to-worst.
    /// Port of C++ SortStandings() with tiebreakers.
    /// </summary>
    public static List<int> SortTeamsByStandings(League league, string groupName)
    {
        // Collect teams in this group
        var teamIndices = new List<int>();
        for (int i = 0; i < league.Teams.Count; i++)
        {
            var rec = league.Teams[i].Record;
            if (rec.Conference == groupName || rec.Division == groupName)
                teamIndices.Add(i);
        }

        // Sort using C++ SortStandings tiebreaker logic
        teamIndices.Sort((a, b) =>
        {
            var recA = league.Teams[a].Record;
            var recB = league.Teams[b].Record;

            // Primary: win% (with division champ bonus for sort priority)
            double pctA = recA.LeaguePercentage + (recA.IsDivisionChamps ? 1.0 : 0.0);
            double pctB = recB.LeaguePercentage + (recB.IsDivisionChamps ? 1.0 : 0.0);

            if (pctA != pctB) return pctB.CompareTo(pctA); // descending

            // Tiebreaker 1: games back (ascending — fewer games back is better)
            if (recA.LeagueGamesBack != recB.LeagueGamesBack)
                return recA.LeagueGamesBack.CompareTo(recB.LeagueGamesBack);

            // Tiebreaker 2: division record (if same division)
            if (recA.Division == recB.Division)
            {
                if (recA.DivisionPercentage != recB.DivisionPercentage)
                    return recB.DivisionPercentage.CompareTo(recA.DivisionPercentage);
            }

            // Tiebreaker 3: conference record (if same conference)
            if (recA.Conference == recB.Conference)
            {
                if (recA.ConferencePercentage != recB.ConferencePercentage)
                    return recB.ConferencePercentage.CompareTo(recA.ConferencePercentage);
            }

            return 0;
        });

        // Head-to-head tiebreaker pass (bubble adjacent tied teams)
        for (int i = 0; i < teamIndices.Count - 1; i++)
        {
            var recI = league.Teams[teamIndices[i]].Record;
            var recNext = league.Teams[teamIndices[i + 1]].Record;

            if (recI.Wins == recNext.Wins && recI.Losses == recNext.Losses)
            {
                int idxI = teamIndices[i];
                int idxNext = teamIndices[i + 1];

                double h2hI = recI.VsOpponentPercentage.GetValueOrDefault(idxNext, 0);
                double h2hNext = recNext.VsOpponentPercentage.GetValueOrDefault(idxI, 0);

                if (h2hNext > h2hI)
                {
                    // Swap
                    (teamIndices[i], teamIndices[i + 1]) = (teamIndices[i + 1], teamIndices[i]);
                }
            }
        }

        return teamIndices;
    }

    /// <summary>
    /// Marks division champions (first-place team in each division, pre-sorted by standings).
    /// Port of C++ SetDivisionChamps().
    /// </summary>
    public static void SetDivisionChamps(League league)
    {
        // Clear existing
        foreach (var team in league.Teams)
            team.Record.IsDivisionChamps = false;

        var divisionNames = new[]
        {
            league.Settings.DivisionName1,
            league.Settings.DivisionName2,
            league.Settings.DivisionName3,
            league.Settings.DivisionName4
        };

        foreach (var divName in divisionNames)
        {
            if (string.IsNullOrEmpty(divName)) continue;

            // Sort by standings within division, first team is champ
            var sorted = SortTeamsByStandings(league, divName);
            if (sorted.Count > 0)
                league.Teams[sorted[0]].Record.IsDivisionChamps = true;
        }
    }

    /// <summary>
    /// Marks playoff teams and assigns seeds.
    /// Port of C++ SetAllPlayoffTeams().
    /// </summary>
    public static void MarkPlayoffTeams(League league, PlayoffMode mode, int teamsPerGroup)
    {
        // Clear existing playoff flags
        foreach (var team in league.Teams)
        {
            team.Record.IsPlayoffTeam = false;
            team.Record.PlayoffSeed = 0;
        }

        if (mode == PlayoffMode.Conference)
        {
            MarkGroupPlayoffTeams(league, league.Settings.ConferenceName1, teamsPerGroup);
            MarkGroupPlayoffTeams(league, league.Settings.ConferenceName2, teamsPerGroup);
        }
        else
        {
            MarkGroupPlayoffTeams(league, league.Settings.DivisionName1, teamsPerGroup);
            MarkGroupPlayoffTeams(league, league.Settings.DivisionName2, teamsPerGroup);
            MarkGroupPlayoffTeams(league, league.Settings.DivisionName3, teamsPerGroup);
            MarkGroupPlayoffTeams(league, league.Settings.DivisionName4, teamsPerGroup);
        }
    }

    private static void MarkGroupPlayoffTeams(League league, string groupName, int teamsPerGroup)
    {
        if (string.IsNullOrEmpty(groupName)) return;

        var sorted = SortTeamsByStandings(league, groupName);
        int count = Math.Min(teamsPerGroup, sorted.Count);
        for (int i = 0; i < count; i++)
        {
            league.Teams[sorted[i]].Record.IsPlayoffTeam = true;
            league.Teams[sorted[i]].Record.PlayoffSeed = i + 1;
        }
    }

    // ── Bracket Generation ─────────────────────────────────────────

    /// <summary>
    /// Generates the full playoff bracket from league settings and standings.
    /// Port of C++ SetPlayoffs() + WriteFirstRound().
    /// </summary>
    public static PlayoffBracket GenerateBracket(League league)
    {
        var settings = league.Settings;
        var (mode, teamsPerGroup) = ParsePlayoffFormat(settings.PlayoffFormat);
        int totalRounds = CalculatePlayoffRounds(
            settings.Round1Format, settings.Round2Format,
            settings.Round3Format, settings.Round4Format);

        if (totalRounds == 0)
            return new PlayoffBracket { TotalRounds = 0 };

        // Set division champs and mark playoff teams
        SetDivisionChamps(league);
        MarkPlayoffTeams(league, mode, teamsPerGroup);

        // Collect playoff teams in bracket order
        var matchups = BuildMatchupOrder(league, mode, teamsPerGroup);

        // Build seeds list
        var seeds = new List<PlayoffSeed>();
        for (int i = 0; i < matchups.Count; i++)
        {
            seeds.Add(new PlayoffSeed
            {
                TeamIndex = matchups[i],
                SeedNumber = (i % teamsPerGroup) + 1,
                GroupName = league.Teams[matchups[i]].Record.Conference
            });
        }

        var bracket = new PlayoffBracket
        {
            Mode = mode,
            TeamsPerGroup = teamsPerGroup,
            TotalRounds = totalRounds,
            CurrentRound = 1,
            PlayoffsStarted = true,
            Seeds = seeds
        };

        // Create first round
        var rd1Format = ParseRoundFormat(settings.Round1Format);
        if (rd1Format == null) return bracket;

        var round1 = CreateRound(1, rd1Format.Value, matchups, league, totalRounds);
        bracket.Rounds.Add(round1);

        return bracket;
    }

    /// <summary>
    /// Builds the matchup order from standings based on conference/division mode and seeding.
    /// Port of C++ SetPlayoffs() matchup arrays.
    /// </summary>
    private static List<int> BuildMatchupOrder(League league, PlayoffMode mode, int teamsPerGroup)
    {
        var playoffTeams = new List<int>();

        if (mode == PlayoffMode.Conference)
        {
            var conf1 = SortTeamsByStandings(league, league.Settings.ConferenceName1)
                .Take(teamsPerGroup).ToList();
            var conf2 = SortTeamsByStandings(league, league.Settings.ConferenceName2)
                .Take(teamsPerGroup).ToList();

            playoffTeams.AddRange(conf1);
            playoffTeams.AddRange(conf2);
        }
        else
        {
            var div1 = SortTeamsByStandings(league, league.Settings.DivisionName1)
                .Take(teamsPerGroup).ToList();
            var div2 = SortTeamsByStandings(league, league.Settings.DivisionName2)
                .Take(teamsPerGroup).ToList();
            var div3 = SortTeamsByStandings(league, league.Settings.DivisionName3)
                .Take(teamsPerGroup).ToList();
            var div4 = SortTeamsByStandings(league, league.Settings.DivisionName4)
                .Take(teamsPerGroup).ToList();

            playoffTeams.AddRange(div1);
            playoffTeams.AddRange(div2);
            playoffTeams.AddRange(div3);
            playoffTeams.AddRange(div4);
        }

        // Apply seeding matchup pattern (C++ matchup array logic)
        return ApplyMatchupPattern(playoffTeams, mode, teamsPerGroup);
    }

    /// <summary>
    /// Applies the C++ matchup seeding pattern.
    /// Returns pairs: [higher,lower, higher,lower, ...].
    /// </summary>
    private static List<int> ApplyMatchupPattern(List<int> teams, PlayoffMode mode, int teamsPerGroup)
    {
        var matchups = new List<int>();

        if (mode == PlayoffMode.Conference)
        {
            switch (teamsPerGroup)
            {
                case 1:
                    // 2 teams total: conf1 winner vs conf2 winner
                    if (teams.Count >= 2)
                    {
                        matchups.Add(teams[0]);
                        matchups.Add(teams[1]);
                    }
                    break;

                case 2:
                    // 4 teams: 1v2 per conference
                    if (teams.Count >= 4)
                    {
                        matchups.Add(teams[0]); matchups.Add(teams[1]); // conf1 1v2
                        matchups.Add(teams[2]); matchups.Add(teams[3]); // conf2 1v2
                    }
                    break;

                case 4:
                    // 8 teams: 1v4, 2v3 per conference
                    if (teams.Count >= 8)
                    {
                        matchups.Add(teams[0]); matchups.Add(teams[3]); // conf1 1v4
                        matchups.Add(teams[1]); matchups.Add(teams[2]); // conf1 2v3
                        matchups.Add(teams[4]); matchups.Add(teams[7]); // conf2 1v4
                        matchups.Add(teams[5]); matchups.Add(teams[6]); // conf2 2v3
                    }
                    break;

                case 8:
                    // 16 teams: standard bracket seeding per conference
                    if (teams.Count >= 16)
                    {
                        // Conf1: 1v8, 4v5, 2v7, 3v6
                        matchups.Add(teams[0]); matchups.Add(teams[7]);
                        matchups.Add(teams[3]); matchups.Add(teams[4]);
                        matchups.Add(teams[1]); matchups.Add(teams[6]);
                        matchups.Add(teams[2]); matchups.Add(teams[5]);
                        // Conf2: 1v8, 4v5, 2v7, 3v6
                        matchups.Add(teams[8]); matchups.Add(teams[15]);
                        matchups.Add(teams[11]); matchups.Add(teams[12]);
                        matchups.Add(teams[9]); matchups.Add(teams[14]);
                        matchups.Add(teams[10]); matchups.Add(teams[13]);
                    }
                    break;
            }
        }
        else // Division mode
        {
            switch (teamsPerGroup)
            {
                case 1:
                    // 4 division winners: div1vdiv2, div3vdiv4
                    if (teams.Count >= 4)
                    {
                        matchups.Add(teams[0]); matchups.Add(teams[1]);
                        matchups.Add(teams[2]); matchups.Add(teams[3]);
                    }
                    break;

                case 2:
                    // 8 teams: 1v2 per division
                    if (teams.Count >= 8)
                    {
                        matchups.Add(teams[0]); matchups.Add(teams[1]);
                        matchups.Add(teams[2]); matchups.Add(teams[3]);
                        matchups.Add(teams[4]); matchups.Add(teams[5]);
                        matchups.Add(teams[6]); matchups.Add(teams[7]);
                    }
                    break;

                case 4:
                    // 16 teams: 1v4, 2v3 per division
                    if (teams.Count >= 16)
                    {
                        matchups.Add(teams[0]); matchups.Add(teams[3]);
                        matchups.Add(teams[1]); matchups.Add(teams[2]);
                        matchups.Add(teams[4]); matchups.Add(teams[7]);
                        matchups.Add(teams[5]); matchups.Add(teams[6]);
                        matchups.Add(teams[8]); matchups.Add(teams[11]);
                        matchups.Add(teams[9]); matchups.Add(teams[10]);
                        matchups.Add(teams[12]); matchups.Add(teams[15]);
                        matchups.Add(teams[13]); matchups.Add(teams[14]);
                    }
                    break;
            }
        }

        return matchups;
    }

    /// <summary>
    /// Creates a round of playoff series from paired matchup teams.
    /// </summary>
    private static PlayoffRound CreateRound(
        int roundNumber, SeriesFormat format, List<int> matchupTeams,
        League league, int totalRounds)
    {
        bool isFinal = roundNumber == totalRounds || totalRounds == 1;
        var pattern = GetHomeAwayPattern(format, isFinal);
        int maxGames = (int)format * 2 - 1; // 7 for BestOf7, 5 for BestOf5, etc.

        var round = new PlayoffRound
        {
            RoundNumber = roundNumber,
            Format = format
        };

        // Create series from pairs
        for (int i = 0; i + 1 < matchupTeams.Count; i += 2)
        {
            int team1Idx = matchupTeams[i];
            int team2Idx = matchupTeams[i + 1];

            // Determine higher seed (home court advantage)
            var (higherIdx, lowerIdx) = DetermineHomeCourt(league, team1Idx, team2Idx);

            var series = new PlayoffSeries
            {
                HigherSeedTeamIndex = higherIdx,
                LowerSeedTeamIndex = lowerIdx,
                WinsToAdvance = (int)format,
                Format = format
            };

            // Pre-create game slots with home/away assignments
            for (int g = 0; g < maxGames; g++)
            {
                bool higherSeedHome = g < pattern.Length && pattern[g];
                series.Games.Add(new PlayoffGame
                {
                    GameNumberInSeries = g + 1,
                    HomeTeamIndex = higherSeedHome ? higherIdx : lowerIdx,
                    VisitorTeamIndex = higherSeedHome ? lowerIdx : higherIdx
                });
            }

            round.Series.Add(series);
        }

        return round;
    }

    /// <summary>
    /// Determines which team gets home court advantage.
    /// Better record = home court. Tiebreakers: H2H, division record, conference record.
    /// Port of C++ exchange logic in WriteFirstRound/WriteRound.
    /// </summary>
    private static (int higherIdx, int lowerIdx) DetermineHomeCourt(League league, int team1Idx, int team2Idx)
    {
        var rec1 = league.Teams[team1Idx].Record;
        var rec2 = league.Teams[team2Idx].Record;

        bool exchange = false;

        if (rec2.LeaguePercentage > rec1.LeaguePercentage)
        {
            exchange = true;
        }
        else if (Math.Abs(rec2.LeaguePercentage - rec1.LeaguePercentage) < 0.0001)
        {
            // Head-to-head
            double h2h1 = rec1.VsOpponentPercentage.GetValueOrDefault(team2Idx, 0);
            double h2h2 = rec2.VsOpponentPercentage.GetValueOrDefault(team1Idx, 0);
            bool tie = Math.Abs(h2h1 - h2h2) < 0.0001;

            if (h2h2 > h2h1) { exchange = true; tie = false; }

            // Division record (if same division)
            if (tie && rec1.Division == rec2.Division)
            {
                if (rec2.DivisionRecord > rec1.DivisionRecord) { exchange = true; tie = false; }
                else if (rec2.DivisionRecord == rec1.DivisionRecord) { /* still tied */ }
            }

            // Conference record (if same conference)
            if (tie && rec1.Conference == rec2.Conference)
            {
                if (rec2.ConferenceRecord > rec1.ConferenceRecord) { exchange = true; tie = false; }
                else if (rec2.ConferenceRecord == rec1.ConferenceRecord) { /* still tied */ }
            }

            // Conference record (any conference)
            if (tie)
            {
                if (rec2.ConferenceRecord > rec1.ConferenceRecord) { exchange = true; tie = false; }
            }

            // Division record (any division)
            if (tie)
            {
                if (rec2.DivisionRecord > rec1.DivisionRecord) { exchange = true; }
            }
        }

        return exchange ? (team2Idx, team1Idx) : (team1Idx, team2Idx);
    }

    // ── Game Management ────────────────────────────────────────────

    /// <summary>
    /// Gets the next unplayed playoff game in the current round.
    /// Returns null if no games remain (round complete or playoffs over).
    /// </summary>
    public static PlayoffGame? GetNextPlayoffGame(PlayoffBracket bracket)
    {
        if (bracket.PlayoffsComplete || bracket.Rounds.Count == 0)
            return null;

        var currentRound = bracket.Rounds.LastOrDefault();
        if (currentRound == null) return null;

        foreach (var series in currentRound.Series)
        {
            if (series.IsComplete) continue;

            foreach (var game in series.Games)
            {
                if (!game.Played) return game;
            }
        }

        return null;
    }

    /// <summary>
    /// Records the result of a played playoff game and updates series wins.
    /// </summary>
    public static void RecordGameResult(PlayoffSeries series, PlayoffGame game,
        int homeScore, int visitorScore)
    {
        game.HomeScore = homeScore;
        game.VisitorScore = visitorScore;
        game.Played = true;

        // Determine winner and increment wins
        int winnerIdx = homeScore > visitorScore ? game.HomeTeamIndex : game.VisitorTeamIndex;

        if (winnerIdx == series.HigherSeedTeamIndex)
            series.HigherSeedWins++;
        else
            series.LowerSeedWins++;
    }

    // ── Round Advancement ──────────────────────────────────────────

    /// <summary>
    /// Checks if the current round is complete and advances to the next round if so.
    /// Returns true if a round was advanced (or playoffs are complete).
    /// Port of C++ WriteRound() + GetPlayoffResults() round advancement logic.
    /// </summary>
    public static bool TryAdvanceRound(PlayoffBracket bracket, League league)
    {
        if (bracket.PlayoffsComplete) return false;

        var currentRound = bracket.Rounds.LastOrDefault();
        if (currentRound == null || !currentRound.IsComplete) return false;

        // If this is the final round, playoffs are complete
        if (bracket.CurrentRound >= bracket.TotalRounds)
        {
            bracket.PlayoffsComplete = true;

            // Find champion (winner of final series)
            var finalSeries = currentRound.Series.FirstOrDefault();
            if (finalSeries?.WinnerTeamIndex != null)
            {
                bracket.ChampionTeamIndex = finalSeries.WinnerTeamIndex;
            }

            return true;
        }

        // Advance to next round
        bracket.CurrentRound++;

        // Collect winners in bracket order (preserves bracket halves)
        var winners = new List<int>();
        foreach (var series in currentRound.Series)
        {
            if (series.WinnerTeamIndex.HasValue)
                winners.Add(series.WinnerTeamIndex.Value);
        }

        // Determine next round format
        var nextFormat = GetRoundFormat(bracket, league.Settings);
        if (nextFormat == null) return true;

        var nextRound = CreateRound(
            bracket.CurrentRound, nextFormat.Value, winners,
            league, bracket.TotalRounds);
        bracket.Rounds.Add(nextRound);

        return true;
    }

    /// <summary>
    /// Gets the series format for the current round.
    /// </summary>
    private static SeriesFormat? GetRoundFormat(PlayoffBracket bracket, LeagueSettings settings)
    {
        return bracket.CurrentRound switch
        {
            1 => ParseRoundFormat(settings.Round1Format),
            2 => ParseRoundFormat(settings.Round2Format),
            3 => ParseRoundFormat(settings.Round3Format),
            4 => ParseRoundFormat(settings.Round4Format),
            _ => null
        };
    }

    /// <summary>
    /// Finalizes the playoffs: sets HasRing on champion, marks InPlayoffs on all playoff teams.
    /// </summary>
    public static void FinalizePlayoffs(League league)
    {
        var bracket = league.Bracket;
        if (bracket == null || !bracket.PlayoffsComplete) return;

        // Mark all playoff teams as InPlayoffs
        foreach (var seed in bracket.Seeds)
        {
            if (seed.TeamIndex >= 0 && seed.TeamIndex < league.Teams.Count)
                league.Teams[seed.TeamIndex].Record.InPlayoffs = true;
        }

        // Set HasRing on champion
        if (bracket.ChampionTeamIndex.HasValue)
        {
            int champIdx = bracket.ChampionTeamIndex.Value;
            if (champIdx >= 0 && champIdx < league.Teams.Count)
                league.Teams[champIdx].Record.HasRing = true;
        }
    }
}
