using RogueLikeNet.Core.Algorithms;

namespace RogueLikeNet.Core.Tests;

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
    public void Line_VerticalLine()
    {
        var points = new List<(int, int)>();
        Bresenham.Line(0, 0, 0, 5, (x, y) => points.Add((x, y)));
        Assert.Equal(6, points.Count);
        Assert.Equal((0, 0), points[0]);
        Assert.Equal((0, 5), points[5]);
    }

    [Fact]
    public void Line_SinglePoint()
    {
        var points = new List<(int, int)>();
        Bresenham.Line(3, 3, 3, 3, (x, y) => points.Add((x, y)));
        Assert.Single(points);
        Assert.Equal((3, 3), points[0]);
    }

    [Fact]
    public void Line_ReversedDirection()
    {
        var points = new List<(int, int)>();
        Bresenham.Line(5, 0, 0, 0, (x, y) => points.Add((x, y)));
        Assert.Equal(6, points.Count);
        Assert.Equal((5, 0), points[0]);
        Assert.Equal((0, 0), points[5]);
    }

    [Fact]
    public void Line_SteepLine()
    {
        // dy > dx
        var points = new List<(int, int)>();
        Bresenham.Line(0, 0, 1, 5, (x, y) => points.Add((x, y)));
        Assert.Contains((0, 0), points);
        Assert.Contains((1, 5), points);
        Assert.Equal(6, points.Count);
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

    [Fact]
    public void HasLineOfSight_SamePoint()
    {
        bool result = Bresenham.HasLineOfSight(3, 3, 3, 3, (_, _) => true);
        Assert.True(result);
    }

    [Fact]
    public void HasLineOfSight_VerticalClear()
    {
        bool result = Bresenham.HasLineOfSight(0, 0, 0, 5, (_, _) => false);
        Assert.True(result);
    }

    [Fact]
    public void HasLineOfSight_VerticalBlocked()
    {
        bool result = Bresenham.HasLineOfSight(0, 0, 0, 5, (_, y) => y == 3);
        Assert.False(result);
    }

    [Fact]
    public void HasLineOfSight_SteepBlocked()
    {
        // Steep line (dy > dx) with blocking in the middle
        bool result = Bresenham.HasLineOfSight(0, 0, 1, 5, (_, y) => y == 3);
        Assert.False(result);
    }
}
