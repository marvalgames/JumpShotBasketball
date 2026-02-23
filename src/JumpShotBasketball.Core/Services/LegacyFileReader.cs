using System.Text;
using JumpShotBasketball.Core.Models.Player;
using JumpShotBasketball.Core.Models.Team;
using JumpShotBasketball.Core.Models.League;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Reads legacy C++ BBall fixed-width text files (.plr, .lge, .sch, .frn, .bud, .sal).
/// All legacy files use space-padded ASCII fields. The .plr file is line-based (607 bytes
/// per line + CRLF), while .lge/.frn/.bud/.sal are binary with fixed offsets.
/// </summary>
public static class LegacyFileReader
{
    // .plr file constants
    private const int PlrLineLength = 607;
    private const int LinesPerTeam = 30;
    private const int MaxTeamBlocks = 32;
    private const int FreeAgentStartLine = MaxTeamBlocks * LinesPerTeam; // 960

    // .plr field positions within each 607-byte line
    private const int PlrNameStart = 4;
    private const int PlrNameLength = 32;
    private const int PlrAgeStart = 36;
    private const int PlrIdStart = 38;
    private const int PlrPositionStart = 50;
    private const int PlrGamesStart = 52;
    private const int PlrMinStart = 56;
    private const int PlrFgmStart = 60;
    private const int PlrFgaStart = 64;
    private const int PlrFtmStart = 68;
    private const int PlrFtaStart = 72;
    private const int PlrTfgmStart = 76;
    private const int PlrTfgaStart = 80;
    private const int PlrOrebStart = 84;
    private const int PlrRebStart = 88;
    private const int PlrAstStart = 92;
    private const int PlrStlStart = 96;
    private const int PlrToStart = 100;
    private const int PlrBlkStart = 104;
    private const int PlrPfStart = 108;
    private const int PlrMovOffStart = 112;
    private const int PlrClutchStart = 128;
    private const int PlrConsistencyStart = 130;
    private const int PlrPgRotStart = 132;
    private const int PlrActiveStart = 137;
    private const int PlrBetterStart = 138;
    private const int PlrHeightStart = 549;
    private const int PlrWeightStart = 552;

    // ─── Parsing helpers ───────────────────────────────────────────

    private static string ReadFixed(string data, int offset, int length)
    {
        if (offset + length > data.Length) return string.Empty;
        return data.Substring(offset, length).Trim();
    }

    private static int ReadFixedInt(string data, int offset, int length)
    {
        string s = ReadFixed(data, offset, length);
        return int.TryParse(s, out int result) ? result : 0;
    }

    private static int ReadFixedInt(byte[] data, int offset, int length)
    {
        if (offset + length > data.Length) return 0;
        string s = Encoding.ASCII.GetString(data, offset, length).Trim();
        return int.TryParse(s, out int result) ? result : 0;
    }

    private static string ReadFixedString(byte[] data, int offset, int length)
    {
        if (offset + length > data.Length) return string.Empty;
        return Encoding.ASCII.GetString(data, offset, length).Trim();
    }

    // ─── .plr file ────────────────────────────────────────────────

    /// <summary>
    /// Reads a legacy .plr file and returns team rosters and free agents.
    /// The .plr file is line-based: 30 lines per team (32 team blocks),
    /// free agents starting at line 960.
    /// </summary>
    public static (List<List<Player>> teamRosters, List<Player> freeAgents) ReadPlrFile(
        string filePath, int numTeams)
    {
        byte[] raw = File.ReadAllBytes(filePath);
        string[] lines = Encoding.ASCII.GetString(raw).Split("\r\n");

        var teamRosters = new List<List<Player>>();
        for (int t = 0; t < numTeams; t++)
        {
            var roster = new List<Player>();
            int blockStart = t * LinesPerTeam;

            for (int slot = 1; slot < LinesPerTeam; slot++)
            {
                int lineIndex = blockStart + slot;
                if (lineIndex >= lines.Length) break;

                string line = lines[lineIndex];
                if (line.Length < PlrBetterStart + 2) continue;

                var player = ParsePlayerLine(line, lineIndex);
                if (player != null)
                    roster.Add(player);
            }

            teamRosters.Add(roster);
        }

        var freeAgents = new List<Player>();
        for (int lineIndex = FreeAgentStartLine; lineIndex < lines.Length; lineIndex++)
        {
            string line = lines[lineIndex];
            if (line.Length < PlrBetterStart + 2) continue;

            var player = ParsePlayerLine(line, lineIndex);
            if (player != null)
                freeAgents.Add(player);
        }

        return (teamRosters, freeAgents);
    }

