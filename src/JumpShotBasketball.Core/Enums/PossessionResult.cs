namespace JumpShotBasketball.Core.Enums;

/// <summary>
/// Outcome of a possession attempt, mapping to C++ Result() return values.
/// </summary>
public enum PossessionResult
{
    TwoPointAttempt = 1,
    ThreePointAttempt = 2,
    Foul = 3,
    Turnover = 4,
    Injury = 5
}
