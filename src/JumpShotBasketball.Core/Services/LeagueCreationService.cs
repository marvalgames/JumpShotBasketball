using JumpShotBasketball.Core.Models.League;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Staff;
using JumpShotBasketball.Core.Models.Team;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Factory service that generates a complete league from scratch.
/// Creates teams, players, ratings, contracts, financials, staff, draft board, schedule, and rotations.
/// </summary>
public static class LeagueCreationService
{
    // ── Static Team Data (30 NBA-style teams) ──────────────────────────────

    private static readonly (string City, string Name, int Conference, int Division)[] TeamData =
    {
        // Eastern Conference — Division 0 (Atlantic)
        ("Boston", "Celtics", 0, 0),
        ("Brooklyn", "Nets", 0, 0),
        ("New York", "Knicks", 0, 0),
        ("Philadelphia", "76ers", 0, 0),
        ("Toronto", "Raptors", 0, 0),
        // Eastern Conference — Division 1 (Central)
        ("Chicago", "Bulls", 0, 1),
        ("Cleveland", "Cavaliers", 0, 1),
        ("Detroit", "Pistons", 0, 1),
        ("Indiana", "Pacers", 0, 1),
        ("Milwaukee", "Bucks", 0, 1),
        // Eastern Conference — Division 2 (Southeast)
        ("Atlanta", "Hawks", 0, 2),
        ("Charlotte", "Hornets", 0, 2),
        ("Miami", "Heat", 0, 2),
        ("Orlando", "Magic", 0, 2),
        ("Washington", "Wizards", 0, 2),
        // Western Conference — Division 3 (Southwest)
        ("Dallas", "Mavericks", 1, 3),
        ("Houston", "Rockets", 1, 3),
        ("Memphis", "Grizzlies", 1, 3),
        ("New Orleans", "Pelicans", 1, 3),
        ("San Antonio", "Spurs", 1, 3),
        // Western Conference — Division 4 (Northwest)
        ("Denver", "Nuggets", 1, 4),
        ("Minnesota", "Timberwolves", 1, 4),
        ("Oklahoma City", "Thunder", 1, 4),
        ("Portland", "Trail Blazers", 1, 4),
        ("Utah", "Jazz", 1, 4),
        // Western Conference — Division 5 (Pacific)
        ("Golden State", "Warriors", 1, 5),
        ("Los Angeles", "Lakers", 1, 5),
        ("Los Angeles", "Clippers", 1, 5),
        ("Phoenix", "Suns", 1, 5),
        ("Sacramento", "Kings", 1, 5),
    };

    // ── Name Arrays ────────────────────────────────────────────────────────

    private static readonly string[] FirstNames =
    {
        "James", "Michael", "Robert", "David", "William", "Richard", "Joseph", "Thomas", "Charles", "Christopher",
        "Daniel", "Matthew", "Anthony", "Mark", "Donald", "Steven", "Paul", "Andrew", "Joshua", "Kenneth",
        "Kevin", "Brian", "George", "Timothy", "Ronald", "Edward", "Jason", "Jeffrey", "Ryan", "Jacob",
        "Gary", "Nicholas", "Eric", "Stephen", "Jonathan", "Larry", "Justin", "Scott", "Brandon", "Benjamin",
        "Samuel", "Raymond", "Gregory", "Frank", "Alexander", "Patrick", "Jack", "Dennis", "Jerry", "Tyler",
        "Aaron", "Nathan", "Henry", "Peter", "Adam", "Zachary", "Douglas", "Harold", "Kyle", "Carl",
        "Arthur", "Gerald", "Roger", "Keith", "Lawrence", "Terry", "Albert", "Jesse", "Austin", "Bruce",
        "Christian", "Ralph", "Roy", "Eugene", "Randy", "Wayne", "Philip", "Russell", "Bobby", "Vincent",
        "Louis", "Martin", "Ernest", "Craig", "Stanley", "Harry", "Leonard", "Travis", "Rodney", "Curtis",
        "Norman", "Allen", "Marvin", "Glenn", "Marcus", "Derrick", "Andre", "Omar", "Jamal", "Darius",
        "Malik", "DeAndre", "Terrence", "Lamar", "Cedric", "Tyrone", "Maurice", "Jerome", "Reggie", "Devin",
        "Trevon", "Jaylen", "Donovan", "Jalen", "Damian", "Kyrie", "Kemba", "Kawhi", "Luka", "Giannis",
        "Nikola", "Pascal", "Bam", "Shai", "Zion", "Ja", "Trae", "Dejounte", "Cade", "Evan",
        "Scottie", "Franz", "Paolo", "Jett", "Chet", "Jabari", "Keegan", "Bennedict", "Walker", "Dyson",
        "Victor", "Brandon", "Scoot", "Amen", "Ausar", "Gradey", "Jarace", "Cam", "Bilal", "Dereck"
    };

