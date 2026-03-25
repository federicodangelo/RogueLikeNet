namespace RogueLikeNet.Core.Generation;

/// <summary>
/// 2D Perlin noise generator. Produces continuous, deterministic values
/// for any world-space coordinate, ensuring seamless terrain across chunk boundaries.
/// </summary>
internal sealed class PerlinNoise
{
    private readonly byte[] _perm;

    // Gradients: unit vectors at 45-degree intervals
    private static readonly (double x, double y)[] Gradients =
    [
        (1, 0), (-1, 0), (0, 1), (0, -1),
        (1, 1), (-1, 1), (1, -1), (-1, -1),
    ];

    public PerlinNoise(long seed)
    {
        _perm = new byte[512];
        var rng = new SeededRandom(seed);
        // Fisher-Yates shuffle of 0..255
        for (int i = 0; i < 256; i++)
            _perm[i] = (byte)i;
        for (int i = 255; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (_perm[i], _perm[j]) = (_perm[j], _perm[i]);
        }
        // Duplicate for overflow-free indexing
        for (int i = 0; i < 256; i++)
            _perm[i + 256] = _perm[i];
    }

    /// <summary>
    /// Sample noise at world-space coordinates. Returns a value in roughly [-1, 1].
    /// </summary>
    public double Sample(double x, double y)
    {
        int xi = (int)Math.Floor(x);
        int yi = (int)Math.Floor(y);
        double xf = x - xi;
        double yf = y - yi;

        // Fade curves
        double u = Fade(xf);
        double v = Fade(yf);

        // Hash corners
        int aa = _perm[(_perm[xi & 255] + (yi & 255)) & 255];
        int ab = _perm[(_perm[xi & 255] + ((yi + 1) & 255)) & 255];
        int ba = _perm[(_perm[((xi + 1) & 255)] + (yi & 255)) & 255];
        int bb = _perm[(_perm[((xi + 1) & 255)] + ((yi + 1) & 255)) & 255];

        // Gradient dot products
        double g00 = Grad(aa, xf, yf);
        double g10 = Grad(ba, xf - 1, yf);
        double g01 = Grad(ab, xf, yf - 1);
        double g11 = Grad(bb, xf - 1, yf - 1);

        // Bilinear interpolation
        double x0 = Lerp(g00, g10, u);
        double x1 = Lerp(g01, g11, u);
        return Lerp(x0, x1, v);
    }

    /// <summary>
    /// Fractal Brownian Motion: sum of multiple octaves for natural terrain.
    /// </summary>
    public double FBM(double x, double y, int octaves, double lacunarity = 2.0, double persistence = 0.5)
    {
        double total = 0;
        double amplitude = 1;
        double frequency = 1;
        double maxAmplitude = 0;

        for (int i = 0; i < octaves; i++)
        {
            total += Sample(x * frequency, y * frequency) * amplitude;
            maxAmplitude += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return total / maxAmplitude; // Normalize to [-1, 1]
    }

    private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);
    private static double Lerp(double a, double b, double t) => a + t * (b - a);

    private static double Grad(int hash, double x, double y)
    {
        var g = Gradients[hash & 7];
        return g.x * x + g.y * y;
    }
}
