using FluentAssertions;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

/// <summary>
/// Tests CsvImportService against actual CSV files from jsb599/jsb 599 release files/.
/// </summary>
public class CsvImportServiceTests
{
    private static readonly string TestDataPath =
        Environment.GetEnvironmentVariable("JSB_TEST_DATA_PATH")
        ?? "/mnt/c/JSB/BBall/jsb599/jsb 599 release files";

    private static string FilePath(string name) => Path.Combine(TestDataPath, name);
    private static bool TestDataExists => Directory.Exists(TestDataPath);

    // ─── Historical format tests ─────────────────────────────────

    [Fact]
    public void ImportHistorical_08_09_ParsesPlayers()
    {
        if (!TestDataExists) return;
        string path = FilePath("default_08-09.csv");
        if (!File.Exists(path)) return;

        var players = CsvImportService.ImportHistorical(path, filterYear: 2009);

        players.Should().HaveCountGreaterThan(100, "should import many players");
    }

    [Fact]
    public void ImportHistorical_08_09_ParsesIguodala()
    {
        if (!TestDataExists) return;
        string path = FilePath("default_08-09.csv");
        if (!File.Exists(path)) return;

        var players = CsvImportService.ImportHistorical(path, filterYear: 2009);

        var iguodala = players.FirstOrDefault(p => p.Name.Contains("IGUODALA"));
        iguodala.Should().NotBeNull("Andre Iguodala should be in the CSV");
        iguodala!.Position.Should().Be("SF");
        iguodala.Age.Should().Be(24);
        iguodala.SeasonStats.Games.Should().Be(82);
        iguodala.SeasonStats.Minutes.Should().Be(3266);
        iguodala.SeasonStats.FieldGoalsMade.Should().Be(542);
        iguodala.SeasonStats.FieldGoalsAttempted.Should().Be(1147);
        iguodala.SeasonStats.ThreePointersMade.Should().Be(80);
        iguodala.SeasonStats.ThreePointersAttempted.Should().Be(261);
        iguodala.SeasonStats.FreeThrowsMade.Should().Be(377);
        iguodala.SeasonStats.FreeThrowsAttempted.Should().Be(521);
        iguodala.SeasonStats.OffensiveRebounds.Should().Be(92);
        iguodala.SeasonStats.Rebounds.Should().Be(471);
        iguodala.SeasonStats.Assists.Should().Be(434);
        iguodala.SeasonStats.Steals.Should().Be(131);
        iguodala.SeasonStats.Turnovers.Should().Be(222);
        iguodala.SeasonStats.Blocks.Should().Be(36);
        iguodala.SeasonStats.PersonalFouls.Should().Be(152);
        iguodala.Height.Should().Be(78); // 6'6" = 78 inches
        iguodala.Weight.Should().Be(207);
        iguodala.Team.Should().Be("phi");
    }

    [Fact]
    public void ImportHistorical_FilterByYear()
    {
        if (!TestDataExists) return;
        string path = FilePath("historical.csv");
        if (!File.Exists(path)) return;

        var all = CsvImportService.ImportHistorical(path);
        var year2011 = CsvImportService.ImportHistorical(path, filterYear: 2011);

        year2011.Should().HaveCountLessThan(all.Count, "filtered should be subset");
        year2011.Should().HaveCountGreaterThan(0, "should have 2011 players");
    }

    [Fact]
    public void ImportHistorical_ParsesHeightWeight()
    {
        if (!TestDataExists) return;
        string path = FilePath("default_08-09.csv");
        if (!File.Exists(path)) return;

        var players = CsvImportService.ImportHistorical(path, filterYear: 2009);

        foreach (var player in players.Take(10))
        {
            player.Height.Should().BeGreaterThan(60, $"{player.Name} height should be > 60 inches");
            player.Height.Should().BeLessThan(96, $"{player.Name} height should be < 96 inches");
            player.Weight.Should().BeGreaterThan(100, $"{player.Name} weight should be > 100 lbs");
        }
    }

