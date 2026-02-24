using FluentAssertions;
using JumpShotBasketball.Core.Constants;
using JumpShotBasketball.Core.Enums;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Staff;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

public class StaffManagementServiceTests
{
    // ───────────────────────────────────────────────────────────────
    // Helpers
    // ───────────────────────────────────────────────────────────────

    private static StaffMember CreateStaff(int age = 50, int power = 3, int points = 20,
        string status = "active", int loyalty = 3)
    {
        return new StaffMember
        {
            Name = "Test Staff",
            Age = age,
            Power = power,
            Points = points,
            Status = status,
            Loyalty = loyalty,
            Pot1Rating = 4,
            Pot2Rating = 4,
            EffortRating = 4,
            ScoringRating = 4,
            ShootingRating = 4,
            ReboundingRating = 4,
            PassingRating = 4,
            DefenseRating = 4,
            CoachPot1 = 3,
            CoachPot2 = 3,
            CoachEffort = 3,
            CoachScoring = 3,
            CoachShooting = 3,
            CoachRebounding = 3,
            CoachPassing = 3,
            CoachDefense = 3,
            CoachOutside = 3,
            CoachPenetration = 3,
            CoachInside = 3,
            CoachFastbreak = 3,
            CoachOutsideDefense = 3,
            CoachPenetrationDefense = 3,
            CoachInsideDefense = 3,
            CoachFastbreakDefense = 3
        };
    }

    private static Team CreateTeam(int id, string name, int wins = 41, int losses = 41)
    {
        var team = new Team
        {
            Id = id,
            Name = name,
            Record = new TeamRecord { Wins = wins, Losses = losses },
            Financial = new TeamFinancial { OwnerPatience = 3 }
        };
        return team;
    }

    private static League CreateTestLeague(int numTeams = 4, int staffPoolSize = 20)
    {
        var league = new League();
        league.Settings.NumberOfTeams = numTeams;

        for (int t = 0; t < numTeams; t++)
        {
            var team = CreateTeam(t, $"Team {t}");
            var scout = CreateStaff(points: 20);
            scout.Name = $"Scout {t}";
            scout.CurrentScout = t;
            var coach = CreateStaff(points: 20);
            coach.Name = $"Coach {t}";
            coach.CurrentCoach = t;
            var gm = CreateStaff(points: 20);
            gm.Name = $"GM {t}";
            gm.CurrentGM = t;

            team.Scout = scout;
            team.Coach = coach;
            team.GeneralManager = gm;

            // Add some roster players for roster value
            for (int p = 0; p < 15; p++)
            {
                team.Roster.Add(new Player
                {
                    Name = $"Player {t}-{p}",
                    Position = p < 3 ? "PG" : p < 6 ? "SG" : p < 9 ? "SF" : p < 12 ? "PF" : " C",
                    Ratings = new PlayerRatings { TradeValue = 5.0 + p, TradeTrueRating = 5.0 + p }
                });
            }

            league.Teams.Add(team);
            league.StaffPool.Add(scout);
            league.StaffPool.Add(coach);
            league.StaffPool.Add(gm);
        }

        // Add unassigned pool members
        for (int i = numTeams * 3; i < staffPoolSize; i++)
        {
            var staff = CreateStaff(status: "");
            staff.Name = $"Free Staff {i}";
            staff.CurrentScout = -1;
            staff.CurrentCoach = -1;
            staff.CurrentGM = -1;
            league.StaffPool.Add(staff);
        }

        return league;
    }

    // ───────────────────────────────────────────────────────────────
    // CalculateScoutRating
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void CalculateScoutRating_IdentityPermutation_Returns7()
    {
        // history[1..5] = {1,2,3,4,5} → sum_abs_dev = 0 → 8 - (0+2)/2 = 8-1 = 7
        int[] history = { 0, 1, 2, 3, 4, 5 };
        StaffManagementService.CalculateScoutRating(history).Should().Be(7);
    }

    [Fact]
    public void CalculateScoutRating_ReversePermutation_Returns3()
    {
        // history[1..5] = {5,4,3,2,1} → |5-1|+|4-2|+|3-3|+|2-4|+|1-5| = 4+2+0+2+4 = 12
        // 8 - (12+2)/2 = 8-7 = 1... wait let me check
        // Actually: 8 - (12+2)/2 = 8 - 7 = 1
        int[] history = { 0, 5, 4, 3, 2, 1 };
        StaffManagementService.CalculateScoutRating(history).Should().Be(1);
    }

