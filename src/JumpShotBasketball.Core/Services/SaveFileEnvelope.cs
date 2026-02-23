using JumpShotBasketball.Core.Models.League;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Versioned wrapper for save files. Contains metadata alongside the League data.
/// </summary>
public class SaveFileEnvelope
{
    public int Version { get; set; } = 1;
    public DateTime SavedAt { get; set; }
    public League Data { get; set; } = new();
}
