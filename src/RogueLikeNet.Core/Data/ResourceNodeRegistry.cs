using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Data;

/// <summary>
/// Holds all loaded resource node definitions with O(1) lookup by numeric ID.
/// </summary>
public sealed class ResourceNodeRegistry : BaseRegistry<ResourceNodeDefinition>
{
    // ── Biome helpers ────────────────────────────────────────────────

    /// <summary>
    /// Returns an array of (NodeDefinition, Weight) pairs for the given biome.
    /// </summary>
    public (ResourceNodeDefinition Def, int Weight)[] GetForBiome(BiomeType biome)
    {
        var baseList = GetBaseForBiome(biome);
        if (Count == 0) return baseList;

        var coal = Get("coal_deposit");
        var sand = Get("sand_deposit");
        var clay = Get("clay_deposit");
        var mithril = Get("mithril_rock");
        var adamantite = Get("adamantite_rock");

        var extended = new List<(ResourceNodeDefinition, int)>(baseList);

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
            extended.Add((coal, coalWeight));
        }

        if (sand != null)
        {
            int sandWeight = biome switch
            {
                BiomeType.Stone or BiomeType.Ruined => 10,
                BiomeType.Forest or BiomeType.Fungal => 5,
                _ => 3,
            };
            extended.Add((sand, sandWeight));
        }
        if (clay != null)
        {
            int clayWeight = biome switch
            {
                BiomeType.Forest or BiomeType.Fungal or BiomeType.Sewer => 8,
                BiomeType.Stone => 5,
                _ => 3,
            };
            extended.Add((clay, clayWeight));
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
            if (mithrilWeight > 0) extended.Add((mithril, mithrilWeight));
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
            if (adamantiteWeight > 0) extended.Add((adamantite, adamantiteWeight));
        }

        return extended.ToArray();
    }

    private (ResourceNodeDefinition Def, int Weight)[] GetBaseForBiome(BiomeType biome) => biome switch
    {
        BiomeType.Forest => [(Get("tree")!, 60), (Get("copper_rock")!, 20), (Get("iron_rock")!, 10), (Get("gold_rock")!, 5)],
        BiomeType.Fungal => [(Get("tree")!, 40), (Get("copper_rock")!, 25), (Get("iron_rock")!, 15), (Get("gold_rock")!, 5)],
        BiomeType.Stone => [(Get("copper_rock")!, 50), (Get("iron_rock")!, 25), (Get("tree")!, 10), (Get("gold_rock")!, 5)],
        BiomeType.Arcane => [(Get("copper_rock")!, 30), (Get("iron_rock")!, 30), (Get("gold_rock")!, 15), (Get("tree")!, 10)],
        BiomeType.Ice => [(Get("iron_rock")!, 50), (Get("copper_rock")!, 20), (Get("gold_rock")!, 10)],
        BiomeType.Lava => [(Get("gold_rock")!, 40), (Get("iron_rock")!, 30), (Get("copper_rock")!, 15)],
        BiomeType.Infernal => [(Get("gold_rock")!, 35), (Get("iron_rock")!, 35), (Get("copper_rock")!, 15)],
        BiomeType.Crypt => [(Get("iron_rock")!, 35), (Get("copper_rock")!, 25), (Get("gold_rock")!, 10)],
        BiomeType.Sewer => [(Get("copper_rock")!, 40), (Get("iron_rock")!, 20), (Get("tree")!, 10)],
        BiomeType.Ruined => [(Get("copper_rock")!, 30), (Get("iron_rock")!, 30), (Get("gold_rock")!, 10), (Get("tree")!, 10)],
        _ => [(Get("copper_rock")!, 30), (Get("iron_rock")!, 20), (Get("tree")!, 20), (Get("gold_rock")!, 10)],
    };

    private int[] _biomeTreeChances = [];

    public void InitBiomeTreeChances()
    {
        _biomeTreeChances = new int[Enum.GetValues<BiomeType>().Length];
        for (int i = 0; i < _biomeTreeChances.Length; i++)
        {
            var biome = (BiomeType)i;
            int chance = 0;
            var treeNode = Get("tree");
            if (treeNode != null)
            {
                foreach (var (def, w) in GetForBiome(biome))
                    if (def.NumericId == treeNode.NumericId)
                        chance = w;
            }
            _biomeTreeChances[i] = chance;
        }
    }

    public int BiomeTreeChance(BiomeType biome)
    {
        if (_biomeTreeChances.Length == 0) InitBiomeTreeChances();
        return _biomeTreeChances[(int)biome];
    }

    /// <summary>
    /// Picks a random resource node definition weighted by biome distribution.
    /// </summary>
    public ResourceNodeDefinition Pick(SeededRandom rng, BiomeType biome)
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

    public ResourceNodeDefinition PickRock(SeededRandom rng, BiomeType biome)
    {
        var treeNode = Get("tree");
        int treeId = treeNode?.NumericId ?? -1;
        var options = GetForBiome(biome);
        int totalWeight = 0;
        foreach (var (def, w) in options)
            if (def.NumericId != treeId)
                totalWeight += w;

        if (totalWeight == 0)
            return Get("copper_rock")!;

        int roll = rng.Next(totalWeight);
        int cumulative = 0;
        foreach (var (def, w) in options)
        {
            if (def.NumericId == treeId) continue;
            cumulative += w;
            if (roll < cumulative) return def;
        }
        return Get("copper_rock")!;
    }
}
