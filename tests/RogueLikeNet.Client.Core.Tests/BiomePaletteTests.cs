using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Client.Core.Tests;

public class BiomePaletteTests
{
    [Fact]
    public void GetBiomeForChunk_ReturnsDeterministicResult()
    {
        var (cx, cy, _) = Chunk.WorldToChunkCoord(Position.FromCoords(10, 20, Position.DefaultZ));
        var biome1 = BiomeDefinitions.GetBiomeForChunk(Position.FromCoords(cx, cy, 0), 42);
        var biome2 = BiomeDefinitions.GetBiomeForChunk(Position.FromCoords(cx, cy, 0), 42);
        Assert.Equal(biome1, biome2);
    }

    [Fact]
    public void GetBiomeName_ReturnsValidBiomeName()
    {
        string[] validNames = ["Stone", "Lava", "Ice", "Forest", "Arcane", "Crypt", "Sewer", "Fungal", "Ruined", "Infernal"];
        var (cx, cy, _) = Chunk.WorldToChunkCoord(Position.FromCoords(5, 5, Position.DefaultZ));
        var biome = BiomeDefinitions.GetBiomeForChunk(Position.FromCoords(cx, cy, 0), 42);
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
                var (cx, cy, _) = Chunk.WorldToChunkCoord(Position.FromCoords(x, y, Position.DefaultZ));
                biomes.Add(BiomeDefinitions.GetBiomeForChunk(Position.FromCoords(cx, cy, 0), 42));
            }

        Assert.True(biomes.Count >= 2, $"Expected multiple biomes, got: {string.Join(", ", biomes)}");
    }

    [Fact]
    public void GetBiomeForChunk_SameChunk_ReturnsSameBiome()
    {
        var (cx1, cy1, _) = Chunk.WorldToChunkCoord(Position.FromCoords(10, 10, Position.DefaultZ));
        var (cx2, cy2, _) = Chunk.WorldToChunkCoord(Position.FromCoords(12, 15, Position.DefaultZ));
        Assert.Equal(cx1, cx2);
        Assert.Equal(cy1, cy2);
        Assert.Equal(
            BiomeDefinitions.GetBiomeForChunk(Position.FromCoords(cx1, cy1, 0), 42),
            BiomeDefinitions.GetBiomeForChunk(Position.FromCoords(cx2, cy2, 0), 42));
    }

    [Fact]
    public void GetBiomeForChunk_DifferentSeeds_CanProduceDifferentBiomes()
    {
        var biomes = new HashSet<BiomeType>();
        for (long seed = 0; seed < 50; seed++)
            biomes.Add(BiomeDefinitions.GetBiomeForChunk(Position.FromCoords(0, 0, 0), seed));

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

    [Fact]
    public void AllBiomes_HaveDecorations()
    {
        for (int i = 0; i < BiomeDefinitions.BiomeCount; i++)
        {
            var decos = BiomeDefinitions.GetDecorations((BiomeType)i);
            Assert.True(decos.Length > 0, $"Biome {(BiomeType)i} should have decorations");
        }
    }

    [Fact]
    public void AllBiomes_HaveNames()
    {
        for (int i = 0; i < BiomeDefinitions.BiomeCount; i++)
        {
            string name = BiomeDefinitions.GetBiomeName((BiomeType)i);
            Assert.False(string.IsNullOrEmpty(name));
        }
    }

    [Fact]
    public void LiquidBiomes_HaveValidLiquidDefs()
    {
        // Lava, Ice, Forest, Sewer, Fungal, Infernal should have liquid
        Assert.NotNull(BiomeDefinitions.GetLiquid(BiomeType.Lava));
        Assert.NotNull(BiomeDefinitions.GetLiquid(BiomeType.Sewer));
        Assert.NotNull(BiomeDefinitions.GetLiquid(BiomeType.Infernal));
        // Stone, Arcane, Crypt, Ruined should not
        Assert.Null(BiomeDefinitions.GetLiquid(BiomeType.Stone));
        Assert.Null(BiomeDefinitions.GetLiquid(BiomeType.Arcane));
    }
}
