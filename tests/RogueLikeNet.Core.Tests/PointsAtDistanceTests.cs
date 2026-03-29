using RogueLikeNet.Core.Utilities;

namespace RogueLikeNet.Core.Tests;

public class PointsAtDistanceTests
{
    [Fact]
    public void GetPoints_NegativeDistance_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PointsAtDistance.GetPoints(-1));
    }

    [Fact]
    public void GetPoints_Distance0_ReturnsSingleOriginPoint()
    {
        var points = PointsAtDistance.GetPoints(0);

        Assert.Single(points);
        Assert.Equal(new PointsAtDistance.Point(0, 0), points[0]);
    }

    [Fact]
    public void GetPoints_Distance1_Returns9Points()
    {
        var points = PointsAtDistance.GetPoints(1);

        Assert.Equal(9, points.Length);
    }

    [Fact]
    public void GetPoints_Distance2_Returns25Points()
    {
        var points = PointsAtDistance.GetPoints(2);

        Assert.Equal(25, points.Length);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void GetPoints_DistanceN_ReturnsCorrectSquareCount(int distance)
    {
        var points = PointsAtDistance.GetPoints(distance);
        int expected = (2 * distance + 1) * (2 * distance + 1);

        Assert.Equal(expected, points.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void GetPoints_OriginIsAlwaysFirst(int distance)
    {
        var points = PointsAtDistance.GetPoints(distance);

        Assert.Equal(new PointsAtDistance.Point(0, 0), points[0]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void GetPoints_AllPointsWithinBounds(int distance)
    {
        var points = PointsAtDistance.GetPoints(distance);

        foreach (var p in points)
        {
            Assert.InRange(p.X, -distance, distance);
            Assert.InRange(p.Y, -distance, distance);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void GetPoints_ContainsAllExpectedPoints(int distance)
    {
        var points = PointsAtDistance.GetPoints(distance);
        var pointSet = new HashSet<PointsAtDistance.Point>(points);

        for (int x = -distance; x <= distance; x++)
            for (int y = -distance; y <= distance; y++)
                Assert.Contains(new PointsAtDistance.Point(x, y), pointSet);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void GetPoints_SortedByManhattanDistance(int distance)
    {
        var points = PointsAtDistance.GetPoints(distance);

        for (int i = 1; i < points.Length; i++)
        {
            int prevDist = Math.Abs(points[i - 1].X) + Math.Abs(points[i - 1].Y);
            int currDist = Math.Abs(points[i].X) + Math.Abs(points[i].Y);
            Assert.True(prevDist <= currDist,
                $"Point at index {i - 1} ({points[i - 1]}) has greater Manhattan distance than point at index {i} ({points[i]})");
        }
    }

    [Fact]
    public void GetPoints_ReturnsCachedInstance()
    {
        var first = PointsAtDistance.GetPoints(2);
        var second = PointsAtDistance.GetPoints(2);

        Assert.Same(first, second);
    }

    [Fact]
    public void GetPoints_NoDuplicatePoints()
    {
        var points = PointsAtDistance.GetPoints(2);
        var distinct = points.Distinct().ToArray();

        Assert.Equal(points.Length, distinct.Length);
    }
}
