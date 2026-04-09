using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Definitions;

public readonly record struct ResourceNodeDefinition(
    int NodeTypeId, string Name, int GlyphId, int Color,
    int Health, int Defense,
    int ResourceItemTypeId, int MinDrop, int MaxDrop
);

public static class ResourceNodeDefinitions
{
    public const int None = 0;
    public const int CopperRock = 1;
    public const int IronRock = 2;
    public const int GoldRock = 3;
    public const int Tree = 4;

    public static readonly ResourceNodeDefinition[] All =
    [
        new(CopperRock, "Copper Rock", TileDefinitions.GlyphRock, TileDefinitions.ColorCopperFg, 8, 2, ItemDefinitions.CopperOre, 1, 3),
        new(IronRock,   "Iron Rock",   TileDefinitions.GlyphRock, TileDefinitions.ColorIronFg,  12, 4, ItemDefinitions.IronOre,   1, 3),
        new(GoldRock,   "Gold Rock",   TileDefinitions.GlyphRock, TileDefinitions.ColorGoldFg,  15, 6, ItemDefinitions.GoldOre,   1, 2),
        new(Tree,       "Tree",        TileDefinitions.GlyphTree, TileDefinitions.ColorTreeFg,   5, 0, ItemDefinitions.Wood,      2, 4),
    ];

    // Maps old numeric NodeTypeId → new string ID for JSON lookup
    // Index 0 = None (null)
    private static readonly string?[] OldToNewNodeId = [null, "copper_rock", "iron_rock", "gold_rock", "tree"];

    public static ResourceNodeDefinition Get(int nodeTypeId)
    {
        var reg = GameData.Instance.ResourceNodes;
        if (reg.Count > 0 && nodeTypeId > 0 && nodeTypeId < OldToNewNodeId.Length)
        {
            var newId = OldToNewNodeId[nodeTypeId];
            if (newId != null)
            {
                var d = reg.Get(newId);
                if (d != null)
                {
                    // Convert dropItemId string to legacy int via LegacyItemBridge
                    int resItemId = LegacyItemBridge.GetLegacyId(d.DropItemId);
                    return new ResourceNodeDefinition(nodeTypeId, d.Name, d.GlyphId, d.FgColor,
                        d.Health, d.Defense, resItemId, d.MinDrop, d.MaxDrop);
                }
            }
        }

        return nodeTypeId > 0 && nodeTypeId < _byId.Length ? _byId[nodeTypeId] : default;
    }

    /// <summary>
    /// Returns an array of (NodeDefinition, Weight) pairs for the given biome.
    /// </summary>
    public static (ResourceNodeDefinition Def, int Weight)[] GetForBiome(BiomeType biome) => biome switch
    {
        BiomeType.Forest => [(Get(Tree), 60), (Get(CopperRock), 20), (Get(IronRock), 10), (Get(GoldRock), 5)],
        BiomeType.Fungal => [(Get(Tree), 40), (Get(CopperRock), 25), (Get(IronRock), 15), (Get(GoldRock), 5)],
        BiomeType.Stone => [(Get(CopperRock), 50), (Get(IronRock), 25), (Get(Tree), 10), (Get(GoldRock), 5)],
        BiomeType.Arcane => [(Get(CopperRock), 30), (Get(IronRock), 30), (Get(GoldRock), 15), (Get(Tree), 10)],
        BiomeType.Ice => [(Get(IronRock), 50), (Get(CopperRock), 20), (Get(GoldRock), 10)],
        BiomeType.Lava => [(Get(GoldRock), 40), (Get(IronRock), 30), (Get(CopperRock), 15)],
        BiomeType.Infernal => [(Get(GoldRock), 35), (Get(IronRock), 35), (Get(CopperRock), 15)],
        BiomeType.Crypt => [(Get(IronRock), 35), (Get(CopperRock), 25), (Get(GoldRock), 10)],
        BiomeType.Sewer => [(Get(CopperRock), 40), (Get(IronRock), 20), (Get(Tree), 10)],
        BiomeType.Ruined => [(Get(CopperRock), 30), (Get(IronRock), 30), (Get(GoldRock), 10), (Get(Tree), 10)],
        _ => [(Get(CopperRock), 30), (Get(IronRock), 20), (Get(Tree), 20), (Get(GoldRock), 10)],
    };

    private static readonly int[] _biomeTreeChances;
    private static readonly ResourceNodeDefinition[] _byId;

    static ResourceNodeDefinitions()
    {

        int maxId = All.Max(t => t.NodeTypeId);

        // Validate that all IDs are correct
        _byId = new ResourceNodeDefinition[maxId + 1];
        foreach (var def in All)
        {
            _byId[def.NodeTypeId] = def;
        }

        _biomeTreeChances = new int[Enum.GetValues<BiomeType>().Length];
        for (int i = 0; i < _biomeTreeChances.Length; i++)
        {
            var biome = (BiomeType)i;
            int chance = 0;
            foreach (var (def, w) in GetForBiome(biome))
                if (def.NodeTypeId == Tree)
                    chance = w;

            _biomeTreeChances[i] = chance;
        }

    }

    public static int BiomeTreeChance(BiomeType biome)
    {
        return _biomeTreeChances[(int)biome];
    }

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

    public static ResourceNodeDefinition PickRock(SeededRandom rng, BiomeType biome)
    {
        var options = GetForBiome(biome);
        int totalWeight = 0;
        foreach (var (def, w) in options)
            if (def.NodeTypeId != Tree)
                totalWeight += w;

        if (totalWeight == 0)
            return Get(CopperRock);

        int roll = rng.Next(totalWeight);
        int cumulative = 0;
        foreach (var (def, w) in options)
        {
            if (def.NodeTypeId == Tree) continue;
            cumulative += w;
            if (roll < cumulative) return def;
        }
        return Get(CopperRock);
    }

}
