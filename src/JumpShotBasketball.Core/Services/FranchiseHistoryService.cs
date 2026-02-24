using JumpShotBasketball.Core.Models.History;
using JumpShotBasketball.Core.Models.League;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Franchise history archival: captures team W-L, playoff appearances, championships per season.
/// </summary>
public static class FranchiseHistoryService
{
    /// <summary>
    /// Archives the current season record for each team into franchise history.
    /// Should be called after playoffs are complete but before season state is reset.
    /// </summary>
    public static void ArchiveSeasonRecords(League league, int year)
    {
        // Ensure franchise histories exist for all teams
        while (league.FranchiseHistories.Count < league.Teams.Count)
        {
            int idx = league.FranchiseHistories.Count;
            league.FranchiseHistories.Add(new FranchiseHistory
            {
                TeamIndex = idx,
                TeamName = league.Teams[idx].Name
            });
        }

        for (int i = 0; i < league.Teams.Count; i++)
        {
            var team = league.Teams[i];
            var history = league.FranchiseHistories[i];

            // Determine team MVP (highest-rated player by TradeTrueRating)
            string mvpName = string.Empty;
            double mvpValue = 0;
            foreach (var player in team.Roster)
            {
                if (string.IsNullOrEmpty(player.Name)) continue;
                if (player.Ratings.TradeTrueRating > mvpValue)
                {
                    mvpValue = player.Ratings.TradeTrueRating;
                    mvpName = player.Name;
                }
            }

            var seasonRecord = new FranchiseSeasonRecord
            {
                Year = year,
                Wins = team.Record.Wins,
                Losses = team.Record.Losses,
                MadePlayoffs = team.Record.IsPlayoffTeam || team.Record.InPlayoffs,
                WonChampionship = team.Record.HasRing,
                CoachName = team.Coach?.Name ?? string.Empty,
                MvpName = mvpName,
                MvpValue = mvpValue
            };

            history.Seasons.Add(seasonRecord);
        }
    }

    /// <summary>
    /// Gets the number of championships for a franchise.
    /// </summary>
    public static int GetChampionshipCount(FranchiseHistory history)
    {
        return history.TotalChampionships;
    }

    /// <summary>
    /// Gets the best season (by wins) for a franchise, or null if no seasons archived.
    /// </summary>
    public static FranchiseSeasonRecord? GetBestSeason(FranchiseHistory history)
    {
        if (history.Seasons.Count == 0) return null;
        return history.Seasons.OrderByDescending(s => s.Wins).First();
    }
}
