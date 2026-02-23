using FluentAssertions;
using JumpShotBasketball.Core.Services;

namespace JumpShotBasketball.Core.Tests;

/// <summary>
/// Tests LegacyFileReader and LegacyConverter against actual legacy data files
/// from jsb599/jsb 599 release files/.
/// </summary>
public class LegacyFileReaderTests
{
    // Path to the legacy data files — configurable via environment variable
    private static readonly string TestDataPath =
        Environment.GetEnvironmentVariable("JSB_TEST_DATA_PATH")
        ?? "/mnt/c/JSB/BBall/jsb599/jsb 599 release files";

    private static string BasePath(string name) => Path.Combine(TestDataPath, name);
    private static bool TestDataExists => Directory.Exists(TestDataPath);

    // ─── .lge file tests ──────────────────────────────────────────

    [Fact]
    public void ReadLge_08_09_Parses30Teams()
    {
        if (!TestDataExists) return;

        var (settings, teamNames, _) = LegacyFileReader.ReadLgeFile(BasePath("default_08-09.lge"));

        settings.NumberOfTeams.Should().Be(30);
        teamNames.Should().HaveCount(30);
    }

    [Fact]
    public void ReadLge_08_09_ParsesYear()
    {
        if (!TestDataExists) return;

        var (settings, _, _) = LegacyFileReader.ReadLgeFile(BasePath("default_08-09.lge"));

        settings.CurrentYear.Should().Be(2008);
    }

    [Fact]
    public void ReadLge_08_09_ParsesTeamNames()
    {
        if (!TestDataExists) return;

        var (_, teamNames, _) = LegacyFileReader.ReadLgeFile(BasePath("default_08-09.lge"));

        teamNames[0].Should().Be("76ers");
        teamNames[1].Should().Be("Celtics");
    }

    [Fact]
    public void ReadLge_08_09_ParsesPlayoffFormat()
    {
        if (!TestDataExists) return;

        var (settings, _, _) = LegacyFileReader.ReadLgeFile(BasePath("default_08-09.lge"));

        settings.PlayoffFormat.Should().Contain("8 teams per conference");
        settings.Round1Format.Should().Be("4 of 7");
    }

    [Fact]
    public void ReadLge_08_09_ParsesConferenceNames()
    {
        if (!TestDataExists) return;

        var (settings, _, _) = LegacyFileReader.ReadLgeFile(BasePath("default_08-09.lge"));

        settings.ConferenceName1.Should().Be("Eastern");
        settings.ConferenceName2.Should().Be("Western");
    }

    [Fact]
    public void ReadLge_08_09_ParsesDivisionNames()
    {
        if (!TestDataExists) return;

        var (settings, _, _) = LegacyFileReader.ReadLgeFile(BasePath("default_08-09.lge"));

        settings.DivisionName1.Should().Be("Atlantic");
        settings.DivisionName2.Should().Be("Central");
        settings.DivisionName3.Should().Be("Midwest");
        settings.DivisionName4.Should().Be("Pacific");
    }

    // ─── .plr file tests ─────────────────────────────────────────

    [Fact]
    public void ReadPlr_08_09_ParsesTeamRosters()
    {
        if (!TestDataExists) return;

        var (rosters, _) = LegacyFileReader.ReadPlrFile(BasePath("default_08-09.plr"), 30);

        rosters.Should().HaveCount(30);
        rosters[0].Should().HaveCountGreaterThan(0, "76ers should have players");
    }

    [Fact]
    public void ReadPlr_08_09_ParsesAndreMiller()
    {
        if (!TestDataExists) return;

        var (rosters, _) = LegacyFileReader.ReadPlrFile(BasePath("default_08-09.plr"), 30);

        // Andre Miller is the first named player on team 0 (76ers)
        var miller = rosters[0].FirstOrDefault(p => p.Name.Contains("Andre Miller"));
        miller.Should().NotBeNull("Andre Miller should be on the 76ers roster");
        miller!.Age.Should().Be(32);
        miller.Position.Should().Be("PG");
        miller.SeasonStats.Games.Should().Be(82);
        miller.SeasonStats.Minutes.Should().Be(2975);
        miller.SeasonStats.FieldGoalsMade.Should().Be(492);
        miller.SeasonStats.FieldGoalsAttempted.Should().Be(1041);
        miller.SeasonStats.Assists.Should().Be(533);
    }

