using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Core.Tests;

public class CellularAutomataCaveGeneratorTests
{
    [Fact]
    public void Generate_ProducesFloorTiles()
    {
        var gen = new CellularAutomataCaveGenerator(42);
        var result = gen.Generate(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        int floorCount = 0;
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                if (result.Chunk.Tiles[x, y].Type == TileType.Floor) floorCount++;

        Assert.True(floorCount > 0, "Cave should have floor tiles");
    }

    [Fact]
    public void Generate_HasWalls()
    {
        var gen = new CellularAutomataCaveGenerator(42);
        var result = gen.Generate(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        int wallCount = 0;
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                if (result.Chunk.Tiles[x, y].Type == TileType.Blocked) wallCount++;

        Assert.True(wallCount > 0, "Cave should have walls");
    }

    [Fact]
    public void Generate_IsDeterministic()
    {
        var gen = new CellularAutomataCaveGenerator(42);
        var result1 = gen.Generate(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var result2 = gen.Generate(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                Assert.Equal(result1.Chunk.Tiles[x, y].Type, result2.Chunk.Tiles[x, y].Type);
    }

    [Fact]
    public void Generate_ProducesMonsters()
    {
        var gen = new CellularAutomataCaveGenerator(42);
        var result = gen.Generate(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        Assert.True(result.Monsters.Count > 0, "Cave should produce monsters");
    }

    [Fact]
    public void Generate_PlacesDecorations()
    {
        var gen = new CellularAutomataCaveGenerator(42);
        int totalDecorations = 0;
        for (int cx = 0; cx < 10; cx++)
        {
            var biome = BiomeRegistry.GetBiomeForChunk(ChunkPosition.FromCoords(cx, 0, 0), 42);
            int floorTileId = GameData.Instance.Biomes.GetFloorTileId(biome);
            var result = gen.Generate(ChunkPosition.FromCoords(cx, 0, Position.DefaultZ));
            for (int x = 0; x < Chunk.Size; x++)
                for (int y = 0; y < Chunk.Size; y++)
                {
                    ref var t = ref result.Chunk.Tiles[x, y];
                    if (t.Type == TileType.Floor && t.TileId != floorTileId)
                        totalDecorations++;
                }
        }

        Assert.True(totalDecorations > 0, "Cave generator should place decorations");
    }

    [Fact]
    public void Generate_AlwaysHasAtLeastTwoRooms()
    {
        // Generate with many different chunk coordinates to exercise ExtractRooms.
        // The fallback path kicks in when the cellular automata leaves < 2 qualifying rooms,
        // ensuring the generator always places up-stairs and down-stairs tiles.
        var gen = new CellularAutomataCaveGenerator(123456);
        int stairsUp = GameData.Instance.Tiles.GetNumericId("stairs_up");
        int stairsDown = GameData.Instance.Tiles.GetNumericId("stairs_down");
        for (int cx = -5; cx <= 5; cx++)
            for (int cy = -5; cy <= 5; cy++)
            {
                var result = gen.Generate(ChunkPosition.FromCoords(cx, cy, Position.DefaultZ));
                // The generator places stair-up and stair-down tiles in separate rooms.
                // If the fallback ran, rooms were synthesized to ensure >= 2.
                bool hasUp = false, hasDown = false;
                for (int x = 0; x < Chunk.Size && !(hasUp && hasDown); x++)
                    for (int y = 0; y < Chunk.Size && !(hasUp && hasDown); y++)
                    {
                        int tileId = result.Chunk.Tiles[x, y].TileId;
                        if (tileId == stairsUp) hasUp = true;
                        if (tileId == stairsDown) hasDown = true;
                    }
                Assert.True(hasUp && hasDown,
                    $"Chunk ({cx},{cy}): expected both stairs_up and stairs_down tiles");
            }
    }
}
