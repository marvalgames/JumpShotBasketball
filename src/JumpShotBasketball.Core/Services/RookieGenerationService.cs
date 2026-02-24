using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Generates rookies from benchmark league data for the draft pool.
/// Port of CDisk::CreateRookies() — Disk.cpp:2357-2686,
/// CDisk::FindDataForRookie() — Disk.cpp:2809-2829,
/// CDisk::GetRookieTrue() — Disk.cpp:2831-2851.
/// </summary>
public static class RookieGenerationService
{
    private static readonly string[] Positions = { "PG", "SG", "SF", "PF", "C" };

    // Base heights by position index (PG=0..C=4), in inches
    private static readonly int[] BaseHeights = { 73, 77, 78, 79, 81 };

    // Base weights by position index, in lbs
    private static readonly int[] BaseWeights = { 180, 190, 195, 225, 235 };

    // Adjacent positions for fallback matching (index → list of adjacent indices)
    private static readonly int[][] AdjacentPositions =
    {
        new[] { 1 },       // PG → SG
        new[] { 0, 2 },    // SG → PG, SF
        new[] { 1, 3 },    // SF → SG, PF
        new[] { 2, 4 },    // PF → SF, C
        new[] { 3 }        // C  → PF
    };

    /// <summary>
    /// Calculates the "true rating" of a player for benchmark ranking.
    /// Port of CDisk::GetRookieTrue() — Disk.cpp:2831-2851.
    /// Bug fix: C++ line 2834 does fgm = fgm + tgm, then line 2836 uses fgm + tgm
    /// which double-counts three-pointers. We use the simplified formula after cancellation:
    /// the per-48 normalization (tru/min*48) is immediately undone by the caller (tru*mpg/48),
    /// so the effective formula operates on per-game stats directly.
    /// </summary>
    public static double CalculateRookieTrueRating(PlayerStatLine stats)
    {
        if (stats.Games == 0 || stats.Minutes == 0) return 0;

        double fgm = stats.FieldGoalsMade;
        double fga = stats.FieldGoalsAttempted;
        double tgm = stats.ThreePointersMade;
        double tga = stats.ThreePointersAttempted;
        double ftm = stats.FreeThrowsMade;
        double fta = stats.FreeThrowsAttempted;
        double orb = stats.OffensiveRebounds;
        double drb = stats.Rebounds - stats.OffensiveRebounds;
        double ast = stats.Assists;
        double stl = stats.Steals;
        double to = stats.Turnovers;
        double blk = stats.Blocks;
        double min = stats.Minutes;

        // C++ GetRookieTrue formula (with double-counting fix):
        // gun uses original fgm (which includes tgm in C++ FGM), so total FGM = fgm2 + tgm
        // fgm2 + tgm is what C++ already has in fgm, plus it adds tgm again → bug
        // Correct: gun = (fgm - tgm)*2 + tgm*3 + ftm - (fga-fgm)*2/3 + (fta-ftm)*1/6
        // then * 3/2
        double totalFgm = fgm; // includes 3pm in C++ convention
        double totalFga = fga;

        double gun = totalFgm + tgm - (totalFga - totalFgm) * 2.0 / 3.0;
        gun = gun + ftm - fta / 2.0;
        gun = gun + (fta - ftm) * 1.0 / 6.0;
        gun = gun * 3.0 / 2.0;

        double skill = orb * 2.0 / 3.0 + drb * 1.0 / 3.0;
        skill = skill + stl - to + blk;
        skill = skill + ast * 4.0 / 5.0;
        skill = skill * 3.0 / 4.0;

        double tru = gun + skill;
        tru = tru / min * 48;
        return tru;
    }