    [Fact]
    public void CalculateScoutRating_SingleSwap_Returns5()
    {
        // {2,1,3,4,5}: |2-1|+|1-2|+|3-3|+|4-4|+|5-5| = 1+1+0+0+0 = 2
        // 8 - (2+2)/2 = 8-2 = 6
        int[] history = { 0, 2, 1, 3, 4, 5 };
        StaffManagementService.CalculateScoutRating(history).Should().Be(6);
    }

    [Fact]
    public void CalculateScoutRating_KnownValue()
    {
        // {3,2,1,5,4}: |3-1|+|2-2|+|1-3|+|5-4|+|4-5| = 2+0+2+1+1 = 6
        // 8 - (6+2)/2 = 8-4 = 4
        int[] history = { 0, 3, 2, 1, 5, 4 };
        StaffManagementService.CalculateScoutRating(history).Should().Be(4);
    }

    // ───────────────────────────────────────────────────────────────
    // GenerateConstrainedPermutation
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void GenerateConstrainedPermutation_ProducesValidPermutation()
    {
        var random = new Random(42);
        var perm = StaffManagementService.GenerateConstrainedPermutation(random);

        perm.Should().HaveCount(5);
        perm.Should().OnlyContain(v => v >= 1 && v <= 5);
        perm.Distinct().Should().HaveCount(5); // all unique
    }

    [Fact]
    public void GenerateConstrainedPermutation_RespectsConstraints()
    {
        var random = new Random(12345);
        for (int i = 0; i < 100; i++)
        {
            var perm = StaffManagementService.GenerateConstrainedPermutation(random);
            perm[0].Should().NotBe(5, "chart[0] (position 1) must not be 5");
            perm[4].Should().NotBe(1, "chart[4] (position 5) must not be 1");
        }
    }

    [Fact]
    public void GenerateConstrainedPermutation_Deterministic()
    {
        var perm1 = StaffManagementService.GenerateConstrainedPermutation(new Random(999));
        var perm2 = StaffManagementService.GenerateConstrainedPermutation(new Random(999));
        perm1.Should().Equal(perm2);
    }

    // ───────────────────────────────────────────────────────────────
    // InitializeNewStaffMember
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void InitializeNewStaffMember_SetsAgeInRange()
    {
        var staff = new StaffMember();
        var random = new Random(42);
        StaffManagementService.InitializeNewStaffMember(staff, random);

        // Age can be 20-78 (with 1% chance of wide range), but mostly 33-64
        staff.Age.Should().BeGreaterThanOrEqualTo(20);
        staff.Age.Should().BeLessThanOrEqualTo(78);
    }

    [Fact]
    public void InitializeNewStaffMember_SetsRatingsInRange()
    {
        var staff = new StaffMember();
        StaffManagementService.InitializeNewStaffMember(staff, new Random(42));

        staff.Pot1Rating.Should().BeInRange(1, 5);
        staff.Pot2Rating.Should().BeInRange(1, 5);
        staff.Power.Should().BeInRange(1, 5);
        staff.CoachScoring.Should().BeInRange(1, 5);
        staff.Loyalty.Should().BeInRange(1, 5);
    }

    [Fact]
    public void InitializeNewStaffMember_ResetsCareerStats()
    {
        var staff = new StaffMember { Wins = 100, Points = 50 };
        StaffManagementService.InitializeNewStaffMember(staff, new Random(42));

        staff.Wins.Should().Be(0);
        staff.Losses.Should().Be(0);
        staff.Points.Should().Be(0);
        staff.Playoffs.Should().Be(0);
        staff.Rings.Should().Be(0);
    }

    [Fact]
    public void InitializeNewStaffMember_Deterministic()
    {
        var staff1 = new StaffMember();
        var staff2 = new StaffMember();
        StaffManagementService.InitializeNewStaffMember(staff1, new Random(555));
        StaffManagementService.InitializeNewStaffMember(staff2, new Random(555));

        staff1.Age.Should().Be(staff2.Age);
        staff1.Power.Should().Be(staff2.Power);
        staff1.CoachScoring.Should().Be(staff2.CoachScoring);
    }

