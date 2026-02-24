using JumpShotBasketball.Core.Constants;
using JumpShotBasketball.Core.Enums;
using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Staff;
using JumpShotBasketball.Core.Models.Team;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Staff lifecycle management: generation, development, performance evaluation,
/// firing, retirement, resignation, and hiring.
/// Port of C++ Staff.cpp, HireStaff.cpp, StaffDlg.cpp orchestration, and Average.cpp:1686-1762.
/// </summary>
public static class StaffManagementService
{
    // ── Generation ─────────────────────────────────────────────

    /// <summary>
    /// Generates a constrained permutation of [1,2,3,4,5] where chart[0]≠5 and chart[4]≠1.
    /// Port of the inner loop in Staff.cpp CreateNewMember / SetImprovement.
    /// Returns a 5-element array (0-indexed, representing positions 1-5 in C++).
    /// </summary>
    internal static int[] GenerateConstrainedPermutation(Random random)
    {
        while (true)
        {
            int[] chart = new int[5];
            bool[] used = new bool[6]; // indices 0-5, using 1-5

            for (int o = 0; o < 5; o++)
            {
                int r;
                do
                {
                    r = random.Next(1, 6); // IntRandom(5) returns 1-5
                }
                while (used[r]);
                used[r] = true;
                chart[o] = r;
            }

            // Constraint: chart[0] (position 1) ≠ 5, chart[4] (position 5) ≠ 1
            if (chart[0] != 5 && chart[4] != 1)
                return chart;
        }
    }

    /// <summary>
    /// Calculates a scout rating from a history array using the formula:
    /// rating = 8 - (sum_of_abs_deviations + 2) / 2  (integer division).
    /// Port of Staff.cpp SetPot1Rating/SetScoringRating/etc.
    /// History is a 6-element array where indices 1-5 contain the permutation values.
    /// </summary>
    public static int CalculateScoutRating(int[] history)
    {
        int sum = 0;
        for (int i = 1; i <= 5; i++)
        {
            sum += Math.Abs(history[i] - i);
        }
        return 8 - (sum + 2) / 2;
    }

    /// <summary>
    /// Recalculates all 8 scout ratings from their history arrays.
    /// Port of Staff.cpp SetRatings().
    /// </summary>
    public static void RecalculateScoutRatings(StaffMember staff)
    {
        staff.Pot1Rating = CalculateScoutRating(staff.Pot1History);
        staff.Pot2Rating = CalculateScoutRating(staff.Pot2History);
        staff.EffortRating = CalculateScoutRating(staff.EffortHistory);
        staff.ScoringRating = CalculateScoutRating(staff.ScoringHistory);
        staff.ShootingRating = CalculateScoutRating(staff.ShootingHistory);
        staff.ReboundingRating = CalculateScoutRating(staff.ReboundingHistory);
        staff.PassingRating = CalculateScoutRating(staff.PassingHistory);
        staff.DefenseRating = CalculateScoutRating(staff.DefenseHistory);
    }

    /// <summary>
    /// Initializes a new staff member with random age, scout chart permutations, and ratings.
    /// Port of Staff.cpp CreateNewMember().
    /// </summary>
    public static void InitializeNewStaffMember(StaffMember staff, Random? random = null)
    {
        random ??= Random.Shared;

        // Generate 8 constrained permutations for scout rating histories
        int[][] histories = {
            staff.ScoringHistory, staff.ShootingHistory, staff.ReboundingHistory,
            staff.PassingHistory, staff.DefenseHistory, staff.Pot1History,
            staff.Pot2History, staff.EffortHistory
        };

        for (int n = 0; n < 8; n++)
        {
            int[] perm = GenerateConstrainedPermutation(random);
            for (int j = 0; j < 5; j++)
                histories[n][j + 1] = perm[j];
        }

        RecalculateScoutRatings(staff);

        // Age: IntRandom(32)+32 (range 33-64), 1% chance IntRandom(59)+20 (range 20-78)
        staff.Age = random.Next(1, 33) + 32;
        if (random.NextDouble() < 0.01)
            staff.Age = random.Next(1, 60) + 20;

        // Reset career stats
        staff.Status = string.Empty;
        staff.Record = 0;
        staff.Wins = 0;
        staff.Losses = 0;
        staff.Playoffs = 0;
        staff.Rings = 0;
        staff.Points = 0;

        // Scout ratings (1-5 random)
        staff.Pot1Rating = random.Next(1, 6);
        staff.Pot2Rating = random.Next(1, 6);
        staff.EffortRating = random.Next(1, 6);
        staff.ScoringRating = random.Next(1, 6);
        staff.ShootingRating = random.Next(1, 6);
        staff.ReboundingRating = random.Next(1, 6);
        staff.PassingRating = random.Next(1, 6);
        staff.DefenseRating = random.Next(1, 6);

        // Power/GM ratings
        staff.Power = random.Next(1, 6);
        staff.Power1 = random.Next(1, 6);
        staff.Power2 = random.Next(1, 6);

        // Coach ratings
        staff.CoachPot1 = random.Next(1, 6);
        staff.CoachPot2 = random.Next(1, 6);
        staff.CoachEffort = random.Next(1, 6);
        staff.CoachScoring = random.Next(1, 6);
        staff.CoachShooting = random.Next(1, 6);
        staff.CoachRebounding = random.Next(1, 6);
        staff.CoachPassing = random.Next(1, 6);
        staff.CoachDefense = random.Next(1, 6);

        // Coach play-type ratings
        staff.CoachOutside = random.Next(1, 6);
        staff.CoachPenetration = random.Next(1, 6);
        staff.CoachInside = random.Next(1, 6);
        staff.CoachFastbreak = random.Next(1, 6);
        staff.CoachOutsideDefense = random.Next(1, 6);
        staff.CoachPenetrationDefense = random.Next(1, 6);
        staff.CoachInsideDefense = random.Next(1, 6);
        staff.CoachFastbreakDefense = random.Next(1, 6);
        staff.CoachEndurance = random.Next(1, 6);

        // Personality
        staff.Loyalty = random.Next(1, 6);
        staff.Personality = random.Next(1, 6);
        staff.Power20 = random.Next(1, 6);
    }

