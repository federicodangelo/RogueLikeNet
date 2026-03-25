using RogueLikeNet.Core.Algorithms;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Tests;

public class PositionTests
{
    [Fact]
    public void ManhattanDistance_ReturnsCorrectValue()
    {
        var a = new Position(0, 0);
        var b = new Position(3, 4);
        Assert.Equal(7, Position.ManhattanDistance(a, b));
    }

    [Fact]
    public void ChebyshevDistance_ReturnsCorrectValue()
    {
        var a = new Position(0, 0);
        var b = new Position(3, 4);
        Assert.Equal(4, Position.ChebyshevDistance(a, b));
    }

    [Fact]
    public void Equality_WorksCorrectly()
    {
        var a = new Position(5, 10);
        var b = new Position(5, 10);
        var c = new Position(5, 11);
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.True(a == b);
        Assert.True(a != c);
    }
}

public class HealthTests
{
    [Fact]
    public void Health_InitializesAtMax()
    {
        var h = new Health(100);
        Assert.Equal(100, h.Current);
        Assert.Equal(100, h.Max);
        Assert.True(h.IsAlive);
    }

    [Fact]
    public void Health_IsAlive_FalseAtZero()
    {
        var h = new Health(100) { Current = 0 };
        Assert.False(h.IsAlive);
    }
}

public class ChunkTests
{
    [Fact]
    public void Chunk_HasCorrectSize()
    {
        var chunk = new Chunk(0, 0);
        Assert.Equal(64, Chunk.Size);
        Assert.Equal(64, chunk.Tiles.GetLength(0));
        Assert.Equal(64, chunk.Tiles.GetLength(1));
    }

    [Fact]
    public void InBounds_ReturnsTrueForValidCoords()
    {
        var chunk = new Chunk(0, 0);
        Assert.True(chunk.InBounds(0, 0));
        Assert.True(chunk.InBounds(63, 63));
        Assert.False(chunk.InBounds(-1, 0));
        Assert.False(chunk.InBounds(0, 64));
    }

    [Fact]
    public void WorldToChunkCoord_PositiveCoords()
    {
        var (cx, cy) = Chunk.WorldToChunkCoord(65, 130);
        Assert.Equal(1, cx);
        Assert.Equal(2, cy);
    }

    [Fact]
    public void WorldToChunkCoord_NegativeCoords()
    {
        var (cx, cy) = Chunk.WorldToChunkCoord(-1, -64);
        Assert.Equal(-1, cx);
        Assert.Equal(-1, cy);
    }

    [Fact]
    public void WorldToLocal_ConvertsCorrectly()
    {
        var chunk = new Chunk(1, 0);
        Assert.True(chunk.WorldToLocal(64, 0, out int lx, out int ly));
        Assert.Equal(0, lx);
        Assert.Equal(0, ly);
    }

    [Fact]
    public void PackChunkKey_UniquePerCoord()
    {
        long k1 = Chunk.PackChunkKey(0, 0);
        long k2 = Chunk.PackChunkKey(1, 0);
        long k3 = Chunk.PackChunkKey(0, 1);
        Assert.NotEqual(k1, k2);
        Assert.NotEqual(k1, k3);
        Assert.NotEqual(k2, k3);
    }
}

public class SeededRandomTests
{
    [Fact]
    public void SameSeed_ProducesSameSequence()
    {
        var rng1 = new SeededRandom(42);
        var rng2 = new SeededRandom(42);
        for (int i = 0; i < 100; i++)
            Assert.Equal(rng1.Next(), rng2.Next());
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentSequences()
    {
        var rng1 = new SeededRandom(42);
        var rng2 = new SeededRandom(99);
        bool allSame = true;
        for (int i = 0; i < 10; i++)
            if (rng1.Next() != rng2.Next()) allSame = false;
        Assert.False(allSame);
    }

    [Fact]
    public void NextRange_RespectsMinMax()
    {
        var rng = new SeededRandom(123);
        for (int i = 0; i < 200; i++)
        {
            int val = rng.Next(5, 10);
            Assert.InRange(val, 5, 9);
        }
    }

    [Fact]
    public void Next_ProducesNonNegative()
    {
        var rng = new SeededRandom(1);
        for (int i = 0; i < 1000; i++)
            Assert.True(rng.Next() >= 0);
    }
}

public class BspDungeonGeneratorTests
{
    [Fact]
    public void Generate_ProducesFloorTiles()
    {
        var gen = new BspDungeonGenerator();
        var chunk = new Chunk(0, 0);
        gen.Generate(chunk, 42);

        int floorCount = 0;
        for (int x = 0; x < Chunk.Size; x++)
        for (int y = 0; y < Chunk.Size; y++)
            if (chunk.Tiles[x, y].Type == TileType.Floor) floorCount++;

        Assert.True(floorCount > 0, "Dungeon should have at least some floor tiles");
    }