    private static readonly string[] LastNames =
    {
        "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez",
        "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson", "Thomas", "Taylor", "Moore", "Jackson", "Martin",
        "Lee", "Perez", "Thompson", "White", "Harris", "Sanchez", "Clark", "Ramirez", "Lewis", "Robinson",
        "Walker", "Young", "Allen", "King", "Wright", "Scott", "Torres", "Nguyen", "Hill", "Flores",
        "Green", "Adams", "Nelson", "Baker", "Hall", "Rivera", "Campbell", "Mitchell", "Carter", "Roberts",
        "Turner", "Phillips", "Parker", "Evans", "Edwards", "Collins", "Stewart", "Morris", "Murphy", "Cook",
        "Rogers", "Morgan", "Peterson", "Cooper", "Reed", "Bailey", "Bell", "Howard", "Ward", "Cox",
        "Richardson", "Wood", "Watson", "Brooks", "Bennett", "Gray", "James", "Reyes", "Cruz", "Hughes",
        "Price", "Myers", "Long", "Foster", "Sanders", "Ross", "Morales", "Powell", "Sullivan", "Russell",
        "Ortiz", "Jenkins", "Gutierrez", "Perry", "Butler", "Barnes", "Fisher", "Henderson", "Coleman", "Simmons",
        "Patterson", "Jordan", "Reynolds", "Hamilton", "Graham", "Kim", "Gonzales", "Alexander", "Ramos", "Wallace",
        "Griffin", "West", "Cole", "Hayes", "Gibson", "Ellis", "Stevens", "Murray", "Ford", "Marshall",
        "Owens", "Mcdonald", "Harrison", "Ruiz", "Kennedy", "Wells", "Alvarez", "Woods", "Mendoza", "Castillo",
        "Olsen", "Webb", "Washington", "Tucker", "Freeman", "Burns", "Henry", "Vasquez", "Snyder", "Simpson",
        "Crawford", "Jimenez", "Porter", "Mason", "Shaw", "Gordon", "Wagner", "Hunter", "Romero", "Hicks"
    };

    // ── Per-Position Stat Distribution (per 36 minutes) ────────────────────

    // Indices: 0=PG, 1=SG, 2=SF, 3=PF, 4=C
    private static readonly double[] AvgFga = { 14, 16, 14, 12, 10 };
    private static readonly double[] AvgFgPct = { .44, .45, .46, .48, .52 };
    private static readonly double[] AvgThreePA = { 6, 5, 4, 2, 0.5 };
    private static readonly double[] AvgThreePPct = { .36, .37, .35, .33, .28 };
    private static readonly double[] AvgFta = { 4, 5, 5, 6, 6 };
    private static readonly double[] AvgFtPct = { .82, .80, .78, .74, .68 };
    private static readonly double[] AvgReb = { 4, 5, 7, 9, 11 };
    private static readonly double[] AvgAst = { 8, 4, 3, 2, 1.5 };
    private static readonly double[] AvgStl = { 1.5, 1.2, 1.0, 0.8, 0.6 };
    private static readonly double[] AvgBlk = { 0.3, 0.3, 0.6, 1.2, 2.0 };
    private static readonly double[] AvgTo = { 3.0, 2.5, 2.0, 1.8, 1.8 };
    private static readonly double[] AvgPf = { 2.0, 2.2, 2.5, 3.0, 3.2 };

    private static readonly string[] Positions = { "PG", "SG", "SF", "PF", " C" };

    private static readonly string[] ConferenceNames = { "Eastern", "Western" };
    private static readonly string[] DivisionNames = { "Atlantic", "Central", "Southeast", "Southwest", "Northwest", "Pacific" };

    // ── Main Entry Point ───────────────────────────────────────────────────

