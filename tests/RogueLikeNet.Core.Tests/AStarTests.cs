using RogueLikeNet.Core.Algorithms;

namespace RogueLikeNet.Core.Tests;

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

    [Fact]
    public void FindPath_MaxStepsExceeded_ReturnsNull()
    {
        // Large open area but very few steps allowed
        var path = AStarPathfinder.FindPath(0, 0, 50, 50, (_, _) => true, maxSteps: 5);
        Assert.Null(path);
    }
}