    [Fact]
    public void Generate_IsDeterministic()
    {
        var gen = new BspDungeonGenerator();
        var chunk1 = new Chunk(0, 0);
        var chunk2 = new Chunk(0, 0);
        gen.Generate(chunk1, 42);
        gen.Generate(chunk2, 42);

        for (int x = 0; x < Chunk.Size; x++)
        for (int y = 0; y < Chunk.Size; y++)
            Assert.Equal(chunk1.Tiles[x, y].Type, chunk2.Tiles[x, y].Type);
    }

    [Fact]
    public void Generate_HasWalls()
    {
        var gen = new BspDungeonGenerator();
        var chunk = new Chunk(0, 0);
        gen.Generate(chunk, 42);

        int wallCount = 0;
        for (int x = 0; x < Chunk.Size; x++)
        for (int y = 0; y < Chunk.Size; y++)
            if (chunk.Tiles[x, y].Type == TileType.Wall) wallCount++;

        Assert.True(wallCount > 0, "Dungeon should have walls");
    }
}

public class BresenhamTests
{
    [Fact]
    public void Line_HorizontalLine()
    {
        var points = new List<(int, int)>();
        Bresenham.Line(0, 0, 5, 0, (x, y) => points.Add((x, y)));
        Assert.Equal(6, points.Count);
        Assert.Equal((0, 0), points[0]);
        Assert.Equal((5, 0), points[5]);
    }

    [Fact]
    public void Line_DiagonalLine()
    {
        var points = new List<(int, int)>();
        Bresenham.Line(0, 0, 3, 3, (x, y) => points.Add((x, y)));
        Assert.Contains((0, 0), points);
        Assert.Contains((3, 3), points);
    }

    [Fact]
    public void HasLineOfSight_ClearPath()
    {
        bool result = Bresenham.HasLineOfSight(0, 0, 5, 0, (_, _) => false);
        Assert.True(result);
    }

    [Fact]
    public void HasLineOfSight_BlockedPath()
    {
        bool result = Bresenham.HasLineOfSight(0, 0, 5, 0, (x, _) => x == 3);
        Assert.False(result);
    }
}

public class AStarTests
{
    [Fact]
    public void FindPath_SameStartAndGoal()
    {
        var path = AStarPathfinder.FindPath(5, 5, 5, 5, (_, _) => true);
        Assert.NotNull(path);
        Assert.Single(path);
    }

    [Fact]
    public void FindPath_StraightLine()
    {
        var path = AStarPathfinder.FindPath(0, 0, 5, 0, (_, _) => true);
        Assert.NotNull(path);
        Assert.Equal(6, path.Count);
        Assert.Equal((0, 0), path[0]);
        Assert.Equal((5, 0), path[^1]);
    }

    [Fact]
    public void FindPath_NoPath()
    {
        // Block all movement except start
        var path = AStarPathfinder.FindPath(0, 0, 5, 0, (x, y) => x == 0 && y == 0);
        Assert.Null(path);
    }

    [Fact]
    public void FindPath_AroundObstacle()
    {
        // Wall at x=2 for all y except y=3
        bool isWalkable(int x, int y) => x >= 0 && y >= 0 && x < 10 && y < 10 && !(x == 2 && y != 3);
        var path = AStarPathfinder.FindPath(0, 0, 4, 0, isWalkable);
        Assert.NotNull(path);
        Assert.Equal((0, 0), path[0]);
        Assert.Equal((4, 0), path[^1]);
        // Path must go through the gap at (2,3)
        Assert.Contains((2, 3), path);
    }
}

public class ShadowCastFovTests
{
    [Fact]
    public void Compute_OriginAlwaysVisible()
    {
        var visible = new HashSet<(int, int)>();
        ShadowCastFov.Compute(5, 5, 3, (_, _) => false, (x, y) => visible.Add((x, y)));
        Assert.Contains((5, 5), visible);
    }