    /// <summary>
    /// Creates a brand-new league with teams, players, ratings, contracts,
    /// financials, staff, draft board, schedule, and rotations — ready to play.
    /// </summary>
    public static League CreateNewLeague(LeagueCreationOptions? options = null, Random? random = null)
    {
        options ??= new LeagueCreationOptions();
        random ??= Random.Shared;

        var league = new League();

        // Configure league settings from options
        league.Settings.NumberOfTeams = options.NumberOfTeams;
        league.Settings.CurrentYear = options.StartingYear;
        league.Settings.LeagueName = options.LeagueName;
        league.Settings.FinancialEnabled = options.FinancialEnabled;
        league.Settings.FreeAgencyEnabled = options.FreeAgencyEnabled;
        league.Settings.ComputerTradesEnabled = options.ComputerTradesEnabled;
        league.Settings.InjuriesEnabled = true;
        league.Settings.CareerStatsEnabled = true;
        league.Settings.ThreePointLineEnabled = true;
        league.Settings.ScoutsEnabled = true;
        league.Settings.SalaryMatchingEnabled = true;
        league.Settings.ConferenceName1 = ConferenceNames[0];
        league.Settings.ConferenceName2 = ConferenceNames[1];
        league.Settings.DivisionName1 = DivisionNames[0];
        league.Settings.DivisionName2 = DivisionNames[1];
        league.Settings.DivisionName3 = DivisionNames[3]; // Southwest maps to Div3 slot
        league.Settings.DivisionName4 = DivisionNames[4]; // Northwest maps to Div4 slot
        league.Settings.PlayoffFormat = "16";
        league.Settings.Round1Format = "7";
        league.Settings.Round2Format = "7";
        league.Settings.Round3Format = "7";
        league.Settings.Round4Format = "7";
        league.Schedule.GamesInSeason = options.GamesPerSeason;

        // Pipeline
        GenerateTeams(league, options.NumberOfTeams, random);
        PopulateRosters(league, options.PlayersPerTeam, random);
        InitializeAllPlayerRatings(league);
        ContractService.GenerateContracts(league, skipAges: true, skipContracts: false, skipPotentials: true, random: random);
        if (options.FinancialEnabled)
            InitializeFinancials(league, random);
        InitializeStaff(league, random);
        InitializeDraftBoard(league);
        ScheduleGenerationService.GenerateSchedule(league, options.GamesPerSeason, random: random);
        RotationService.SetComputerRotations(league);

        return league;
    }

    // ── Team Generation ────────────────────────────────────────────────────

    internal static void GenerateTeams(League league, int count, Random random)
    {
        int teamsToCreate = Math.Min(count, TeamData.Length);
        for (int i = 0; i < teamsToCreate; i++)
        {
            var td = TeamData[i];
            // Map division number to division name used by schedule generation
            string divisionName = GetDivisionName(td.Conference, td.Division, count);
            string conferenceName = ConferenceNames[td.Conference];

            var team = new Team
            {
                Id = i,
                Name = td.Name,
                CityName = td.City,
                Record = new TeamRecord
                {
                    TeamName = td.Name,
                    CityName = td.City,
                    Conference = conferenceName,
                    Division = divisionName,
                    Control = "Computer",
                    InitialNumber = i
                },
                Financial = new TeamFinancial
                {
                    OriginalSlot = i,
                    TeamName = td.Name,
                    CityName = td.City
                }
            };
            league.Teams.Add(team);
        }
    }

    private static string GetDivisionName(int conference, int division, int teamCount)
    {
        // For 30 teams: 6 divisions (3 per conference)
        // For smaller leagues: collapse to 4 divisions (2 per conference)
        if (teamCount <= 16)
        {
            // 4 divisions: Eastern gets Div1/Div2, Western gets Div3/Div4
            int divIdx = conference * 2 + (division % 2);
            string[] smallDivNames = { "Atlantic", "Central", "Southwest", "Northwest" };
            return smallDivNames[divIdx];
        }

        return DivisionNames[division];
    }

    // ── Player Generation ──────────────────────────────────────────────────

    internal static void PopulateRosters(League league, int playersPerTeam, Random random)
    {
        var usedNames = new HashSet<string>();

        foreach (var team in league.Teams)
        {
            int playerId = team.Id * playersPerTeam;

            // 3 tiers × 5 positions = 15 players (star/starter/rotation, one each per position)
            // Tier 0=star, 1=starter, 2=rotation
            for (int tier = 0; tier < 3; tier++)
            {
                for (int posIdx = 0; posIdx < 5; posIdx++)
                {
                    int rosterSlot = tier * 5 + posIdx;
                    if (rosterSlot >= playersPerTeam) break;

                    var player = GeneratePlayer(Positions[posIdx], tier, random, usedNames);
                    player.Id = playerId++;
                    player.TeamIndex = team.Id;
                    player.Team = team.Name;
                    player.Number = rosterSlot + 1;
                    player.Active = true;
                    team.Roster.Add(player);
                }
            }
        }
    }

