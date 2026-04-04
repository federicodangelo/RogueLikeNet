using RogueLikeNet.Core.Algorithms;
using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Core.Tests;

public class AStarTests
{
    private const int Z = Position.DefaultZ;

    private static TileWalkability AllWalkable(Position p) => TileWalkability.Walkable;

    [Fact]
    public void FindPath_SameStartAndGoal()
    {
        var path = new AStarPathfinder().FindPath(Position.FromCoords(5, 5, Z), Position.FromCoords(5, 5, Z), AllWalkable);
        Assert.NotNull(path);
        Assert.Single(path);
        Assert.Equal(Position.FromCoords(5, 5, Z), path[0]);
    }

    [Fact]
    public void FindPath_StraightLine()
    {
        var path = new AStarPathfinder().FindPath(Position.FromCoords(0, 0, Z), Position.FromCoords(5, 0, Z), AllWalkable);
        Assert.NotNull(path);
        Assert.Equal(6, path.Count);
        Assert.Equal(Position.FromCoords(0, 0, Z), path[0]);
        Assert.Equal(Position.FromCoords(5, 0, Z), path[^1]);
    }

    [Fact]
    public void FindPath_NoPath()
    {
        // Block all movement except start
        var path = new AStarPathfinder().FindPath(Position.FromCoords(0, 0, Z), Position.FromCoords(5, 0, Z),
            p => p.X == 0 && p.Y == 0 ? TileWalkability.Walkable : TileWalkability.None);
        Assert.Null(path);
    }

    [Fact]
    public void FindPath_AroundObstacle()
    {
        // Wall at x=2 for all y except y=3
        TileWalkability isWalkable(Position p) =>
            p.X >= 0 && p.Y >= 0 && p.X < 10 && p.Y < 10 && !(p.X == 2 && p.Y != 3)
                ? TileWalkability.Walkable : TileWalkability.None;
        var path = new AStarPathfinder().FindPath(Position.FromCoords(0, 0, Z), Position.FromCoords(4, 0, Z), isWalkable);
        Assert.NotNull(path);
        Assert.Equal(Position.FromCoords(0, 0, Z), path[0]);
        Assert.Equal(Position.FromCoords(4, 0, Z), path[^1]);
        // Path must go through the gap at (2,3)
        Assert.Contains(Position.FromCoords(2, 3, Z), path);
    }

    [Fact]
    public void FindPath_MaxStepsExceeded_ReturnsNull()
    {
        // Large open area but very few steps allowed
        var path = new AStarPathfinder().FindPath(Position.FromCoords(0, 0, Z), Position.FromCoords(50, 50, Z), AllWalkable, maxSteps: 5);
        Assert.Null(path);
    }

    [Fact]
    public void FindPath_ReturnsXYZ_Coordinates()
    {
        var path = new AStarPathfinder().FindPath(Position.FromCoords(0, 0, Z), Position.FromCoords(3, 0, Z), AllWalkable);
        Assert.NotNull(path);
        foreach (var step in path)
            Assert.Equal(Z, step.Z);
    }

    [Fact]
    public void FindPath_AcrossZLevels_ViaStairs()
    {
        // Setup: flat plane on Z=127, stairs-down at (5,0,127), stairs-up at (5,0,126), goal at (8,0,126)
        TileWalkability getWalkability(Position p)
        {
            if (p.X < 0 || p.X > 10 || p.Y < -1 || p.Y > 1) return TileWalkability.None;
            if (p.Z == Z && p.X == 5 && p.Y == 0) return TileWalkability.StairsDown;
            if (p.Z == Z - 1 && p.X == 5 && p.Y == 0) return TileWalkability.StairsUp;
            if (p.Z == Z || p.Z == Z - 1) return TileWalkability.Walkable;
            return TileWalkability.None;
        }

        var path = new AStarPathfinder().FindPath(Position.FromCoords(0, 0, Z), Position.FromCoords(8, 0, Z - 1), getWalkability);
        Assert.NotNull(path);
        Assert.Equal(Position.FromCoords(0, 0, Z), path[0]);
        Assert.Equal(Position.FromCoords(8, 0, Z - 1), path[^1]);

        // Path must pass through the stair tile to change Z
        Assert.Contains(Position.FromCoords(5, 0, Z), path);
        Assert.Contains(Position.FromCoords(5, 0, Z - 1), path);
    }

    [Fact]
    public void FindPath_NoStairs_CannotCrossZ()
    {
        // Different Z, no stair tiles → no path
        var path = new AStarPathfinder().FindPath(Position.FromCoords(0, 0, Z), Position.FromCoords(0, 0, Z - 1), AllWalkable);
        Assert.Null(path);
    }
}
