using JumpShotBasketball.Core.Constants;

namespace JumpShotBasketball.Core.Models.Team;

/// <summary>
/// Team standings and record data.
/// Maps to C++ CRecord nearly 1:1.
/// </summary>
public class TeamRecord
{
    public string TeamName { get; set; } = string.Empty;
    public string Control { get; set; } = string.Empty;
    public string Conference { get; set; } = string.Empty;
    public string Division { get; set; } = string.Empty;
    public string CityName { get; set; } = string.Empty;

    public int LeagueRecord { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int DivisionRecord { get; set; }
    public int ConferenceRecord { get; set; }
    public int HomeRecord { get; set; }
    public int VisitorRecord { get; set; }

    public double LeaguePercentage { get; set; }
    public double DivisionPercentage { get; set; }
    public double ConferencePercentage { get; set; }
    public int LeagueGamesBack { get; set; }

    public int InitialNumber { get; set; }
    public int Points { get; set; }

    // Head-to-head records (replaces m_vsOpponent[33] fixed array)
    public Dictionary<int, int> VsOpponent { get; set; } = new();
    public Dictionary<int, double> VsOpponentPercentage { get; set; } = new();

    // Playoff flags
    public bool IsPlayoffTeam { get; set; }
    public bool IsDivisionChamps { get; set; }
    public bool InPlayoffs { get; set; }
    public bool HasRing { get; set; }
    public int PlayoffSeed { get; set; }
}