    internal static Player GeneratePlayer(string position, int minutesTier, Random random, HashSet<string> usedNames)
    {
        // Determine minutes based on tier
        int minutes = minutesTier switch
        {
            0 => random.Next(33, 37), // Star: 33-36 mpg
            1 => random.Next(27, 33), // Starter: 27-32 mpg
            2 => random.Next(18, 27), // Rotation: 18-26 mpg
            _ => random.Next(8, 18)   // Bench: 8-17 mpg
        };

        // Determine age based on tier
        int age = minutesTier switch
        {
            0 => random.Next(25, 31), // Star: 25-30
            1 => random.Next(24, 30), // Starter: 24-29
            2 => random.Next(22, 29), // Rotation: 22-28
            _ => random.Next(20, 27)  // Bench: 20-26
        };

        var stats = GenerateStatLine(position, minutes, random);
        string name = GeneratePlayerName(random, usedNames);

        // Extract last name (everything after the first space)
        string lastName = name.Contains(' ') ? name[(name.IndexOf(' ') + 1)..] : name;

        var player = new Player
        {
            Name = name,
            LastName = lastName,
            Position = position,
            Age = age,
            SeasonStats = stats,
            Health = 100
        };

        // Set potentials and effort
        player.Ratings.Potential1 = random.Next(1, 10); // 1-9
        player.Ratings.Potential2 = random.Next(1, 10); // 1-9
        player.Ratings.Effort = random.Next(3, 8);      // 3-7

        // Set years of service based on age (entered league at ~19-21)
        int entryAge = 19 + random.Next(0, 3);
        player.Contract.YearsOfService = Math.Max(0, age - entryAge);

        return player;
    }

    internal static PlayerStatLine GenerateStatLine(string position, int minutes, Random random)
    {
        int posIdx = GetPositionIndex(position);
        int totalMinutes = minutes * 82; // 82 games
        double minutesFactor = minutes / 36.0;

        // Generate raw per-game stats with ±30% variance
        double fga = AvgFga[posIdx] * minutesFactor * Variance(random);
        double fgPct = AvgFgPct[posIdx] * (0.85 + random.NextDouble() * 0.30); // tighter variance on percentages
        double threePA = AvgThreePA[posIdx] * minutesFactor * Variance(random);
        double threePPct = AvgThreePPct[posIdx] * (0.85 + random.NextDouble() * 0.30);
        double fta = AvgFta[posIdx] * minutesFactor * Variance(random);
        double ftPct = AvgFtPct[posIdx] * (0.85 + random.NextDouble() * 0.30);
        double reb = AvgReb[posIdx] * minutesFactor * Variance(random);
        double ast = AvgAst[posIdx] * minutesFactor * Variance(random);
        double stl = AvgStl[posIdx] * minutesFactor * Variance(random);
        double blk = AvgBlk[posIdx] * minutesFactor * Variance(random);
        double to = AvgTo[posIdx] * minutesFactor * Variance(random);
        double pf = AvgPf[posIdx] * minutesFactor * Variance(random);

        // Offensive rebounds are a fraction of total rebounds
        double oreb = reb * (0.2 + random.NextDouble() * 0.15);

        // Scale per-game stats to season totals
        int totalFga = Math.Max(1, (int)(fga * 82));
        int totalFgm = Math.Max(0, (int)(totalFga * fgPct));
        int totalThreePA = Math.Max(0, (int)(threePA * 82));
        int totalThreePm = Math.Max(0, (int)(totalThreePA * threePPct));
        int totalFta = Math.Max(0, (int)(fta * 82));
        int totalFtm = Math.Max(0, (int)(totalFta * ftPct));

        // FGM includes 2-pointers + 3-pointers; ensure FGM >= 3PM
        if (totalFgm < totalThreePm) totalFgm = totalThreePm;
        // FGA must be >= FGM
        if (totalFga < totalFgm) totalFga = totalFgm;
        // 3PA must be <= FGA
        if (totalThreePA > totalFga) totalThreePA = totalFga;

        return new PlayerStatLine
        {
            Games = 82,
            Minutes = totalMinutes,
            FieldGoalsMade = totalFgm,
            FieldGoalsAttempted = totalFga,
            FreeThrowsMade = totalFtm,
            FreeThrowsAttempted = totalFta,
            ThreePointersMade = totalThreePm,
            ThreePointersAttempted = totalThreePA,
            OffensiveRebounds = Math.Max(0, (int)(oreb * 82)),
            Rebounds = Math.Max(0, (int)(reb * 82)),
            Assists = Math.Max(0, (int)(ast * 82)),
            Steals = Math.Max(0, (int)(stl * 82)),
            Turnovers = Math.Max(0, (int)(to * 82)),
            Blocks = Math.Max(0, (int)(blk * 82)),
            PersonalFouls = Math.Max(0, (int)(pf * 82))
        };
    }

