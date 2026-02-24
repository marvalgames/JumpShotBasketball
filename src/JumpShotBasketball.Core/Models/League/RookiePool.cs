using JumpShotBasketball.Core.Models.Player;

namespace JumpShotBasketball.Core.Models.League;

/// <summary>
/// Container for generated rookies awaiting the draft.
/// Created by RookieGenerationService, consumed by draft execution.
/// </summary>
public class RookiePool
{
    public List<Player.Player> Rookies { get; set; } = new();
    public int Year { get; set; }
}
