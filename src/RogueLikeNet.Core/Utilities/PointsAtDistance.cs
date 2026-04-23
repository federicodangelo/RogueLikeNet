using System.Collections.Concurrent;

namespace RogueLikeNet.Core.Utilities;

public static class PointsAtDistance
{
    public readonly record struct Point(int X, int Y);

    private static readonly ConcurrentDictionary<int, Point[]> _cache = new();

    /// <summary>
    /// Returns the points in a rectangle around an origin sorted by distance to the center (using manhattan distance for sorting) with (0,0) always first
    /// If distance=0, returns just (0,0).
    /// If distance=1, returns (0,0) then the 8 surrounding points in a square around it.
    /// </summary>
    public static Point[] GetPoints(int distance)
    {
        if (distance < 0)
            throw new ArgumentOutOfRangeException(nameof(distance), "Distance must be non-negative.");

        if (_cache.TryGetValue(distance, out var points))
            return points;

        var pointList = new List<Point>();

        if (distance == 0)
        {
            pointList.Add(new Point(0, 0));
        }
        else
        {
            for (int x = -distance; x <= distance; x++)
            {
                for (int y = -distance; y <= distance; y++)
                {
                    pointList.Add(new Point(x, y));
                }
            }
        }

        points = pointList.OrderBy(p => Math.Abs(p.X) + Math.Abs(p.Y)).ToArray();

        _cache.TryAdd(distance, points);

        return points;
    }

    public static int[] GetZLevels(int distance)
    {
        if (distance < 0)
            throw new ArgumentOutOfRangeException(nameof(distance), "Distance must be non-negative.");

        return
            Enumerable.Range(-distance, distance * 2 + 1)
            .OrderBy(Math.Abs)
            .ToArray();
    }
}