    private static Player? ParsePlayerLine(string line, int lineIndex)
    {
        string name = ReadFixed(line, PlrNameStart, PlrNameLength);
        if (string.IsNullOrWhiteSpace(name)) return null;

        int games = ReadFixedInt(line, PlrGamesStart, 4);
        if (games <= 0) return null;

        var player = new Player
        {
            Id = ReadFixedInt(line, PlrIdStart, 6),
            Name = name,
            Age = ReadFixedInt(line, PlrAgeStart, 2),
            Position = ReadFixed(line, PlrPositionStart, 2),
            Number = lineIndex,
            Active = ReadFixedInt(line, PlrActiveStart, 1) != 0,
            Better = ReadFixedInt(line, PlrBetterStart, 2),
            OriginalNumber = lineIndex,
            SeasonStats = new PlayerStatLine
            {
                Games = games,
                Minutes = ReadFixedInt(line, PlrMinStart, 4),
                FieldGoalsMade = ReadFixedInt(line, PlrFgmStart, 4),
                FieldGoalsAttempted = ReadFixedInt(line, PlrFgaStart, 4),
                FreeThrowsMade = ReadFixedInt(line, PlrFtmStart, 4),
                FreeThrowsAttempted = ReadFixedInt(line, PlrFtaStart, 4),
                ThreePointersMade = ReadFixedInt(line, PlrTfgmStart, 4),
                ThreePointersAttempted = ReadFixedInt(line, PlrTfgaStart, 4),
                OffensiveRebounds = ReadFixedInt(line, PlrOrebStart, 4),
                Rebounds = ReadFixedInt(line, PlrRebStart, 4),
                Assists = ReadFixedInt(line, PlrAstStart, 4),
                Steals = ReadFixedInt(line, PlrStlStart, 4),
                Turnovers = ReadFixedInt(line, PlrToStart, 4),
                Blocks = ReadFixedInt(line, PlrBlkStart, 4),
                PersonalFouls = ReadFixedInt(line, PlrPfStart, 4)
            },
            Ratings = new PlayerRatings
            {
                MovementOffenseRaw = ClampMovement(ReadFixedInt(line, PlrMovOffStart, 2)),
                MovementDefenseRaw = ClampMovement(ReadFixedInt(line, PlrMovOffStart + 2, 2)),
                PenetrationOffenseRaw = ClampMovement(ReadFixedInt(line, PlrMovOffStart + 4, 2)),
                PenetrationDefenseRaw = ClampMovement(ReadFixedInt(line, PlrMovOffStart + 6, 2)),
                PostOffenseRaw = ClampMovement(ReadFixedInt(line, PlrMovOffStart + 8, 2)),
                PostDefenseRaw = ClampMovement(ReadFixedInt(line, PlrMovOffStart + 10, 2)),
                TransitionOffenseRaw = ClampMovement(ReadFixedInt(line, PlrMovOffStart + 12, 2)),
                TransitionDefenseRaw = ClampMovement(ReadFixedInt(line, PlrMovOffStart + 14, 2)),
                Clutch = ReadFixedInt(line, PlrClutchStart, 2),
                Consistency = ReadFixedInt(line, PlrConsistencyStart, 2)
            }
        };

        // Position rotation flags
        player.PgRotation = ReadFixedInt(line, PlrPgRotStart, 1) != 0;
        player.SgRotation = ReadFixedInt(line, PlrPgRotStart + 1, 1) != 0;
        player.SfRotation = ReadFixedInt(line, PlrPgRotStart + 2, 1) != 0;
        player.PfRotation = ReadFixedInt(line, PlrPgRotStart + 3, 1) != 0;
        player.CRotation = ReadFixedInt(line, PlrPgRotStart + 4, 1) != 0;

        // Height and weight (near end of line)
        if (line.Length >= PlrWeightStart + 3)
        {
            player.Height = ReadFixedInt(line, PlrHeightStart, 3);
            player.Weight = ReadFixedInt(line, PlrWeightStart, 3);
        }

        // Default clutch/consistency to 2 if 0 (matches C++ behavior)
        if (player.Ratings.Clutch == 0) player.Ratings.Clutch = 2;
        if (player.Ratings.Consistency == 0) player.Ratings.Consistency = 2;

        return player;
    }

