namespace RogueLikeNet.Core.Generation;

/// <summary>
/// Deterministic PRNG using xoshiro256** algorithm.
/// Seeded per-chunk for reproducible generation. All integer operations.
/// </summary>
public class SeededRandom
{
    private ulong _s0, _s1, _s2, _s3;

    public SeededRandom(long seed)
    {
        // SplitMix64 to initialize state from a single seed
        ulong s = (ulong)seed;
        _s0 = SplitMix64(ref s);
        _s1 = SplitMix64(ref s);
        _s2 = SplitMix64(ref s);
        _s3 = SplitMix64(ref s);
    }

    private static ulong SplitMix64(ref ulong state)
    {
        ulong z = state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    public int Next()
    {
        ulong result = RotateLeft(_s1 * 5, 7) * 9;
        ulong t = _s1 << 17;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = RotateLeft(_s3, 45);

        return (int)(result >> 33); // positive int
    }

    /// <summary>Returns a value in [0, maxExclusive).</summary>
    public int Next(int maxExclusive)
    {
        if (maxExclusive <= 0) return 0;
        return Next() % maxExclusive;
    }

    public bool NextBool()
    {
        return (Next() & 1) == 0;
    }

    /// <summary>Returns a value in [min, maxExclusive).</summary>
    public int Next(int min, int maxExclusive)
    {
        if (maxExclusive <= min) return min;
        return min + Next(maxExclusive - min);
    }

    private static ulong RotateLeft(ulong x, int k)
        => (x << k) | (x >> (64 - k));
}
