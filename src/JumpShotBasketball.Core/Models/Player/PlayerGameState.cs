namespace JumpShotBasketball.Core.Models.Player;

/// <summary>
/// Transient per-game state for a player during simulation.
/// Maps to CPlayer's m_game_* fields.
/// </summary>
public class PlayerGameState
{
    public string GameName { get; set; } = string.Empty;
    public int GameInjury { get; set; }
    public int Minutes { get; set; }
    public int FieldGoalsMade { get; set; }
    public int FieldGoalsAttempted { get; set; }
    public int FreeThrowsMade { get; set; }
    public int FreeThrowsAttempted { get; set; }
    public int ThreePointersMade { get; set; }
    public int ThreePointersAttempted { get; set; }
    public int OffensiveRebounds { get; set; }
    public int DefensiveRebounds { get; set; }
    public int Assists { get; set; }
    public int Steals { get; set; }
    public int Turnovers { get; set; }
    public int Blocks { get; set; }
    public int PersonalFouls { get; set; }
    public int CurrentStamina { get; set; }
    public int StatSlot { get; set; }
    public int PlayerPointer { get; set; }

    public int Points => (FieldGoalsMade * 2) + FreeThrowsMade + ThreePointersMade;
    public int TotalRebounds => OffensiveRebounds + DefensiveRebounds;

    public void Reset()
    {
        GameName = string.Empty;
        GameInjury = 0;
        Minutes = 0;
        FieldGoalsMade = 0;
        FieldGoalsAttempted = 0;
        FreeThrowsMade = 0;
        FreeThrowsAttempted = 0;
        ThreePointersMade = 0;
        ThreePointersAttempted = 0;
        OffensiveRebounds = 0;
        DefensiveRebounds = 0;
        Assists = 0;
        Steals = 0;
        Turnovers = 0;
        Blocks = 0;
        PersonalFouls = 0;
        CurrentStamina = 0;
        StatSlot = 0;
        PlayerPointer = 0;
    }
}
