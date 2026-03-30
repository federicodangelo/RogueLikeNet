using RogueLikeNet.Core.Generation;

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

    /// <summary>Decoration entry: glyph, fg color, chance (per-mille per floor tile).</summary>
    public readonly record struct DecorationDef(int GlyphId, int FgColor, int Chance1000);

    /// <summary>Enemy spawn entry: NPC type ID and relative weight for biome spawning.</summary>
    public readonly record struct EnemySpawnDef(int NpcTypeId, int Weight);

    /// <summary>Liquid feature: tile type, glyph, fg/bg colors, room fill chance (%).</summary>
    public readonly record struct LiquidDef(World.TileType Type, int GlyphId, int FgColor, int BgColor, int Chance100RoomBecomesLiquid);

    // Per-biome decoration tables
    private static readonly DecorationDef[][] Decorations =
    [
        // Stone — sparse rubble and pillars
        [new(TileDefinitions.GlyphRubble, TileDefinitions.ColorRubbleFg, 4),
         new(TileDefinitions.GlyphPillar, TileDefinitions.ColorStatueFg, 2)],

        // Lava — embers and cracks
        [new(TileDefinitions.GlyphEmber, TileDefinitions.ColorEmberFg, 8),
         new(TileDefinitions.GlyphCrack, TileDefinitions.ColorCrackFg, 6)],

        // Ice — icicles and frost cracks
        [new(TileDefinitions.GlyphIcicle, TileDefinitions.ColorIceFg, 8),
         new(TileDefinitions.GlyphCrack, TileDefinitions.ColorIceFg, 4)],

        // Forest — grass, moss, and vines
        [new(TileDefinitions.GlyphGrass, TileDefinitions.ColorGrassFg, 12),
         new(TileDefinitions.GlyphMoss, TileDefinitions.ColorMossFg, 8),
         new(TileDefinitions.GlyphVines, TileDefinitions.ColorVinesFg, 4)],

        // Arcane — runes and crystals
        [new(TileDefinitions.GlyphRune, TileDefinitions.ColorRuneFg, 6),
         new(TileDefinitions.GlyphCrystal, TileDefinitions.ColorCrystalFg, 6)],

        // Crypt — bones, coffins, and webs
        [new(TileDefinitions.GlyphBones, TileDefinitions.ColorBonesFg, 10),
         new(TileDefinitions.GlyphCoffin, TileDefinitions.ColorBonesFg, 4),
         new(TileDefinitions.GlyphWeb, TileDefinitions.ColorWebFg, 6)],

        // Sewer — moss and barrels
        [new(TileDefinitions.GlyphMoss, TileDefinitions.ColorMossFg, 8),
         new(TileDefinitions.GlyphBarrel, TileDefinitions.ColorBarrelFg, 4),
         new(TileDefinitions.GlyphCrack, TileDefinitions.ColorRubbleFg, 4)],

        // Fungal — mushrooms and moss
        [new(TileDefinitions.GlyphMushroom, TileDefinitions.ColorMushroomFg, 12),
         new(TileDefinitions.GlyphMoss, TileDefinitions.ColorMossFg, 6),
         new(TileDefinitions.GlyphGrass, TileDefinitions.ColorVinesFg, 4)],

        // Ruined — rubble, pillars, and statues
        [new(TileDefinitions.GlyphRubble, TileDefinitions.ColorRubbleFg, 10),
         new(TileDefinitions.GlyphPillar, TileDefinitions.ColorStatueFg, 4),
         new(TileDefinitions.GlyphStatue, TileDefinitions.ColorStatueFg, 2)],

        // Infernal — embers, cracks, and bones
        [new(TileDefinitions.GlyphEmber, TileDefinitions.ColorEmberFg, 10),
         new(TileDefinitions.GlyphCrack, TileDefinitions.ColorCrackFg, 6),
         new(TileDefinitions.GlyphBones, TileDefinitions.ColorBonesFg, 4)],
    ];

    // Per-biome enemy spawn tables (NpcTypeId, relative weight)
    private static readonly EnemySpawnDef[][] EnemySpawns =
    [
        // Stone — goblins and skeletons lurk in the caves
        [new(NpcDefinitions.Goblin, 50), new(NpcDefinitions.Skeleton, 50)],

        // Lava — orcs and dragons thrive in the heat
        [new(NpcDefinitions.Orc, 70), new(NpcDefinitions.Dragon, 30)],

        // Ice — skeletons and hardy orcs
        [new(NpcDefinitions.Skeleton, 70), new(NpcDefinitions.Orc, 30)],

        // Forest — goblins are most common
        [new(NpcDefinitions.Goblin, 75), new(NpcDefinitions.Orc, 25)],

        // Arcane — balanced mix with more dragons
        [new(NpcDefinitions.Skeleton, 30), new(NpcDefinitions.Orc, 25), new(NpcDefinitions.Goblin, 25), new(NpcDefinitions.Dragon, 20)],

        // Crypt — undead-heavy
        [new(NpcDefinitions.Skeleton, 70), new(NpcDefinitions.Goblin, 15), new(NpcDefinitions.Orc, 15)],

        // Sewer — goblins and orcs
        [new(NpcDefinitions.Goblin, 65), new(NpcDefinitions.Orc, 35)],

        // Fungal — goblins and skeletons
        [new(NpcDefinitions.Goblin, 65), new(NpcDefinitions.Skeleton, 35)],

        // Ruined — orcs and skeletons dominate
        [new(NpcDefinitions.Orc, 50), new(NpcDefinitions.Skeleton, 50)],

        // Infernal — dragons and orcs rule
        [new(NpcDefinitions.Dragon, 50), new(NpcDefinitions.Orc, 50)],
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

    /// <summary>Returns the enemy spawn table for a biome.</summary>
    public static ReadOnlySpan<EnemySpawnDef> GetEnemySpawns(BiomeType biome) => EnemySpawns[(int)biome];

    /// <summary>
    /// Picks a random enemy type for the given biome, weighted by spawn table entries.
    /// Higher difficulty unlocks harder monsters (same gating as NpcDefinitions.Pick).
    /// </summary>
    public static NpcDefinition PickEnemy(BiomeType biome, SeededRandom rng, int difficulty)
    {
        var spawns = EnemySpawns[(int)biome];
        int maxTypeId = Math.Min(difficulty + 1, NpcDefinitions.All.Length - 1);

        int totalWeight = 0;
        foreach (var s in spawns)
            if (s.NpcTypeId <= maxTypeId)
                totalWeight += s.Weight;

        if (totalWeight <= 0)
            return NpcDefinitions.All[0];

        int roll = rng.Next(totalWeight);
        foreach (var s in spawns)
        {
            if (s.NpcTypeId <= maxTypeId)
            {
                roll -= s.Weight;
                if (roll < 0)
                    return NpcDefinitions.Get(s.NpcTypeId);
            }
        }

        return NpcDefinitions.All[0];
    }

    /// <summary>
    /// Maps continuous temperature/moisture noise values (each in [-1,1]) to a biome type.
    /// This creates gradual transitions between biomes in world space.
    /// </summary>
    /// <remarks>
    /// Layout (temperature → right, moisture → up):
    /// <code>
    ///              cold        cool       warm        hot
    /// wet    |  Fungal   |  Forest  |  Sewer   | Infernal |
    /// damp   |  Ice      |  Arcane  |  Crypt   |  Lava    |
    /// dry    |  Ice      |  Stone   |  Ruined  |  Lava    |
    /// </code>
    /// </remarks>
    public static BiomeType GetBiomeFromClimate(double temperature, double moisture)
    {
        // Map [-1,1] → column/row indices
        // Temperature: 4 columns  Moisture: 3 rows
        int col = temperature switch
        {
            < -0.4 => 0, // cold
            < 0.0 => 1,  // cool
            < 0.4 => 2,  // warm
            _ => 3,       // hot
        };

        int row = moisture switch
        {
            < -0.3 => 0, // dry
            < 0.3 => 1,  // damp
            _ => 2,       // wet
        };

        return (col, row) switch
        {
            (0, 0) => BiomeType.Ice,
            (0, 1) => BiomeType.Ice,
            (0, 2) => BiomeType.Fungal,

            (1, 0) => BiomeType.Stone,
            (1, 1) => BiomeType.Arcane,
            (1, 2) => BiomeType.Forest,

            (2, 0) => BiomeType.Ruined,
            (2, 1) => BiomeType.Crypt,
            (2, 2) => BiomeType.Sewer,

            (3, 0) => BiomeType.Lava,
            (3, 1) => BiomeType.Lava,
            (3, 2) => BiomeType.Infernal,

            _ => BiomeType.Stone,
        };
    }

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
