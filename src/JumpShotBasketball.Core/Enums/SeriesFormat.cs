namespace JumpShotBasketball.Core.Enums;

/// <summary>
/// Playoff series format. Values encode wins-to-advance.
/// Port of C++ SetSeriesFormat format encoding.
/// </summary>
public enum SeriesFormat
{
    BestOf7 = 4,
    BestOf5 = 3,
    BestOf3 = 2,
    SingleGame = 1
}