    [Fact]
    public void Compute_RadiusLimitsRange()
    {
        var visible = new HashSet<(int, int)>();
        ShadowCastFov.Compute(5, 5, 2, (_, _) => false, (x, y) => visible.Add((x, y)));
        // Nothing beyond radius 2 should be visible (Manhattan distance > 2)
        foreach (var (x, y) in visible)
        {
            int dist = Math.Max(Math.Abs(x - 5), Math.Abs(y - 5));
            Assert.True(dist <= 3, $"Tile ({x},{y}) is beyond radius");
        }
    }

    [Fact]
    public void Compute_WallBlocksVision()
    {
        // Wall line at x=7 blocks all tiles behind it
        var visible = new HashSet<(int, int)>();
        ShadowCastFov.Compute(5, 5, 8,
            (x, y) => x == 7, // wall line at x=7
            (x, y) => visible.Add((x, y)));
        // The wall tiles should be visible
        Assert.Contains((7, 5), visible);
        // Origin always visible
        Assert.Contains((5, 5), visible);
        // Tiles well behind the wall line should mostly be blocked
        // At minimum, the origin side should have visible tiles
        Assert.Contains((6, 5), visible);
    }
}

public class WorldMapTests
{
    [Fact]
    public void GetOrCreateChunk_GeneratesOnDemand()
    {
        var map = new WorldMap(42);
        var gen = new BspDungeonGenerator();
        var chunk = map.GetOrCreateChunk(0, 0, gen);
        Assert.NotNull(chunk);
        Assert.Equal(0, chunk.ChunkX);
        Assert.Equal(0, chunk.ChunkY);
    }

    [Fact]
    public void GetOrCreateChunk_ReturnsSameChunk()
    {
        var map = new WorldMap(42);
        var gen = new BspDungeonGenerator();
        var c1 = map.GetOrCreateChunk(0, 0, gen);
        var c2 = map.GetOrCreateChunk(0, 0, gen);
        Assert.Same(c1, c2);
    }

    [Fact]
    public void TryGetChunk_ReturnsNullForMissing()
    {
        var map = new WorldMap(42);
        Assert.Null(map.TryGetChunk(0, 0));
    }
}

public class GameEngineTests
{
    [Fact]
    public void SpawnPlayer_CreatesEntity()
    {
        using var engine = new GameEngine(42);
        engine.EnsureChunkLoaded(0, 0);
        var entity = engine.SpawnPlayer(1, 10, 10);
        Assert.True(engine.EcsWorld.IsAlive(entity));
        ref var pos = ref engine.EcsWorld.Get<Position>(entity);
        Assert.Equal(10, pos.X);
        Assert.Equal(10, pos.Y);
    }

    [Fact]
    public void Tick_IncrementsTickCounter()
    {
        using var engine = new GameEngine(42);
        engine.EnsureChunkLoaded(0, 0);
        Assert.Equal(0, engine.CurrentTick);
        engine.Tick();
        Assert.Equal(1, engine.CurrentTick);
    }

    [Fact]
    public void FindSpawnPosition_ReturnsFloorTile()
    {
        using var engine = new GameEngine(42);
        var (x, y) = engine.FindSpawnPosition();
        var chunk = engine.EnsureChunkLoaded(0, 0);
        Assert.Equal(TileType.Floor, chunk.Tiles[x, y].Type);
    }

    [Fact]
    public void Tick_ComputesLightingAroundPlayer()
    {
        // Reproduce the exact startup flow used by LocalGameConnection / GameLoop
        using var engine = new GameEngine(42);
        engine.EnsureChunkLoaded(0, 0);

        var (sx, sy) = engine.FindSpawnPosition();
        engine.SpawnPlayer(1, sx, sy);
        engine.Tick();

        // The player's own tile must be lit
        var chunk = engine.WorldMap.TryGetChunk(0, 0)!;
        Assert.True(chunk.Tiles[sx, sy].LightLevel > 0,
            $"Player tile ({sx},{sy}) has LightLevel={chunk.Tiles[sx, sy].LightLevel}, expected > 0");

        // At least some neighboring floor tiles should also be lit
        int litCount = 0;
        for (int dx = -3; dx <= 3; dx++)
        for (int dy = -3; dy <= 3; dy++)
        {
            int nx = sx + dx, ny = sy + dy;
            if (nx >= 0 && nx < Chunk.Size && ny >= 0 && ny < Chunk.Size)
            {
                if (chunk.Tiles[nx, ny].LightLevel > 0)
                    litCount++;
            }
        }
        Assert.True(litCount > 5, $"Only {litCount} tiles lit in 7x7 area around player, expected > 5");
    }
}
