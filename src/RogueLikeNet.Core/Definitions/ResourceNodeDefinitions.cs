using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Definitions;

public readonly record struct ResourceNodeDef(
    int NodeTypeId, string Name, int GlyphId, int Color,
    int Health, int Defense,
    int ResourceItemTypeId, int MinDrop, int MaxDrop
);

public static class ResourceNodeDefinitions
{
    public static ResourceNodeDef[] All
    {
        get
        {
            var reg = GameData.Instance.ResourceNodes;
            if (reg.Count == 0) return [];
            return reg.All.Select(ConvertFromData).ToArray();
        }
    }

    public static ResourceNodeDef Get(int nodeTypeId)
    {
        var d = GameData.Instance.ResourceNodes.Get(nodeTypeId);
        if (d != null)
            return ConvertFromData(d);
        return default;
    }

    public static ResourceNodeDef Get(string nodeId)
    {
        var d = GameData.Instance.ResourceNodes.Get(nodeId);
        if (d != null)
            return ConvertFromData(d);
        return default;
    }

    private static ResourceNodeDef ConvertFromData(Data.ResourceNodeDefinition d)
    {
        int resItemId = GameData.Instance.Items.GetNumericId(d.DropItemId);
        return new ResourceNodeDef(d.NumericId, d.Name, d.GlyphId, d.FgColor,
            d.Health, d.Defense, resItemId, d.MinDrop, d.MaxDrop);
    }

    /// <summary>
    /// Returns an array of (NodeDefinition, Weight) pairs for the given biome.
    /// </summary>
    public static (ResourceNodeDef Def, int Weight)[] GetForBiome(BiomeType biome)
    {
        var baseList = GetBaseForBiome(biome);
        var reg = GameData.Instance.ResourceNodes;
        if (reg.Count == 0) return baseList;

        // Look up new node types by string ID
        var coal = reg.Get("coal_deposit");
        var sand = reg.Get("sand_deposit");
        var clay = reg.Get("clay_deposit");
        var mithril = reg.Get("mithril_rock");
        var adamantite = reg.Get("adamantite_rock");

        var extended = new List<(ResourceNodeDef, int)>(baseList);

        if (coal != null)
        {
            int coalWeight = biome switch
            {
                BiomeType.Stone or BiomeType.Ruined => 15,
                BiomeType.Ice or BiomeType.Crypt => 10,
                BiomeType.Lava or BiomeType.Infernal => 5,
                BiomeType.Forest or BiomeType.Fungal or BiomeType.Sewer => 8,
                _ => 10,
            };
            extended.Add((ConvertFromData(coal), coalWeight));
        }

        if (sand != null)
        {
            int sandWeight = biome switch
            {
                BiomeType.Stone or BiomeType.Ruined => 10,
                BiomeType.Forest or BiomeType.Fungal => 5,
                _ => 3,
            };
            extended.Add((ConvertFromData(sand), sandWeight));
        }
        if (clay != null)
        {
            int clayWeight = biome switch
            {
                BiomeType.Forest or BiomeType.Fungal or BiomeType.Sewer => 8,
                BiomeType.Stone => 5,
                _ => 3,
            };
            extended.Add((ConvertFromData(clay), clayWeight));
        }

        if (mithril != null)
        {
            int mithrilWeight = biome switch
            {
                BiomeType.Stone or BiomeType.Arcane => 5,
                BiomeType.Lava or BiomeType.Infernal => 8,
                BiomeType.Crypt or BiomeType.Ice => 3,
                _ => 0,
            };
            if (mithrilWeight > 0) extended.Add((ConvertFromData(mithril), mithrilWeight));
        }
        if (adamantite != null)
        {
            int adamantiteWeight = biome switch
            {
                BiomeType.Lava => 5,
                BiomeType.Infernal => 4,
                BiomeType.Arcane => 2,
                _ => 0,
            };
            if (adamantiteWeight > 0) extended.Add((ConvertFromData(adamantite), adamantiteWeight));
        }

        return extended.ToArray();
    }

    private static (ResourceNodeDef Def, int Weight)[] GetBaseForBiome(BiomeType biome) => biome switch
    {
        BiomeType.Forest => [(Get("tree"), 60), (Get("copper_rock"), 20), (Get("iron_rock"), 10), (Get("gold_rock"), 5)],
        BiomeType.Fungal => [(Get("tree"), 40), (Get("copper_rock"), 25), (Get("iron_rock"), 15), (Get("gold_rock"), 5)],
        BiomeType.Stone => [(Get("copper_rock"), 50), (Get("iron_rock"), 25), (Get("tree"), 10), (Get("gold_rock"), 5)],
        BiomeType.Arcane => [(Get("copper_rock"), 30), (Get("iron_rock"), 30), (Get("gold_rock"), 15), (Get("tree"), 10)],
        BiomeType.Ice => [(Get("iron_rock"), 50), (Get("copper_rock"), 20), (Get("gold_rock"), 10)],
        BiomeType.Lava => [(Get("gold_rock"), 40), (Get("iron_rock"), 30), (Get("copper_rock"), 15)],
        BiomeType.Infernal => [(Get("gold_rock"), 35), (Get("iron_rock"), 35), (Get("copper_rock"), 15)],
        BiomeType.Crypt => [(Get("iron_rock"), 35), (Get("copper_rock"), 25), (Get("gold_rock"), 10)],
        BiomeType.Sewer => [(Get("copper_rock"), 40), (Get("iron_rock"), 20), (Get("tree"), 10)],
        BiomeType.Ruined => [(Get("copper_rock"), 30), (Get("iron_rock"), 30), (Get("gold_rock"), 10), (Get("tree"), 10)],
        _ => [(Get("copper_rock"), 30), (Get("iron_rock"), 20), (Get("tree"), 20), (Get("gold_rock"), 10)],
    };

    private static int[] _biomeTreeChances = [];

    public static void InitBiomeTreeChances()
    {
        _biomeTreeChances = new int[Enum.GetValues<BiomeType>().Length];
        for (int i = 0; i < _biomeTreeChances.Length; i++)
        {
            var biome = (BiomeType)i;
            int chance = 0;
            var treeNode = GameData.Instance.ResourceNodes.Get("tree");
            if (treeNode != null)
            {
                foreach (var (def, w) in GetForBiome(biome))
                    if (def.NodeTypeId == treeNode.NumericId)
                        chance = w;
            }
            _biomeTreeChances[i] = chance;
        }
    }

    public static int BiomeTreeChance(BiomeType biome)
    {
        if (_biomeTreeChances.Length == 0) InitBiomeTreeChances();
        return _biomeTreeChances[(int)biome];
    }

    /// <summary>
    /// Picks a random resource node definition weighted by biome distribution.
    /// </summary>
    public static ResourceNodeDef Pick(SeededRandom rng, BiomeType biome)
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

    public static ResourceNodeDef PickRock(SeededRandom rng, BiomeType biome)
    {
        var treeNode = GameData.Instance.ResourceNodes.Get("tree");
        int treeId = treeNode?.NumericId ?? -1;
        var options = GetForBiome(biome);
        int totalWeight = 0;
        foreach (var (def, w) in options)
            if (def.NodeTypeId != treeId)
                totalWeight += w;

        if (totalWeight == 0)
            return Get("copper_rock");

        int roll = rng.Next(totalWeight);
        int cumulative = 0;
        foreach (var (def, w) in options)
        {
            if (def.NodeTypeId == treeId) continue;
            cumulative += w;
            if (roll < cumulative) return def;
        }
        return Get("copper_rock");
    }

}
