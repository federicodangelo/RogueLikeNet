using RogueLikeNet.Core.Utilities;

namespace RogueLikeNet.Core.Data;

/// <summary>
/// Holds all loaded biome definitions with lookup by numeric ID.
/// Also provides climate-based biome mapping and utility functions
/// previously in the old BiomeDefinitions static class.
/// </summary>
public sealed class BiomeRegistry
{
    private readonly Dictionary<string, BiomeDefinition> _byStringId = new();
    private BiomeDefinition?[] _byNumericId = [];

    public IReadOnlyCollection<BiomeDefinition> All => _byStringId.Values;
    public int Count => _byStringId.Count;

    public void Register(IEnumerable<BiomeDefinition> biomes)
    {
        var sorted = biomes.OrderBy(b => b.NumericId).ToList();
        int maxId = sorted.Max(b => b.NumericId);
        _byNumericId = new BiomeDefinition?[maxId + 1];

        foreach (var biome in sorted)
        {
            _byStringId[biome.Id] = biome;
            _byNumericId[biome.NumericId] = biome;
        }
    }

    public BiomeDefinition? Get(string id) =>
        _byStringId.GetValueOrDefault(id);

    public BiomeDefinition? Get(int numericId) =>
        numericId >= 0 && numericId < _byNumericId.Length ? _byNumericId[numericId] : null;

    /// <summary>
    /// Applies biome tint to a packed 0xRRGGBB color.
    /// </summary>
    public int ApplyBiomeTint(int packedRgb, int biomeId)
    {
        if (packedRgb == 0) return 0;
        var biome = Get(biomeId);
        if (biome == null) return packedRgb;
        var color = ColorUtils.IntToColor4(packedRgb);
        var scaledColor = ColorUtils.ScaleColor(color, biome.TintR, biome.TintG, biome.TintB);
        return ColorUtils.Color4ToInt(scaledColor);
    }
}