    /// <summary>
    /// Generates a rookie age based on draft position.
    /// Port of Disk.cpp:2400-2402.
    /// C++ logic: age = 19 + IntRandom(4) → 19-22
    /// if 23: age = 18 + IntRandom(6) → 18-23
    /// if 23 again: age = 17 + IntRandom(8) → 17-24
    /// </summary>
    public static int GenerateRookieAge(int pickNumber, Random random)
    {
        int age = 19 + random.Next(1, 5); // 20-22 typical, 19+1..4
        if (age == 23) age = 18 + random.Next(1, 7); // wider range
        if (age == 23) age = 17 + random.Next(1, 9); // even wider
        int tmpAge = age;
        if (tmpAge > 23) tmpAge = 23;
        return age;
    }

    /// <summary>
    /// Calculates the age-based scaling factor for rookie stats.
    /// Port of Disk.cpp:2407-2415.
    /// ageFactor = 0.45 + tmpAge/100*2, then temp = (1-ageFactor)*2,
    /// ageFactor = 1 - temp + Random(temp*blow)
    /// </summary>
    public static double CalculateAgeFactor(int age, Random random)
    {
        int tmpAge = Math.Min(age, 23);
        double ageFactor = 0.45 + (double)tmpAge / 100.0 * 2.0;

        double blow = 0.75;
        int nBlow = random.Next(1, 10); // IntRandom(9) → 1-9
        if (nBlow == 1) blow = 1.0;

        double temp = (1.0 - ageFactor) * 2.0;
        ageFactor = 1.0 - temp + random.NextDouble() * (temp * blow);

        return ageFactor;
    }

    /// <summary>
    /// Builds the eligible benchmark pool from league players.
    /// Port of Disk.cpp:2372-2380.
    /// Filters: non-empty name, games >= hiG/4, minutes > hiG*8.
    /// Returns players sorted by true rating descending.
    /// </summary>
    public static List<(Player player, double trueRating)> BuildEligiblePool(League league)
    {
        int hiG = 0;
        foreach (var team in league.Teams)
            foreach (var player in team.Roster)
                if (player.SimulatedStats.Games > hiG)
                    hiG = player.SimulatedStats.Games;

        if (hiG == 0) return new();

        int minGames = hiG / 4;
        int minMinutes = hiG * 8;

        var pool = new List<(Player player, double trueRating)>();
        foreach (var team in league.Teams)
        {
            foreach (var player in team.Roster)
            {
                if (string.IsNullOrEmpty(player.Name)) continue;
                if (player.SimulatedStats.Games < minGames) continue;
                if (player.SimulatedStats.Minutes <= minMinutes) continue;

                double tru = CalculateRookieTrueRating(player.SimulatedStats);
                pool.Add((player, tru));
            }
        }

        pool.Sort((a, b) => b.trueRating.CompareTo(a.trueRating));
        return pool;
    }

    /// <summary>
    /// Finds a benchmark player from the eligible pool matching the target position.
    /// Port of CDisk::FindDataForRookie() — Disk.cpp:2809-2829.
    /// Improved: prefers same position, then adjacent positions, then any.
    /// C++ loops randomly until a match; we collect candidates and pick randomly for efficiency.
    /// </summary>
    public static int FindBenchmarkIndex(
        List<(Player player, double trueRating)> pool, int posIndex, int stat, Random random)
    {
        if (pool.Count == 0) return 0;

        string targetPos = Positions[posIndex];

        // Collect same-position candidates
        var candidates = new List<int>();
        for (int i = 0; i < pool.Count; i++)
        {
            string pos = pool[i].player.Position.Trim();
            if (pos == targetPos) candidates.Add(i);
        }

        // C++ special case: stat==2 (shooting stats) allows PF/C cross-position
        if (candidates.Count == 0 || (stat == 2 && (posIndex == 3 || posIndex == 4)))
        {
            if (candidates.Count == 0)
            {
                // Try adjacent positions
                foreach (int adj in AdjacentPositions[posIndex])
                {
                    string adjPos = Positions[adj];
                    for (int i = 0; i < pool.Count; i++)
                    {
                        string pos = pool[i].player.Position.Trim();
                        if (pos == adjPos) candidates.Add(i);
                    }
                }
            }
        }

        // Final fallback: any position
        if (candidates.Count == 0)
        {
            for (int i = 0; i < pool.Count; i++)
                candidates.Add(i);
        }

        return candidates[random.Next(candidates.Count)];
    }