    // ── Development ────────────────────────────────────────────

    /// <summary>
    /// Adjusts a single rating based on age relative to prime.
    /// Port of Staff.cpp AdjustRating().
    /// isActive = true when staff has a current job assignment (status != "").
    /// </summary>
    internal static int AdjustRating(int rating, int age, int prime, int max,
        int power, bool isActive, Random random)
    {
        bool decline = false;
        bool change = false;

        int f1 = isActive ? 60 : 75;
        int f2 = isActive ? 480 : 600;

        // Primary direction
        int n = random.Next(1, f1 + 1);
        if (age <= prime && n <= (2 + power)) { change = true; }
        if (age > prime && n <= (2 + (6 - power))) { decline = true; change = true; }

        // Rare reverse
        n = random.Next(1, f2 + 1);
        if (age <= prime && n <= (2 + (6 - power))) { decline = true; change = true; }
        if (age > prime && n <= (2 + power)) { decline = false; change = true; }

        if (change && decline && rating > 1) rating--;
        else if (change && !decline && rating < max) rating++;

        return rating;
    }

    /// <summary>
    /// Calculates prime age for a staff role.
    /// Port of StaffDlg.cpp:1376-1380.
    /// Note: C++ bug uses scout's power for coach prime; we port faithfully.
    /// </summary>
    public static int CalculateStaffPrime(StaffRole role, int power)
    {
        return role switch
        {
            StaffRole.Scout => 54 + power,
            StaffRole.Coach => 50 + power,
            StaffRole.GM => 52 + power,
            _ => 52 + power
        };
    }

