namespace JumpShotBasketball.Core.Models.History;

/// <summary>
/// A sorted ranked list for one stat category in the record book.
/// </summary>
public class StatRecordList
{
    public string StatName { get; set; } = string.Empty;
    public int MaxEntries { get; set; } = 10;
    public List<RecordBookEntry> Entries { get; set; } = new();
}