    /// <summary>
    /// Generates ODPT raw ratings from benchmark player stats.
    /// Port of CPlayer::GenerateODPT() — Player.h:1557-1601.
    /// Computes 8 ODPT values from stats, adds random variance ±3, clamps 1-9.
    /// </summary>
    public static void GenerateOdptRatings(Player rookie, PlayerStatLine benchmarkStats, Random random)
    {
        // Calculate base ODPT ratings from stats
        double oo = StatisticsCalculator.CalculateOutsideRating(benchmarkStats);
        double od = StatisticsCalculator.CalculateOutsideDefenseRating(benchmarkStats);
        double po = StatisticsCalculator.CalculateDrivingRating(benchmarkStats);
        double pd = StatisticsCalculator.CalculateDrivingDefenseRating(benchmarkStats);
        double io = StatisticsCalculator.CalculatePostRating(benchmarkStats);
        double id = StatisticsCalculator.CalculatePostDefenseRating(benchmarkStats);
        double fo = StatisticsCalculator.CalculateTransitionRating(benchmarkStats);
        double fd = StatisticsCalculator.CalculateTransitionDefenseRating(benchmarkStats);

        // Round and add random variance ±3 (C++: Round(x) - 3 + IntRandom(3))
        // IntRandom(3) returns 1-3, so net is -2 to 0
        // Actually: -3 + IntRandom(3) → -3+1=-2 to -3+3=0... but the plan says ±1
        // Looking at C++ more carefully: Round(x) - 3 + IntRandom(3)
        // IntRandom(3) → 1,2,3 so offset is -2, -1, 0 → net bias is slight reduction
        rookie.Ratings.MovementOffenseRaw = Clamp1to9((int)Math.Round(oo) - 3 + random.Next(1, 4));
        rookie.Ratings.MovementDefenseRaw = Clamp1to9((int)Math.Round(od) - 3 + random.Next(1, 4));
        rookie.Ratings.PenetrationOffenseRaw = Clamp1to9((int)Math.Round(po) - 3 + random.Next(1, 4));
        rookie.Ratings.PenetrationDefenseRaw = Clamp1to9((int)Math.Round(pd) - 3 + random.Next(1, 4));
        rookie.Ratings.PostOffenseRaw = Clamp1to9((int)Math.Round(io) - 3 + random.Next(1, 4));
        rookie.Ratings.PostDefenseRaw = Clamp1to9((int)Math.Round(id) - 3 + random.Next(1, 4));
        rookie.Ratings.TransitionOffenseRaw = Clamp1to9((int)Math.Round(fo) - 3 + random.Next(1, 4));
        rookie.Ratings.TransitionDefenseRaw = Clamp1to9((int)Math.Round(fd) - 3 + random.Next(1, 4));
    }