    private static int ClampMovement(int value) => value <= 0 ? 1 : value;

    // ─── .lge file ────────────────────────────────────────────────

    /// <summary>
    /// Reads a legacy .lge file into LeagueSettings and team metadata.
    /// Returns settings plus arrays of team names, control types, conferences, and divisions.
    /// </summary>
    public static (LeagueSettings settings, string[] teamNames, string[] controlTypes)
        ReadLgeFile(string filePath)
    {
        byte[] data = File.ReadAllBytes(filePath);
        var settings = new LeagueSettings();

        // Year at offset 3992 (4 bytes)
        settings.CurrentYear = ReadFixedInt(data, 3992, 4);

        // Number of teams at offset 4002 (2 bytes)
        settings.NumberOfTeams = ReadFixedInt(data, 4002, 2);
        if (settings.NumberOfTeams <= 0) settings.NumberOfTeams = 30;

        // Playoff format at offset 0 (32 bytes)
        settings.PlayoffFormat = ReadFixedString(data, 0, 32);
        settings.Round1Format = ReadFixedString(data, 32, 8);
        settings.Round2Format = ReadFixedString(data, 40, 8);
        settings.Round3Format = ReadFixedString(data, 48, 8);
        settings.Round4Format = ReadFixedString(data, 56, 8);

        // Conference names at offset 64 (2 × 16 bytes)
        settings.ConferenceName1 = ReadFixedString(data, 64, 16);
        settings.ConferenceName2 = ReadFixedString(data, 80, 16);

        // Division names at offset 96 (4 × 16 bytes)
        settings.DivisionName1 = ReadFixedString(data, 96, 16);
        settings.DivisionName2 = ReadFixedString(data, 112, 16);
        settings.DivisionName3 = ReadFixedString(data, 128, 16);
        settings.DivisionName4 = ReadFixedString(data, 144, 16);

        // Team blocks at offset 160 (72 bytes each: name(32) + control(8) + conference(16) + division(16))
        var teamNames = new string[settings.NumberOfTeams];
        var controlTypes = new string[settings.NumberOfTeams];

        for (int i = 0; i < settings.NumberOfTeams; i++)
        {
            int offset = 160 + i * 72;
            teamNames[i] = ReadFixedString(data, offset, 32);
            controlTypes[i] = ReadFixedString(data, offset + 32, 8);
        }

        // Career check at 3996 (2 bytes)
        settings.CareerStatsEnabled = ReadFixedInt(data, 3996, 2) != 0;

        // Injury setting at 4000 (2 bytes)
        int injury = ReadFixedInt(data, 4000, 2);
        settings.InjuriesEnabled = injury >= 1 && injury <= 2;

        // Feature flags (at offsets 10000+) — only read if file is large enough
        if (data.Length > 10700)
        {
            settings.ThreePointLineEnabled = ReadFixedInt(data, 10500, 1) != 0;
            settings.ThreePointFreeThrowEnabled = ReadFixedInt(data, 10501, 1) != 0;
            settings.InjuryPromptEnabled = ReadFixedInt(data, 10600, 2) != 0;
            settings.AstGmMode = ReadFixedInt(data, 10602, 1);
            settings.SalaryMatchingEnabled = ReadFixedInt(data, 10604, 1) != 0;
            settings.FinancialEnabled = ReadFixedInt(data, 10606, 1) != 0;
            settings.FreeAgencyEnabled = ReadFixedInt(data, 10607, 1) != 0;

            // Salary cap at 10608 (5 bytes) — base 3550 + delta
            int capDelta = ReadFixedInt(data, 10608, 5);
            settings.SalaryCap = 3550 + capDelta;

            // Scoring factor at 10614 (3 bytes) — stored as percentage
            int scoringPct = ReadFixedInt(data, 10614, 3);
            settings.ScoringFactor = scoringPct > 0 ? scoringPct / 100.0 : 1.0;
        }

        if (data.Length > 12010)
        {
            settings.ComputerTradesEnabled = ReadFixedInt(data, 12000, 2) != 0;
            settings.CurrentStage = ReadFixedInt(data, 12002, 2);
            int speed = ReadFixedInt(data, 12004, 2);
            settings.SimSpeed = speed > 0 ? speed : 50;

            int deadline = ReadFixedInt(data, 12006, 1);
            settings.TradeDeadlineEnabled = deadline != 1;
            settings.Expanding = ReadFixedInt(data, 12007, 1) != 0;
        }

        return (settings, teamNames, controlTypes);
    }

