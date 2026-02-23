using JumpShotBasketball.Core.Models.Player;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Imports player data from CSV files in three formats:
/// - Standard: Name,Pos,Age,G,Min,FGM,FGA,FTM,FTA,3PM,3PA,OREB,REB,AST,STL,TO,BLK,PF[,Ft,In,Wt,Pot1,Pot2,Effort,Team,Comp,Dur,Tenure]
/// - Historical: Year,Pos,First,Last,Team,Ft,In,Wt,YrsPro,Age,G,Min,FGM,FGA,3PM,3PA,FTM,FTA,OREB,REB,AST,STL,TO,BLK,PF
/// - Rookie: Name,Pos,Age,GP,Min,FG,FGA,FTM,FTA,3Pt,3PtA,Off,Reb,Asts,Stls,TOs,Blks,PFs,Ft,In,Wt,Tal,Ski,Int,Team,Comp,Dur,Ability
/// </summary>
public static class CsvImportService
{
    /// <summary>
    /// Options controlling which optional columns are present in Standard CSV format.
    /// </summary>
    public class CsvImportOptions
    {
        public bool IncludesAges { get; set; }
        public bool IncludesContracts { get; set; }
    }

    /// <summary>
    /// Imports players from the Standard CSV format (used by CreatePlayerObjectFromSpreadSheet).
    /// Columns: Name,Pos,Age,G,Min,FGM,FGA,FTM,FTA,3PM,3PA,OREB,REB,AST,STL,TO,BLK,PF
    /// Optional trailing: Ft,In,Wt,Pot1,Pot2,Effort,Team,Comp,Dur,Tenure
    /// </summary>
    public static List<Player> ImportStandard(string csvPath, CsvImportOptions? options = null)
    {
        var players = new List<Player>();
        var lines = File.ReadAllLines(csvPath);

        for (int i = 1; i < lines.Length; i++) // skip header
        {
            string line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] fields = SplitCsvLine(line);
            if (fields.Length < 18) continue;

            int col = 0;
            var player = new Player
            {
                Name = fields[col++].Trim(),
                Position = NormalizePosition(fields[col++].Trim()),
                Age = ParseInt(fields[col++])
            };

            var stats = new PlayerStatLine
            {
                Games = ParseInt(fields[col++]),
                Minutes = ParseInt(fields[col++]),
                FieldGoalsMade = ParseInt(fields[col++]),
                FieldGoalsAttempted = ParseInt(fields[col++]),
                FreeThrowsMade = ParseInt(fields[col++]),
                FreeThrowsAttempted = ParseInt(fields[col++]),
                ThreePointersMade = ParseInt(fields[col++]),
                ThreePointersAttempted = ParseInt(fields[col++]),
                OffensiveRebounds = ParseInt(fields[col++]),
                Rebounds = ParseInt(fields[col++]),
                Assists = ParseInt(fields[col++]),
                Steals = ParseInt(fields[col++]),
                Turnovers = ParseInt(fields[col++]),
                Blocks = ParseInt(fields[col++]),
                PersonalFouls = ParseInt(fields[col++])
            };
            player.SeasonStats = stats;

            // Optional physical/potential fields
            if (col < fields.Length)
            {
                int ft = ParseInt(fields[col++]);
                if (col < fields.Length)
                {
                    int inches = ParseInt(fields[col++]);
                    player.Height = ft * 12 + inches;
                }
                if (col < fields.Length)
                    player.Weight = ParseInt(fields[col++]);
            }

            if (col < fields.Length)
                player.Ratings.Potential1 = ParseInt(fields[col++]);
            if (col < fields.Length)
                player.Ratings.Potential2 = ParseInt(fields[col++]);
            if (col < fields.Length)
                player.Ratings.Effort = ParseInt(fields[col++]);

            // Default clutch/consistency
            player.Ratings.Clutch = 2;
            player.Ratings.Consistency = 2;

            if (!ValidatePlayer(player)) continue;

            players.Add(player);
        }

