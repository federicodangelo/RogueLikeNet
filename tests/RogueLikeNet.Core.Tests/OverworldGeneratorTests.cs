using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Core.Tests;

public class OverworldGeneratorTests
{
    [Fact]
    public void Generate_ProducesFloorTiles()
    {
        var gen = new OverworldGenerator(42);
        var result = gen.Generate(0, 0);

        int floorCount = 0;
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                if (result.Chunk.Tiles[x, y].Type == TileType.Floor) floorCount++;

        Assert.True(floorCount > 100, $"Expected many floor tiles, got {floorCount}");
    }

    [Fact]
    public void Generate_HasWalls()
    {
        var gen = new OverworldGenerator(42);
        var result = gen.Generate(0, 0);

        int wallCount = 0;
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                if (result.Chunk.Tiles[x, y].Type == TileType.Wall) wallCount++;

        Assert.True(wallCount > 0, "Overworld should have walls");
    }

    [Fact]
    public void Generate_IsDeterministic()
    {
        var gen = new OverworldGenerator(42);
        var result1 = gen.Generate(3, -2);
        var result2 = gen.Generate(3, -2);

        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
            {
                Assert.Equal(result1.Chunk.Tiles[x, y].Type, result2.Chunk.Tiles[x, y].Type);
                Assert.Equal(result1.Chunk.Tiles[x, y].GlyphId, result2.Chunk.Tiles[x, y].GlyphId);
            }
    }

    [Fact]
    public void Generate_ProducesMonsters()
    {
        var gen = new OverworldGenerator(42);
        // Generate a few chunks to ensure we get monsters
        int totalMonsters = 0;
        for (int cx = 0; cx < 5; cx++)
        {
            var result = gen.Generate(cx, 0);
            totalMonsters += result.Monsters.Count;
        }

        Assert.True(totalMonsters > 0, "Overworld should produce monsters");
    }

    [Fact]
    public void Generate_AdjacentChunks_HaveContinuousTerrain()
    {
        var gen = new OverworldGenerator(42);

        // Generate two horizontally adjacent chunks
        var left = gen.Generate(0, 0).Chunk;
        var right = gen.Generate(1, 0).Chunk;

        // Check border tiles: the rightmost column of 'left' vs leftmost column of 'right'
        // should not be a solid wall barrier. Count matching floor/wall patterns.
        int leftEdgeFloors = 0;
        int rightEdgeFloors = 0;
        for (int y = 0; y < Chunk.Size; y++)
        {
            if (left.Tiles[Chunk.Size - 1, y].IsWalkable) leftEdgeFloors++;
            if (right.Tiles[0, y].IsWalkable) rightEdgeFloors++;
        }

        // Both edges should have some walkable tiles (not solid wall barriers)
        Assert.True(leftEdgeFloors > 0, "Left chunk right edge should have walkable tiles");
        Assert.True(rightEdgeFloors > 0, "Right chunk left edge should have walkable tiles");
    }

    [Fact]
    public void Generate_VerticallyAdjacentChunks_HaveContinuousTerrain()
    {
        var gen = new OverworldGenerator(42);

        var top = gen.Generate(0, 0).Chunk;
        var bottom = gen.Generate(0, 1).Chunk;

        int topEdgeFloors = 0;
        int bottomEdgeFloors = 0;
        for (int x = 0; x < Chunk.Size; x++)
        {
            if (top.Tiles[x, Chunk.Size - 1].IsWalkable) topEdgeFloors++;
            if (bottom.Tiles[x, 0].IsWalkable) bottomEdgeFloors++;
        }

        Assert.True(topEdgeFloors > 0, "Top chunk bottom edge should have walkable tiles");
        Assert.True(bottomEdgeFloors > 0, "Bottom chunk top edge should have walkable tiles");
    }

    [Fact]
    public void Generate_NoVoidTiles()
    {
        var gen = new OverworldGenerator(42);
        var result = gen.Generate(0, 0);

        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                Assert.NotEqual(TileType.Void, result.Chunk.Tiles[x, y].Type);
    }

    [Fact]
    public void Generate_PlacesDecorations()
    {
        var gen = new OverworldGenerator(42);
        int totalDecorations = 0;
        for (int cx = 0; cx < 5; cx++)
        {
            var result = gen.Generate(cx, 0);
            for (int x = 0; x < Chunk.Size; x++)
                for (int y = 0; y < Chunk.Size; y++)
                    if (result.Chunk.Tiles[x, y].Type == TileType.Decoration)
                        totalDecorations++;
        }

        Assert.True(totalDecorations > 0, "Overworld should place decorations");
    }

    [Fact]
    public void Generate_DifferentSeedsProduceDifferentTerrain()
    {
        var gen1 = new OverworldGenerator(42);
        var gen2 = new OverworldGenerator(99999);
        var result1 = gen1.Generate(0, 0);
        var result2 = gen2.Generate(0, 0);

        int differences = 0;
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                if (result1.Chunk.Tiles[x, y].Type != result2.Chunk.Tiles[x, y].Type)
                    differences++;

        Assert.True(differences > 0, "Different seeds should produce different terrain");
    }
}
