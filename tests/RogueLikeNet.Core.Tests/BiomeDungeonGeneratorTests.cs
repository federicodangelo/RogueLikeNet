using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Core.Tests;

public class BiomeDungeonGeneratorTests
{
    [Fact]
    public void Generate_AllBiomes_ProduceFloorTiles()
    {
        var gen = new BiomeDungeonGenerator();

        // Generate chunks for all 10 biome types
        for (int cx = 0; cx < 50; cx++)
        {
            var chunk = new Chunk(cx, 0);
            gen.Generate(chunk, 42);

            int floorCount = 0;
            for (int x = 0; x < Chunk.Size; x++)
                for (int y = 0; y < Chunk.Size; y++)
                    if (chunk.Tiles[x, y].Type == TileType.Floor) floorCount++;

            Assert.True(floorCount > 0, $"Chunk at ({cx},0) should have floor tiles");
        }
    }

    [Fact]
    public void Generate_UsesMatchingGenerator_ForStructuredBiomes()
    {
        // Stone biome should produce BSP-style rooms (rectangular open areas)
        var gen = new BiomeDungeonGenerator();
        Chunk? stoneChunk = null;
        for (int cx = 0; cx < 100; cx++)
        {
            if (BiomeDefinitions.GetBiomeForChunk(cx, 0, 42) == BiomeType.Stone)
            {
                stoneChunk = new Chunk(cx, 0);
                gen.Generate(stoneChunk, 42);
                break;
            }
        }

        Assert.NotNull(stoneChunk);

        // BSP rooms have stairs
        bool hasStairsUp = false, hasStairsDown = false;
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
            {
                if (stoneChunk.Tiles[x, y].Type == TileType.StairsUp) hasStairsUp = true;
                if (stoneChunk.Tiles[x, y].Type == TileType.StairsDown) hasStairsDown = true;
            }
        Assert.True(hasStairsUp, "Stone biome should have stairs up");
        Assert.True(hasStairsDown, "Stone biome should have stairs down");
    }

    [Fact]
    public void Generate_UsesMatchingGenerator_ForCaveBiomes()
    {
        var gen = new BiomeDungeonGenerator();
        Chunk? caveChunk = null;
        for (int cx = 0; cx < 100; cx++)
        {
            var biome = BiomeDefinitions.GetBiomeForChunk(cx, 0, 42);
            if (biome is BiomeType.Lava or BiomeType.Forest or BiomeType.Fungal or BiomeType.Infernal)
            {
                caveChunk = new Chunk(cx, 0);
                gen.Generate(caveChunk, 42);
                break;
            }
        }

        Assert.NotNull(caveChunk);

        // Cave generator should produce many floor tiles
        int floorCount = 0;
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                if (caveChunk.Tiles[x, y].Type == TileType.Floor) floorCount++;

        Assert.True(floorCount > 100, "Cave biome should have many floor tiles");
    }

    [Fact]
    public void Generate_UsesMatchingGenerator_ForTunnelBiomes()
    {
        var gen = new BiomeDungeonGenerator();
        Chunk? tunnelChunk = null;
        for (int cx = 0; cx < 100; cx++)
        {
            var biome = BiomeDefinitions.GetBiomeForChunk(cx, 0, 42);
            if (biome is BiomeType.Ice or BiomeType.Sewer)
            {
                tunnelChunk = new Chunk(cx, 0);
                gen.Generate(tunnelChunk, 42);
                break;
            }
        }

        Assert.NotNull(tunnelChunk);

        int floorCount = 0;
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                if (tunnelChunk.Tiles[x, y].Type == TileType.Floor) floorCount++;

        Assert.True(floorCount > 100, "Tunnel biome should have many floor tiles");
    }

    [Fact]
    public void Generate_IsDeterministic_AcrossBiomes()
    {
        var gen = new BiomeDungeonGenerator();

        for (int cx = 0; cx < 20; cx++)
        {
            var c1 = new Chunk(cx, 0);
            var c2 = new Chunk(cx, 0);
            gen.Generate(c1, 42);
            gen.Generate(c2, 42);

            for (int x = 0; x < Chunk.Size; x++)
                for (int y = 0; y < Chunk.Size; y++)
                    Assert.Equal(c1.Tiles[x, y].Type, c2.Tiles[x, y].Type);
        }
    }
}
