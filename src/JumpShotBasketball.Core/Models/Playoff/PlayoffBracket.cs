using JumpShotBasketball.Core.Enums;

namespace JumpShotBasketball.Core.Models.Playoff;

/// <summary>
/// Root playoff bracket state. Replaces C++ 3D schedule arrays and file-based tracking.
/// </summary>
public class PlayoffBracket
{
    public List<PlayoffRound> Rounds { get; set; } = new();
    public PlayoffMode Mode { get; set; }
    public int TeamsPerGroup { get; set; }
    public int TotalRounds { get; set; }
    public int CurrentRound { get; set; } = 1;
    public bool PlayoffsStarted { get; set; }
    public bool PlayoffsComplete { get; set; }
    public int? ChampionTeamIndex { get; set; }
    public List<PlayoffSeed> Seeds { get; set; } = new();
}
