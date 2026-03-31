using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Components;
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
        var result = gen.Generate(0, 0, Position.DefaultZ);

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
        var result = gen.Generate(0, 0, Position.DefaultZ);

        int wallCount = 0;
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                if (result.Chunk.Tiles[x, y].Type == TileType.Blocked) wallCount++;

        Assert.True(wallCount > 0, "Overworld should have walls");
    }

    [Fact]
    public void Generate_IsDeterministic()
    {
        var gen = new OverworldGenerator(42);
        var result1 = gen.Generate(3, -2, Position.DefaultZ);
        var result2 = gen.Generate(3, -2, Position.DefaultZ);

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
            var result = gen.Generate(cx, 0, Position.DefaultZ);
            totalMonsters += result.Monsters.Count;
        }

        Assert.True(totalMonsters > 0, "Overworld should produce monsters");
    }

    [Fact]
    public void Generate_AdjacentChunks_HaveContinuousTerrain()
    {
        var gen = new OverworldGenerator(42);

        // Generate two horizontally adjacent chunks
        var left = gen.Generate(0, 0, Position.DefaultZ).Chunk;
        var right = gen.Generate(1, 0, Position.DefaultZ).Chunk;

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

        var top = gen.Generate(0, 0, Position.DefaultZ).Chunk;
        var bottom = gen.Generate(0, 1, Position.DefaultZ).Chunk;

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
        var result = gen.Generate(0, 0, Position.DefaultZ);

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
            var result = gen.Generate(cx, 0, Position.DefaultZ);
            for (int x = 0; x < Chunk.Size; x++)
                for (int y = 0; y < Chunk.Size; y++)
                {
                    ref var t = ref result.Chunk.Tiles[x, y];
                    if (t.Type == TileType.Floor && t.GlyphId != TileDefinitions.GlyphFloor)
                        totalDecorations++;
                }
        }

        Assert.True(totalDecorations > 0, "Overworld should place decorations");
    }

    [Fact]
    public void Generate_DifferentSeedsProduceDifferentTerrain()
    {
        var gen1 = new OverworldGenerator(42);
        var gen2 = new OverworldGenerator(99999);
        var result1 = gen1.Generate(0, 0, Position.DefaultZ);
        var result2 = gen2.Generate(0, 0, Position.DefaultZ);

        int differences = 0;
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                if (result1.Chunk.Tiles[x, y].Type != result2.Chunk.Tiles[x, y].Type)
                    differences++;

        Assert.True(differences > 0, "Different seeds should produce different terrain");
    }

    [Fact]
    public void Exists_DefaultZMinus1_TrueForSomeChunks()
    {
        var gen = new OverworldGenerator(42);
        int caveCount = 0;
        // Scan a grid of chunks; at least some should have caves
        for (int cx = -10; cx <= 10; cx++)
            for (int cy = -10; cy <= 10; cy++)
                if (gen.Exists(cx, cy, Position.DefaultZ - 1))
                    caveCount++;

        Assert.True(caveCount > 0, "Some chunks should have caves at DefaultZ - 1");
        Assert.True(caveCount < 21 * 21, "Not every chunk should have a cave");
    }

    [Fact]
    public void Exists_DefaultZMinus2_AlwaysFalse()
    {
        var gen = new OverworldGenerator(42);
        for (int cx = -5; cx <= 5; cx++)
            for (int cy = -5; cy <= 5; cy++)
                Assert.False(gen.Exists(cx, cy, Position.DefaultZ - 2));
    }

    [Fact]
    public void Generate_CaveChunk_HasUpStairsAndRooms()
    {
        var gen = new OverworldGenerator(42);

        // Find a chunk that has a cave
        (int cx, int cy) caveChunk = default;
        bool found = false;
        for (int cx = -20; cx <= 20 && !found; cx++)
            for (int cy = -20; cy <= 20 && !found; cy++)
                if (gen.Exists(cx, cy, Position.DefaultZ - 1))
                {
                    caveChunk = (cx, cy);
                    found = true;
                }

        Assert.True(found, "Should find at least one cave chunk in a 41x41 grid");

        var result = gen.Generate(caveChunk.cx, caveChunk.cy, Position.DefaultZ - 1);

        // Should have an up-stair
        int upStairs = 0;
        int downStairs = 0;
        int floorCount = 0;
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
            {
                var type = result.Chunk.Tiles[x, y].Type;
                if (type == TileType.StairsUp) upStairs++;
                if (type == TileType.StairsDown) downStairs++;
                if (type == TileType.Floor) floorCount++;
            }

        Assert.Equal(1, upStairs);
        Assert.Equal(0, downStairs); // Can't go deeper
        Assert.True(floorCount > 50, $"Cave should have floor tiles, got {floorCount}");
    }

    [Fact]
    public void Generate_SurfaceChunkWithCave_HasDownStairs()
    {
        var gen = new OverworldGenerator(42);

        // Find a chunk that has a cave below
        (int cx, int cy) caveChunk = default;
        bool found = false;
        for (int cx = -20; cx <= 20 && !found; cx++)
            for (int cy = -20; cy <= 20 && !found; cy++)
                if (gen.Exists(cx, cy, Position.DefaultZ - 1))
                {
                    caveChunk = (cx, cy);
                    found = true;
                }

        Assert.True(found);

        var surface = gen.Generate(caveChunk.cx, caveChunk.cy, Position.DefaultZ);

        // The surface chunk should have a StairsDown at the entrance
        int downStairs = 0;
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                if (surface.Chunk.Tiles[x, y].Type == TileType.StairsDown)
                    downStairs++;

        Assert.Equal(1, downStairs);
    }

    [Fact]
    public void Generate_CaveAndSurface_StairsAtSamePosition()
    {
        var gen = new OverworldGenerator(42);

        // Find a chunk that has a cave
        (int cx, int cy) caveChunk = default;
        bool found = false;
        for (int cx = -20; cx <= 20 && !found; cx++)
            for (int cy = -20; cy <= 20 && !found; cy++)
                if (gen.Exists(cx, cy, Position.DefaultZ - 1))
                {
                    caveChunk = (cx, cy);
                    found = true;
                }

        Assert.True(found);

        var surface = gen.Generate(caveChunk.cx, caveChunk.cy, Position.DefaultZ);
        var cave = gen.Generate(caveChunk.cx, caveChunk.cy, Position.DefaultZ - 1);

        // Find StairsDown on surface
        (int x, int y) surfaceStair = default;
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                if (surface.Chunk.Tiles[x, y].Type == TileType.StairsDown)
                    surfaceStair = (x, y);

        // Find StairsUp in cave
        (int x, int y) caveStair = default;
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                if (cave.Chunk.Tiles[x, y].Type == TileType.StairsUp)
                    caveStair = (x, y);

        // They must be at the same local position
        Assert.Equal(surfaceStair, caveStair);
    }
}
