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
    public static List<(int X, int Y, int Z)>? FindPath(
        int startX, int startY, int startZ,
        int goalX, int goalY, int goalZ,
        Func<int, int, int, TileWalkability> getWalkability,
        int maxSteps = 1000)
    {
        if (startX == goalX && startY == goalY && startZ == goalZ)
            return [(startX, startY, startZ)];

        var openSet = new PriorityQueue<(int X, int Y, int Z), int>();
        var cameFrom = new Dictionary<long, long>();
        var gScore = new Dictionary<long, int>();

        long startKey = Position.PackCoord(startX, startY, startZ);
        long goalKey = Position.PackCoord(goalX, goalY, goalZ);

        gScore[startKey] = 0;
        openSet.Enqueue((startX, startY, startZ), Heuristic(startX, startY, startZ, goalX, goalY, goalZ));

        int steps = 0;

        // 4-directional movement
        ReadOnlySpan<int> dx = [0, 1, 0, -1];
        ReadOnlySpan<int> dy = [-1, 0, 1, 0];

        while (openSet.Count > 0 && steps++ < maxSteps)
        {
            var (cx, cy, cz) = openSet.Dequeue();
            long currentKey = Position.PackCoord(cx, cy, cz);

            if (currentKey == goalKey)
                return ReconstructPath(cameFrom, goalKey, startKey);

            int currentG = gScore[currentKey];

            // Try 4-directional movement on the same Z level
            for (int i = 0; i < 4; i++)
            {
                int nx = cx + dx[i];
                int ny = cy + dy[i];

                var walkability = getWalkability(nx, ny, cz);
                if (walkability == TileWalkability.None) continue;

                long neighborKey = Position.PackCoord(nx, ny, cz);
                int tentativeG = currentG + 1;

                if (!gScore.TryGetValue(neighborKey, out int existingG) || tentativeG < existingG)
                {
                    cameFrom[neighborKey] = currentKey;
                    gScore[neighborKey] = tentativeG;
                    int f = tentativeG + Heuristic(nx, ny, cz, goalX, goalY, goalZ);
                    openSet.Enqueue((nx, ny, cz), f);
                }
            }

            // Try stair transitions from the current tile
            var currentWalkability = getWalkability(cx, cy, cz);
            if (currentWalkability == TileWalkability.StairsUp && cz < 255)
            {
                TryAddNeighbor(cx, cy, cz + 1, currentG + 1, goalX, goalY, goalZ,
                    currentKey, openSet, cameFrom, gScore, getWalkability);
            }
            else if (currentWalkability == TileWalkability.StairsDown && cz > 0)
            {
                TryAddNeighbor(cx, cy, cz - 1, currentG + 1, goalX, goalY, goalZ,
                    currentKey, openSet, cameFrom, gScore, getWalkability);
            }
        }

        return null;
    }

    private static void TryAddNeighbor(
        int nx, int ny, int nz, int tentativeG,
        int goalX, int goalY, int goalZ,
        long currentKey,
        PriorityQueue<(int X, int Y, int Z), int> openSet,
        Dictionary<long, long> cameFrom,
        Dictionary<long, int> gScore,
        Func<int, int, int, TileWalkability> getWalkability)
    {
        if (getWalkability(nx, ny, nz) == TileWalkability.None) return;

        long neighborKey = Position.PackCoord(nx, ny, nz);
        if (!gScore.TryGetValue(neighborKey, out int existingG) || tentativeG < existingG)
        {
            cameFrom[neighborKey] = currentKey;
            gScore[neighborKey] = tentativeG;
            int f = tentativeG + Heuristic(nx, ny, nz, goalX, goalY, goalZ);
            openSet.Enqueue((nx, ny, nz), f);
        }
    }

    private static List<(int X, int Y, int Z)> ReconstructPath(Dictionary<long, long> cameFrom, long current, long start)
    {
        var path = new List<(int X, int Y, int Z)>();
        while (current != start)
        {
            var (x, y, z) = Position.UnpackCoord(current);
            path.Add((x, y, z));
            current = cameFrom[current];
        }
        var (sx, sy, sz) = Position.UnpackCoord(start);
        path.Add((sx, sy, sz));
        path.Reverse();
        return path;
    }

    private static int Heuristic(int x0, int y0, int z0, int x1, int y1, int z1)
        => Math.Abs(x0 - x1) + Math.Abs(y0 - y1) + Math.Abs(z0 - z1);
}
