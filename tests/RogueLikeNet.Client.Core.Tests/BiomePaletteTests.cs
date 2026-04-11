using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Client.Core.Tests;

public class BiomePaletteTests
{
    [Fact]
    public void GetBiomeForChunk_ReturnsDeterministicResult()
    {
        var (cx, cy, _) = Chunk.WorldToChunkCoord(Position.FromCoords(10, 20, Position.DefaultZ));
        var biome1 = BiomeRegistry.GetBiomeForChunk(ChunkPosition.FromCoords(cx, cy, 0), 42);
        var biome2 = BiomeRegistry.GetBiomeForChunk(ChunkPosition.FromCoords(cx, cy, 0), 42);
        Assert.Equal(biome1, biome2);
    }

    [Fact]
    public void GetBiomeName_ReturnsValidBiomeName()
    {
        string[] validNames = ["Stone", "Lava", "Ice", "Forest", "Arcane", "Crypt", "Sewer", "Fungal", "Ruined", "Infernal"];
        var (cx, cy, _) = Chunk.WorldToChunkCoord(Position.FromCoords(5, 5, Position.DefaultZ));
        var biome = BiomeRegistry.GetBiomeForChunk(ChunkPosition.FromCoords(cx, cy, 0), 42);
        string name = GameData.Instance.Biomes.GetBiomeName(biome);
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
                biomes.Add(BiomeRegistry.GetBiomeForChunk(ChunkPosition.FromCoords(cx, cy, 0), 42));
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
            BiomeRegistry.GetBiomeForChunk(ChunkPosition.FromCoords(cx1, cy1, 0), 42),
            BiomeRegistry.GetBiomeForChunk(ChunkPosition.FromCoords(cx2, cy2, 0), 42));
    }

    [Fact]
    public void GetBiomeForChunk_DifferentSeeds_CanProduceDifferentBiomes()
    {
        var biomes = new HashSet<BiomeType>();
        for (long seed = 0; seed < 50; seed++)
            biomes.Add(BiomeRegistry.GetBiomeForChunk(ChunkPosition.FromCoords(0, 0, 0), seed));

        Assert.True(biomes.Count >= 2, $"Different seeds should produce different biomes, got: {string.Join(", ", biomes)}");
    }

    [Fact]
    public void GetFloorTileId_ReturnsNonZeroForAllBiomes()
    {
        for (int i = 0; i < BiomeRegistry.BiomeCount; i++)
        {
            int floorId = GameData.Instance.Biomes.GetFloorTileId((BiomeType)i);
            Assert.True(floorId != 0, $"Biome {(BiomeType)i} should have a valid floor tile ID");
        }
    }

    [Fact]
    public void GetWallTileId_ReturnsNonZeroForAllBiomes()
    {
        for (int i = 0; i < BiomeRegistry.BiomeCount; i++)
        {
            int wallId = GameData.Instance.Biomes.GetWallTileId((BiomeType)i);
            Assert.True(wallId != 0, $"Biome {(BiomeType)i} should have a valid wall tile ID");
        }
    }

    [Fact]
    public void GetFloorTileId_DifferentBiomes_CanHaveDifferentTiles()
    {
        var floorIds = new HashSet<int>();
        for (int i = 0; i < BiomeRegistry.BiomeCount; i++)
            floorIds.Add(GameData.Instance.Biomes.GetFloorTileId((BiomeType)i));
        Assert.True(floorIds.Count >= 2, "Different biomes should have different floor tile IDs");
    }

    [Fact]
    public void AllBiomes_HaveDecorations()
    {
        for (int i = 0; i < BiomeRegistry.BiomeCount; i++)
        {
            var decos = GameData.Instance.Biomes.GetDecorations((BiomeType)i);
            Assert.True(decos.Length > 0, $"Biome {(BiomeType)i} should have decorations");
        }
    }

    [Fact]
    public void AllBiomes_HaveNames()
    {
        for (int i = 0; i < BiomeRegistry.BiomeCount; i++)
        {
            string name = GameData.Instance.Biomes.GetBiomeName((BiomeType)i);
            Assert.False(string.IsNullOrEmpty(name));
        }
    }

    [Fact]
    public void LiquidBiomes_HaveValidLiquidDefs()
    {
        // Lava, Ice, Forest, Sewer, Fungal, Infernal should have liquid
        Assert.NotNull(GameData.Instance.Biomes.GetLiquid(BiomeType.Lava));
        Assert.NotNull(GameData.Instance.Biomes.GetLiquid(BiomeType.Sewer));
        Assert.NotNull(GameData.Instance.Biomes.GetLiquid(BiomeType.Infernal));
        // Stone, Arcane, Crypt, Ruined should not
        Assert.Null(GameData.Instance.Biomes.GetLiquid(BiomeType.Stone));
        Assert.Null(GameData.Instance.Biomes.GetLiquid(BiomeType.Arcane));
    }
}