    /// <summary>
    /// Develops a staff member: increments age, adjusts all ratings, regenerates scout charts.
    /// Port of Staff.cpp SetImprovement().
    /// Bug fix: adds 1000-attempt guard + clamp to [3,7] for chart regeneration
    /// (C++ can infinite-loop when target rating is 1 or 2, which constrained permutations can't produce).
    /// </summary>
    public static void DevelopStaffMember(StaffMember staff, int prime, Random? random = null)
    {
        random ??= Random.Shared;

        bool isActive = !string.IsNullOrEmpty(staff.Status) && staff.Status != "retires";

        staff.Age++;

        // Adjust scout ratings (max 7)
        staff.Pot1Rating = AdjustRating(staff.Pot1Rating, staff.Age, prime, 7, staff.Power, isActive, random);
        staff.Pot2Rating = AdjustRating(staff.Pot2Rating, staff.Age, prime, 7, staff.Power, isActive, random);
        staff.EffortRating = AdjustRating(staff.EffortRating, staff.Age, prime, 7, staff.Power, isActive, random);
        staff.ScoringRating = AdjustRating(staff.ScoringRating, staff.Age, prime, 7, staff.Power, isActive, random);
        staff.ShootingRating = AdjustRating(staff.ShootingRating, staff.Age, prime, 7, staff.Power, isActive, random);
        staff.ReboundingRating = AdjustRating(staff.ReboundingRating, staff.Age, prime, 7, staff.Power, isActive, random);
        staff.PassingRating = AdjustRating(staff.PassingRating, staff.Age, prime, 7, staff.Power, isActive, random);
        staff.DefenseRating = AdjustRating(staff.DefenseRating, staff.Age, prime, 7, staff.Power, isActive, random);

        // Adjust coach/GM ratings (max 5)
        staff.Power = AdjustRating(staff.Power, staff.Age, prime, 5, staff.Power, isActive, random);
        staff.Power1 = AdjustRating(staff.Power1, staff.Age, prime, 5, staff.Power, isActive, random);
        staff.Power2 = AdjustRating(staff.Power2, staff.Age, prime, 5, staff.Power, isActive, random);
        staff.CoachPot1 = AdjustRating(staff.CoachPot1, staff.Age, prime, 5, staff.Power, isActive, random);
        staff.CoachPot2 = AdjustRating(staff.CoachPot2, staff.Age, prime, 5, staff.Power, isActive, random);
        staff.CoachEffort = AdjustRating(staff.CoachEffort, staff.Age, prime, 5, staff.Power, isActive, random);
        staff.CoachScoring = AdjustRating(staff.CoachScoring, staff.Age, prime, 5, staff.Power, isActive, random);
        staff.CoachShooting = AdjustRating(staff.CoachShooting, staff.Age, prime, 5, staff.Power, isActive, random);
        staff.CoachRebounding = AdjustRating(staff.CoachRebounding, staff.Age, prime, 5, staff.Power, isActive, random);
        staff.CoachPassing = AdjustRating(staff.CoachPassing, staff.Age, prime, 5, staff.Power, isActive, random);
        staff.CoachDefense = AdjustRating(staff.CoachDefense, staff.Age, prime, 5, staff.Power, isActive, random);
        staff.CoachOutside = AdjustRating(staff.CoachOutside, staff.Age, prime, 5, staff.Power, isActive, random);
        staff.CoachPenetration = AdjustRating(staff.CoachPenetration, staff.Age, prime, 5, staff.Power, isActive, random);
        staff.CoachInside = AdjustRating(staff.CoachInside, staff.Age, prime, 5, staff.Power, isActive, random);
        staff.CoachFastbreak = AdjustRating(staff.CoachFastbreak, staff.Age, prime, 5, staff.Power, isActive, random);
        staff.CoachOutsideDefense = AdjustRating(staff.CoachOutsideDefense, staff.Age, prime, 5, staff.Power, isActive, random);
        staff.CoachPenetrationDefense = AdjustRating(staff.CoachPenetrationDefense, staff.Age, prime, 5, staff.Power, isActive, random);
        staff.CoachInsideDefense = AdjustRating(staff.CoachInsideDefense, staff.Age, prime, 5, staff.Power, isActive, random);
        staff.CoachFastbreakDefense = AdjustRating(staff.CoachFastbreakDefense, staff.Age, prime, 5, staff.Power, isActive, random);
        staff.CoachEndurance = AdjustRating(staff.CoachEndurance, staff.Age, prime, 5, staff.Power, isActive, random);
        staff.Loyalty = AdjustRating(staff.Loyalty, staff.Age, prime, 5, staff.Power, isActive, random);

        // Regenerate scout rating charts to match new ratings (with 1000-attempt guard)
        RegenerateCharts(staff, random);
    }

    /// <summary>
    /// Regenerates all 8 scout history charts to match current scout ratings.
    /// Clamps target to [3,7] to prevent infinite loops (constrained permutation range).
    /// </summary>
    private static void RegenerateCharts(StaffMember staff, Random random)
    {
        int[] targets = {
            staff.Pot1Rating, staff.Pot2Rating, staff.EffortRating, staff.ScoringRating,
            staff.ShootingRating, staff.ReboundingRating, staff.PassingRating, staff.DefenseRating
        };
        int[][] histories = {
            staff.Pot1History, staff.Pot2History, staff.EffortHistory, staff.ScoringHistory,
            staff.ShootingHistory, staff.ReboundingHistory, staff.PassingHistory, staff.DefenseHistory
        };

        for (int r = 0; r < 8; r++)
        {
            int wanted = Math.Clamp(targets[r], 3, 7); // Bug fix: clamp to producible range
            int attempts = 0;

            while (attempts < 1000)
            {
                attempts++;
                int[] perm = GenerateConstrainedPermutation(random);
                for (int j = 0; j < 5; j++)
                    histories[r][j + 1] = perm[j];

                int calculated = CalculateScoutRating(histories[r]);
                if (calculated == wanted)
                    break;
            }

            // After regeneration, update the actual rating to what the chart produces
            targets[r] = CalculateScoutRating(histories[r]);
        }

        // Write back calculated ratings
        staff.Pot1Rating = CalculateScoutRating(staff.Pot1History);
        staff.Pot2Rating = CalculateScoutRating(staff.Pot2History);
        staff.EffortRating = CalculateScoutRating(staff.EffortHistory);
        staff.ScoringRating = CalculateScoutRating(staff.ScoringHistory);
        staff.ShootingRating = CalculateScoutRating(staff.ShootingHistory);
        staff.ReboundingRating = CalculateScoutRating(staff.ReboundingHistory);
        staff.PassingRating = CalculateScoutRating(staff.PassingHistory);
        staff.DefenseRating = CalculateScoutRating(staff.DefenseHistory);
    }

    // ── Performance ────────────────────────────────────────────

