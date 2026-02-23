namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Single source of randomness, replacing the 8+ copies of IntRandom()
/// scattered across C++ classes (CPlayer, CStaff, CTeamFinancial, etc.).
/// Thread-safe via Random.Shared (.NET 6+).
/// </summary>
public static class RandomProvider
{
    /// <summary>
    /// Returns a random integer from 1 to n (inclusive).
    /// Matches the C++ IntRandom(n) behavior: (rand() / (RAND_MAX+1)) * n + 1.
    /// </summary>
    public static int IntRandom(int n)
    {
        return Random.Shared.Next(1, n + 1);
    }

    /// <summary>
    /// Returns a random double from 0.0 (inclusive) to n (exclusive).
    /// Matches the C++ Random(double n) behavior.
    /// </summary>
    public static double NextDouble(double n)
    {
        return Random.Shared.NextDouble() * n;
    }

    /// <summary>
    /// Returns a random integer from 0 to max-1 (inclusive).
    /// </summary>
    public static int Next(int max)
    {
        return Random.Shared.Next(max);
    }

    /// <summary>
    /// Returns a random integer from min (inclusive) to max (exclusive).
    /// </summary>
    public static int Next(int min, int max)
    {
        return Random.Shared.Next(min, max);
    }
}
