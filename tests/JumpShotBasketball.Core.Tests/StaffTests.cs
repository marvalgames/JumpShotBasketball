using FluentAssertions;
using JumpShotBasketball.Core.Constants;
using JumpShotBasketball.Core.Models.Staff;

namespace JumpShotBasketball.Core.Tests;

public class StaffTests
{
    [Fact]
    public void NewStaffMember_HasCorrectDefaults()
    {
        var staff = new StaffMember();

        staff.ScoutJob.Should().Be(-1);
        staff.CoachJob.Should().Be(-1);
        staff.GmJob.Should().Be(-1);
        staff.Pot1Rating.Should().Be(3);
        staff.Power.Should().Be(3);
        staff.Loyalty.Should().Be(3);
        staff.Personality.Should().Be(3);
    }

    [Fact]
    public void StaffMember_ProgressionArrays_HaveCorrectSize()
    {
        var staff = new StaffMember();

        staff.Pot1History.Should().HaveCount(LeagueConstants.MaxStaffProgressionYears);
        staff.Pot2History.Should().HaveCount(LeagueConstants.MaxStaffProgressionYears);
        staff.EffortHistory.Should().HaveCount(LeagueConstants.MaxStaffProgressionYears);
        staff.ScoringHistory.Should().HaveCount(LeagueConstants.MaxStaffProgressionYears);
        staff.ShootingHistory.Should().HaveCount(LeagueConstants.MaxStaffProgressionYears);
        staff.ReboundingHistory.Should().HaveCount(LeagueConstants.MaxStaffProgressionYears);
        staff.PassingHistory.Should().HaveCount(LeagueConstants.MaxStaffProgressionYears);
        staff.DefenseHistory.Should().HaveCount(LeagueConstants.MaxStaffProgressionYears);
    }

    [Fact]
    public void StaffMember_LinesArray_InitializedWithEmptyStrings()
    {
        var staff = new StaffMember();

        foreach (var line in staff.Lines)
            line.Should().BeEmpty();
    }
}