    // ─── .sch file ────────────────────────────────────────────────

    /// <summary>
    /// Reads a legacy .sch file into a list of scheduled games.
    /// The schedule is a 3D array: [12 months][31 days][16 games per day].
    /// Each game slot = 4 bytes (teams) + 6 bytes (scores) = 10 bytes.
    /// Teams encoded as visitor×100 + home; scores as visitorScore×1000 + homeScore.
    /// </summary>
    public static List<ScheduledGame> ReadSchFile(string filePath)
    {
        byte[] data = File.ReadAllBytes(filePath);
        var games = new List<ScheduledGame>();
        int gameNumber = 1;
        int pos = 0;

        for (int month = 0; month <= 11; month++)
        {
            for (int day = 0; day <= 30; day++)
            {
                for (int slot = 0; slot <= 15; slot++)
                {
                    if (pos + 10 > data.Length) break;

                    int teamCode = ReadFixedInt(data, pos, 4);
                    int scoreCode = ReadFixedInt(data, pos + 4, 6);
                    pos += 10;

                    if (teamCode <= 0) continue;

                    int visitor = teamCode / 100;
                    int home = teamCode % 100;
                    int visitorScore = scoreCode / 1000;
                    int homeScore = scoreCode % 1000;

                    games.Add(new ScheduledGame
                    {
                        GameNumber = gameNumber++,
                        HomeTeamIndex = home,
                        VisitorTeamIndex = visitor,
                        Played = scoreCode > 0,
                        HomeScore = homeScore,
                        VisitorScore = visitorScore,
                        Week = month * 4 + day / 7
                    });
                }
            }
        }

        return games;
    }

    // ─── .frn file ────────────────────────────────────────────────

