using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Core.Tests;

public class BiomeDungeonGeneratorTests
{
    [Fact]
    public void Generate_AllBiomes_ProduceFloorTiles()
    {
        var gen = new BiomeDungeonGenerator(42);

        // Generate chunks for all 10 biome types
        for (int cx = 0; cx < 50; cx++)
        {
            var result = gen.Generate(Position.FromCoords(cx, 0, Position.DefaultZ));

            int floorCount = 0;
            for (int x = 0; x < Chunk.Size; x++)
                for (int y = 0; y < Chunk.Size; y++)
                    if (result.Chunk.Tiles[x, y].Type == TileType.Floor) floorCount++;

            Assert.True(floorCount > 0, $"Chunk at ({cx},0) should have floor tiles");
        }
    }

    [Fact]
    public void Generate_UsesMatchingGenerator_ForStructuredBiomes()
    {
        // Stone biome should produce BSP-style rooms (rectangular open areas)
        var gen = new BiomeDungeonGenerator(42);
        Chunk? stoneChunk = null;
        for (int cx = 0; cx < 100; cx++)
        {
            if (BiomeDefinitions.GetBiomeForChunk(Position.FromCoords(cx, 0, 0), 42) == BiomeType.Stone)
            {
                stoneChunk = gen.Generate(Position.FromCoords(cx, 0, Position.DefaultZ)).Chunk;
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
        var gen = new BiomeDungeonGenerator(42);
        Chunk? caveChunk = null;
        for (int cx = 0; cx < 100; cx++)
        {
            var biome = BiomeDefinitions.GetBiomeForChunk(Position.FromCoords(cx, 0, 0), 42);
            if (biome is BiomeType.Lava or BiomeType.Forest or BiomeType.Fungal or BiomeType.Infernal)
            {
                caveChunk = gen.Generate(Position.FromCoords(cx, 0, Position.DefaultZ)).Chunk;
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
        var gen = new BiomeDungeonGenerator(42);
        Chunk? tunnelChunk = null;
        for (int cx = 0; cx < 100; cx++)
        {
            var biome = BiomeDefinitions.GetBiomeForChunk(Position.FromCoords(cx, 0, 0), 42);
            if (biome is BiomeType.Ice or BiomeType.Sewer)
            {
                tunnelChunk = gen.Generate(Position.FromCoords(cx, 0, Position.DefaultZ)).Chunk;
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
        var gen = new BiomeDungeonGenerator(42);

        for (int cx = 0; cx < 20; cx++)
        {
            var r1 = gen.Generate(Position.FromCoords(cx, 0, Position.DefaultZ));
            var r2 = gen.Generate(Position.FromCoords(cx, 0, Position.DefaultZ));

            for (int x = 0; x < Chunk.Size; x++)
                for (int y = 0; y < Chunk.Size; y++)
                    Assert.Equal(r1.Chunk.Tiles[x, y].Type, r2.Chunk.Tiles[x, y].Type);
        }
    }
}
