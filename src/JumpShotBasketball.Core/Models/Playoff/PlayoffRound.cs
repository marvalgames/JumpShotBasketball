using JumpShotBasketball.Core.Enums;

namespace JumpShotBasketball.Core.Models.Playoff;

/// <summary>
/// One round of the playoffs containing multiple series.
/// </summary>
public class PlayoffRound
{
    public int RoundNumber { get; set; }
    public SeriesFormat Format { get; set; }
    public List<PlayoffSeries> Series { get; set; } = new();

    public bool IsComplete => Series.Count > 0 && Series.All(s => s.IsComplete);
}
