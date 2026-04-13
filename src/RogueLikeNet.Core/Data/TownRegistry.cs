using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Data;

/// <summary>
/// Registry for town type definitions. Provides lookup by biome eligibility.
/// </summary>
public sealed class TownRegistry : BaseRegistry<TownDefinition>
{
    public IReadOnlyList<TownDefinition> GetForBiome(BiomeType biome)
    {
        var biomeName = biome.ToString().ToLowerInvariant();
        var result = new List<TownDefinition>();
        foreach (var def in All)
        {
            if (def.BiomeOverrides is null || def.BiomeOverrides.Length == 0)
            {
                result.Add(def);
            }
            else
            {
                foreach (var b in def.BiomeOverrides)
                {
                    if (string.Equals(b, biomeName, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(def);
                        break;
                    }
                }
            }
        }
        return result;
    }

    public TownDefinition? PickRandom(BiomeType biome, SeededRandom rng)
    {
        var eligible = GetForBiome(biome);
        if (eligible.Count == 0) return null;
        return eligible[rng.Next(eligible.Count)];
    }
}
