namespace JumpShotBasketball.Core.Models.History;

/// <summary>
/// Top-level record book tracking single-game, season, and career records.
/// Port of C++ CHigh (High.cpp).
/// </summary>
public class RecordBook
{
    /// <summary>
    /// Single-game records. Key1 = scope ("League", "Playoff", "Team_0", "TeamPlayoff_0"), Key2 = stat name.
    /// </summary>
    public Dictionary<string, Dictionary<string, StatRecordList>> SingleGameRecords { get; set; } = new();

    /// <summary>
    /// Season-average records. Key1 = scope ("League", "Team_0"), Key2 = stat name.
    /// </summary>
    public Dictionary<string, Dictionary<string, StatRecordList>> SeasonRecords { get; set; } = new();

    /// <summary>
    /// Career-average records. Key = stat name (league-wide, deduplicated by PlayerId).
    /// </summary>
    public Dictionary<string, StatRecordList> CareerRecords { get; set; } = new();
}
