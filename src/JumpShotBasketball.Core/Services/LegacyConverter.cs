using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Team;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Orchestrator that reads a set of legacy C++ files and assembles a League object.
/// All files must share the same base path (e.g., "path/to/default_08-09").
/// </summary>
public static class LegacyConverter
{
    /// <summary>
    /// Converts a set of legacy C++ files into a League object.
    /// Reads .lge (required), .plr (required), .sch (required), .frn (required),
    /// and optionally .bud and .sal for full-season leagues.
    /// </summary>
    public static League ConvertFromLegacyFiles(string basePath)
    {
        string lgePath = basePath + ".lge";
        string plrPath = basePath + ".plr";
        string schPath = basePath + ".sch";
        string frnPath = basePath + ".frn";
        string budPath = basePath + ".bud";
        string salPath = basePath + ".sal";

        if (!File.Exists(lgePath))
            throw new FileNotFoundException("League file not found.", lgePath);
        if (!File.Exists(plrPath))
            throw new FileNotFoundException("Player file not found.", plrPath);

        // 1. Read league settings and team metadata
        var (settings, teamNames, controlTypes) = LegacyFileReader.ReadLgeFile(lgePath);
        int numTeams = settings.NumberOfTeams;

        // 2. Create league with teams
        var league = new League { Settings = settings };

        for (int i = 0; i < numTeams; i++)
        {
            league.Teams.Add(new Team
            {
                Id = i + 1,
                Name = teamNames[i]
            });
        }

        // 3. Read player data and assign to teams
        var (teamRosters, freeAgents) = LegacyFileReader.ReadPlrFile(plrPath, numTeams);

        for (int i = 0; i < numTeams && i < teamRosters.Count; i++)
        {
            var team = league.Teams[i];
            foreach (var player in teamRosters[i])
            {
                player.Team = team.Name;
                player.TeamIndex = i + 1;
                team.Roster.Add(player);
            }
        }

        // Store free agents — they don't belong to any team
        // Add them as a pool accessible from the league level
        // For now, create a virtual "Free Agents" entry or store separately
        if (freeAgents.Count > 0)
        {
            // Free agents go into team rosters with TeamIndex = 0
            foreach (var fa in freeAgents)
            {
                fa.Team = string.Empty;
                fa.TeamIndex = 0;
                fa.Contract.IsFreeAgent = true;
            }

            // If any team has space, we could distribute, but for now
            // just add a placeholder team or leave them unassigned
            // Convention: store FAs in a "team" with Id=0
            var faTeam = new Team
            {
                Id = 0,
                Name = "Free Agents"
            };
            faTeam.Roster.AddRange(freeAgents);
            league.Teams.Add(faTeam);
        }

        // 4. Read schedule
        if (File.Exists(schPath))
        {
            var games = LegacyFileReader.ReadSchFile(schPath);
            league.Schedule = new Schedule { Games = games };
        }

        // 5. Read franchise financial data
        if (File.Exists(frnPath))
        {
            var financials = LegacyFileReader.ReadFrnFile(frnPath, numTeams);
            for (int i = 0; i < numTeams && i < financials.Count; i++)
            {
                var team = league.Teams[i];
                team.Financial = financials[i];
                team.Financial.TeamName = team.Name;
                team.CityName = financials[i].CityName;
            }
        }

        // 6. Read budget data (optional, full leagues only)
        if (File.Exists(budPath))
        {
            var financials = league.Teams
                .Where(t => t.Id > 0)
                .Select(t => t.Financial)
                .ToList();
            LegacyFileReader.ReadBudFile(budPath, financials);
        }

        // 7. Read salary/contract data (optional, full leagues only)
        if (File.Exists(salPath))
        {
            var allPlayers = league.Teams
                .SelectMany(t => t.Roster)
                .ToList();
            LegacyFileReader.ReadSalFile(salPath, allPlayers);
        }

        return league;
    }

    /// <summary>
    /// Converts legacy files and saves directly to .jsb format.
    /// </summary>
    public static void ConvertAndSave(string basePath, string outputPath)
    {
        var league = ConvertFromLegacyFiles(basePath);
        SaveLoadService.Save(league, outputPath);
    }
}