    /// <summary>
    /// Reads a legacy .frn (franchise) file into TeamFinancial objects.
    /// 1000 bytes per team at offset team×1000.
    /// </summary>
    public static List<TeamFinancial> ReadFrnFile(string filePath, int numTeams)
    {
        byte[] data = File.ReadAllBytes(filePath);
        var financials = new List<TeamFinancial>();

        for (int t = 0; t < numTeams; t++)
        {
            int baseOffset = t * 1000;
            if (baseOffset + 200 > data.Length) break;

            int pos = baseOffset;
            var fin = new TeamFinancial
            {
                CityName = ReadFixedString(data, pos, 32),
                ArenaName = ReadFixedString(data, pos + 32, 32)
            };
            pos += 64;

            fin.TicketPrice = ReadFixedInt(data, pos, 5); pos += 5;
            fin.SuitePrice = ReadFixedInt(data, pos, 5); pos += 5;
            fin.ClubPrice = ReadFixedInt(data, pos, 5); pos += 5;
            fin.NetworkShare = ReadFixedInt(data, pos, 5); pos += 5;
            fin.LocalTv = ReadFixedInt(data, pos, 6); pos += 6;
            fin.LocalRadio = ReadFixedInt(data, pos, 6); pos += 6;
            fin.Concessions = ReadFixedInt(data, pos, 5); pos += 5;
            fin.Parking = ReadFixedInt(data, pos, 5); pos += 5;
            fin.Advertising = ReadFixedInt(data, pos, 5); pos += 5;
            fin.Sponsorship = ReadFixedInt(data, pos, 5); pos += 5;
            fin.CurrentValue = ReadFixedInt(data, pos, 5); pos += 5;

            // Owner salary cap (10 bytes)
            int ownerCap = ReadFixedInt(data, pos, 10); pos += 10;
            fin.OwnerSalaryCap = ownerCap;

            fin.Capacity = ReadFixedInt(data, pos, 5); pos += 5;
            fin.Suites = ReadFixedInt(data, pos, 5); pos += 5;
            fin.ClubSeats = ReadFixedInt(data, pos, 5); pos += 5;
            fin.ParkingSpots = ReadFixedInt(data, pos, 5); pos += 5;
            fin.FanSupport = ReadFixedInt(data, pos, 5); pos += 5;
            fin.Economy = ReadFixedInt(data, pos, 5); pos += 5;
            fin.PoliticalSupport = ReadFixedInt(data, pos, 5); pos += 5;
            fin.Interest = ReadFixedInt(data, pos, 5); pos += 5;
            fin.OperatingLevel = ReadFixedInt(data, pos, 5); pos += 5;
            fin.ArenaLevel = ReadFixedInt(data, pos, 5); pos += 5;
            fin.MarketingLevel = ReadFixedInt(data, pos, 5); pos += 5;
            fin.Population = ReadFixedInt(data, pos, 5); pos += 5;
            fin.CostOfLiving = ReadFixedInt(data, pos, 5); pos += 5;
            fin.Competition = ReadFixedInt(data, pos, 5); pos += 5;
            fin.StadiumValue = ReadFixedInt(data, pos, 5);

            if (string.IsNullOrWhiteSpace(fin.CityName))
                fin.CityName = $"City {t + 1}";
            if (string.IsNullOrWhiteSpace(fin.ArenaName))
                fin.ArenaName = $"Arena {t + 1}";

            financials.Add(fin);
        }

        return financials;
    }

    // ─── .bud file ────────────────────────────────────────────────

    /// <summary>
    /// Reads a legacy .bud (budget) file, updating existing TeamFinancial objects.
    /// 1000 bytes per team at offset team×1000. 18 fields of 10 bytes each.
    /// </summary>
    public static void ReadBudFile(string filePath, List<TeamFinancial> financials)
    {
        byte[] data = File.ReadAllBytes(filePath);

        for (int t = 0; t < financials.Count && t <= 100; t++)
        {
            int pos = t * 1000;
            if (pos + 180 > data.Length) break;

            var fin = financials[t];
            fin.HomeGames = ReadFixedInt(data, pos, 10); pos += 10;
            fin.SeasonAttendance = ReadFixedInt(data, pos, 10); pos += 10;
            fin.SeasonSuitesSold = ReadFixedInt(data, pos, 10); pos += 10;
            fin.SeasonClubsSold = ReadFixedInt(data, pos, 10); pos += 10;
            fin.AttendanceRevenue = ReadFixedInt(data, pos, 10); pos += 10;
            fin.SeasonSuiteRevenue = ReadFixedInt(data, pos, 10); pos += 10;
            fin.SeasonClubRevenue = ReadFixedInt(data, pos, 10); pos += 10;
            fin.SeasonConcessionRevenue = ReadFixedInt(data, pos, 10); pos += 10;
            fin.SeasonParkingRevenue = ReadFixedInt(data, pos, 10); pos += 10;
            fin.SeasonTotalRevenue = ReadFixedInt(data, pos, 10); pos += 10;
            fin.SeasonPayroll = ReadFixedInt(data, pos, 10); pos += 10;
            fin.SeasonScoutExpenses = ReadFixedInt(data, pos, 10); pos += 10;
            fin.SeasonCoachExpenses = ReadFixedInt(data, pos, 10); pos += 10;
            fin.SeasonGmExpenses = ReadFixedInt(data, pos, 10); pos += 10;
            fin.SeasonOperatingExpenses = ReadFixedInt(data, pos, 10); pos += 10;
            fin.SeasonArenaExpenses = ReadFixedInt(data, pos, 10); pos += 10;
            fin.SeasonMarketingExpenses = ReadFixedInt(data, pos, 10); pos += 10;
            fin.SeasonTotalExpenses = ReadFixedInt(data, pos, 10);
        }
    }

