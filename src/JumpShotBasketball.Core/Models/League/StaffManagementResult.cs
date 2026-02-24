namespace JumpShotBasketball.Core.Models.League;

/// <summary>
/// Result of running the staff lifecycle via StaffManagementService.RunStaffLifecycle.
/// </summary>
public class StaffManagementResult
{
    public List<string> Fired { get; set; } = new();
    public List<string> Retired { get; set; } = new();
    public List<string> Resigned { get; set; } = new();
    public List<string> Hired { get; set; } = new();
    public int StaffDeveloped { get; set; }
    public int NewStaffGenerated { get; set; }
}
