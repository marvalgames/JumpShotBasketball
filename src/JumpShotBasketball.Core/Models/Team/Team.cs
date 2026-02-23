using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Staff;

namespace JumpShotBasketball.Core.Models.Team;

/// <summary>
/// Aggregate team entity owning its roster, record, financial data, staff, and draft picks.
/// In C++, these were scattered across CAverage arrays; here each Team owns its own data.
/// </summary>
public class Team
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CityName { get; set; } = string.Empty;

    // Composed data (previously in separate CAverage arrays)
    public List<Player.Player> Roster { get; set; } = new();
    public TeamRecord Record { get; set; } = new();
    public TeamFinancial Financial { get; set; } = new();
    public DraftBoard DraftBoard { get; set; } = new();

    // Staff references (indexes into league staff pool, or direct references)
    public StaffMember? Scout { get; set; }
    public StaffMember? Coach { get; set; }
    public StaffMember? GeneralManager { get; set; }
}