    // ───────────────────────────────────────────────────────────────
    // AdjustRating
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void AdjustRating_YoungStaff_CanImprove()
    {
        // Young staff (age < prime) with high power should improve sometimes
        int improved = 0;
        for (int i = 0; i < 1000; i++)
        {
            int result = StaffManagementService.AdjustRating(3, 40, 55, 7, 5, true, new Random(i));
            if (result > 3) improved++;
        }
        improved.Should().BeGreaterThan(0, "young staff with high power should sometimes improve");
    }

    [Fact]
    public void AdjustRating_OldStaff_CanDecline()
    {
        int declined = 0;
        for (int i = 0; i < 1000; i++)
        {
            int result = StaffManagementService.AdjustRating(4, 65, 55, 7, 1, true, new Random(i));
            if (result < 4) declined++;
        }
        declined.Should().BeGreaterThan(0, "old staff with low power should sometimes decline");
    }

    [Fact]
    public void AdjustRating_RespectsMinBound()
    {
        // Even with decline, rating should not go below 1
        for (int i = 0; i < 100; i++)
        {
            int result = StaffManagementService.AdjustRating(1, 70, 50, 7, 1, true, new Random(i));
            result.Should().BeGreaterThanOrEqualTo(1);
        }
    }

    [Fact]
    public void AdjustRating_RespectsMaxBound()
    {
        for (int i = 0; i < 100; i++)
        {
            int result = StaffManagementService.AdjustRating(5, 40, 55, 5, 5, true, new Random(i));
            result.Should().BeLessThanOrEqualTo(5);
        }
    }

    [Fact]
    public void AdjustRating_ActiveVsInactive_DifferentRates()
    {
        // Active staff uses f1=60, inactive uses f1=75 — different change rates
        int changesActive = 0;
        int changesInactive = 0;
        for (int i = 0; i < 1000; i++)
        {
            var rng = new Random(i);
            int active = StaffManagementService.AdjustRating(4, 45, 55, 7, 3, true, rng);
            if (active != 4) changesActive++;

            rng = new Random(i);
            int inactive = StaffManagementService.AdjustRating(4, 45, 55, 7, 3, false, rng);
            if (inactive != 4) changesInactive++;
        }
        // Active staff should change more often (lower threshold)
        changesActive.Should().BeGreaterThan(changesInactive);
    }

    [Fact]
    public void AdjustRating_Deterministic()
    {
        var r1 = StaffManagementService.AdjustRating(4, 50, 55, 7, 3, true, new Random(42));
        var r2 = StaffManagementService.AdjustRating(4, 50, 55, 7, 3, true, new Random(42));
        r1.Should().Be(r2);
    }

    // ───────────────────────────────────────────────────────────────
    // DevelopStaffMember
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void DevelopStaffMember_IncrementsAge()
    {
        var staff = CreateStaff(age: 45);
        StaffManagementService.DevelopStaffMember(staff, 55, new Random(42));
        staff.Age.Should().Be(46);
    }

    [Fact]
    public void DevelopStaffMember_RatingsCanChange()
    {
        var staff = CreateStaff(age: 45, power: 5);
        int originalPot1 = staff.Pot1Rating;
        int originalCoachPot1 = staff.CoachPot1;

        // Run development many times with different seeds to find at least one change
        bool anyScoutChange = false;
        bool anyCoachChange = false;
        for (int seed = 0; seed < 100; seed++)
        {
            var testStaff = CreateStaff(age: 45, power: 5);
            StaffManagementService.DevelopStaffMember(testStaff, 55, new Random(seed));
            if (testStaff.Pot1Rating != originalPot1) anyScoutChange = true;
            if (testStaff.CoachPot1 != originalCoachPot1) anyCoachChange = true;
            if (anyScoutChange && anyCoachChange) break;
        }
        (anyScoutChange || anyCoachChange).Should().BeTrue("ratings should change after many development cycles");
    }

    [Fact]
    public void DevelopStaffMember_Deterministic()
    {
        var staff1 = CreateStaff(age: 50);
        var staff2 = CreateStaff(age: 50);

        StaffManagementService.DevelopStaffMember(staff1, 55, new Random(42));
        StaffManagementService.DevelopStaffMember(staff2, 55, new Random(42));

        staff1.Age.Should().Be(staff2.Age);
        staff1.Power.Should().Be(staff2.Power);
    }

    // ───────────────────────────────────────────────────────────────
    // CalculateStaffPrime
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void CalculateStaffPrime_Scout()
    {
        StaffManagementService.CalculateStaffPrime(StaffRole.Scout, 3).Should().Be(57);
        StaffManagementService.CalculateStaffPrime(StaffRole.Scout, 5).Should().Be(59);
    }

