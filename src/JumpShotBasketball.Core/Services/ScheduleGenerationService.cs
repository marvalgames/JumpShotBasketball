using JumpShotBasketball.Core.Enums;
using JumpShotBasketball.Core.Models.League;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Generates a full regular-season schedule.
/// Port of AutoSchDlg.cpp (OnButtonCreate pipeline).
/// </summary>
public static class ScheduleGenerationService
{
    /// <summary>
    /// Generates a full season schedule for the league.
    /// </summary>
    public static void GenerateSchedule(
        League league,
        int gamesPerTeam = 82,
        int divisionTimes = 4,
        int conferenceTimes = 2,
        int interConferenceTimes = 2,
        Random? random = null)
    {
        random ??= Random.Shared;
        int numTeams = league.Teams.Count;
        if (numTeams < 2) return;

        var (divisions, conferences) = BuildDivisionTables(league);

        var matchups = GenerateMatchups(
            divisions, conferences, divisionTimes, conferenceTimes,
            interConferenceTimes, gamesPerTeam, numTeams, random);

        ShuffleMatchups(matchups, random);

        int gamesPerDay = Math.Max(1, numTeams / 4);
        int totalGames = matchups.Count;
        int allStarBreakDay = (int)(45.0 / 82.0 * totalGames / gamesPerDay);

        var scheduledGames = AssignToDays(matchups, numTeams, allStarBreakDay);

        league.Schedule.Games = scheduledGames;
        league.Schedule.GamesInSeason = gamesPerTeam;
        league.Schedule.SeasonStarted = false;
        league.Schedule.RegularSeasonEnded = false;
        league.Schedule.PlayoffsStarted = false;
    }

    /// <summary>
    /// Groups team indices by Division and Conference names.
    /// Port of CreateTables() — AutoSchDlg.cpp:163-221.
    /// </summary>
    public static (Dictionary<string, List<int>> Divisions, Dictionary<string, List<int>> Conferences)
        BuildDivisionTables(League league)
    {
        var divisions = new Dictionary<string, List<int>>();
        var conferences = new Dictionary<string, List<int>>();

        for (int i = 0; i < league.Teams.Count; i++)
        {
            var record = league.Teams[i].Record;

            if (!string.IsNullOrEmpty(record.Division))
            {
                if (!divisions.ContainsKey(record.Division))
                    divisions[record.Division] = new List<int>();
                divisions[record.Division].Add(i);
            }

            if (!string.IsNullOrEmpty(record.Conference))
            {
                if (!conferences.ContainsKey(record.Conference))
                    conferences[record.Conference] = new List<int>();
                conferences[record.Conference].Add(i);
            }
        }

        return (divisions, conferences);
    }