    /// <summary>
    /// Calculates roster value as sum of TradeValue for top 12 active non-injured players.
    /// Port of HireStaff.cpp SetRosterValue().
    /// </summary>
    public static int CalculateRosterValue(Team team)
    {
        var values = team.Roster
            .Where(p => !string.IsNullOrEmpty(p.Name) && p.Injury == 0)
            .Select(p => Math.Max(0, p.Ratings.TradeValue))
            .OrderByDescending(v => v)
            .Take(12)
            .Sum();

        return (int)values;
    }

    /// <summary>
    /// Evaluates staff performance using role-weighted scoring.
    /// Port of HireStaff.cpp ScoutStatus/CoachStatus/GmStatus().
    /// Bug fix: C++ UpdateStaffStats applies wrong formula per role; we use correct formula.
    /// </summary>
    public static int EvaluatePerformance(StaffRole role, int currentPoints,
        int oldRosterValue, int currentRosterValue,
        int careerWins, int careerLosses, TeamRecord record)
    {
        // Roster change
        int addRoster;
        if (role == StaffRole.Scout)
            addRoster = oldRosterValue <= currentRosterValue ? 1 : -1;
        else
            addRoster = oldRosterValue < currentRosterValue ? 1 : (oldRosterValue > currentRosterValue ? -1 : 0);

        // Win percentage change
        int currentWins = record.Wins;
        int currentLosses = record.Losses;
        if (currentWins + currentLosses == 0) { currentWins = 1; currentLosses = 1; }
        double currentPct = (double)currentWins / (currentWins + currentLosses);

        int prevW = careerWins;
        int prevL = careerLosses;
        if (prevW + prevL == 0) { prevW = 1; prevL = 1; }
        double oldPct = (double)prevW / (prevW + prevL);

        int addPct = 0;
        if (oldPct < currentPct) addPct = 1;
        else if (oldPct > currentPct) addPct = -1;
        addPct = addPct + (int)(currentPct * 10) - 5;

        int result = role switch
        {
            StaffRole.Scout => currentPoints + record.Points * 1 + addRoster * 3 + addPct * 2,
            StaffRole.Coach => currentPoints + record.Points * 3 + addRoster * 1 + addPct * 2,
            StaffRole.GM => currentPoints + record.Points * 2 + addRoster * 2 + addPct * 2,
            _ => currentPoints
        };

        return Math.Max(0, result);
    }

    // ── Firing ─────────────────────────────────────────────────

    /// <summary>
    /// Calculates the firing line: max(8, points/2).
    /// Port of HireStaff.cpp SetScoutFiringLine/SetCoachFiringLine/SetGmFiringLine.
    /// </summary>
    public static int CalculateFiringLine(StaffMember staff)
    {
        int line = staff.Points / 2;
        return Math.Max(8, line);
    }

    /// <summary>
    /// Determines if a staff member should be fired.
    /// Port of HireStaff.cpp FireScout/FireCoach/FireGM.
    /// </summary>
    public static bool ShouldFire(StaffMember staff, int firingLine, int ownerPatience)
    {
        int fire = firingLine + 6 - ownerPatience * 2;
        if (fire < 0) fire = 0;
        return staff.Points <= fire;
    }

    // ── Retirement ─────────────────────────────────────────────

    /// <summary>
    /// Determines if a staff member should retire based on age.
    /// Port of HireStaff.cpp RetireScout/RetireCoach/RetireGM.
    /// Thresholds: Scout=74, Coach=72, GM=73.
    /// </summary>
    public static bool ShouldRetire(StaffRole role, StaffMember staff, Random? random = null)
    {
        random ??= Random.Shared;

        int threshold = role switch
        {
            StaffRole.Scout => 74,
            StaffRole.Coach => 72,
            StaffRole.GM => 73,
            _ => 73
        };

        int factor = (threshold - staff.Age) * 2;

        if (factor < 2)
            factor = 2;
        else
            factor = factor * factor;

        int n = random.Next(1, factor + 1); // IntRandom(factor) returns 1..factor
        return n == 1;
    }

    // ── Resignation ────────────────────────────────────────────

    /// <summary>
    /// Calculates staff self-valuation by role.
    /// Scout/GM: sum of 8 scout ratings / 56.
    /// Coach: (scout_8 + coach_8 + playType_8) / 136.
    /// Port of HireStaff.cpp ResignScout/ResignCoach/ResignGM value calculations.
    /// </summary>
    internal static double CalculateStaffValue(StaffRole role, StaffMember staff)
    {
        int scoutTotal = staff.Pot1Rating + staff.Pot2Rating + staff.EffortRating +
                         staff.ScoringRating + staff.ShootingRating + staff.ReboundingRating +
                         staff.PassingRating + staff.DefenseRating;

        if (role == StaffRole.Coach)
        {
            int coachTotal = staff.CoachPot1 + staff.CoachPot2 + staff.CoachEffort +
                             staff.CoachScoring + staff.CoachShooting + staff.CoachRebounding +
                             staff.CoachPassing + staff.CoachDefense;
            int playTypeTotal = staff.CoachOutside + staff.CoachPenetration +
                                staff.CoachInside + staff.CoachFastbreak +
                                staff.CoachOutsideDefense + staff.CoachPenetrationDefense +
                                staff.CoachInsideDefense + staff.CoachFastbreakDefense;
            return (double)(scoutTotal + coachTotal + playTypeTotal) / 136.0;
        }

        return (double)scoutTotal / 56.0;
    }

