namespace Squiggly.Engine;

/// <summary>
/// SplitMix64 (Steele, Lea and Flood, 2014): a tiny, well-known generator whose output is fully
/// specified, so every runtime produces the same sequence. System.Random is deliberately not used:
/// its algorithm is an implementation detail that has changed between .NET versions.
/// </summary>
/// <remarks>
/// A <see cref="Prng"/> is a local value, never shared state. Game code creates one with
/// <see cref="For"/> from the game seed plus the tick (or another counter) at the moment it needs
/// randomness, so the same header always yields the same food cell, piece bag or mine layout.
/// </remarks>
public struct Prng
{
    private ulong _state;

    /// <summary>Creates a generator from a raw 64-bit state.</summary>
    public Prng(ulong state)
    {
        _state = state;
    }

    /// <summary>
    /// Creates a generator for one decision point. <paramref name="counter"/> is usually the tick;
    /// <paramref name="stream"/> separates unrelated uses of the same seed and counter.
    /// </summary>
    public static Prng For(uint seed, int counter, uint stream = 0) =>
        new((((ulong)seed << 32) | (uint)counter) ^ (stream * 0xD1B54A32D192ED03UL));

    /// <summary>Returns the next 64-bit output.</summary>
    public ulong NextUInt64()
    {
        var z = _state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Returns a value in [0, <paramref name="bound"/>), using the high 32 bits (multiply-shift).</summary>
    public int NextBelow(int bound) => (int)(((NextUInt64() >> 32) * (ulong)bound) >> 32);
}