    /// <summary>
    /// Generates height and weight based on position and ODPT ratings.
    /// Port of CPlayer::GenerateRandomHeightWeight() — Player.cpp:1603-1681.
    /// </summary>
    public static void GenerateRandomHeightWeight(Player rookie, Random random)
    {
        int posIdx = GetPositionIndex(rookie.Position);
        int initH = BaseHeights[posIdx];
        int initW = BaseWeights[posIdx];

        var r = rookie.Ratings;
        int rSum = r.MovementOffenseRaw + r.MovementDefenseRaw
                 + r.PenetrationOffenseRaw + r.PenetrationDefenseRaw
                 + r.PostOffenseRaw + r.PostDefenseRaw
                 + r.TransitionOffenseRaw + r.TransitionDefenseRaw;
        if (rSum == 0) rSum = 1; // avoid division by zero

        double fa1 = 20;
        double fa2 = 40;
        string pos = rookie.Position.Trim();
        if (pos == "C" || pos == "PF") { fa1 = 12; fa2 = 30; }
        else if (pos == "SF") { fa1 = 18; fa2 = 36; }

        double fh = (double)(r.PostOffenseRaw + r.PostDefenseRaw) / rSum * fa1;
        double fw1 = (double)(r.PostOffenseRaw + r.PostDefenseRaw) / rSum * fa2;
        double fw2 = fw1 + (double)((10 - r.TransitionOffenseRaw) + (10 - r.TransitionDefenseRaw)) / rSum * fa2;

        double height = initH + random.NextDouble() * fh;
        double weight = initW + random.NextDouble() * (fw1 + fw2);

        int h = (int)height;
        int w = (int)weight;

        // Post-rating based adjustment loops
        double r1 = (double)(r.PostOffenseRaw + r.PostDefenseRaw) / 100.0;
        if (r1 > 0.18) r1 = 0.18;

        // Decrease height loop
        double n;
        do
        {
            n = random.NextDouble();
            if (n > (0.78 + r1)) h--;
        } while (n > (0.78 + r1));

        // Increase height loop
        do
        {
            n = random.NextDouble();
            if (n < r1) h++;
        } while (n < r1);

        // Decrease weight loop
        do
        {
            n = random.NextDouble();
            if (n > (0.78 + r1)) w -= 5;
        } while (n > (0.78 + r1));

        // Increase weight loop
        do
        {
            n = random.NextDouble();
            if (n < r1) w += 5;
        } while (n < r1);

        // Age-based weight adjustment
        if (rookie.Age < 28)
            w -= random.Next(1, 28 - rookie.Age + 1);
        else
            w += random.Next(1, rookie.Age - 28 + 1);

        rookie.Height = h;
        rookie.Weight = w;
    }