    [Fact]
    public void ReadPlr_08_09_ParsesIguodala()
    {
        if (!TestDataExists) return;

        var (rosters, _) = LegacyFileReader.ReadPlrFile(BasePath("default_08-09.plr"), 30);

        var iguodala = rosters[0].FirstOrDefault(p => p.Name.Contains("Iguodala"));
        iguodala.Should().NotBeNull();
        iguodala!.Age.Should().Be(24);
        iguodala.Position.Should().Be("SF");
        iguodala.SeasonStats.Games.Should().Be(82);
        iguodala.SeasonStats.Minutes.Should().Be(3266);
        iguodala.SeasonStats.FieldGoalsMade.Should().Be(542);
        iguodala.SeasonStats.FieldGoalsAttempted.Should().Be(1147);
        iguodala.SeasonStats.FreeThrowsMade.Should().Be(377);
        iguodala.SeasonStats.FreeThrowsAttempted.Should().Be(521);
        iguodala.SeasonStats.ThreePointersMade.Should().Be(80);
        iguodala.SeasonStats.ThreePointersAttempted.Should().Be(261);
        iguodala.SeasonStats.OffensiveRebounds.Should().Be(92);
        iguodala.SeasonStats.Rebounds.Should().Be(471);
        iguodala.SeasonStats.Assists.Should().Be(434);
        iguodala.SeasonStats.Steals.Should().Be(131);
        iguodala.SeasonStats.Turnovers.Should().Be(222);
        iguodala.SeasonStats.Blocks.Should().Be(36);
        iguodala.SeasonStats.PersonalFouls.Should().Be(152);

        // Movement ratings
        iguodala.Ratings.MovementOffenseRaw.Should().Be(5);
        iguodala.Ratings.MovementDefenseRaw.Should().Be(7);

        // Height/weight
        iguodala.Height.Should().Be(78); // 6'6"
        iguodala.Weight.Should().Be(207);
    }

    [Fact]
    public void ReadPlr_08_09_ParsesCelticsRoster()
    {
        if (!TestDataExists) return;

        var (rosters, _) = LegacyFileReader.ReadPlrFile(BasePath("default_08-09.plr"), 30);

        // Celtics are team 1 in the lge file
        var celtics = rosters[1];
        celtics.Should().HaveCountGreaterThan(0, "Celtics should have players");

        var rondo = celtics.FirstOrDefault(p => p.Name.Contains("Rondo"));
        rondo.Should().NotBeNull("Rajon Rondo should be on the Celtics roster");
        rondo!.Position.Should().Be("PG");
    }

    [Fact]
    public void ReadPlr_08_09_ParsesFreeAgents()
    {
        if (!TestDataExists) return;

        var (_, freeAgents) = LegacyFileReader.ReadPlrFile(BasePath("default_08-09.plr"), 30);

        freeAgents.Should().HaveCountGreaterThan(0, "should have free agents");

        // Jamaal Tinsley is a known FA
        var tinsley = freeAgents.FirstOrDefault(p => p.Name.Contains("Tinsley"));
        tinsley.Should().NotBeNull();
    }

    [Fact]
    public void ReadPlr_08_09_MovementRatingsArePositive()
    {
        if (!TestDataExists) return;

        var (rosters, _) = LegacyFileReader.ReadPlrFile(BasePath("default_08-09.plr"), 30);

        foreach (var player in rosters[0])
        {
            player.Ratings.MovementOffenseRaw.Should().BeGreaterThan(0);
            player.Ratings.MovementDefenseRaw.Should().BeGreaterThan(0);
        }
    }

    // ─── .sch file tests ─────────────────────────────────────────

    [Fact]
    public void ReadSch_08_09_ParsesGames()
    {
        if (!TestDataExists) return;

        var games = LegacyFileReader.ReadSchFile(BasePath("default_08-09.sch"));

        games.Should().HaveCountGreaterThan(0, "should have scheduled games");
    }

    [Fact]
    public void ReadSch_08_09_GamesHaveValidTeamIndices()
    {
        if (!TestDataExists) return;

        var games = LegacyFileReader.ReadSchFile(BasePath("default_08-09.sch"));

        foreach (var game in games)
        {
            game.HomeTeamIndex.Should().BeGreaterThan(0);
            game.VisitorTeamIndex.Should().BeGreaterThan(0);
            game.HomeTeamIndex.Should().BeLessThanOrEqualTo(30);
            game.VisitorTeamIndex.Should().BeLessThanOrEqualTo(30);
        }
    }

