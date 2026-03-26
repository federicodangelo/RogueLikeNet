using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Core.Tests;

public class OverworldGeneratorTests
{
    [Fact]
    public void Generate_ProducesFloorTiles()
    {
        var gen = new OverworldGenerator();
        var chunk = new Chunk(0, 0);
        gen.Generate(chunk, 42);

        int floorCount = 0;
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                if (chunk.Tiles[x, y].Type == TileType.Floor) floorCount++;

        Assert.True(floorCount > 100, $"Expected many floor tiles, got {floorCount}");
    }

    [Fact]
    public void Generate_HasWalls()
    {
        var gen = new OverworldGenerator();
        var chunk = new Chunk(0, 0);
        gen.Generate(chunk, 42);

        int wallCount = 0;
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                if (chunk.Tiles[x, y].Type == TileType.Wall) wallCount++;

        Assert.True(wallCount > 0, "Overworld should have walls");
    }

    [Fact]
    public void Generate_IsDeterministic()
    {
        var gen = new OverworldGenerator();
        var chunk1 = new Chunk(3, -2);
        var chunk2 = new Chunk(3, -2);
        gen.Generate(chunk1, 42);
        gen.Generate(chunk2, 42);

        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
            {
                Assert.Equal(chunk1.Tiles[x, y].Type, chunk2.Tiles[x, y].Type);
                Assert.Equal(chunk1.Tiles[x, y].GlyphId, chunk2.Tiles[x, y].GlyphId);
            }
    }

    [Fact]
    public void Generate_ProducesSpawnPoints()
    {
        var gen = new OverworldGenerator();
        // Generate a few chunks to ensure we get spawns
        int totalSpawns = 0;
        for (int cx = 0; cx < 5; cx++)
        {
            var chunk = new Chunk(cx, 0);
            var result = gen.Generate(chunk, 42);
            totalSpawns += result.SpawnPoints.Count;
        }

        Assert.True(totalSpawns > 0, "Overworld should produce spawn points");
    }

    [Fact]
    public void Generate_AdjacentChunks_HaveContinuousTerrain()
    {
        var gen = new OverworldGenerator();
        long seed = 42;

        // Generate two horizontally adjacent chunks
        var left = new Chunk(0, 0);
        var right = new Chunk(1, 0);
        gen.Generate(left, seed);
        gen.Generate(right, seed);

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
        var gen = new OverworldGenerator();
        long seed = 42;

        var top = new Chunk(0, 0);
        var bottom = new Chunk(0, 1);
        gen.Generate(top, seed);
        gen.Generate(bottom, seed);

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
        var gen = new OverworldGenerator();
        var chunk = new Chunk(0, 0);
        gen.Generate(chunk, 42);

        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                Assert.NotEqual(TileType.Void, chunk.Tiles[x, y].Type);
    }

    [Fact]
    public void Generate_PlacesDecorations()
    {
        var gen = new OverworldGenerator();
        int totalDecorations = 0;
        for (int cx = 0; cx < 5; cx++)
        {
            var chunk = new Chunk(cx, 0);
            gen.Generate(chunk, 42);
            for (int x = 0; x < Chunk.Size; x++)
                for (int y = 0; y < Chunk.Size; y++)
                    if (chunk.Tiles[x, y].Type == TileType.Decoration)
                        totalDecorations++;
        }

        Assert.True(totalDecorations > 0, "Overworld should place decorations");
    }

    [Fact]
    public void Generate_DifferentSeedsProduceDifferentTerrain()
    {
        var gen = new OverworldGenerator();
        var chunk1 = new Chunk(0, 0);
        var chunk2 = new Chunk(0, 0);
        gen.Generate(chunk1, 42);
        gen.Generate(chunk2, 99999);

        int differences = 0;
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                if (chunk1.Tiles[x, y].Type != chunk2.Tiles[x, y].Type)
                    differences++;

        Assert.True(differences > 0, "Different seeds should produce different terrain");
    }
}
