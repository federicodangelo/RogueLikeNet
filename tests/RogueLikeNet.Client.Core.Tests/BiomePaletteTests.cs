using RogueLikeNet.Client.Core.Rendering;

namespace RogueLikeNet.Client.Core.Tests;

public class BiomePaletteTests
{
    [Fact]
    public void GetBiomeName_ReturnsDeterministicResult()
    {
        string biome1 = TileRenderer.GetBiomeName(10, 20);
        string biome2 = TileRenderer.GetBiomeName(10, 20);
        Assert.Equal(biome1, biome2);
    }

    [Fact]
    public void GetBiomeName_ReturnsValidBiomeName()
    {
        string[] validNames = ["Stone", "Lava", "Ice", "Forest", "Arcane"];
        string biome = TileRenderer.GetBiomeName(5, 5);
        Assert.Contains(biome, validNames);
    }

    [Fact]
    public void GetBiomeName_DifferentChunks_CanProduceDifferentBiomes()
    {
        // Test a range of positions — we expect at least 2 distinct biomes
        var biomes = new HashSet<string>();
        for (int x = 0; x < 500; x += 64)
        for (int y = 0; y < 500; y += 64)
            biomes.Add(TileRenderer.GetBiomeName(x, y));

        Assert.True(biomes.Count >= 2, $"Expected multiple biomes, got: {string.Join(", ", biomes)}");
    }

    [Fact]
    public void GetBiomeName_SameChunk_ReturnsSameBiome()
    {
        // Two positions in the same chunk (size=64) should return the same biome
        string biome1 = TileRenderer.GetBiomeName(10, 10);
        string biome2 = TileRenderer.GetBiomeName(12, 15);
        Assert.Equal(biome1, biome2);
    }
}
