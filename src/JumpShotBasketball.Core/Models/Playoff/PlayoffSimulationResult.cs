using JumpShotBasketball.Core.Models.Game;

namespace JumpShotBasketball.Core.Models.Playoff;

/// <summary>
/// Result of a playoff simulation batch (day/round/all).
/// </summary>
public class PlayoffSimulationResult
{
    public List<GameResult> GameResults { get; set; } = new();
    public int GamesSimulated { get; set; }
    public bool RoundComplete { get; set; }
    public bool PlayoffsComplete { get; set; }
    public int? ChampionTeamIndex { get; set; }
}
