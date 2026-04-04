using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Core.Algorithms;

public enum TileWalkability
{
    None,
    Walkable,
    StairsUp,
    StairsDown,
}

/// <summary>
/// A* pathfinding with Manhattan distance heuristic and 3D stair traversal.
/// Returns a list of (X, Y, Z) positions from start to goal.
/// </summary>
public static class AStarPathfinder
{
    public static List<Position>? FindPath(
        Position start,
        Position goal,
        Func<Position, TileWalkability> getWalkability,
        int maxSteps = 1000)
    {
        if (start == goal)
            return [start];

        var openSet = new PriorityQueue<Position, int>();
        var cameFrom = new Dictionary<long, long>();
        var gScore = new Dictionary<long, int>();

        long startKey = start.Pack();
        long goalKey = goal.Pack();

        gScore[startKey] = 0;
        openSet.Enqueue(start, Heuristic(start, goal));

        int steps = 0;

        // 4-directional movement
        ReadOnlySpan<int> dx = [0, 1, 0, -1];
        ReadOnlySpan<int> dy = [-1, 0, 1, 0];

        while (openSet.Count > 0 && steps++ < maxSteps)
        {
            var c = openSet.Dequeue();
            long currentKey = c.Pack();

            if (currentKey == goalKey)
                return ReconstructPath(cameFrom, goalKey, startKey);

            int currentG = gScore[currentKey];

            // Try 4-directional movement on the same Z level
            for (int i = 0; i < 4; i++)
            {
                var n = Position.FromCoords(c.X + dx[i], c.Y + dy[i], c.Z);

                var walkability = getWalkability(n);
                if (walkability == TileWalkability.None) continue;

                long neighborKey = n.Pack();
                int tentativeG = currentG + 1;

                if (!gScore.TryGetValue(neighborKey, out int existingG) || tentativeG < existingG)
                {
                    cameFrom[neighborKey] = currentKey;
                    gScore[neighborKey] = tentativeG;
                    openSet.Enqueue(n, tentativeG + Heuristic(n, goal));
                }
            }

            // Try stair transitions from the current tile
            var currentWalkability = getWalkability(c);
            if (currentWalkability == TileWalkability.StairsUp && c.Z < 255)
            {
                TryAddNeighbor(Position.FromCoords(c.X, c.Y, c.Z + 1), currentG + 1, goal,
                    currentKey, openSet, cameFrom, gScore, getWalkability);
            }
            else if (currentWalkability == TileWalkability.StairsDown && c.Z > 0)
            {
                TryAddNeighbor(Position.FromCoords(c.X, c.Y, c.Z - 1), currentG + 1, goal,
                    currentKey, openSet, cameFrom, gScore, getWalkability);
            }
        }

        return null;
    }

    private static void TryAddNeighbor(
        Position n, int tentativeG,
        Position goal,
        long currentKey,
        PriorityQueue<Position, int> openSet,
        Dictionary<long, long> cameFrom,
        Dictionary<long, int> gScore,
        Func<Position, TileWalkability> getWalkability)
    {
        if (getWalkability(n) == TileWalkability.None) return;

        long neighborKey = n.Pack();
        if (!gScore.TryGetValue(neighborKey, out int existingG) || tentativeG < existingG)
        {
            cameFrom[neighborKey] = currentKey;
            gScore[neighborKey] = tentativeG;
            int f = tentativeG + Heuristic(n, goal);
            openSet.Enqueue(n, f);
        }
    }

    private static List<Position> ReconstructPath(Dictionary<long, long> cameFrom, long current, long start)
    {
        var path = new List<Position>();
        while (current != start)
        {
            path.Add(Position.UnpackCoord(current));
            current = cameFrom[current];
        }
        path.Add(Position.UnpackCoord(start));
        path.Reverse();
        return path;
    }

    private static int Heuristic(Position a, Position b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z);
}