    /// <summary>
    /// Determines if a staff member should resign.
    /// Port of HireStaff.cpp ResignScout/ResignCoach/ResignGM.
    /// </summary>
    public static bool ShouldResign(StaffRole role, StaffMember staff,
        int teamWins, int teamLosses, double leagueAvgWinPct, Random? random = null)
    {
        random ??= Random.Shared;

        int games = teamWins + teamLosses;
        if (games == 0) games = 1;
        double pct = (double)teamWins / games;
        double loyalty = (2.0 + staff.Loyalty) / 10.0;
        double score = (pct + leagueAvgWinPct + loyalty) / 3.0;

        double value = CalculateStaffValue(role, staff);

        double diff = (value - score) / 3.0;
        if (diff <= 0) diff = 0;

        double n = random.NextDouble();
        return n <= diff;
    }

    // ── Hiring ─────────────────────────────────────────────────

    /// <summary>
    /// Calculates a hiring rating for a staff member by role.
    /// Port of HireStaff.cpp SetTopScout/SetTopCoach/SetTopGM rating formulas.
    /// </summary>
    public static int CalculateHiringRating(StaffMember staff, StaffRole role)
    {
        int scoutRatings = staff.Pot1Rating + staff.Pot2Rating + staff.EffortRating +
                           staff.ScoringRating + staff.ShootingRating + staff.ReboundingRating +
                           staff.PassingRating + staff.DefenseRating;

        return role switch
        {
            StaffRole.Scout => (staff.Pot1Rating + staff.Pot2Rating + staff.EffortRating) * 2 +
                               staff.ScoringRating + staff.ShootingRating + staff.ReboundingRating +
                               staff.PassingRating + staff.DefenseRating,
            StaffRole.Coach => scoutRatings * 1 +
                               (staff.CoachPot1 + staff.CoachPot2 + staff.CoachEffort +
                                staff.CoachScoring + staff.CoachShooting + staff.CoachRebounding +
                                staff.CoachPassing + staff.CoachDefense) * 3 +
                               (staff.CoachOutside + staff.CoachPenetration +
                                staff.CoachInside + staff.CoachFastbreak +
                                staff.CoachOutsideDefense + staff.CoachPenetrationDefense +
                                staff.CoachInsideDefense + staff.CoachFastbreakDefense) * 3,
            StaffRole.GM => scoutRatings * 1,
            _ => scoutRatings
        };
    }

    /// <summary>
    /// Finds the top candidate from the staff pool for a given role and team.
    /// Filters: unassigned, not from original team, not retired/active status.
    /// Port of HireStaff.cpp SetTopScout/SetTopCoach/SetTopGM.
    /// Returns index into pool, or -1 if none found.
    /// </summary>
    public static int FindTopCandidate(List<StaffMember> pool, StaffRole role, int teamIndex)
    {
        int hiRating = 0;
        int hiIndex = -1;

        for (int i = 0; i < pool.Count; i++)
        {
            var staff = pool[i];

            // Check assignment status for the specific role
            int currentTeam = role switch
            {
                StaffRole.Scout => staff.CurrentScout,
                StaffRole.Coach => staff.CurrentCoach,
                StaffRole.GM => staff.CurrentGM,
                _ => -1
            };

            int originalTeam = role switch
            {
                StaffRole.Scout => staff.OriginalScout,
                StaffRole.Coach => staff.OriginalCoach,
                StaffRole.GM => staff.OriginalGM,
                _ => 0
            };

            // Filter: unassigned, not from original team (1-based), not retired/active
            if (currentTeam != -1) continue;
            if (originalTeam == teamIndex + 1) continue; // C++ uses owner = t+1
            if (staff.Status == "retires" || staff.Status == "active") continue;

            int rating = CalculateHiringRating(staff, role);
            if (rating > hiRating)
            {
                hiRating = rating;
                hiIndex = i;
            }
        }

        return hiIndex;
    }

    /// <summary>
    /// Calculates team interest in hiring a staff member.
    /// Port of StaffDlg.cpp SetInterest() formulas.
    /// </summary>
    public static double CalculateInterest(StaffRole role, double winRatio,
        int ownerPatience, int rosterValue)
    {
        return role switch
        {
            StaffRole.Scout => winRatio + (double)ownerPatience / 10.0 + (double)rosterValue / 100.0 * 2.0,
            StaffRole.Coach => winRatio * 2.0 + (double)ownerPatience / 20.0 + (double)rosterValue / 100.0 * 3.0,
            StaffRole.GM => winRatio / 2.0 + (double)ownerPatience / 5.0 + (double)rosterValue / 100.0 * 4.0,
            _ => 0
        };
    }

