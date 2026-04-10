using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.Utilities;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Data;

/// <summary>
/// Holds all loaded biome definitions with lookup by numeric ID.
/// Provides biome palette data, decoration tables, enemy spawn tables, and tinting logic.
/// </summary>
public sealed class BiomeRegistry : BaseRegistry<BiomeDefinition>
{
    public const int BiomeCount = 10;

    private readonly BiomeDefinition?[] _byBiomeType = new BiomeDefinition?[BiomeCount];

    protected override void ExtraRegister(BiomeDefinition biome)
    {
        // Map string Id to BiomeType enum for fast lookup by enum value
        if (Enum.TryParse<BiomeType>(biome.Id, ignoreCase: true, out var biomeType))
            _byBiomeType[(int)biomeType] = biome;

        // Resolve liquid tile type from string
        if (biome.Liquid != null)
            biome.Liquid.ResolvedTileType = Enum.Parse<TileType>(biome.Liquid.TileType, ignoreCase: true);
    }

    public BiomeDefinition? Get(BiomeType biome) =>
        _byBiomeType[(int)biome];

    // ===== Instance data-access methods =====

    /// <summary>Returns the display name for a biome type.</summary>
    public string GetBiomeName(BiomeType biome) => Get(biome)?.Name ?? "Unknown";

    /// <summary>Returns the decoration table for a biome.</summary>
    public BiomeDecorationDef[] GetDecorations(BiomeType biome) => Get(biome)?.Decorations ?? [];

    /// <summary>Returns the liquid definition for a biome, or null if it has no liquid.</summary>
    public BiomeLiquidDef? GetLiquid(BiomeType biome) => Get(biome)?.Liquid;

    /// <summary>Returns the enemy spawn table for a biome.</summary>
    public BiomeEnemySpawnDef[] GetEnemySpawns(BiomeType biome) => Get(biome)?.EnemySpawns ?? [];

    /// <summary>Returns the floor color for a biome.</summary>
    public int GetFloorColor(BiomeType biome) => Get(biome)?.FloorColor ?? 0;

    /// <summary>Applies biome tint to a packed 0xRRGGBB color using the biome's tint values.</summary>
    public int ApplyBiomeTint(int packedRgb, BiomeType biome)
    {
        if (packedRgb == 0) return 0;
        var def = Get(biome);
        if (def == null) return packedRgb;
        var color = ColorUtils.IntToColor4(packedRgb);
        var scaledColor = ColorUtils.ScaleColor(color, def.TintR, def.TintG, def.TintB);
        return ColorUtils.Color4ToInt(scaledColor);
    }

    /// <summary>Applies biome tint using numeric biome ID.</summary>
    public int ApplyBiomeTint(int packedRgb, int biomeId)
    {
        if (packedRgb == 0) return 0;
        var biome = Get(biomeId);
        if (biome == null) return packedRgb;
        var color = ColorUtils.IntToColor4(packedRgb);
        var scaledColor = ColorUtils.ScaleColor(color, biome.TintR, biome.TintG, biome.TintB);
        return ColorUtils.Color4ToInt(scaledColor);
    }

    /// <summary>
    /// Picks a random enemy type for the given biome, weighted by spawn table entries.
    /// Higher difficulty unlocks harder monsters (gated by NPC attack stat).
    /// </summary>
    public NpcDefinition PickEnemy(BiomeType biome, SeededRandom rng, int difficulty)
    {
        var spawns = GetEnemySpawns(biome);
        var npcReg = GameData.Instance.Npcs;

        // Resolve spawns and filter by difficulty (attack-based gating)
        var resolved = new List<(NpcDefinition Def, int Weight)>();
        foreach (var s in spawns)
        {
            var npc = npcReg.Get(s.NpcId);
            if (npc == null) continue;
            // Higher attack NPCs require higher difficulty
            int requiredDifficulty = npc.Attack / 4;
            if (difficulty >= requiredDifficulty)
                resolved.Add((npc, s.Weight));
        }

        if (resolved.Count == 0)
        {
            // Fallback: return weakest NPC
            var weakest = npcReg.All.OrderBy(n => n.Attack).FirstOrDefault();
            return weakest!;
        }

        int totalWeight = 0;
        foreach (var (_, w) in resolved) totalWeight += w;

        int roll = rng.Next(totalWeight);
        foreach (var (def, weight) in resolved)
        {
            roll -= weight;
            if (roll < 0)
                return def;
        }

        return resolved[^1].Def;
    }

    // ===== Static biome logic =====

    /// <summary>
    /// Deterministically picks a biome for the given chunk coordinates and world seed.
    /// </summary>
    public static BiomeType GetBiomeForChunk(ChunkPosition chunkPos, long seed)
    {
        long hash = chunkPos.X * 73856093L ^ chunkPos.Y * 19349663L ^ seed * 0x27BB2EE687B0B0FDL;
        int idx = (int)((hash & 0x7FFFFFFFL) % BiomeCount);
        return (BiomeType)idx;
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
}
