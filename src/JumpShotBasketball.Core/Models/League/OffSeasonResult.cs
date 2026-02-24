namespace JumpShotBasketball.Core.Models.League;

/// <summary>
/// Result of running the off-season pipeline via OffSeasonService.AdvanceSeason.
/// </summary>
public class OffSeasonResult
{
    public int PreviousYear { get; set; }
    public int NewYear { get; set; }
    public List<string> RetiredPlayerNames { get; set; } = new();
    public int PlayersRetired { get; set; }
    public int ContractsExpired { get; set; }
    public int NewFreeAgents { get; set; }
    public int RookiesGenerated { get; set; }
    public DraftResult? DraftResult { get; set; }
    public FreeAgencyResult? FreeAgencyResult { get; set; }
    public StaffManagementResult? StaffResult { get; set; }
}