    /// <summary>
    /// Staff member chooses the team with the highest interest among those that selected them.
    /// Port of Staff.cpp ChooseScoutJob/ChooseCoachJob/ChooseGmJob.
    /// Returns the chosen team index, or -1 if no offers.
    /// </summary>
    public static int ChooseBestOffer(StaffMember staff, StaffRole role,
        int[] topCandidateByTeam, int staffIndex, int numTeams)
    {
        int topTeam = -1;
        double topInterest = -1.0;

        if (staff.Status == "retires" || staff.Status == "hired")
            return -1;

        for (int i = 0; i < numTeams; i++)
        {
            if (topCandidateByTeam[i] == staffIndex && staff.Interested[i] > topInterest)
            {
                topInterest = staff.Interested[i];
                topTeam = i;
            }
        }

        return topTeam;
    }

    // ── Orchestrators ──────────────────────────────────────────

    /// <summary>
    /// Updates staff performance scores for all teams at end of season.
    /// Port of Average.cpp:1686-1762 UpdateStaffStats().
    /// Bug fix: C++ applies GmStatus to scouts, ScoutStatus to coaches, CoachStatus to GMs;
    /// we apply the correct formula per role.
    /// </summary>
    public static void UpdateStaffPerformance(League league)
    {
        foreach (var team in league.Teams)
        {
            int teamIndex = team.Id;
            int rosterValue = CalculateRosterValue(team);
            int oldRosterValue = (int)team.Financial.TeamRosterValue;

            // Update scout performance
            if (team.Scout != null)
            {
                int careerWins = team.Scout.Wins;
                int careerLosses = team.Scout.Losses;
                team.Scout.Points = EvaluatePerformance(StaffRole.Scout, team.Scout.Points,
                    oldRosterValue, rosterValue, careerWins, careerLosses, team.Record);
                team.Scout.Wins += team.Record.Wins;
                team.Scout.Losses += team.Record.Losses;
                team.Scout.Playoffs += team.Record.IsPlayoffTeam ? 1 : 0;
                team.Scout.Rings += team.Record.HasRing ? 1 : 0;
            }

            // Update coach performance
            if (team.Coach != null)
            {
                int careerWins = team.Coach.Wins;
                int careerLosses = team.Coach.Losses;
                team.Coach.Points = EvaluatePerformance(StaffRole.Coach, team.Coach.Points,
                    oldRosterValue, rosterValue, careerWins, careerLosses, team.Record);
                team.Coach.Wins += team.Record.Wins;
                team.Coach.Losses += team.Record.Losses;
                team.Coach.Playoffs += team.Record.IsPlayoffTeam ? 1 : 0;
                team.Coach.Rings += team.Record.HasRing ? 1 : 0;
            }

            // Update GM performance
            if (team.GeneralManager != null)
            {
                int careerWins = team.GeneralManager.Wins;
                int careerLosses = team.GeneralManager.Losses;
                team.GeneralManager.Points = EvaluatePerformance(StaffRole.GM, team.GeneralManager.Points,
                    oldRosterValue, rosterValue, careerWins, careerLosses, team.Record);
                team.GeneralManager.Wins += team.Record.Wins;
                team.GeneralManager.Losses += team.Record.Losses;
                team.GeneralManager.Playoffs += team.Record.IsPlayoffTeam ? 1 : 0;
                team.GeneralManager.Rings += team.Record.HasRing ? 1 : 0;
            }

            // Update roster value for next season comparison
            team.Financial.TeamRosterValue = rosterValue;
        }
    }

