namespace JumpShotBasketball.Core.Models.Game;

/// <summary>
/// Depth chart entry for a single rotation slot.
/// Maps to C++ ROTATION struct: Chart, Priority, Minutes, InRotation.
/// </summary>
public class RotationEntry
{
    public int PlayerIndex { get; set; }
    public double Priority { get; set; }
    public int Minutes { get; set; }
    public bool InRotation { get; set; }
}
