using RogueLikeNet.Core.Definitions;
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
        var result = gen.Generate(0, 0);

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
        var result = gen.Generate(0, 0);

        int wallCount = 0;
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                if (result.Chunk.Tiles[x, y].Type == TileType.Wall) wallCount++;

        Assert.True(wallCount > 0, "Cave should have walls");
    }

    [Fact]
    public void Generate_IsDeterministic()
    {
        var gen = new CellularAutomataCaveGenerator(42);
        var result1 = gen.Generate(0, 0);
        var result2 = gen.Generate(0, 0);

        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                Assert.Equal(result1.Chunk.Tiles[x, y].Type, result2.Chunk.Tiles[x, y].Type);
    }

    [Fact]
    public void Generate_ProducesMonsters()
    {
        var gen = new CellularAutomataCaveGenerator(42);
        var result = gen.Generate(0, 0);

        Assert.True(result.Monsters.Count > 0, "Cave should produce monsters");
    }

    [Fact]
    public void Generate_PlacesDecorations()
    {
        var gen = new CellularAutomataCaveGenerator(42);
        int totalDecorations = 0;
        for (int cx = 0; cx < 10; cx++)
        {
            var result = gen.Generate(cx, 0);
            for (int x = 0; x < Chunk.Size; x++)
                for (int y = 0; y < Chunk.Size; y++)
                    if (result.Chunk.Tiles[x, y].Type == TileType.Decoration)
                        totalDecorations++;
        }

        Assert.True(totalDecorations > 0, "Cave generator should place decorations");
    }
}
