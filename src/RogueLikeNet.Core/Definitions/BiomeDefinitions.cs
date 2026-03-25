namespace RogueLikeNet.Core.Definitions;

public enum BiomeType
{
    Stone,
    Lava,
    Ice,
    Forest,
    Arcane,
}

/// <summary>
/// Biome palette data and tinting logic. Used by the generator to bake biome colors into tiles.
/// </summary>
public static class BiomeDefinitions
{
    // RGB multipliers as percentages (100 = no change)
    private static readonly (int r, int g, int b)[] Palettes =
    [
        (100, 100, 100), // Stone — neutral
        (110, 85, 75),   // Lava — warm red/orange
        (80, 95, 115),   // Ice — cool blue
        (85, 110, 80),   // Forest — green
        (90, 80, 110),   // Arcane — purple
    ];

    private static readonly string[] Names = ["Stone", "Lava", "Ice", "Forest", "Arcane"];

    /// <summary>
    /// Deterministically picks a biome for the given chunk coordinates and world seed.
    /// </summary>
    public static BiomeType GetBiomeForChunk(int chunkX, int chunkY, long seed)
    {
        long hash = chunkX * 73856093L ^ chunkY * 19349663L ^ seed * 0x27BB2EE687B0B0FDL;
        int idx = (int)((hash & 0x7FFFFFFFL) % Palettes.Length);
        return (BiomeType)idx;
    }

    /// <summary>
    /// Returns the display name for a biome type.
    /// </summary>
    public static string GetBiomeName(BiomeType biome) => Names[(int)biome];

    /// <summary>
    /// Applies the biome tint to a packed 0xRRGGBB color, returning a new packed color.
    /// </summary>
    public static int ApplyBiomeTint(int packedRgb, BiomeType biome)
    {
        if (packedRgb == 0) return 0;
        var (rr, gg, bb) = Palettes[(int)biome];
        int r = Math.Clamp(((packedRgb >> 16) & 0xFF) * rr / 100, 0, 255);
        int g = Math.Clamp(((packedRgb >> 8) & 0xFF) * gg / 100, 0, 255);
        int b = Math.Clamp((packedRgb & 0xFF) * bb / 100, 0, 255);
        return (r << 16) | (g << 8) | b;
    }
}
