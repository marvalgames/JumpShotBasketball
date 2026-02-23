using JumpShotBasketball.Core.Constants;

namespace JumpShotBasketball.Core.Models.Staff;

/// <summary>
/// Coach, scout, or GM staff member.
/// Maps to C++ CStaff with scout/coach/GM ratings and progression history.
/// </summary>
public class StaffMember
{
    // Identity
    public int Id { get; set; }
    public int Slot { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Team { get; set; } = string.Empty;
    public int Age { get; set; }
    public bool Fired { get; set; }

    // Record
    public int Record { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Playoffs { get; set; }
    public int Rings { get; set; }
    public int Points { get; set; }

    // Current job assignments (-1 = none)
    public int ScoutJob { get; set; } = -1;
    public int CoachJob { get; set; } = -1;
    public int GmJob { get; set; } = -1;
    public int CurrentScout { get; set; } = -1;
    public int CurrentCoach { get; set; } = -1;
    public int CurrentGM { get; set; } = -1;

    // Original assignments
    public int OriginalCoach { get; set; }
    public int OriginalScout { get; set; }
    public int OriginalGM { get; set; }

    // Retirement/resignation flags
    public bool RetiredCoach { get; set; }
    public bool RetiredScout { get; set; }
    public bool RetiredGM { get; set; }
    public bool ResignedCoach { get; set; }
    public bool ResignedScout { get; set; }
    public bool ResignedGM { get; set; }

    // Scout ratings (player evaluation ability)
    public int Pot1Rating { get; set; } = 3;
    public int Pot2Rating { get; set; } = 3;
    public int EffortRating { get; set; } = 3;
    public int ScoringRating { get; set; } = 3;
    public int ShootingRating { get; set; } = 3;
    public int ReboundingRating { get; set; } = 3;
    public int PassingRating { get; set; } = 3;
    public int DefenseRating { get; set; } = 3;

    // Coach ratings (team strategy impact)
    public int CoachPot1 { get; set; } = 3;
    public int CoachPot2 { get; set; } = 3;
    public int CoachEffort { get; set; } = 3;
    public int CoachScoring { get; set; } = 3;
    public int CoachShooting { get; set; } = 3;
    public int CoachRebounding { get; set; } = 3;
    public int CoachPassing { get; set; } = 3;
    public int CoachDefense { get; set; } = 3;

    // Coach play-type ratings (O=outside, P=penetration, I=inside, F=fastbreak + defense variants)
    public int CoachOutside { get; set; } = 3;
    public int CoachPenetration { get; set; } = 3;
    public int CoachInside { get; set; } = 3;
    public int CoachFastbreak { get; set; } = 3;
    public int CoachOutsideDefense { get; set; } = 3;
    public int CoachPenetrationDefense { get; set; } = 3;
    public int CoachInsideDefense { get; set; } = 3;
    public int CoachFastbreakDefense { get; set; } = 3;
    public int CoachEndurance { get; set; } = 3;

    // GM/personality ratings
    public int Power { get; set; } = 3;
    public int Power1 { get; set; } = 3;
    public int Power2 { get; set; } = 3;
    public int Power20 { get; set; } = 3;
    public int Loyalty { get; set; } = 3;
    public int Personality { get; set; } = 3;

    // Rating progression history (6 years, maps to m_pot1[6], m_pot2[6], etc.)
    public int[] Pot1History { get; set; } = new int[LeagueConstants.MaxStaffProgressionYears];
    public int[] Pot2History { get; set; } = new int[LeagueConstants.MaxStaffProgressionYears];
    public int[] EffortHistory { get; set; } = new int[LeagueConstants.MaxStaffProgressionYears];
    public int[] ScoringHistory { get; set; } = new int[LeagueConstants.MaxStaffProgressionYears];
    public int[] ShootingHistory { get; set; } = new int[LeagueConstants.MaxStaffProgressionYears];
    public int[] ReboundingHistory { get; set; } = new int[LeagueConstants.MaxStaffProgressionYears];
    public int[] PassingHistory { get; set; } = new int[LeagueConstants.MaxStaffProgressionYears];
    public int[] DefenseHistory { get; set; } = new int[LeagueConstants.MaxStaffProgressionYears];

    // Free agency: offers and interest per team
    public double[] Offered { get; set; } = new double[LeagueConstants.MaxTeams + 1];
    public double[] Interested { get; set; } = new double[LeagueConstants.MaxTeams + 1];
    public string[] Lines { get; set; } = new string[LeagueConstants.MaxTeams + 1];

    public StaffMember()
    {
        // Initialize Lines array with empty strings
        for (int i = 0; i < Lines.Length; i++)
            Lines[i] = string.Empty;
    }
}
