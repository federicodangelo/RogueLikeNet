using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Definitions;

public readonly record struct ResourceNodeDefinition(
    int NodeTypeId, string Name, int GlyphId, int Color,
    int Health, int Defense,
    int ResourceItemTypeId, int MinDrop, int MaxDrop
);

public static class ResourceNodeDefinitions
{
    public const int Tree = 0;
    public const int CopperRock = 1;
    public const int IronRock = 2;
    public const int GoldRock = 3;

    public static readonly ResourceNodeDefinition[] All =
    [
        new(Tree,       "Tree",        TileDefinitions.GlyphTree, TileDefinitions.ColorTreeFg,   5, 0, ItemDefinitions.Wood,      2, 4),
        new(CopperRock, "Copper Rock", TileDefinitions.GlyphRock, TileDefinitions.ColorCopperFg, 8, 2, ItemDefinitions.CopperOre, 1, 3),
        new(IronRock,   "Iron Rock",   TileDefinitions.GlyphRock, TileDefinitions.ColorIronFg,  12, 4, ItemDefinitions.IronOre,   1, 3),
        new(GoldRock,   "Gold Rock",   TileDefinitions.GlyphRock, TileDefinitions.ColorGoldFg,  15, 6, ItemDefinitions.GoldOre,   1, 2),
    ];

    public static ResourceNodeDefinition Get(int nodeTypeId) =>
        Array.Find(All, d => d.NodeTypeId == nodeTypeId);

    /// <summary>
    /// Returns an array of (NodeDefinition, Weight) pairs for the given biome.
    /// </summary>
    public static (ResourceNodeDefinition Def, int Weight)[] GetForBiome(BiomeType biome) => biome switch
    {
        BiomeType.Forest => [(All[Tree], 60), (All[CopperRock], 20), (All[IronRock], 10), (All[GoldRock], 5)],
        BiomeType.Fungal => [(All[Tree], 40), (All[CopperRock], 25), (All[IronRock], 15), (All[GoldRock], 5)],
        BiomeType.Stone => [(All[CopperRock], 50), (All[IronRock], 25), (All[Tree], 10), (All[GoldRock], 5)],
        BiomeType.Arcane => [(All[CopperRock], 30), (All[IronRock], 30), (All[GoldRock], 15), (All[Tree], 10)],
        BiomeType.Ice => [(All[IronRock], 50), (All[CopperRock], 20), (All[GoldRock], 10)],
        BiomeType.Lava => [(All[GoldRock], 40), (All[IronRock], 30), (All[CopperRock], 15)],
        BiomeType.Infernal => [(All[GoldRock], 35), (All[IronRock], 35), (All[CopperRock], 15)],
        BiomeType.Crypt => [(All[IronRock], 35), (All[CopperRock], 25), (All[GoldRock], 10)],
        BiomeType.Sewer => [(All[CopperRock], 40), (All[IronRock], 20), (All[Tree], 10)],
        BiomeType.Ruined => [(All[CopperRock], 30), (All[IronRock], 30), (All[GoldRock], 10), (All[Tree], 10)],
        _ => [(All[CopperRock], 30), (All[IronRock], 20), (All[Tree], 20), (All[GoldRock], 10)],
    };

    /// <summary>
    /// Picks a random resource node definition weighted by biome distribution.
    /// </summary>
    public static ResourceNodeDefinition Pick(SeededRandom rng, BiomeType biome)
    {
        var options = GetForBiome(biome);
        int totalWeight = 0;
        foreach (var (_, w) in options) totalWeight += w;

        int roll = rng.Next(totalWeight);
        int cumulative = 0;
        foreach (var (def, w) in options)
        {
            cumulative += w;
            if (roll < cumulative) return def;
        }
        return options[^1].Def;
    }
}
