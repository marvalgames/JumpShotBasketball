using JumpShotBasketball.Core.Enums;

namespace JumpShotBasketball.Core.Models.Playoff;

/// <summary>
/// A best-of-N series between two teams.
/// HigherSeed = team with home court advantage (better record or higher seed).
/// </summary>
public class PlayoffSeries
{
    public int HigherSeedTeamIndex { get; set; }
    public int LowerSeedTeamIndex { get; set; }
    public int HigherSeedWins { get; set; }
    public int LowerSeedWins { get; set; }
    public int WinsToAdvance { get; set; }
    public SeriesFormat Format { get; set; }
    public List<PlayoffGame> Games { get; set; } = new();

    public bool IsComplete => HigherSeedWins >= WinsToAdvance || LowerSeedWins >= WinsToAdvance;

    public int? WinnerTeamIndex =>
        HigherSeedWins >= WinsToAdvance ? HigherSeedTeamIndex :
        LowerSeedWins >= WinsToAdvance ? LowerSeedTeamIndex :
        null;

    public int? LoserTeamIndex =>
        HigherSeedWins >= WinsToAdvance ? LowerSeedTeamIndex :
        LowerSeedWins >= WinsToAdvance ? HigherSeedTeamIndex :
        null;

    public int GamesPlayed => HigherSeedWins + LowerSeedWins;
}
