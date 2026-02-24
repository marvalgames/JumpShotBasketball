namespace JumpShotBasketball.Core.Models.Player;

/// <summary>
/// Player skill ratings and per-48-minute production rates.
/// Maps to CPlayer's m_trueRating, m_outside, m_driving, m_post, etc.
/// Also includes projection ratings (m_prFga, m_prFgp, etc.).
/// </summary>
public class PlayerRatings
{
    // Core ratings (m_trueRating, m_outside, m_driving, etc.)
    public double TrueRating { get; set; }
    public double TrueRatingSimple { get; set; }
    public double TradeTrueRating { get; set; }
    public double TradeValue { get; set; }
    public double TeammatesBetterRating { get; set; }
    public double Mvp { get; set; }

    // Offensive skill ratings
    public double Outside { get; set; }
    public double Driving { get; set; }
    public double Post { get; set; }
    public double Transition { get; set; }

    // Defensive skill ratings
    public double Defense { get; set; }
    public double OutsideDefense { get; set; }
    public double DrivingDefense { get; set; }
    public double PostDefense { get; set; }
    public double TransitionDefense { get; set; }

    // Per-48-minute offensive rates
    public double OutsidePer48Min { get; set; }
    public double DrivingPer48Min { get; set; }
    public double PostPer48Min { get; set; }
    public double TransitionPer48Min { get; set; }

    // Per-48-minute defensive rates
    public double OutsideDefensePer48Min { get; set; }
    public double DrivingDefensePer48Min { get; set; }
    public double PostDefensePer48Min { get; set; }
    public double TransitionDefensePer48Min { get; set; }

    public double AstToRatio { get; set; }

    // Movement/penetration raw ratings (m_movoff, m_movdef, m_penoff, m_pendef, m_postoff, m_postdef, m_transoff, m_transdef)
    public int MovementOffenseRaw { get; set; }
    public int MovementDefenseRaw { get; set; }
    public int PenetrationOffenseRaw { get; set; }
    public int PenetrationDefenseRaw { get; set; }
    public int PostOffenseRaw { get; set; }
    public int PostDefenseRaw { get; set; }
    public int TransitionOffenseRaw { get; set; }
    public int TransitionDefenseRaw { get; set; }

    // Production per-48-minute (m_FgaPer48Min, etc.)
    public double MinutesPerGame { get; set; }
    public int FieldGoalPercentage { get; set; }
    public int AdjustedFieldGoalPercentage { get; set; }
    public int FreeThrowPercentage { get; set; }
    public int ThreePointPercentage { get; set; }
    public double FieldGoalsAttemptedPer48Min { get; set; }
    public double AdjustedFieldGoalsAttemptedPer48Min { get; set; }
    public double CoachFieldGoalsAttemptedPer48Min { get; set; }
    public double ThreePointersAttemptedPer48Min { get; set; }
    public double AdjustedThreePointersAttemptedPer48Min { get; set; }
    public double CoachThreePointersAttemptedPer48Min { get; set; }
    public double FoulsDrawnPer48Min { get; set; }
    public double AdjustedFoulsDrawnPer48Min { get; set; }
    public double OffensiveReboundsPer48Min { get; set; }
    public double DefensiveReboundsPer48Min { get; set; }
    public double AssistsPer48Min { get; set; }
    public double StealsPer48Min { get; set; }
    public double TurnoversPer48Min { get; set; }
    public double AdjustedTurnoversPer48Min { get; set; }
    public double BlocksPer48Min { get; set; }
    public double PersonalFoulsPer48Min { get; set; }

    // Original adjusted values (saved for reset/comparison)
    public double OriginalAdjustedFoulsDrawnPer48Min { get; set; }
    public double OriginalAdjustedFieldGoalsAttemptedPer48Min { get; set; }
    public double OriginalAdjustedThreePointersAttemptedPer48Min { get; set; }
    public double OriginalStealsPer48Min { get; set; }
    public double OriginalBlocksPer48Min { get; set; }
    public double OriginalAdjustedTurnoversPer48Min { get; set; }
    public double OriginalPersonalFoulsPer48Min { get; set; }
    public double OriginalAssistsPer48Min { get; set; }
    public double OriginalOffensiveReboundsPer48Min { get; set; }
    public double OriginalDefensiveReboundsPer48Min { get; set; }

    // Projection ratings (m_prFga, m_prFgp, etc.)
    public int ProjectionFieldGoalsAttempted { get; set; }
    public int ProjectionFieldGoalPercentage { get; set; }
    public int ProjectionFreeThrowsAttempted { get; set; }
    public int ProjectionFreeThrowPercentage { get; set; }
    public int ProjectionThreePointersAttempted { get; set; }
    public int ProjectionThreePointPercentage { get; set; }
    public int ProjectionOffensiveRebounds { get; set; }
    public int ProjectionDefensiveRebounds { get; set; }
    public int ProjectionAssists { get; set; }
    public int ProjectionSteals { get; set; }
    public int ProjectionTurnovers { get; set; }
    public int ProjectionBlocks { get; set; }

    // Adjusted projection ratings (m_adjPrFga, etc.)
    public int AdjustedProjectionFieldGoalsAttempted { get; set; }
    public int AdjustedProjectionFieldGoalPercentage { get; set; }
    public int AdjustedProjectionFreeThrowsAttempted { get; set; }
    public int AdjustedProjectionFreeThrowPercentage { get; set; }
    public int AdjustedProjectionThreePointersAttempted { get; set; }
    public int AdjustedProjectionThreePointPercentage { get; set; }
    public int AdjustedProjectionOffensiveRebounds { get; set; }
    public int AdjustedProjectionDefensiveRebounds { get; set; }
    public int AdjustedProjectionAssists { get; set; }
    public int AdjustedProjectionSteals { get; set; }
    public int AdjustedProjectionTurnovers { get; set; }
    public int AdjustedProjectionBlocks { get; set; }
    public int AdjustedPotential1 { get; set; }
    public int AdjustedPotential2 { get; set; }
    public int AdjustedEffort { get; set; }
    public int AdjustedPrime { get; set; }

    // Potential and development (m_pot1, m_pot2, m_effort, m_prime)
    public int Potential1 { get; set; }
    public int Potential2 { get; set; }
    public int Effort { get; set; }
    public int Prime { get; set; }

    // Physical attributes
    public int InjuryRating { get; set; }
    public int Stamina { get; set; }
    public int Clutch { get; set; }
    public int Consistency { get; set; }
}
