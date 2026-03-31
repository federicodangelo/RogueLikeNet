using RogueLikeNet.Core.Algorithms;
using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Core.Tests;

public class AStarTests
{
    private const int Z = Position.DefaultZ;

    private static TileWalkability AllWalkable(int x, int y, int z) => TileWalkability.Walkable;

    [Fact]
    public void FindPath_SameStartAndGoal()
    {
        var path = AStarPathfinder.FindPath(5, 5, Z, 5, 5, Z, AllWalkable);
        Assert.NotNull(path);
        Assert.Single(path);
        Assert.Equal((5, 5, Z), path[0]);
    }

    [Fact]
    public void FindPath_StraightLine()
    {
        var path = AStarPathfinder.FindPath(0, 0, Z, 5, 0, Z, AllWalkable);
        Assert.NotNull(path);
        Assert.Equal(6, path.Count);
        Assert.Equal((0, 0, Z), path[0]);
        Assert.Equal((5, 0, Z), path[^1]);
    }

    [Fact]
    public void FindPath_NoPath()
    {
        // Block all movement except start
        var path = AStarPathfinder.FindPath(0, 0, Z, 5, 0, Z,
            (x, y, z) => x == 0 && y == 0 ? TileWalkability.Walkable : TileWalkability.None);
        Assert.Null(path);
    }

    [Fact]
    public void FindPath_AroundObstacle()
    {
        // Wall at x=2 for all y except y=3
        TileWalkability isWalkable(int x, int y, int z) =>
            x >= 0 && y >= 0 && x < 10 && y < 10 && !(x == 2 && y != 3)
                ? TileWalkability.Walkable : TileWalkability.None;
        var path = AStarPathfinder.FindPath(0, 0, Z, 4, 0, Z, isWalkable);
        Assert.NotNull(path);
        Assert.Equal((0, 0, Z), path[0]);
        Assert.Equal((4, 0, Z), path[^1]);
        // Path must go through the gap at (2,3)
        Assert.Contains((2, 3, Z), path);
    }

    [Fact]
    public void FindPath_MaxStepsExceeded_ReturnsNull()
    {
        // Large open area but very few steps allowed
        var path = AStarPathfinder.FindPath(0, 0, Z, 50, 50, Z, AllWalkable, maxSteps: 5);
        Assert.Null(path);
    }

    [Fact]
    public void FindPath_ReturnsXYZ_Coordinates()
    {
        var path = AStarPathfinder.FindPath(0, 0, Z, 3, 0, Z, AllWalkable);
        Assert.NotNull(path);
        foreach (var step in path)
            Assert.Equal(Z, step.Z);
    }

    [Fact]
    public void FindPath_AcrossZLevels_ViaStairs()
    {
        // Setup: flat plane on Z=127, stairs-down at (5,0,127), stairs-up at (5,0,126), goal at (8,0,126)
        TileWalkability getWalkability(int x, int y, int z)
        {
            if (x < 0 || x > 10 || y < -1 || y > 1) return TileWalkability.None;
            if (z == Z && x == 5 && y == 0) return TileWalkability.StairsDown;
            if (z == Z - 1 && x == 5 && y == 0) return TileWalkability.StairsUp;
            if (z == Z || z == Z - 1) return TileWalkability.Walkable;
            return TileWalkability.None;
        }

        var path = AStarPathfinder.FindPath(0, 0, Z, 8, 0, Z - 1, getWalkability);
        Assert.NotNull(path);
        Assert.Equal((0, 0, Z), path[0]);
        Assert.Equal((8, 0, Z - 1), path[^1]);

        // Path must pass through the stair tile to change Z
        Assert.Contains((5, 0, Z), path);
        Assert.Contains((5, 0, Z - 1), path);
    }

    [Fact]
    public void FindPath_NoStairs_CannotCrossZ()
    {
        // Different Z, no stair tiles → no path
        var path = AStarPathfinder.FindPath(0, 0, Z, 0, 0, Z - 1, AllWalkable);
        Assert.Null(path);
    }
}