        return players;
    }

    /// <summary>
    /// Imports players from the Historical CSV format (used by CreatePlayerObjectFromPlayerSpreadSheet).
    /// Columns: Year,Pos,First,Last,Team,Ft,In,Wt,YrsPro,Age,G,Min,FGM,FGA,3PM,3PA,FTM,FTA,OREB,REB,AST,STL,TO,BLK,PF
    /// Optional trailing (when check_ages): Age,YrsPro,Ft,In,Wt
    /// Optional trailing (when check_contracts): YrsOnTeam,Sal1-6
    /// </summary>
    public static List<Player> ImportHistorical(string csvPath, int? filterYear = null,
        CsvImportOptions? options = null)
    {
        var players = new List<Player>();
        var lines = File.ReadAllLines(csvPath);

        for (int i = 1; i < lines.Length; i++) // skip header
        {
            string line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] fields = SplitCsvLine(line);
            if (fields.Length < 25) continue;

            int year = ParseInt(fields[0]);
            if (filterYear.HasValue && year != filterYear.Value) continue;

            string pos = NormalizePosition(fields[1].Trim());
            if (string.IsNullOrWhiteSpace(pos)) continue;

            // Validate position
            if (pos != "PG" && pos != "SG" && pos != "SF" && pos != "PF" && pos != "C")
                continue;

            string firstName = fields[2].Trim().Replace("\"", "");
            string lastName = fields[3].Trim().Replace("\"", "");
            string name = $"{firstName} {lastName}".ToUpperInvariant();

            string teamAbbr = fields[4].Trim();

            int ft = ParseInt(fields[5]);
            int inches = ParseInt(fields[6]);
            int weight = ParseInt(fields[7]);
            int yrsPro = ParseInt(fields[8]);
            int age = ParseInt(fields[9]);

            var player = new Player
            {
                Name = name,
                Position = pos == "C" ? " C" : pos,
                Age = age,
                Height = ft * 12 + inches,
                Weight = weight,
                Ratings = new PlayerRatings { Clutch = 2, Consistency = 2 },
                Contract = new PlayerContract { YearsOfService = yrsPro > 0 ? yrsPro : 1 }
            };

            player.SeasonStats = new PlayerStatLine
            {
                Games = ParseInt(fields[10]),
                Minutes = ParseInt(fields[11]),
                FieldGoalsMade = ParseInt(fields[12]),
                FieldGoalsAttempted = ParseInt(fields[13]),
                ThreePointersMade = ParseInt(fields[14]),
                ThreePointersAttempted = ParseInt(fields[15]),
                FreeThrowsMade = ParseInt(fields[16]),
                FreeThrowsAttempted = ParseInt(fields[17]),
                OffensiveRebounds = ParseInt(fields[18]),
                Rebounds = ParseInt(fields[19]),
                Assists = ParseInt(fields[20]),
                Steals = ParseInt(fields[21]),
                Turnovers = ParseInt(fields[22]),
                Blocks = ParseInt(fields[23]),
                PersonalFouls = ParseInt(fields[24])
            };

            // Store team abbreviation in Team field for later matching
            player.Team = teamAbbr;

            if (!ValidatePlayer(player)) continue;

            players.Add(player);
        }

        return players;
    }

    /// <summary>
    /// Imports players from the Rookie/Draft CSV format.
    /// Columns: Name,Pos,Age,GP,Min,FG,FGA,FTM,FTA,3Pt,3PtA,Off,Reb,Asts,Stls,TOs,Blks,PFs,Ft,In,Wt,Tal,Ski,Int,Team,Comp,Dur,Ability
    /// </summary>
    public static List<Player> ImportRookies(string csvPath)
    {
        var players = new List<Player>();
        var lines = File.ReadAllLines(csvPath);

        for (int i = 1; i < lines.Length; i++) // skip header
        {
            string line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] fields = SplitCsvLine(line);
            if (fields.Length < 18) continue;

            int col = 0;
            var player = new Player
            {
                Name = fields[col++].Trim(),
                Position = NormalizePosition(fields[col++].Trim()),
                Age = ParseInt(fields[col++])
            };

            player.SeasonStats = new PlayerStatLine
            {
                Games = ParseInt(fields[col++]),
                Minutes = ParseInt(fields[col++]),
                FieldGoalsMade = ParseInt(fields[col++]),
                FieldGoalsAttempted = ParseInt(fields[col++]),
                FreeThrowsMade = ParseInt(fields[col++]),
                FreeThrowsAttempted = ParseInt(fields[col++]),
                ThreePointersMade = ParseInt(fields[col++]),
                ThreePointersAttempted = ParseInt(fields[col++]),
                OffensiveRebounds = ParseInt(fields[col++]),
                Rebounds = ParseInt(fields[col++]),
                Assists = ParseInt(fields[col++]),
                Steals = ParseInt(fields[col++]),
                Turnovers = ParseInt(fields[col++]),
                Blocks = ParseInt(fields[col++]),
                PersonalFouls = ParseInt(fields[col++])
            };

            // Physical attributes
            if (col + 2 < fields.Length)
            {
                int ft = ParseInt(fields[col++]);
                int inches = ParseInt(fields[col++]);
                player.Height = ft * 12 + inches;
                player.Weight = ParseInt(fields[col++]);
            }

            // Rookie-specific ratings
            if (col < fields.Length)
                player.Ratings.Potential1 = ParseInt(fields[col++]); // Talent
            if (col < fields.Length)
                player.Ratings.Potential2 = ParseInt(fields[col++]); // Skill
            // Intangibles — store in Effort for now
            if (col < fields.Length)
                player.Ratings.Effort = ParseInt(fields[col++]);

            // Team abbreviation
            if (col < fields.Length)
                player.Team = fields[col++].Trim();

            player.Ratings.Clutch = 2;
            player.Ratings.Consistency = 2;
            player.Contract.IsRookie = true;
            player.Contract.YearsOfService = 0;

            if (!ValidatePlayer(player)) continue;

            players.Add(player);
        }

        return players;
    }

    // ─── Helpers ──────────────────────────────────────────────────

    private static string[] SplitCsvLine(string line) =>
        line.Split(',');

    private static int ParseInt(string s) =>
        int.TryParse(s.Trim(), out int result) ? result : 0;

    /// <summary>
    /// Normalizes position codes: "C" → " C" to match C++ convention.
    /// Handles case-insensitive matching.
    /// </summary>
    private static string NormalizePosition(string pos)
    {
        string upper = pos.ToUpperInvariant().Trim();
        return upper switch
        {
            "C" => " C",
            "PG" => "PG",
            "SG" => "SG",
            "SF" => "SF",
            "PF" => "PF",
            _ => upper
        };
    }

    /// <summary>
    /// Validates a player has minimum required data (matches C++ validation rules).
    /// </summary>
    private static bool ValidatePlayer(Player player)
    {
        var s = player.SeasonStats;
        if (string.IsNullOrWhiteSpace(player.Name)) return false;
        if (s.Minutes < 96) return false;
        if (s.FieldGoalsAttempted < 1) return false;
        if (s.FreeThrowsAttempted < 1) return false;
        if (s.Turnovers < 1) return false;
        if (s.ThreePointersAttempted > s.FieldGoalsAttempted) return false;
        if (s.FieldGoalsMade > s.FieldGoalsAttempted) return false;
        if (s.FreeThrowsMade > s.FreeThrowsAttempted) return false;
        if (s.ThreePointersMade > s.ThreePointersAttempted) return false;
        if ((s.FieldGoalsMade - s.ThreePointersMade) > (s.FieldGoalsAttempted - s.ThreePointersAttempted))
            return false;
        if (s.Steals * 8 > s.Minutes) return false;
        return true;
    }
}