    /// <summary>
    /// Builds the complete matchup list: division + conference + inter-conference + fill.
    /// Parameters represent the TOTAL number of games each pair plays (not per-direction).
    /// Port of CreateDivisionMatchups + CreateOtherDivisionMatchups +
    /// CreateOtherConferenceMatchups + SetOtherGames.
    /// </summary>
    public static List<(int Visitor, int Home)> GenerateMatchups(
        Dictionary<string, List<int>> divisions,
        Dictionary<string, List<int>> conferences,
        int divisionTimes,
        int conferenceTimes,
        int interConferenceTimes,
        int gamesPerTeam,
        int numTeams,
        Random random)
    {
        var matchups = new List<(int Visitor, int Home)>();
        var gamesPlayed = new int[numTeams];

        // Phase 1: Division matchups
        // Each pair plays divisionTimes games total, alternating home/away.
        // C++ uses GetCurSel()=divisionTimes/2 passes of all ordered pairs.
        foreach (var divTeams in divisions.Values)
        {
            for (int i = 0; i < divTeams.Count; i++)
            {
                for (int j = i + 1; j < divTeams.Count; j++)
                {
                    int a = divTeams[i];
                    int b = divTeams[j];
                    for (int g = 0; g < divisionTimes; g++)
                    {
                        if (g % 2 == 0)
                            matchups.Add((a, b)); // a is visitor, b is home
                        else
                            matchups.Add((b, a)); // b is visitor, a is home
                        gamesPlayed[a]++;
                        gamesPlayed[b]++;
                    }
                }
            }
        }

        // Phase 2: Same-conference cross-division matchups
        // Group divisions by conference
        var confDivisions = new Dictionary<string, List<string>>();
        foreach (var conf in conferences)
        {
            confDivisions[conf.Key] = new List<string>();
            foreach (var div in divisions)
            {
                if (div.Value.Count > 0 && conf.Value.Contains(div.Value[0]))
                    confDivisions[conf.Key].Add(div.Key);
            }
        }

        foreach (var conf in confDivisions)
        {
            var divNames = conf.Value;
            for (int d1 = 0; d1 < divNames.Count; d1++)
            {
                for (int d2 = d1 + 1; d2 < divNames.Count; d2++)
                {
                    var div1Teams = divisions[divNames[d1]];
                    var div2Teams = divisions[divNames[d2]];

                    foreach (int t1 in div1Teams)
                    {
                        foreach (int t2 in div2Teams)
                        {
                            for (int g = 0; g < conferenceTimes; g++)
                            {
                                if (g % 2 == 0)
                                    matchups.Add((t1, t2));
                                else
                                    matchups.Add((t2, t1));
                                gamesPlayed[t1]++;
                                gamesPlayed[t2]++;
                            }
                        }
                    }
                }
            }
        }

        // Phase 3: Inter-conference matchups
        var confNames = conferences.Keys.ToList();
        for (int c1 = 0; c1 < confNames.Count; c1++)
        {
            for (int c2 = c1 + 1; c2 < confNames.Count; c2++)
            {
                var conf1Teams = conferences[confNames[c1]];
                var conf2Teams = conferences[confNames[c2]];

                foreach (int t1 in conf1Teams)
                {
                    foreach (int t2 in conf2Teams)
                    {
                        for (int g = 0; g < interConferenceTimes; g++)
                        {
                            if (g % 2 == 0)
                                matchups.Add((t1, t2));
                            else
                                matchups.Add((t2, t1));
                            gamesPlayed[t1]++;
                            gamesPlayed[t2]++;
                        }
                    }
                }
            }
        }

        // Phase 4: Fill remaining games with same-conference matchups
        FillRemainingGames(matchups, gamesPlayed, conferences, gamesPerTeam, numTeams, random);

        return matchups;
    }

    /// <summary>
    /// Fills remaining games to reach gamesPerTeam for each team using same-conference matchups.
    /// Port of SetOtherGames() — AutoSchDlg.cpp:344-552.
    /// </summary>
    private static void FillRemainingGames(
        List<(int Visitor, int Home)> matchups,
        int[] gamesPlayed,
        Dictionary<string, List<int>> conferences,
        int gamesPerTeam,
        int numTeams,
        Random random)
    {
        // Ensure gamesPerTeam is at least as high as any team's current games
        int hiG = gamesPlayed.Max();
        if (hiG > gamesPerTeam) gamesPerTeam = hiG;

        int totalGamesTarget = gamesPerTeam * numTeams / 2;

        // Build conference lookup: team index → conference team list
        var teamConference = new Dictionary<int, List<int>>();
        foreach (var conf in conferences)
        {
            foreach (int t in conf.Value)
                teamConference[t] = conf.Value;
        }

        // Track how many games each team still needs
        var homeGamesLeft = new int[numTeams];
        var roadGamesLeft = new int[numTeams];
        for (int i = 0; i < numTeams; i++)
        {
            int left = gamesPerTeam - gamesPlayed[i];
            roadGamesLeft[i] = left / 2;
            homeGamesLeft[i] = left - roadGamesLeft[i];
        }

        // played[t1,t2] prevents same matchup from repeating before a full round is done
        var played = new bool[numTeams, numTeams];
        var playedOnce = new int[numTeams];

        int currentGames = matchups.Count;
        int lastGames = 0;

        for (int x = 0; x < 5000; x++)
        {
            if (currentGames >= totalGamesTarget) break;

            for (int t1 = 0; t1 < numTeams; t1++)
            {
                for (int t2 = 0; t2 < numTeams; t2++)
                {
                    if (currentGames >= totalGamesTarget) break;
                    if (t1 == t2) continue;
                    if (roadGamesLeft[t1] <= 0 || homeGamesLeft[t2] <= 0) continue;

                    // Must be same conference
                    if (!teamConference.ContainsKey(t1) || !teamConference.ContainsKey(t2)) continue;
                    if (teamConference[t1] != teamConference[t2]) continue;

                    // Don't repeat matchup until round-robin reset
                    if (played[t1, t2] || played[t2, t1]) continue;

                    // Add the game
                    matchups.Add((t1, t2));
                    currentGames++;
                    gamesPlayed[t1]++;
                    gamesPlayed[t2]++;
                    roadGamesLeft[t1]--;
                    homeGamesLeft[t2]--;
                    playedOnce[t1]++;
                    playedOnce[t2]++;
                    played[t1, t2] = true;
                    played[t2, t1] = true;

                    // Reset when a team has played all same-conference opponents once
                    int confSize = teamConference[t1].Count - 1;
                    if (playedOnce[t1] >= confSize)
                    {
                        playedOnce[t1] = 0;
                        for (int k = 0; k < numTeams; k++)
                            played[t1, k] = false;
                    }

                    confSize = teamConference[t2].Count - 1;
                    if (playedOnce[t2] >= confSize)
                    {
                        playedOnce[t2] = 0;
                        for (int k = 0; k < numTeams; k++)
                            played[t2, k] = false;
                    }
                }
                if (currentGames >= totalGamesTarget) break;
            }

            // Stall detection: if no progress, reset played matrix
            if (currentGames == lastGames)
            {
                for (int i = 0; i < numTeams; i++)
                {
                    playedOnce[i] = 0;
                    for (int j = 0; j < numTeams; j++)
                        played[i, j] = false;
                }
            }
            lastGames = currentGames;
        }
    }