    [Fact]
    public void CalculateStaffPrime_Coach()
    {
        StaffManagementService.CalculateStaffPrime(StaffRole.Coach, 3).Should().Be(53);
        StaffManagementService.CalculateStaffPrime(StaffRole.Coach, 1).Should().Be(51);
    }

    [Fact]
    public void CalculateStaffPrime_GM()
    {
        StaffManagementService.CalculateStaffPrime(StaffRole.GM, 3).Should().Be(55);
        StaffManagementService.CalculateStaffPrime(StaffRole.GM, 5).Should().Be(57);
    }

    // ───────────────────────────────────────────────────────────────
    // EvaluatePerformance
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void EvaluatePerformance_ScoutWeights_PointsTimes1_RosterTimes3()
    {
        var record = new TeamRecord { Wins = 41, Losses = 41, Points = 10 };
        int result = StaffManagementService.EvaluatePerformance(
            StaffRole.Scout, 0, 100, 110, 40, 42, record);
        // points×1 = 10, roster improved (+1)×3 = 3, pctChange = (1+5-5)×2 = 2
        // total = 0 + 10 + 3 + 2 = 15
        result.Should().Be(15);
    }

    [Fact]
    public void EvaluatePerformance_CoachWeights_PointsTimes3_RosterTimes1()
    {
        var record = new TeamRecord { Wins = 41, Losses = 41, Points = 10 };
        int result = StaffManagementService.EvaluatePerformance(
            StaffRole.Coach, 0, 100, 110, 40, 42, record);
        // points×3 = 30, roster improved (+1)×1 = 1, pctChange = (1+5-5)×2 = 2
        // total = 0 + 30 + 1 + 2 = 33
        result.Should().Be(33);
    }

    [Fact]
    public void EvaluatePerformance_GmWeights_AllTimes2()
    {
        var record = new TeamRecord { Wins = 41, Losses = 41, Points = 10 };
        int result = StaffManagementService.EvaluatePerformance(
            StaffRole.GM, 0, 100, 110, 40, 42, record);
        // points×2 = 20, roster improved (+1)×2 = 2, pctChange = (1+5-5)×2 = 2
        // total = 0 + 20 + 2 + 2 = 24
        result.Should().Be(24);
    }