    /// <summary>
    /// Runs the full staff lifecycle: fire → retire → resign → set interest → hire → replace retired → develop.
    /// Port of StaffDlg.cpp Firings() + OnButtonEnd() + OrderList() orchestration.
    /// </summary>
    public static StaffManagementResult RunStaffLifecycle(League league, Random? random = null)
    {
        random ??= Random.Shared;
        var result = new StaffManagementResult();
        int numTeams = league.Teams.Count;
        var pool = league.StaffPool;

        // Phase 1: Set original assignments and active status for employed staff
        for (int i = 0; i < numTeams; i++)
        {
            var team = league.Teams[i];
            if (team.Scout != null)
            {
                team.Scout.Status = "active";
                team.Scout.CurrentScout = i;
                team.Scout.OriginalScout = i + 1;
            }
            if (team.Coach != null)
            {
                team.Coach.Status = "active";
                team.Coach.CurrentCoach = i;
                team.Coach.OriginalCoach = i + 1;
            }
            if (team.GeneralManager != null)
            {
                team.GeneralManager.Status = "active";
                team.GeneralManager.CurrentGM = i;
                team.GeneralManager.OriginalGM = i + 1;
            }
        }

        // Phase 2: Fire, retire, resign for each team
        bool[] needScout = new bool[numTeams];
        bool[] needCoach = new bool[numTeams];
        bool[] needGM = new bool[numTeams];

        for (int i = 0; i < numTeams; i++)
        {
            var team = league.Teams[i];
            int patience = team.Financial.OwnerPatience;

            // Scout
            if (team.Scout != null)
            {
                int firingLine = CalculateFiringLine(team.Scout);
                bool fired = ShouldFire(team.Scout, firingLine, patience);
                bool retired = ShouldRetire(StaffRole.Scout, team.Scout, random);
                bool resigned = ShouldResign(StaffRole.Scout, team.Scout,
                    team.Record.Wins, team.Record.Losses,
                    CalculateLeagueAvgWinPct(league), random);

                if (fired)
                {
                    team.Scout.CurrentScout = -1;
                    team.Scout.Fired = true;
                    team.Scout.Status = "fired";
                    needScout[i] = true;
                    result.Fired.Add($"{team.Scout.Name} fired as {team.Name} scout");
                }
                else if (retired)
                {
                    team.Scout.CurrentScout = -1;
                    team.Scout.Fired = true;
                    team.Scout.Status = "retires";
                    needScout[i] = true;
                    result.Retired.Add($"{team.Scout.Name} retires as {team.Name} scout");
                }
                else if (resigned)
                {
                    team.Scout.CurrentScout = -1;
                    team.Scout.Fired = true;
                    team.Scout.Status = "quits";
                    needScout[i] = true;
                    result.Resigned.Add($"{team.Scout.Name} resigns as {team.Name} scout");
                }
            }
            else
            {
                needScout[i] = true;
            }

            // Coach
            if (team.Coach != null)
            {
                int firingLine = CalculateFiringLine(team.Coach);
                bool fired = ShouldFire(team.Coach, firingLine, patience);
                bool retired = ShouldRetire(StaffRole.Coach, team.Coach, random);
                bool resigned = ShouldResign(StaffRole.Coach, team.Coach,
                    team.Record.Wins, team.Record.Losses,
                    CalculateLeagueAvgWinPct(league), random);

                if (fired)
                {
                    team.Coach.CurrentCoach = -1;
                    team.Coach.Fired = true;
                    team.Coach.Status = "fired";
                    needCoach[i] = true;
                    result.Fired.Add($"{team.Coach.Name} fired as {team.Name} coach");
                }
                else if (retired)
                {
                    team.Coach.CurrentCoach = -1;
                    team.Coach.Fired = true;
                    team.Coach.Status = "retires";
                    needCoach[i] = true;
                    result.Retired.Add($"{team.Coach.Name} retires as {team.Name} coach");
                }
                else if (resigned)
                {
                    team.Coach.CurrentCoach = -1;
                    team.Coach.Fired = true;
                    team.Coach.Status = "quits";
                    needCoach[i] = true;
                    result.Resigned.Add($"{team.Coach.Name} resigns as {team.Name} coach");
                }
            }
            else
            {
                needCoach[i] = true;
            }

            // GM
            if (team.GeneralManager != null)
            {
                int firingLine = CalculateFiringLine(team.GeneralManager);
                bool fired = ShouldFire(team.GeneralManager, firingLine, patience);
                bool retired = ShouldRetire(StaffRole.GM, team.GeneralManager, random);
                bool resigned = ShouldResign(StaffRole.GM, team.GeneralManager,
                    team.Record.Wins, team.Record.Losses,
                    CalculateLeagueAvgWinPct(league), random);

                if (fired)
                {
                    team.GeneralManager.CurrentGM = -1;
                    team.GeneralManager.Fired = true;
                    team.GeneralManager.Status = "fired";
                    needGM[i] = true;
                    result.Fired.Add($"{team.GeneralManager.Name} fired as {team.Name} GM");
                }
                else if (retired)
                {
                    team.GeneralManager.CurrentGM = -1;
                    team.GeneralManager.Fired = true;
                    team.GeneralManager.Status = "retires";
                    needGM[i] = true;
                    result.Retired.Add($"{team.GeneralManager.Name} retires as {team.Name} GM");
                }
                else if (resigned)
                {
                    team.GeneralManager.CurrentGM = -1;
                    team.GeneralManager.Fired = true;
                    team.GeneralManager.Status = "quits";
                    needGM[i] = true;
                    result.Resigned.Add($"{team.GeneralManager.Name} resigns as {team.Name} GM");
                }
            }
            else
            {
                needGM[i] = true;
            }
        }

        // Also retire unassigned pool members
        for (int i = numTeams; i < pool.Count; i++)
        {
            var staff = pool[i];
            if (ShouldRetire(StaffRole.Scout, staff, random))
            {
                staff.CurrentScout = -1;
                staff.Status = "retires";
            }
            if (ShouldRetire(StaffRole.Coach, staff, random))
            {
                staff.CurrentCoach = -1;
                staff.Status = "retires";
            }
            if (ShouldRetire(StaffRole.GM, staff, random))
            {
                staff.CurrentGM = -1;
                staff.Status = "retires";
            }
        }

        // Phase 3: Calculate interest and hire replacements
        // Calculate roster values for interest formula
        int[] rosterValues = new int[numTeams];
        for (int i = 0; i < numTeams; i++)
            rosterValues[i] = CalculateRosterValue(league.Teams[i]);

        // Set interest values
        for (int i = 0; i < pool.Count; i++)
        {
            for (int t = 0; t < numTeams; t++)
            {
                var team = league.Teams[t];
                int w = team.Record.Wins;
                int l = team.Record.Losses;
                if (l == 0) l = 1;
                double winRatio = (double)w / l;
                int patience = team.Financial.OwnerPatience;

                pool[i].Interested[t] = CalculateInterest(StaffRole.Scout, winRatio, patience, rosterValues[t]);
            }
        }

        // Hiring loop (matching C++ OnButtonEnd pattern)
        for (int pass = 0; pass < pool.Count; pass++)
        {
            // Find top candidates for each team that needs hiring
            int[] topScout = new int[numTeams];
            int[] topCoach = new int[numTeams];
            int[] topGM = new int[numTeams];

            for (int t = 0; t < numTeams; t++)
            {
                topScout[t] = needScout[t] ? FindTopCandidate(pool, StaffRole.Scout, t) : -1;
                topCoach[t] = needCoach[t] ? FindTopCandidate(pool, StaffRole.Coach, t) : -1;
                topGM[t] = needGM[t] ? FindTopCandidate(pool, StaffRole.GM, t) : -1;
            }

            bool anyHired = false;

            // Each staff member chooses their best offer
            for (int s = 0; s < pool.Count; s++)
            {
                var staff = pool[s];

                // Scout hiring
                int chosenTeam = ChooseBestOffer(staff, StaffRole.Scout, topScout, s, numTeams);
                if (chosenTeam >= 0 && needScout[chosenTeam])
                {
                    staff.CurrentScout = chosenTeam;
                    staff.Status = "hired";
                    staff.Fired = false;
                    staff.Team = league.Teams[chosenTeam].Name;
                    league.Teams[chosenTeam].Scout = staff;
                    needScout[chosenTeam] = false;
                    anyHired = true;
                    result.Hired.Add($"{staff.Name} hired as {league.Teams[chosenTeam].Name} scout");
                }

                // Coach hiring
                chosenTeam = ChooseBestOffer(staff, StaffRole.Coach, topCoach, s, numTeams);
                if (chosenTeam >= 0 && needCoach[chosenTeam])
                {
                    staff.CurrentCoach = chosenTeam;
                    staff.Status = "hired";
                    staff.Fired = false;
                    staff.Team = league.Teams[chosenTeam].Name;
                    league.Teams[chosenTeam].Coach = staff;
                    needCoach[chosenTeam] = false;
                    anyHired = true;
                    result.Hired.Add($"{staff.Name} hired as {league.Teams[chosenTeam].Name} coach");
                }

                // GM hiring
                chosenTeam = ChooseBestOffer(staff, StaffRole.GM, topGM, s, numTeams);
                if (chosenTeam >= 0 && needGM[chosenTeam])
                {
                    staff.CurrentGM = chosenTeam;
                    staff.Status = "hired";
                    staff.Fired = false;
                    staff.Team = league.Teams[chosenTeam].Name;
                    league.Teams[chosenTeam].GeneralManager = staff;
                    needGM[chosenTeam] = false;
                    anyHired = true;
                    result.Hired.Add($"{staff.Name} hired as {league.Teams[chosenTeam].Name} GM");
                }
            }

            if (!anyHired) break; // No more matches possible
        }

        // Phase 4: Replace retired staff with new members and set newly-hired points floor
        int newGenerated = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].Status == "retires")
            {
                InitializeNewStaffMember(pool[i], random);
                newGenerated++;
            }
            if (pool[i].Status == "hired" && pool[i].Points < 16)
                pool[i].Points = 16;
        }
        result.NewStaffGenerated = newGenerated;

        // Phase 5: Develop all staff members
        for (int i = 0; i < pool.Count; i++)
        {
            int prime = CalculateStaffPrime(StaffRole.Scout, pool[i].Power);
            DevelopStaffMember(pool[i], prime, random);
            result.StaffDeveloped++;
        }

        return result;
    }

    /// <summary>
    /// Calculates league average win percentage (always 0.5 for a balanced league,
    /// but computed from actual records for robustness).
    /// </summary>
    private static double CalculateLeagueAvgWinPct(League league)
    {
        int totalWins = 0;
        int totalGames = 0;

        foreach (var team in league.Teams)
        {
            totalWins += team.Record.Wins;
            totalGames += team.Record.Wins + team.Record.Losses;
        }

        return totalGames > 0 ? (double)totalWins / totalGames : 0.5;
    }
}
