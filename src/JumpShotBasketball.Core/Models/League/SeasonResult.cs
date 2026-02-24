using JumpShotBasketball.Core.Models.Awards;
using JumpShotBasketball.Core.Models.Game;
using JumpShotBasketball.Core.Models.Playoff;

namespace JumpShotBasketball.Core.Models.League;

/// <summary>
/// Result of simulating a full season: regular season (two halves),
/// All-Star weekend, playoffs, and awards.
/// </summary>
public class SeasonResult
{
    public int Year { get; set; }
    public SimulationResult FirstHalfResult { get; set; } = new();
    public SimulationResult SecondHalfResult { get; set; } = new();
    public int TotalRegularSeasonGames { get; set; }
    public int TotalRegularSeasonDays { get; set; }
    public AllStarWeekendResult? AllStarWeekendResult { get; set; }
    public bool AllStarWeekendPlayed { get; set; }
    public PlayoffSimulationResult? PlayoffResult { get; set; }
    public int? ChampionTeamIndex { get; set; }
    public SeasonAwards? Awards { get; set; }
}