    // ─── .sal file ────────────────────────────────────────────────

    /// <summary>
    /// Reads a legacy .sal (salary/contract) file, updating player contracts.
    /// Each player record is at offset 2000 + id×500 + 1.
    /// </summary>
    public static void ReadSalFile(string filePath, List<Player> allPlayers)
    {
        byte[] data = File.ReadAllBytes(filePath);

        foreach (var player in allPlayers)
        {
            int id = player.Id;
            int seek = 2000 + id * 500 + 1;
            if (seek + 50 > data.Length) continue;

            // Name (16 bytes) — for verification, not used
            // string salName = ReadFixedString(data, seek, 16);
            int pos = seek + 16;

            // 4 boolean flags (1 byte each)
            // random_contract_ratings, random_contracts, random_projections, random_yrs_service
            pos += 4; // skip boolean flags

            // Potential and personality factors (1 byte each)
            player.Ratings.Potential1 = ReadFixedInt(data, pos, 1); pos += 1;
            player.Ratings.Potential2 = ReadFixedInt(data, pos, 1); pos += 1;
            player.Ratings.Effort = ReadFixedInt(data, pos, 1); pos += 1;
            player.Contract.CoachFactor = ReadFixedInt(data, pos, 1); pos += 1;
            player.Contract.LoyaltyFactor = ReadFixedInt(data, pos, 1); pos += 1;
            player.Contract.PlayingTimeFactor = ReadFixedInt(data, pos, 1); pos += 1;
            player.Contract.WinningFactor = ReadFixedInt(data, pos, 1); pos += 1;
            player.Contract.TraditionFactor = ReadFixedInt(data, pos, 1); pos += 1;
            player.Contract.SecurityFactor = ReadFixedInt(data, pos, 1); pos += 1;

            // Current team (2 bytes), previous team (2 bytes)
            pos += 2; // skip current team (we derive it from roster position)
            player.Contract.PreviousTeam = ReadFixedInt(data, pos, 2); pos += 2;

            // Years of service (2), years on team (2)
            player.Contract.YearsOfService = ReadFixedInt(data, pos, 2); pos += 2;
            player.Contract.YearsOnTeam = ReadFixedInt(data, pos, 2); pos += 2;

            // Current contract year (1), contract years (1)
            player.Contract.CurrentContractYear = ReadFixedInt(data, pos, 1); pos += 1;
            player.Contract.ContractYears = ReadFixedInt(data, pos, 1); pos += 1;

            // Read year-by-year salaries
            int yrs = player.Contract.ContractYears;
            int salarySeek = 2000 + id * 500 + 48 + player.Contract.YearsOfService * 4;
            salarySeek -= (yrs - 1) * 4;

            int totalSalary = 0;
            int remainingSalary = 0;
            for (int y = 1; y <= yrs && y < player.Contract.ContractSalaries.Length; y++)
            {
                if (salarySeek + 4 > data.Length) break;
                int sal = ReadFixedInt(data, salarySeek, 4);
                player.Contract.ContractSalaries[y] = sal;
                salarySeek += 4;
                totalSalary += sal;
                if (y >= player.Contract.CurrentContractYear)
                    remainingSalary += sal;
            }

            player.Contract.TotalSalary = totalSalary;
            player.Contract.RemainingSalary = remainingSalary;

            if (player.Contract.ContractYears > 0 &&
                player.Contract.ContractYears == player.Contract.CurrentContractYear)
                player.Contract.IsFreeAgent = true;

            if (player.Contract.CurrentContractYear > 0 &&
                player.Contract.CurrentContractYear < player.Contract.ContractSalaries.Length)
                player.Contract.CurrentYearSalary =
                    player.Contract.ContractSalaries[player.Contract.CurrentContractYear];
        }
    }
}