    [Fact]
    public void ImportHistorical_SkipsInvalidPositions()
    {
        if (!TestDataExists) return;
        string path = FilePath("default_08-09.csv");
        if (!File.Exists(path)) return;

        var players = CsvImportService.ImportHistorical(path, filterYear: 2009);

        foreach (var player in players)
        {
            string pos = player.Position.Trim();
            pos.Should().BeOneOf("PG", "SG", "SF", "PF", "C",
                $"position '{player.Position}' for {player.Name} should be valid");
        }
    }

    [Fact]
    public void ImportHistorical_NamesAreUpperCase()
    {
        if (!TestDataExists) return;
        string path = FilePath("default_08-09.csv");
        if (!File.Exists(path)) return;

        var players = CsvImportService.ImportHistorical(path, filterYear: 2009);

        foreach (var player in players.Take(10))
        {
            player.Name.Should().Be(player.Name.ToUpperInvariant(),
                "historical format names should be upper-cased");
        }
    }

    // ─── Rookie format tests ─────────────────────────────────────

    [Fact]
    public void ImportRookies_09_ParsesPlayers()
    {
        if (!TestDataExists) return;
        string path = FilePath("rookies09.csv");
        if (!File.Exists(path)) return;

        var players = CsvImportService.ImportRookies(path);

        players.Should().HaveCountGreaterThan(0, "should import rookies");
    }

    [Fact]
    public void ImportRookies_09_ParsesBlakeGriffin()
    {
        if (!TestDataExists) return;
        string path = FilePath("rookies09.csv");
        if (!File.Exists(path)) return;

        var players = CsvImportService.ImportRookies(path);

        var griffin = players.FirstOrDefault(p => p.Name.Contains("Blake Griffin"));
        griffin.Should().NotBeNull("Blake Griffin should be in rookies09.csv");
        griffin!.Position.Should().Be("PF");
        griffin.Age.Should().Be(21);
        griffin.SeasonStats.Games.Should().Be(35);
        griffin.SeasonStats.FieldGoalsMade.Should().Be(300);
        griffin.SeasonStats.FieldGoalsAttempted.Should().Be(459);
        griffin.Height.Should().Be(82); // 6'10" = 82 inches
        griffin.Weight.Should().Be(251);
        griffin.Ratings.Potential1.Should().Be(5); // Talent
        griffin.Ratings.Potential2.Should().Be(3); // Skill
        griffin.Ratings.Effort.Should().Be(5);     // Intangibles
        griffin.Contract.IsRookie.Should().BeTrue();
    }

    [Fact]
    public void ImportRookies_AllHavePositiveStats()
    {
        if (!TestDataExists) return;
        string path = FilePath("rookies09.csv");
        if (!File.Exists(path)) return;

        var players = CsvImportService.ImportRookies(path);

        foreach (var player in players)
        {
            player.SeasonStats.Games.Should().BeGreaterThan(0);
            player.SeasonStats.Minutes.Should().BeGreaterThanOrEqualTo(96);
        }
    }

    // ─── Validation tests ────────────────────────────────────────

    [Fact]
    public void ImportHistorical_SkipsPlayersWithTooFewMinutes()
    {
        if (!TestDataExists) return;
        string path = FilePath("default_08-09.csv");
        if (!File.Exists(path)) return;

        var players = CsvImportService.ImportHistorical(path, filterYear: 2009);

        foreach (var player in players)
        {
            player.SeasonStats.Minutes.Should().BeGreaterThanOrEqualTo(96,
                $"{player.Name} should have at least 96 minutes");
        }
    }

    [Fact]
    public void ImportHistorical_MostPlayersHaveTeamAbbr()
    {
        if (!TestDataExists) return;
        string path = FilePath("default_08-09.csv");
        if (!File.Exists(path)) return;

        var players = CsvImportService.ImportHistorical(path, filterYear: 2009);

        // Some players (free agents) may have empty team abbreviation
        var withTeam = players.Where(p => !string.IsNullOrWhiteSpace(p.Team)).ToList();
        withTeam.Should().HaveCountGreaterThan(players.Count * 9 / 10,
            "the vast majority of players should have a team abbreviation");
    }
}
