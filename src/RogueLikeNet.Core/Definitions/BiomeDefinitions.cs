namespace RogueLikeNet.Core.Definitions;

public enum BiomeType
{
    Stone,
    Lava,
    Ice,
    Forest,
    Arcane,
    Crypt,
    Sewer,
    Fungal,
    Ruined,
    Infernal,
}

/// <summary>
/// Biome palette data, decoration tables, and tinting logic.
/// Used by the generator to bake biome colors and features into tiles.
/// </summary>
public static class BiomeDefinitions
{
    public const int BiomeCount = 10;

    // RGB multipliers as percentages (100 = no change)
    private static readonly (int r, int g, int b)[] Palettes =
    [
        (100, 100, 100), // Stone — neutral
        (110, 85, 75),   // Lava — warm red/orange
        (80, 95, 115),   // Ice — cool blue
        (85, 110, 80),   // Forest — green
        (90, 80, 110),   // Arcane — purple
        (95, 90, 85),    // Crypt — desaturated brown
        (85, 95, 90),    // Sewer — murky green-gray
        (95, 85, 105),   // Fungal — pink-purple
        (105, 95, 85),   // Ruined — warm gray
        (115, 80, 70),   // Infernal — deep red
    ];

    private static readonly string[] Names =
        ["Stone", "Lava", "Ice", "Forest", "Arcane", "Crypt", "Sewer", "Fungal", "Ruined", "Infernal"];

    /// <summary>Decoration entry: glyph, fg color, chance (percent per floor tile).</summary>
    public readonly record struct DecorationDef(int GlyphId, int FgColor, int Chance);

    /// <summary>Liquid feature: tile type, glyph, fg/bg colors, room fill chance (%).</summary>
    public readonly record struct LiquidDef(World.TileType Type, int GlyphId, int FgColor, int BgColor, int RoomChance);

    // Per-biome decoration tables
    private static readonly DecorationDef[][] Decorations =
    [
        // Stone — sparse rubble and pillars
        [new(TileDefinitions.GlyphRubble, TileDefinitions.ColorRubbleFg, 2),
         new(TileDefinitions.GlyphPillar, TileDefinitions.ColorStatueFg, 1)],

        // Lava — embers and cracks
        [new(TileDefinitions.GlyphEmber, TileDefinitions.ColorEmberFg, 4),
         new(TileDefinitions.GlyphCrack, TileDefinitions.ColorCrackFg, 3)],

        // Ice — icicles and frost cracks
        [new(TileDefinitions.GlyphIcicle, TileDefinitions.ColorIceFg, 4),
         new(TileDefinitions.GlyphCrack, TileDefinitions.ColorIceFg, 2)],

        // Forest — grass, moss, and vines
        [new(TileDefinitions.GlyphGrass, TileDefinitions.ColorGrassFg, 6),
         new(TileDefinitions.GlyphMoss, TileDefinitions.ColorMossFg, 4),
         new(TileDefinitions.GlyphVines, TileDefinitions.ColorVinesFg, 2)],

        // Arcane — runes and crystals
        [new(TileDefinitions.GlyphRune, TileDefinitions.ColorRuneFg, 3),
         new(TileDefinitions.GlyphCrystal, TileDefinitions.ColorCrystalFg, 3)],

        // Crypt — bones, coffins, and webs
        [new(TileDefinitions.GlyphBones, TileDefinitions.ColorBonesFg, 5),
         new(TileDefinitions.GlyphCoffin, TileDefinitions.ColorBonesFg, 2),
         new(TileDefinitions.GlyphWeb, TileDefinitions.ColorWebFg, 3)],

        // Sewer — moss and barrels
        [new(TileDefinitions.GlyphMoss, TileDefinitions.ColorMossFg, 4),
         new(TileDefinitions.GlyphBarrel, TileDefinitions.ColorBarrelFg, 2),
         new(TileDefinitions.GlyphCrack, TileDefinitions.ColorRubbleFg, 2)],

        // Fungal — mushrooms and moss
        [new(TileDefinitions.GlyphMushroom, TileDefinitions.ColorMushroomFg, 6),
         new(TileDefinitions.GlyphMoss, TileDefinitions.ColorMossFg, 3),
         new(TileDefinitions.GlyphGrass, TileDefinitions.ColorVinesFg, 2)],

        // Ruined — rubble, pillars, and statues
        [new(TileDefinitions.GlyphRubble, TileDefinitions.ColorRubbleFg, 5),
         new(TileDefinitions.GlyphPillar, TileDefinitions.ColorStatueFg, 2),
         new(TileDefinitions.GlyphStatue, TileDefinitions.ColorStatueFg, 1)],

        // Infernal — embers, cracks, and bones
        [new(TileDefinitions.GlyphEmber, TileDefinitions.ColorEmberFg, 5),
         new(TileDefinitions.GlyphCrack, TileDefinitions.ColorCrackFg, 3),
         new(TileDefinitions.GlyphBones, TileDefinitions.ColorBonesFg, 2)],
    ];

    // Per-biome liquid features (null = no liquid in this biome)
    private static readonly LiquidDef?[] Liquids =
    [
        null, // Stone — no liquid
        new(World.TileType.Lava, TileDefinitions.GlyphLava, TileDefinitions.ColorLavaFg, TileDefinitions.ColorLavaBg, 30), // Lava
        new(World.TileType.Water, TileDefinitions.GlyphWater, TileDefinitions.ColorIceFg, TileDefinitions.ColorIceBg, 25), // Ice — frozen pools
        new(World.TileType.Water, TileDefinitions.GlyphWater, TileDefinitions.ColorWaterFg, TileDefinitions.ColorWaterBg, 20), // Forest — streams
        null, // Arcane — no liquid
        null, // Crypt — no liquid
        new(World.TileType.Water, TileDefinitions.GlyphWater, TileDefinitions.ColorWaterFg, TileDefinitions.ColorWaterBg, 40), // Sewer — flooded tunnels
        new(World.TileType.Water, TileDefinitions.GlyphWater, TileDefinitions.ColorMossFg, TileDefinitions.ColorWaterBg, 15), // Fungal — toxic pools
        null, // Ruined — no liquid
        new(World.TileType.Lava, TileDefinitions.GlyphLava, TileDefinitions.ColorLavaFg, TileDefinitions.ColorLavaBg, 35), // Infernal — lava
    ];

    /// <summary>
    /// Deterministically picks a biome for the given chunk coordinates and world seed.
    /// </summary>
    public static BiomeType GetBiomeForChunk(int chunkX, int chunkY, long seed)
    {
        long hash = chunkX * 73856093L ^ chunkY * 19349663L ^ seed * 0x27BB2EE687B0B0FDL;
        int idx = (int)((hash & 0x7FFFFFFFL) % BiomeCount);
        return (BiomeType)idx;
    }

    /// <summary>Returns the display name for a biome type.</summary>
    public static string GetBiomeName(BiomeType biome) => Names[(int)biome];

    /// <summary>Returns the decoration table for a biome.</summary>
    public static ReadOnlySpan<DecorationDef> GetDecorations(BiomeType biome) => Decorations[(int)biome];

    /// <summary>Returns the liquid definition for a biome, or null if it has no liquid.</summary>
    public static LiquidDef? GetLiquid(BiomeType biome) => Liquids[(int)biome];

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
