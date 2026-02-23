namespace JumpShotBasketball.Core.Models.Player;

/// <summary>
/// Core player entity composing sub-objects for stats, ratings, contract, etc.
/// Maps to C++ CPlayer (~150 flat members decomposed into focused classes).
/// </summary>
public class Player
{
    // Identity
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string Team { get; set; } = string.Empty;
    public int TeamIndex { get; set; }
    public int Number { get; set; } = 1;
    public int Age { get; set; }
    public int Height { get; set; }
    public int Weight { get; set; }

    // Status
    public bool Retired { get; set; }
    public bool Active { get; set; }
    public bool OnBlock { get; set; }
    public int Starter { get; set; }
    public int Health { get; set; } = 100;
    public int Injury { get; set; }
    public string InjuryDescription { get; set; } = string.Empty;
    public int Content { get; set; }
    public int Better { get; set; }

    // Position rotation eligibility
    public bool PgRotation { get; set; }
    public bool SgRotation { get; set; }
    public bool SfRotation { get; set; }
    public bool PfRotation { get; set; }
    public bool CRotation { get; set; }

    // Game plan settings (m_game_minutes, m_offensive_focus, etc.)
    public int GameMinutes { get; set; }
    public int OffensiveFocus { get; set; }
    public int DefensiveFocus { get; set; }
    public int OffensiveIntensity { get; set; }
    public int DefensiveIntensity { get; set; }
    public int PlayMaker { get; set; }

    // Draft info
    public int RoundSelected { get; set; }
    public int PickSelected { get; set; }
    public int OriginalNumber { get; set; }
    public int OriginalPlayoffNumber { get; set; }
    public int OriginalSlot { get; set; }
    public int PlayerPointer { get; set; }

    // All-star and special event flags
    public int AllStar { get; set; }
    public int RookieStar { get; set; }
    public int ThreePointContest { get; set; }
    public int DunkContest { get; set; }
    public int AllStarIndex { get; set; }
    public int RookieStarIndex { get; set; }
    public int ThreePointContestIndex { get; set; }
    public int DunkContestIndex { get; set; }

    // 3pt/dunk contest scores (4 rounds)
    public int[] ThreePointScores { get; set; } = new int[4];
    public int[] DunkScores { get; set; } = new int[4];

    // Composed sub-objects (replacing 150+ flat fields)
    public PlayerStatLine SeasonStats { get; set; } = new();
    public PlayerStatLine SimulatedStats { get; set; } = new();
    public PlayerStatLine CareerStats { get; set; } = new();
    public PlayerStatLine PlayoffStats { get; set; } = new();
    public PlayerRatings Ratings { get; set; } = new();
    public PlayerContract Contract { get; set; } = new();
    public PlayerGameState GameState { get; set; } = new();
    public PlayerSeasonHighs SeasonHighs { get; set; } = new();
}