    /// <summary>
    /// Fisher-Yates shuffle of the matchup list.
    /// Port of ScrambleTeams() — AutoSchDlg.cpp:739-868 (simplified).
    /// </summary>
    public static void ShuffleMatchups(List<(int Visitor, int Home)> matchups, Random random)
    {
        for (int i = matchups.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (matchups[i], matchups[j]) = (matchups[j], matchups[i]);
        }
    }

    /// <summary>
    /// Assigns matchups to sequential Day values.
    /// Max numTeams/4 games per day; no team plays twice on same day.
    /// 3-day All-Star break at allStarBreakDay.
    /// Port of SetSchedule() — AutoSchDlg.cpp:554-737 (simplified to flat day list).
    /// </summary>
    public static List<ScheduledGame> AssignToDays(
        List<(int Visitor, int Home)> matchups,
        int numTeams,
        int allStarBreakDay)
    {
        int gamesPerDay = Math.Max(1, numTeams / 4);
        var result = new List<ScheduledGame>();
        var placed = new bool[matchups.Count];

        int day = 1;
        int gameNumber = 1;
        int totalPlaced = 0;
        int consecutiveEmptyDays = 0;

        while (totalPlaced < matchups.Count)
        {
            // All-Star break: skip 3 days
            if (day == allStarBreakDay && allStarBreakDay > 0)
            {
                day += 3;
                continue;
            }

            var teamsUsedToday = new HashSet<int>();
            int gamesToday = 0;

            for (int g = 0; g < matchups.Count; g++)
            {
                if (placed[g]) continue;
                if (gamesToday >= gamesPerDay) break;

                var (visitor, home) = matchups[g];
                if (teamsUsedToday.Contains(visitor) || teamsUsedToday.Contains(home))
                    continue;

                teamsUsedToday.Add(visitor);
                teamsUsedToday.Add(home);
                placed[g] = true;
                totalPlaced++;
                gamesToday++;

                result.Add(new ScheduledGame
                {
                    GameNumber = gameNumber++,
                    Day = day,
                    HomeTeamIndex = home,
                    VisitorTeamIndex = visitor,
                    Type = GameType.League,
                    Played = false
                });
            }

            // Stall detection: break if no games placed for many consecutive days
            if (gamesToday == 0)
                consecutiveEmptyDays++;
            else
                consecutiveEmptyDays = 0;

            if (consecutiveEmptyDays > numTeams)
                break;

            day++;
        }

        return result;
    }
}