    // ─── .frn file tests ─────────────────────────────────────────

    [Fact]
    public void ReadFrn_08_09_ParsesCityNames()
    {
        if (!TestDataExists) return;

        var financials = LegacyFileReader.ReadFrnFile(BasePath("default_08-09.frn"), 30);

        financials.Should().HaveCount(30);
        // Verify some known city names exist
        var cities = financials.Select(f => f.CityName).ToList();
        cities.Should().Contain(c => c.Contains("Philadelphia") || c.Contains("Phila"));
    }

    [Fact]
    public void ReadFrn_08_09_HasPositiveCapacity()
    {
        if (!TestDataExists) return;

        var financials = LegacyFileReader.ReadFrnFile(BasePath("default_08-09.frn"), 30);

        foreach (var fin in financials)
        {
            fin.Capacity.Should().BeGreaterThan(0, $"team '{fin.CityName}' should have arena capacity");
        }
    }

    // ─── .bud file tests ─────────────────────────────────────────

    [Fact]
    public void ReadBud_20_21_UpdatesFinancials()
    {
        if (!TestDataExists) return;

        string budPath = BasePath("default_20-21.bud");
        if (!File.Exists(budPath)) return;

        var financials = LegacyFileReader.ReadFrnFile(BasePath("default_20-21.frn"), 30);
        LegacyFileReader.ReadBudFile(budPath, financials);

        // Start leagues have zeroed budget data — just verify parsing doesn't crash
        // and that all 30 financials are still present
        financials.Should().HaveCount(30, "all teams should still have financial data after .bud read");
    }

    // ─── LegacyConverter tests ───────────────────────────────────

    [Fact]
    public void Converter_08_09_ProducesValidLeague()
    {
        if (!TestDataExists) return;

        var league = LegacyConverter.ConvertFromLegacyFiles(BasePath("default_08-09"));

        league.Settings.NumberOfTeams.Should().Be(30);
        league.Settings.CurrentYear.Should().Be(2008);

        // Should have 30 real teams + possibly FA team
        league.Teams.Where(t => t.Id > 0).Should().HaveCount(30);

        // Each team should have players
        foreach (var team in league.Teams.Where(t => t.Id > 0))
        {
            team.Roster.Should().HaveCountGreaterThan(0,
                $"team '{team.Name}' should have players");
        }
    }

    [Fact]
    public void Converter_08_09_RoundTrip()
    {
        if (!TestDataExists) return;

        var league = LegacyConverter.ConvertFromLegacyFiles(BasePath("default_08-09"));

        // Save to JSON and reload
        string json = SaveLoadService.SerializeToJson(league);
        var reloaded = SaveLoadService.DeserializeFromJson(json);

        reloaded.Settings.NumberOfTeams.Should().Be(30);
        reloaded.Teams.Where(t => t.Id > 0).Should().HaveCount(30);

        // Verify a specific player survives round-trip
        var team0 = reloaded.Teams.First(t => t.Id == 1);
        var iguodala = team0.Roster.FirstOrDefault(p => p.Name.Contains("Iguodala"));
        iguodala.Should().NotBeNull();
        iguodala!.SeasonStats.FieldGoalsMade.Should().Be(542);
    }

    [Fact]
    public void Converter_20_21_FullLeague()
    {
        if (!TestDataExists) return;

        string budPath = BasePath("default_20-21.bud");
        if (!File.Exists(budPath)) return;

        var league = LegacyConverter.ConvertFromLegacyFiles(BasePath("default_20-21"));

        league.Settings.NumberOfTeams.Should().Be(30);
        league.Teams.Where(t => t.Id > 0).Should().HaveCount(30);

        // Each team should have players
        foreach (var team in league.Teams.Where(t => t.Id > 0))
        {
            team.Roster.Should().HaveCountGreaterThan(0,
                $"team '{team.Name}' should have players");
        }
    }

    [Fact]
    public void Converter_2003Start_MinimalFiles()
    {
        if (!TestDataExists) return;

        // Start leagues have only .plr, .lge, .sch, .frn
        var league = LegacyConverter.ConvertFromLegacyFiles(BasePath("default2003start"));

        league.Settings.NumberOfTeams.Should().BeGreaterThan(0);
        league.Teams.Where(t => t.Id > 0).Should().HaveCountGreaterThan(0);
    }
}