    internal static string GeneratePlayerName(Random random, HashSet<string> usedNames)
    {
        string name;
        int attempts = 0;
        do
        {
            string first = FirstNames[random.Next(FirstNames.Length)];
            string last = LastNames[random.Next(LastNames.Length)];
            name = $"{first} {last}";

            if (usedNames.Contains(name))
            {
                // Append suffix for collision
                attempts++;
                name = $"{first} {last} {(char)('A' + (attempts % 26))}";
            }
        } while (usedNames.Contains(name));

        usedNames.Add(name);
        return name;
    }

    // ── Initialization Pipeline ────────────────────────────────────────────

    internal static void InitializeAllPlayerRatings(League league)
    {
        foreach (var team in league.Teams)
        {
            foreach (var player in team.Roster)
            {
                // CalculateAllRatings uses SeasonStats to compute ODPT + TrueRating + TradeValue
                StatisticsCalculator.CalculateAllRatings(player);

                // Generate height/weight from ODPT ratings
                RookieGenerationService.GenerateRandomHeightWeight(player, Random.Shared);
            }
        }

        // Clear SeasonStats after rating computation (ready for season simulation)
        foreach (var team in league.Teams)
        {
            foreach (var player in team.Roster)
            {
                player.SeasonStats.Reset();
            }
        }
    }

    internal static void InitializeFinancials(League league, Random random)
    {
        foreach (var team in league.Teams)
        {
            FinancialSimulationService.GenerateRandomArena(team.Financial, random);
            FinancialSimulationService.GenerateRandomFinances(team.Financial, random);
            FinancialSimulationService.GenerateRandomCity(team.Financial, random);
        }
    }

    internal static void InitializeStaff(League league, Random random)
    {
        foreach (var team in league.Teams)
        {
            int teamIdx1 = team.Id + 1; // 1-based team index for staff assignments

            // Scout
            var scout = new StaffMember
            {
                Id = league.StaffPool.Count,
                Slot = league.StaffPool.Count,
                Name = GenerateStaffName(random),
                Age = random.Next(35, 61),
                CurrentScout = teamIdx1,
                ScoutJob = teamIdx1,
                OriginalScout = teamIdx1,
                Team = team.Name
            };
            StaffManagementService.InitializeNewStaffMember(scout, random);
            league.StaffPool.Add(scout);
            team.Scout = scout;

            // Coach
            var coach = new StaffMember
            {
                Id = league.StaffPool.Count,
                Slot = league.StaffPool.Count,
                Name = GenerateStaffName(random),
                Age = random.Next(35, 61),
                CurrentCoach = teamIdx1,
                CoachJob = teamIdx1,
                OriginalCoach = teamIdx1,
                Team = team.Name
            };
            StaffManagementService.InitializeNewStaffMember(coach, random);
            league.StaffPool.Add(coach);
            team.Coach = coach;

            // General Manager
            var gm = new StaffMember
            {
                Id = league.StaffPool.Count,
                Slot = league.StaffPool.Count,
                Name = GenerateStaffName(random),
                Age = random.Next(35, 61),
                CurrentGM = teamIdx1,
                GmJob = teamIdx1,
                OriginalGM = teamIdx1,
                Team = team.Name
            };
            StaffManagementService.InitializeNewStaffMember(gm, random);
            league.StaffPool.Add(gm);
            team.GeneralManager = gm;
        }
    }

    internal static void InitializeDraftBoard(League league)
    {
        league.DraftBoard = new DraftBoard();
        DraftService.InitializeDraftChart(league.DraftBoard, league.Settings.NumberOfTeams);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static double Variance(Random random)
    {
        // ±30% variance: range [0.7, 1.3]
        return 0.7 + random.NextDouble() * 0.6;
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

    private static string GenerateStaffName(Random random)
    {
        string first = FirstNames[random.Next(FirstNames.Length)];
        string last = LastNames[random.Next(LastNames.Length)];
        return $"{first} {last}";
    }
}