    /// <summary>
    /// Main orchestrator: generates rookies from league benchmark data.
    /// Port of CDisk::CreateRookies() — Disk.cpp:2357-2607.
    /// </summary>
    public static RookiePool CreateRookies(League league, int count = 96, Random? random = null)
    {
        random ??= Random.Shared;

        var pool = BuildEligiblePool(league);
        if (pool.Count == 0)
        {
            return new RookiePool { Year = league.Settings.CurrentYear };
        }

        int hiG = 0;
        foreach (var team in league.Teams)
            foreach (var player in team.Roster)
                if (player.SimulatedStats.Games > hiG)
                    hiG = player.SimulatedStats.Games;

        var rookiePool = new RookiePool { Year = league.Settings.CurrentYear };

        for (int i = 1; i <= count; i++)
        {
            int posIndex = random.Next(0, 5);
            string posStr = Positions[posIndex];

            // Find benchmark players for each stat category (20 lookups per C++)
            int[] benchIdx = new int[21];
            for (int j = 1; j <= 20; j++)
            {
                benchIdx[j] = FindBenchmarkIndex(pool, posIndex, j, random);
            }

            int age = GenerateRookieAge(i, random);
            double ageFactor = CalculateAgeFactor(age, random);

            // Extract per-game stats from various benchmark players
            var bGames = pool[benchIdx[1]].player.SimulatedStats;
            int ga = bGames.Games;
            double gam = (double)ga / hiG * 82.0;
            int games = (int)gam;
            if (games <= 0) games = 1;
            double mpg = (double)bGames.Minutes / games;
            if (mpg > 40) mpg = 38 + random.NextDouble() * 2;

            // Shooting stats from benchmark[2] (2pt) and benchmark[3] (3pt)
            var b2 = pool[benchIdx[2]].player.SimulatedStats;
            var b3 = pool[benchIdx[3]].player.SimulatedStats;
            var b4 = pool[benchIdx[4]].player.SimulatedStats;

            double fgm = b2.FieldGoalsMade - b2.ThreePointersMade;
            double fga = b2.FieldGoalsAttempted - b2.ThreePointersAttempted;
            double tgm = (double)b3.ThreePointersMade;
            double tga = (double)b3.ThreePointersAttempted;
            double ftm = (double)b4.FreeThrowsMade;
            double fta = (double)b4.FreeThrowsAttempted;

            // Adjust 3pt volume if too high relative to total FGA
            double hi = fga - fta / 3.0 / 2.0;
            if (tga > hi && hi > 0)
            {
                double f1 = fga - fta / 3.0;
                if (f1 > 0)
                {
                    double f2 = f1 / fga;
                    double f3 = 1 - f2;
                    double f4 = random.NextDouble() * f3;
                    double f5 = f2 + f4;
                    if (f5 < 1)
                    {
                        tgm *= f5;
                        tga *= f5;
                    }
                }
            }

            // Rebounding, assists, steals, turnovers, blocks, fouls from benchmarks
            var b5 = pool[benchIdx[5]].player.SimulatedStats;
            var b6 = pool[benchIdx[6]].player.SimulatedStats;
            var b7 = pool[benchIdx[7]].player.SimulatedStats;
            var b8 = pool[benchIdx[8]].player.SimulatedStats;
            var b9 = pool[benchIdx[9]].player.SimulatedStats;
            var b10 = pool[benchIdx[10]].player.SimulatedStats;

            double orb = (double)b5.OffensiveRebounds;
            double drb = (double)(b5.Rebounds - b5.OffensiveRebounds);
            double ast = (double)b6.Assists;
            double stl = (double)b7.Steals;
            double to = (double)b8.Turnovers;
            double blk = (double)b9.Blocks;
            double pf = (double)b10.PersonalFouls;

            // Apply age factor scaling
            fgm *= ageFactor;
            fga *= ageFactor;
            tgm *= ageFactor;
            tga *= ageFactor;
            ftm *= ageFactor;
            fta *= ageFactor;
            orb *= ageFactor;
            drb *= ageFactor;
            ast *= ageFactor;
            stl *= (1 - (1 - ageFactor) / 2);
            to *= (2 - ageFactor);
            blk *= (1 - (1 - ageFactor) / 2);
            // pf unchanged (pf*1.00 in C++)

            // Normalize to per-minute then scale to rookie's mpg
            if (b2.Minutes > 0) { fgm = fgm / b2.Minutes * mpg; fga = fga / b2.Minutes * mpg; }
            if (b3.Minutes > 0) { tgm = tgm / b3.Minutes * mpg; tga = tga / b3.Minutes * mpg; }
            if (b4.Minutes > 0) { ftm = ftm / b4.Minutes * mpg; fta = fta / b4.Minutes * mpg; }
            double ftp = fta > 0 ? ftm / fta : 0;
            if (fta > fga) { fta = fga; ftm = fta * ftp; }

            if (b5.Minutes > 0) { orb = orb / b5.Minutes * mpg; drb = drb / b5.Minutes * mpg; }
            if (b6.Minutes > 0) { ast = ast / b6.Minutes * mpg; }
            if (b7.Minutes > 0) { stl = stl / b7.Minutes * mpg; }
            if (b8.Minutes > 0) { to = to / b8.Minutes * mpg; }
            if (b9.Minutes > 0) { blk = blk / b9.Minutes * mpg; }
            if (b10.Minutes > 0) { pf = pf / b10.Minutes * mpg; }

            // Calculate true rating to determine minutes
            double tru = CalculateRookieTrueRatingFromPerGame(
                games, mpg, fgm, fga, tgm, tga, ftm, fta, orb, drb, ast, stl, to, blk, pf);
            tru *= (mpg / 48.0);

            double mpg2 = tru * 2.5 + 4 + random.Next(1, 8);
            if (mpg2 <= 2) mpg2 = 2;
            double factor = mpg2 / mpg;
            double fgp = fga > 0 ? fgm / fga : 0;
            double tgp = tga > 0 ? tgm / tga : 0;

            // Adjust percentages with age factor
            fgp = fgp * (1 - (1 - ageFactor) / 2) * 1.03;
            ftp = ftp * (1 - (1 - ageFactor) / 2) * 1.05;
            tgp = tgp * (1 - (1 - ageFactor) / 2) * 1.05;

            // Scale attempts by factor
            fga *= factor;
            tga *= factor;
            fta *= factor;
            orb *= factor;
            drb *= factor;
            ast *= factor;
            stl *= factor;
            to *= factor;
            blk *= factor;
            pf *= factor;

            // Apply percentage caps
            if (fgp > 5.0 / 9.0) fgm = fga * ((500 + random.Next(1, 76)) / 1000.0);
            else fgm = fga * fgp;
            if (ftp > 8.0 / 9.0) ftm = fta * ((825 + random.Next(1, 101)) / 1000.0);
            else ftm = fta * ftp;
            if (tgp > 4.0 / 9.0) tgm = tga * ((325 + random.Next(1, 101)) / 1000.0);
            else tgm = tga * tgp;

            // Cap fouls and turnovers
            if (pf > 4) pf = 4 + random.NextDouble() * 0.5;
            if (to > 0 && ast > 0)
            {
                double at = ast / to;
                if (at > 3) at = 3 + random.NextDouble();
                to = ast / at;
            }

            // Ensure minimum FT ratio
            double ftRatio = (fga + orb) > 0 ? fta / (fga + orb) : 0;
            if (ftRatio < 1.0 / 6.0)
            {
                ftRatio = 1.0 / 6.0 + random.NextDouble() * (1.0 / 30.0);
                double ftp2 = fta > 0 ? ftm / fta : 0;
                fta = ftRatio * (fga + orb);
                ftm = fta * ftp2;
            }
            if (to < 0) to = 1;

            // Ensure minimum steals
            double minStl = mpg2 * games * 0.01;
            double totalStl = games * stl;
            if (totalStl < minStl) totalStl = minStl;

            // Ensure minimum FG percentage
            int totalFgm = (int)(games * (fgm + tgm));
            int totalFga2 = (int)(games * (fga + tga));
            if (totalFga2 >= 3)
            {
                double finalFgp = totalFga2 > 0 ? (double)totalFgm / totalFga2 : 0;
                if (finalFgp <= 0.25) totalFgm = totalFga2 / 4;
            }

            // Build rookie player
            var rookie = new Player
            {
                Id = i,
                Name = $"Rookie #{i}",
                LastName = $"#{i}",
                Position = posStr,
                Age = age,
                Active = true,
                Contract = new PlayerContract
                {
                    IsRookie = true,
                    YearsOfService = 0,
                    YearsOnTeam = 0
                },
                SimulatedStats = new PlayerStatLine
                {
                    Games = games,
                    Minutes = (int)(mpg2 * games),
                    FieldGoalsMade = totalFgm,
                    FieldGoalsAttempted = totalFga2,
                    ThreePointersMade = (int)(games * tgm),
                    ThreePointersAttempted = (int)(games * tga),
                    FreeThrowsMade = (int)(games * ftm),
                    FreeThrowsAttempted = (int)(games * fta),
                    OffensiveRebounds = (int)(games * orb),
                    Rebounds = (int)(games * (orb + drb)),
                    Assists = (int)(games * ast),
                    Steals = (int)totalStl,
                    Turnovers = (int)(games * to),
                    Blocks = blk == 0 && random.NextDouble() < 0.5 ? 1 : (int)(games * blk),
                    PersonalFouls = (int)(games * pf)
                }
            };

            // Generate ODPT ratings from the rookie's generated stats
            GenerateOdptRatings(rookie, rookie.SimulatedStats, random);

            // Clutch and consistency (C++ lines 2568-2605)
            int cl = 2;
            int co = 2;
            if (random.Next(1, 9) == 1) cl = random.Next(1, 4);
            if (random.Next(1, 9) == 1) co = random.Next(1, 4);
            // Second pass (C++ lines 2598-2604)
            int r2 = random.Next(1, 7);
            if (r2 == 1) cl = 1;
            else if (r2 == 2) cl = 3;
            r2 = random.Next(1, 7);
            if (r2 == 1) co = 1;
            else if (r2 == 2) co = 3;
            rookie.Ratings.Clutch = cl;
            rookie.Ratings.Consistency = co;

            // Better (improvement trajectory)
            int better = 30 + random.Next(1, 41);
            if (better <= 35 || better >= 66) better = random.Next(1, 100);
            rookie.Better = better;

            // Clear position rotation
            rookie.PgRotation = false;
            rookie.SgRotation = false;
            rookie.SfRotation = false;
            rookie.PfRotation = false;
            rookie.CRotation = false;

            // Preference factors
            rookie.Contract.CoachFactor = random.Next(1, 6);
            rookie.Contract.LoyaltyFactor = random.Next(1, 6);
            rookie.Contract.PlayingTimeFactor = random.Next(1, 6);
            rookie.Contract.WinningFactor = random.Next(1, 6);
            rookie.Contract.TraditionFactor = random.Next(1, 6);
            rookie.Contract.SecurityFactor = random.Next(1, 6);
            rookie.Content = 2 + random.Next(1, 6);

            // Generate height/weight
            GenerateRandomHeightWeight(rookie, random);

            // Potential and effort (C++ lines 2635-2646)
            int pot1 = random.Next(1, 6);
            int pot2 = random.Next(1, 6);
            int effort = random.Next(1, 6);
            if (age < 20 && random.NextDouble() < 1.0 / 3.0 && pot1 < 5) pot1++;
            if (age < 20 && random.NextDouble() < 1.0 / 3.0 && pot2 < 5) pot2++;
            if (age < 20 && random.NextDouble() < 1.0 / 3.0 && effort > 1) effort--;
            if (age > 21 && random.NextDouble() < 1.0 / 3.0 && pot1 > 1) pot1--;
            if (age > 21 && random.NextDouble() < 1.0 / 3.0 && pot2 > 1) pot2--;
            if (age > 21 && random.NextDouble() < 1.0 / 3.0 && effort < 5) effort++;
            rookie.Ratings.Potential1 = pot1;
            rookie.Ratings.Potential2 = pot2;
            rookie.Ratings.Effort = effort;

            // Generate random prime
            PlayerDevelopmentService.GenerateRandomPrime(rookie, random);

            rookiePool.Rookies.Add(rookie);
        }

        return rookiePool;
    }

