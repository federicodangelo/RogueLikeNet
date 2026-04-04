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
public class AStarPathfinder
{
    private readonly PriorityQueue<Position, int> _openSet = new();
    private readonly Dictionary<long, long> _cameFrom = [];
    private readonly Dictionary<long, int> _gScore = [];

    // 4-directional movement
    private static readonly int[] dx = [0, 1, 0, -1];
    private static readonly int[] dy = [-1, 0, 1, 0];

    public List<Position>? FindPath(
        Position start,
        Position goal,
        Func<Position, TileWalkability> getWalkability,
        int maxSteps = 1000,
        List<Position>? targetPositions = null)
    {
        if (start == goal)
        {
            targetPositions ??= [];
            targetPositions.Add(start);
            return targetPositions;
        }


        long startKey = start.Pack();
        long goalKey = goal.Pack();

        _openSet.Clear();
        _cameFrom.Clear();
        _gScore.Clear();

        _gScore[startKey] = 0;
        _openSet.Enqueue(start, Heuristic(start, goal));

        int steps = 0;

        while (_openSet.Count > 0 && steps++ < maxSteps)
        {
            var c = _openSet.Dequeue();
            long currentKey = c.Pack();

            if (currentKey == goalKey)
                return ReconstructPath(_cameFrom, goalKey, startKey, targetPositions);

            int currentG = _gScore[currentKey];

            // Try 4-directional movement on the same Z level
            for (int i = 0; i < 4; i++)
            {
                var n = Position.FromCoords(c.X + dx[i], c.Y + dy[i], c.Z);

                var walkability = getWalkability(n);
                if (walkability == TileWalkability.None) continue;

                long neighborKey = n.Pack();
                int tentativeG = currentG + 1;

                if (!_gScore.TryGetValue(neighborKey, out int existingG) || tentativeG < existingG)
                {
                    _cameFrom[neighborKey] = currentKey;
                    _gScore[neighborKey] = tentativeG;
                    _openSet.Enqueue(n, tentativeG + Heuristic(n, goal));
                }
            }

            // Try stair transitions from the current tile
            var currentWalkability = getWalkability(c);
            if (currentWalkability == TileWalkability.StairsUp && c.Z < 255)
            {
                TryAddNeighbor(Position.FromCoords(c.X, c.Y, c.Z + 1), currentG + 1, goal,
                    currentKey, _openSet, _cameFrom, _gScore, getWalkability);
            }
            else if (currentWalkability == TileWalkability.StairsDown && c.Z > 0)
            {
                TryAddNeighbor(Position.FromCoords(c.X, c.Y, c.Z - 1), currentG + 1, goal,
                    currentKey, _openSet, _cameFrom, _gScore, getWalkability);
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

    private static List<Position> ReconstructPath(Dictionary<long, long> cameFrom, long current, long start, List<Position>? targetPositions = null)
    {
        var path = targetPositions ?? [];
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
