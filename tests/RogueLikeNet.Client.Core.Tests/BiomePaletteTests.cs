using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Client.Core.Tests;

public class BiomePaletteTests
{
    [Fact]
    public void GetBiomeForChunk_ReturnsDeterministicResult()
    {
        var (cx, cy) = Chunk.WorldToChunkCoord(10, 20);
        var biome1 = BiomeDefinitions.GetBiomeForChunk(cx, cy, 42);
        var biome2 = BiomeDefinitions.GetBiomeForChunk(cx, cy, 42);
        Assert.Equal(biome1, biome2);
    }

    [Fact]
    public void GetBiomeName_ReturnsValidBiomeName()
    {
        string[] validNames = ["Stone", "Lava", "Ice", "Forest", "Arcane"];
        var (cx, cy) = Chunk.WorldToChunkCoord(5, 5);
        var biome = BiomeDefinitions.GetBiomeForChunk(cx, cy, 42);
        string name = BiomeDefinitions.GetBiomeName(biome);
        Assert.Contains(name, validNames);
    }

    [Fact]
    public void GetBiomeForChunk_DifferentChunks_CanProduceDifferentBiomes()
    {
        var biomes = new HashSet<BiomeType>();
        for (int x = 0; x < 500; x += 64)
        for (int y = 0; y < 500; y += 64)
        {
            var (cx, cy) = Chunk.WorldToChunkCoord(x, y);
            biomes.Add(BiomeDefinitions.GetBiomeForChunk(cx, cy, 42));
        }

        Assert.True(biomes.Count >= 2, $"Expected multiple biomes, got: {string.Join(", ", biomes)}");
    }

    [Fact]
    public void GetBiomeForChunk_SameChunk_ReturnsSameBiome()
    {
        var (cx1, cy1) = Chunk.WorldToChunkCoord(10, 10);
        var (cx2, cy2) = Chunk.WorldToChunkCoord(12, 15);
        Assert.Equal(cx1, cx2);
        Assert.Equal(cy1, cy2);
        Assert.Equal(
            BiomeDefinitions.GetBiomeForChunk(cx1, cy1, 42),
            BiomeDefinitions.GetBiomeForChunk(cx2, cy2, 42));
    }

    [Fact]
    public void GetBiomeForChunk_DifferentSeeds_CanProduceDifferentBiomes()
    {
        var biomes = new HashSet<BiomeType>();
        for (long seed = 0; seed < 50; seed++)
            biomes.Add(BiomeDefinitions.GetBiomeForChunk(0, 0, seed));

        Assert.True(biomes.Count >= 2, $"Different seeds should produce different biomes, got: {string.Join(", ", biomes)}");
    }

    [Fact]
    public void ApplyBiomeTint_Stone_NoChange()
    {
        int color = 0x808080;
        int result = BiomeDefinitions.ApplyBiomeTint(color, BiomeType.Stone);
        Assert.Equal(color, result);
    }

    [Fact]
    public void ApplyBiomeTint_Lava_ShiftsWarm()
    {
        int color = 0x808080;
        int result = BiomeDefinitions.ApplyBiomeTint(color, BiomeType.Lava);
        int r = (result >> 16) & 0xFF;
        int g = (result >> 8) & 0xFF;
        int b = result & 0xFF;
        Assert.True(r > g && r > b, "Lava biome should shift colors warm (red dominant)");
    }

    [Fact]
    public void ApplyBiomeTint_Zero_ReturnsZero()
    {
        Assert.Equal(0, BiomeDefinitions.ApplyBiomeTint(0, BiomeType.Ice));
    }
}