    // ── Internal helpers ────────────────────────────────────────────

    /// <summary>
    /// Computes the true rating from per-game stats for minutes determination.
    /// This is the same GetRookieTrue formula used on already-normalized per-game values.
    /// </summary>
    private static double CalculateRookieTrueRatingFromPerGame(
        int games, double mpg, double fgm, double fga,
        double tgm, double tga, double ftm, double fta,
        double orb, double drb, double ast, double stl,
        double to, double blk, double pf)
    {
        if (games == 0 || mpg == 0) return 0;

        double totalFgm = fgm + tgm;
        double totalFga = fga + tga;

        double gun = totalFgm + tgm - (totalFga - totalFgm) * 2.0 / 3.0;
        gun = gun + ftm - fta / 2.0;
        gun = gun + (fta - ftm) * 1.0 / 6.0;
        gun = gun * 3.0 / 2.0;

        double skill = orb * 2.0 / 3.0 + drb * 1.0 / 3.0;
        skill = skill + stl - to + blk;
        skill = skill + ast * 4.0 / 5.0;
        skill = skill * 3.0 / 4.0;

        double tru = gun + skill;
        tru = tru / mpg * 48;
        return tru;
    }

    private static int GetPositionIndex(string position)
    {
        return position.Trim() switch
        {
            "PG" => 0,
            "SG" => 1,
            "SF" => 2,
            "PF" => 3,
            "C" => 4,
            _ => 0
        };
    }

    private static int Clamp1to9(int value)
    {
        if (value < 1) return 1;
        if (value > 9) return 9;
        return value;
    }
}
