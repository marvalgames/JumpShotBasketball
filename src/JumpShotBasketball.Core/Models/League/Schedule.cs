using JumpShotBasketball.Core.Enums;

namespace JumpShotBasketball.Core.Models.League;

/// <summary>
/// League schedule composed of structured game entries.
/// Replaces the C++ 3D array m_schedule[13][32][17] with a typed list.
/// </summary>
public class Schedule
{
    public List<ScheduledGame> Games { get; set; } = new();
    public bool SeasonStarted { get; set; }
    public bool RegularSeasonEnded { get; set; }
    public bool PlayoffsStarted { get; set; }
    public int GamesInSeason { get; set; } = 82;
}

/// <summary>
/// A single scheduled game between two teams.
/// </summary>
public class ScheduledGame
{
    public int GameNumber { get; set; }
    public int Week { get; set; }
    public int Day { get; set; }
    public int HomeTeamIndex { get; set; }
    public int VisitorTeamIndex { get; set; }
    public GameType Type { get; set; } = GameType.League;
    public bool Played { get; set; }
    public int HomeScore { get; set; }
    public int VisitorScore { get; set; }
}
