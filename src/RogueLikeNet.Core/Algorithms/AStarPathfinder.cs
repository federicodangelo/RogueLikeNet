using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Core.Algorithms;

/// <summary>
/// A* pathfinding with Manhattan distance heuristic.
/// All integer arithmetic. Returns a list of (x,y) positions from start to goal.
/// </summary>
public static class AStarPathfinder
{
    /// <summary>
    /// Finds a path from (startX, startY) to (goalX, goalY).
    /// <paramref name="isWalkable"/> returns true for passable tiles.
    /// Returns null if no path exists. Limited to <paramref name="maxSteps"/> iterations.
    /// </summary>
    public static List<(int X, int Y)>? FindPath(
        int startX, int startY,
        int goalX, int goalY,
        Func<int, int, bool> isWalkable,
        int maxSteps = 1000)
    {
        if (startX == goalX && startY == goalY)
            return [(startX, startY)];

        var openSet = new PriorityQueue<(int X, int Y), int>();
        var cameFrom = new Dictionary<long, long>();
        var gScore = new Dictionary<long, int>();

        long startKey = Position.PackCoord(startX, startY);
        long goalKey = Position.PackCoord(goalX, goalY);

        gScore[startKey] = 0;
        openSet.Enqueue((startX, startY), ManhattanDistance(startX, startY, goalX, goalY));

        int steps = 0;

        // 4-directional movement
        ReadOnlySpan<int> dx = [0, 1, 0, -1];
        ReadOnlySpan<int> dy = [-1, 0, 1, 0];

        while (openSet.Count > 0 && steps++ < maxSteps)
        {
            var (cx, cy) = openSet.Dequeue();
            long currentKey = Position.PackCoord(cx, cy);

            if (currentKey == goalKey)
                return ReconstructPath(cameFrom, goalKey, startKey);

            int currentG = gScore[currentKey];

            for (int i = 0; i < 4; i++)
            {
                int nx = cx + dx[i];
                int ny = cy + dy[i];

                if (!isWalkable(nx, ny)) continue;

                long neighborKey = Position.PackCoord(nx, ny);
                int tentativeG = currentG + 1; // uniform cost

                if (!gScore.TryGetValue(neighborKey, out int existingG) || tentativeG < existingG)
                {
                    cameFrom[neighborKey] = currentKey;
                    gScore[neighborKey] = tentativeG;
                    int f = tentativeG + ManhattanDistance(nx, ny, goalX, goalY);
                    openSet.Enqueue((nx, ny), f);
                }
            }
        }

        return null; // no path found
    }

    private static List<(int X, int Y)> ReconstructPath(Dictionary<long, long> cameFrom, long current, long start)
    {
        var path = new List<(int X, int Y)>();
        while (current != start)
        {
            var (x, y) = Position.UnpackCoord(current);
            path.Add((x, y));
            current = cameFrom[current];
        }
        var (sx, sy) = Position.UnpackCoord(start);
        path.Add((sx, sy));
        path.Reverse();
        return path;
    }

    private static int ManhattanDistance(int x0, int y0, int x1, int y1)
        => Math.Abs(x0 - x1) + Math.Abs(y0 - y1);
}