    [Fact]
    public void EvaluatePerformance_NegativeClampedToZero()
    {
        var record = new TeamRecord { Wins = 10, Losses = 72, Points = 0 };
        int result = StaffManagementService.EvaluatePerformance(
            StaffRole.Scout, 0, 200, 50, 50, 32, record);
        result.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void EvaluatePerformance_PctChange_Calculated()
    {
        // Career was 50-50 (.500), now 60-22 (.732)
        var record = new TeamRecord { Wins = 60, Losses = 22, Points = 5 };
        int result = StaffManagementService.EvaluatePerformance(
            StaffRole.GM, 10, 100, 100, 50, 50, record);
        // roster no change (100 < 100 is false, 100 > 100 is false) → addRoster = 0
        // currentPct = 60/82 ≈ 0.732, oldPct = 50/100 = 0.5
        // addPct = 1 + int(0.732*10) - 5 = 1 + 7 - 5 = 3
        // total = 10 + 5×2 + 0×2 + 3×2 = 10 + 10 + 0 + 6 = 26
        result.Should().Be(26);
    }

    [Fact]
    public void EvaluatePerformance_AccumulatesOnCurrentPoints()
    {
        var record = new TeamRecord { Wins = 41, Losses = 41, Points = 5 };
        int result = StaffManagementService.EvaluatePerformance(
            StaffRole.Scout, 100, 50, 50, 41, 41, record);
        // current=100, points=5×1=5, roster: 50<=50 → +1×3=3, pct: same → 0+5-5=0 → ×2=0
        result.Should().Be(108);
    }

    // ───────────────────────────────────────────────────────────────
    // CalculateRosterValue
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void CalculateRosterValue_SumsTop12()
    {
        var team = new Team();
        for (int i = 0; i < 15; i++)
        {
            team.Roster.Add(new Player
            {
                Name = $"P{i}",
                Ratings = new PlayerRatings { TradeValue = 10.0 }
            });
        }
        // Top 12 of 15 players, each with TradeValue=10 → sum = 120
        StaffManagementService.CalculateRosterValue(team).Should().Be(120);
    }

    [Fact]
    public void CalculateRosterValue_EmptyRoster_Returns0()
    {
        var team = new Team();
        StaffManagementService.CalculateRosterValue(team).Should().Be(0);
    }

    [Fact]
    public void CalculateRosterValue_ExcludesInjured()
    {
        var team = new Team();
        for (int i = 0; i < 12; i++)
        {
            team.Roster.Add(new Player
            {
                Name = $"P{i}",
                Injury = i < 3 ? 5 : 0, // 3 injured
                Ratings = new PlayerRatings { TradeValue = 10.0 }
            });
        }
        // 9 healthy × 10 = 90
        StaffManagementService.CalculateRosterValue(team).Should().Be(90);
    }

    // ───────────────────────────────────────────────────────────────
    // CalculateFiringLine
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void CalculateFiringLine_Normal()
    {
        var staff = CreateStaff(points: 30);
        StaffManagementService.CalculateFiringLine(staff).Should().Be(15);
    }

    [Fact]
    public void CalculateFiringLine_MinimumIs8()
    {
        var staff = CreateStaff(points: 10);
        StaffManagementService.CalculateFiringLine(staff).Should().Be(8);
    }

    [Fact]
    public void CalculateFiringLine_HighPoints()
    {
        var staff = CreateStaff(points: 100);
        StaffManagementService.CalculateFiringLine(staff).Should().Be(50);
    }

    // ───────────────────────────────────────────────────────────────
    // ShouldFire
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void ShouldFire_LowPoints_Fires()
    {
        var staff = CreateStaff(points: 5);
        // firingLine=8, patience=3 → fire = 8+6-6 = 8 → 5 <= 8 → true
        StaffManagementService.ShouldFire(staff, 8, 3).Should().BeTrue();
    }

    [Fact]
    public void ShouldFire_HighPoints_NoFire()
    {
        var staff = CreateStaff(points: 30);
        // firingLine=15, patience=3 → fire = 15+6-6 = 15 → 30 <= 15 → false
        StaffManagementService.ShouldFire(staff, 15, 3).Should().BeFalse();
    }

    [Fact]
    public void ShouldFire_HighPatience_ReducesFirings()
    {
        var staff = CreateStaff(points: 10);
        // firingLine=8, patience=5 → fire = 8+6-10 = 4 → 10 <= 4 → false
        StaffManagementService.ShouldFire(staff, 8, 5).Should().BeFalse();
    }

    // ───────────────────────────────────────────────────────────────
    // ShouldRetire
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void ShouldRetire_YoungStaff_VeryUnlikely()
    {
        var staff = CreateStaff(age: 40);
        int retireCount = 0;
        for (int i = 0; i < 1000; i++)
        {
            if (StaffManagementService.ShouldRetire(StaffRole.Scout, staff, new Random(i)))
                retireCount++;
        }
        // factor = (74-40)*2 = 68, factor² = 4624, probability = 1/4624
        retireCount.Should().BeLessThan(10, "40-year-old should rarely retire");
    }

    [Fact]
    public void ShouldRetire_OldStaff_HighChance()
    {
        var staff = CreateStaff(age: 75);
        int retireCount = 0;
        for (int i = 0; i < 100; i++)
        {
            if (StaffManagementService.ShouldRetire(StaffRole.Scout, staff, new Random(i)))
                retireCount++;
        }
        // factor = max(2, (74-75)*2) = max(2, -2) = 2, probability = 1/2 = 50%
        retireCount.Should().BeGreaterThan(20);
    }

    [Fact]
    public void ShouldRetire_RoleThresholds_Differ()
    {
        // Coach threshold=72, Scout=74, so a 73-year-old coach is more likely to retire
        var staff = CreateStaff(age: 73);

        int scoutRetires = 0, coachRetires = 0;
        for (int i = 0; i < 10000; i++)
        {
            if (StaffManagementService.ShouldRetire(StaffRole.Scout, staff, new Random(i)))
                scoutRetires++;
            if (StaffManagementService.ShouldRetire(StaffRole.Coach, staff, new Random(i)))
                coachRetires++;
        }
        // Scout factor = max(2, (74-73)*2) = max(2,2) = 2² = 4, p=25%
        // Coach factor = max(2, (72-73)*2) = max(2,-2) = 2, p=50%
        coachRetires.Should().BeGreaterThan(scoutRetires);
    }

    [Fact]
    public void ShouldRetire_Deterministic()
    {
        var staff = CreateStaff(age: 70);
        var r1 = StaffManagementService.ShouldRetire(StaffRole.Scout, staff, new Random(42));
        var r2 = StaffManagementService.ShouldRetire(StaffRole.Scout, staff, new Random(42));
        r1.Should().Be(r2);
    }

    // ───────────────────────────────────────────────────────────────
    // ShouldResign
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void ShouldResign_WinningTeam_StaysMoreOften()
    {
        // All-3 ratings → value = 24/56 ≈ 0.4286
        // Winning team: pct=0.8, leagueAvg=0.5, loyalty 3→(2+3)/10=0.5
        // score = (0.8+0.5+0.5)/3 ≈ 0.6
        // diff = max(0, (0.4286 - 0.6)/3) = 0 → never resigns
        var staff = CreateStaff(loyalty: 3);
        int resignCount = 0;
        for (int i = 0; i < 100; i++)
        {
            if (StaffManagementService.ShouldResign(StaffRole.Scout, staff, 66, 16, 0.5, new Random(i)))
                resignCount++;
        }
        resignCount.Should().Be(0);
    }

    [Fact]
    public void ShouldResign_LosingTeam_HighValue_LeavesMore()
    {
        // High-rated staff on losing team
        var staff = CreateStaff(loyalty: 1);
        staff.Pot1Rating = 7;
        staff.Pot2Rating = 7;
        staff.EffortRating = 7;
        staff.ScoringRating = 7;
        staff.ShootingRating = 7;
        staff.ReboundingRating = 7;
        staff.PassingRating = 7;
        staff.DefenseRating = 7;
        // value = 56/56 = 1.0
        // Losing team: pct=0.2, loyalty=1→(2+1)/10=0.3
        // score = (0.2+0.5+0.3)/3 ≈ 0.333
        // diff = (1.0-0.333)/3 ≈ 0.222

        int resignCount = 0;
        for (int i = 0; i < 100; i++)
        {
            if (StaffManagementService.ShouldResign(StaffRole.Scout, staff, 16, 66, 0.5, new Random(i)))
                resignCount++;
        }
        resignCount.Should().BeGreaterThan(10, "high-value staff on losing team should resign sometimes");
    }

    [Fact]
    public void ShouldResign_LoyaltyMakesStaying()
    {
        // Same scenario but with high loyalty
        var staff = CreateStaff(loyalty: 5);
        staff.Pot1Rating = 5; staff.Pot2Rating = 5; staff.EffortRating = 5;
        staff.ScoringRating = 5; staff.ShootingRating = 5; staff.ReboundingRating = 5;
        staff.PassingRating = 5; staff.DefenseRating = 5;
        // value = 40/56 ≈ 0.714
        // loyalty=5 → (2+5)/10=0.7

        int lowLoyaltyResigns = 0;
        int highLoyaltyResigns = 0;

        var lowLoyaltyStaff = CreateStaff(loyalty: 1);
        lowLoyaltyStaff.Pot1Rating = 5; lowLoyaltyStaff.Pot2Rating = 5; lowLoyaltyStaff.EffortRating = 5;
        lowLoyaltyStaff.ScoringRating = 5; lowLoyaltyStaff.ShootingRating = 5; lowLoyaltyStaff.ReboundingRating = 5;
        lowLoyaltyStaff.PassingRating = 5; lowLoyaltyStaff.DefenseRating = 5;

        for (int i = 0; i < 1000; i++)
        {
            if (StaffManagementService.ShouldResign(StaffRole.Scout, lowLoyaltyStaff, 20, 62, 0.5, new Random(i)))
                lowLoyaltyResigns++;
            if (StaffManagementService.ShouldResign(StaffRole.Scout, staff, 20, 62, 0.5, new Random(i)))
                highLoyaltyResigns++;
        }

        lowLoyaltyResigns.Should().BeGreaterThanOrEqualTo(highLoyaltyResigns,
            "high loyalty staff should resign less often");
    }

    [Fact]
    public void ShouldResign_Deterministic()
    {
        var staff = CreateStaff(loyalty: 3);
        staff.Pot1Rating = 5; staff.Pot2Rating = 5; staff.EffortRating = 5;
        staff.ScoringRating = 5; staff.ShootingRating = 5; staff.ReboundingRating = 5;
        staff.PassingRating = 5; staff.DefenseRating = 5;

        var r1 = StaffManagementService.ShouldResign(StaffRole.Scout, staff, 30, 52, 0.5, new Random(42));
        var r2 = StaffManagementService.ShouldResign(StaffRole.Scout, staff, 30, 52, 0.5, new Random(42));
        r1.Should().Be(r2);
    }

    // ───────────────────────────────────────────────────────────────
    // CalculateStaffValue
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void CalculateStaffValue_Scout_SumsScoutRatings()
    {
        var staff = CreateStaff();
        // All 4s → 32/56 ≈ 0.571
        var value = StaffManagementService.CalculateStaffValue(StaffRole.Scout, staff);
        value.Should().BeApproximately(32.0 / 56.0, 0.001);
    }

    [Fact]
    public void CalculateStaffValue_Coach_IncludesAllThreeCategories()
    {
        var staff = CreateStaff();
        // scout total = 4×8 = 32, coach total = 3×8 = 24, playType total = 3×8 = 24
        // (32+24+24) / 136 ≈ 0.588
        var value = StaffManagementService.CalculateStaffValue(StaffRole.Coach, staff);
        value.Should().BeApproximately(80.0 / 136.0, 0.001);
    }

    [Fact]
    public void CalculateStaffValue_GM_SameAsScout()
    {
        var staff = CreateStaff();
        var scoutValue = StaffManagementService.CalculateStaffValue(StaffRole.Scout, staff);
        var gmValue = StaffManagementService.CalculateStaffValue(StaffRole.GM, staff);
        gmValue.Should().Be(scoutValue);
    }

    // ───────────────────────────────────────────────────────────────
    // Hiring
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void FindTopCandidate_FiltersCorrectly()
    {
        var pool = new List<StaffMember>
        {
            CreateStaff(status: "active"),          // 0: employed, skip
            CreateStaff(status: "retires"),          // 1: retired, skip
            CreateStaff(status: ""),                  // 2: available
            CreateStaff(status: ""),                  // 3: available
        };
        pool[0].CurrentScout = 0;
        pool[1].CurrentScout = -1;
        pool[2].CurrentScout = -1;
        pool[3].CurrentScout = -1;
        pool[2].OriginalScout = 2; // from team 1 (1-based)
        pool[3].OriginalScout = 5; // from team 4

        // Hiring for team 1 (0-based) → originalScout = 2 (1-based) for pool[2] matches, skip
        int top = StaffManagementService.FindTopCandidate(pool, StaffRole.Scout, 1);
        top.Should().Be(3, "only pool[3] is available and not from original team");
    }

    [Fact]
    public void CalculateHiringRating_ScoutFormula()
    {
        var staff = CreateStaff();
        // Scout: (pot1+pot2+effort)*2 + scoring+shooting+reb+passing+def
        // (4+4+4)*2 + 4+4+4+4+4 = 24 + 20 = 44
        StaffManagementService.CalculateHiringRating(staff, StaffRole.Scout).Should().Be(44);
    }

    [Fact]
    public void CalculateInterest_ScoutFormula()
    {
        // pct + patience/10 + rosterValue/100*2
        double interest = StaffManagementService.CalculateInterest(StaffRole.Scout, 1.5, 3, 200);
        // 1.5 + 0.3 + 4.0 = 5.8
        interest.Should().BeApproximately(5.8, 0.001);
    }

    [Fact]
    public void ChooseBestOffer_PicksHighestInterest()
    {
        var staff = CreateStaff(status: "");
        staff.Interested[0] = 5.0;
        staff.Interested[1] = 8.0;
        staff.Interested[2] = 3.0;

        int[] topCandidates = { 7, 7, 7 }; // staff index 7 is top for all teams

        int chosen = StaffManagementService.ChooseBestOffer(staff, StaffRole.Scout,
            topCandidates, 7, 3);

        chosen.Should().Be(1, "team 1 has highest interest (8.0)");
    }

    // ───────────────────────────────────────────────────────────────
    // RunStaffLifecycle
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void RunStaffLifecycle_FullIntegration_CompletesWithoutError()
    {
        var league = CreateTestLeague(4, 40);
        var result = StaffManagementService.RunStaffLifecycle(league, new Random(42));

        result.Should().NotBeNull();
        result.StaffDeveloped.Should().BeGreaterThan(0);
    }

    [Fact]
    public void RunStaffLifecycle_Deterministic()
    {
        var league1 = CreateTestLeague(4, 40);
        var league2 = CreateTestLeague(4, 40);

        var result1 = StaffManagementService.RunStaffLifecycle(league1, new Random(12345));
        var result2 = StaffManagementService.RunStaffLifecycle(league2, new Random(12345));

        result1.Fired.Count.Should().Be(result2.Fired.Count);
        result1.Retired.Count.Should().Be(result2.Retired.Count);
        result1.Resigned.Count.Should().Be(result2.Resigned.Count);
        result1.Hired.Count.Should().Be(result2.Hired.Count);
        result1.StaffDeveloped.Should().Be(result2.StaffDeveloped);
    }

    [Fact]
    public void RunStaffLifecycle_EmptyPool_ReturnsEmptyResult()
    {
        var league = new League();
        league.Teams.Add(CreateTeam(0, "Team 0"));
        // No staff pool
        var result = StaffManagementService.RunStaffLifecycle(league, new Random(42));

        result.Fired.Should().BeEmpty();
        result.Hired.Should().BeEmpty();
        result.StaffDeveloped.Should().Be(0);
    }

    [Fact]
    public void RunStaffLifecycle_FiredStaff_GetsReplaced()
    {
        // Create a league where all staff have 0 points (will be fired)
        var league = CreateTestLeague(2, 20);
        foreach (var team in league.Teams)
        {
            if (team.Scout != null) team.Scout.Points = 0;
            if (team.Coach != null) team.Coach.Points = 0;
            if (team.GeneralManager != null) team.GeneralManager.Points = 0;
        }
        foreach (var staff in league.StaffPool)
            staff.Points = 0;

        var result = StaffManagementService.RunStaffLifecycle(league, new Random(42));

        result.Fired.Count.Should().BeGreaterThan(0, "staff with 0 points should be fired");
    }

    // ───────────────────────────────────────────────────────────────
    // UpdateStaffPerformance
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void UpdateStaffPerformance_UpdatesPoints()
    {
        var league = CreateTestLeague(2, 6);
        league.Teams[0].Record.Wins = 50;
        league.Teams[0].Record.Losses = 32;
        league.Teams[0].Record.Points = 8;

        int oldScoutPoints = league.Teams[0].Scout!.Points;
        StaffManagementService.UpdateStaffPerformance(league);

        league.Teams[0].Scout!.Points.Should().NotBe(oldScoutPoints,
            "performance evaluation should change points");
    }

    [Fact]
    public void UpdateStaffPerformance_AccumulatesRecord()
    {
        var league = CreateTestLeague(1, 3);
        league.Teams[0].Scout!.Wins = 100;
        league.Teams[0].Scout!.Losses = 50;
        league.Teams[0].Record.Wins = 50;
        league.Teams[0].Record.Losses = 32;

        StaffManagementService.UpdateStaffPerformance(league);

        league.Teams[0].Scout!.Wins.Should().Be(150);
        league.Teams[0].Scout!.Losses.Should().Be(82);
    }

    [Fact]
    public void UpdateStaffPerformance_UpdatesRosterValue()
    {
        var league = CreateTestLeague(1, 3);
        league.Teams[0].Financial.TeamRosterValue = 50;

        StaffManagementService.UpdateStaffPerformance(league);

        league.Teams[0].Financial.TeamRosterValue.Should().NotBe(50,
            "roster value should be recalculated");
    }

    // ───────────────────────────────────────────────────────────────
    // OwnerPatience default
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void OwnerPatience_DefaultsTo3()
    {
        new TeamFinancial().OwnerPatience.Should().Be(3);
    }

    // ───────────────────────────────────────────────────────────────
    // StaffManagementResult model
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void StaffManagementResult_InitializesEmpty()
    {
        var result = new StaffManagementResult();
        result.Fired.Should().BeEmpty();
        result.Retired.Should().BeEmpty();
        result.Resigned.Should().BeEmpty();
        result.Hired.Should().BeEmpty();
        result.StaffDeveloped.Should().Be(0);
        result.NewStaffGenerated.Should().Be(0);
    }

    // ───────────────────────────────────────────────────────────────
    // OffSeasonResult integration
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public void OffSeasonResult_HasStaffResultProperty()
    {
        var result = new OffSeasonResult();
        result.StaffResult.Should().BeNull();
        result.StaffResult = new StaffManagementResult();
        result.StaffResult.Should().NotBeNull();
    }
}
