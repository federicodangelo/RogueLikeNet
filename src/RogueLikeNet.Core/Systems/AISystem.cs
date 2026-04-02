using RogueLikeNet.Core.Algorithms;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// Simple monster AI: idle → chase player when in range → attack when adjacent.
/// Uses A* pathfinding for chasing. Respects <see cref="MoveDelay"/> for walk speed.
/// Also handles town NPC wandering behavior.
/// </summary>
public class AISystem
{
    private const int DetectionRange = 8;
    private const int ChaseRange = 12;

    private readonly SeededRandom _rng;

    public AISystem(long seed)
    {
        _rng = new SeededRandom(seed ^ 0xA15EED);
    }

    public void Update(WorldMap map)
    {
        // Collect player positions
        var playerPositions = new List<(int X, int Y, int Z)>();
        foreach (var p in map.Players.Values)
            if (!p.IsDead) playerPositions.Add((p.X, p.Y, p.Z));

        if (playerPositions.Count == 0) return;

        // Collect all actor positions (alive) for collision
        var actorPositions = map.CollectAliveActorPositions();

        // Tick down all move and attack delays
        foreach (var chunk in map.LoadedChunks)
        {
            foreach (var m in chunk.Monsters)
            {
                if (m.MoveDelay.Current > 0) m.MoveDelay.Current--;
                if (m.AttackDelay.Current > 0) m.AttackDelay.Current--;
            }
            foreach (var n in chunk.TownNpcs)
            {
                if (n.MoveDelay.Current > 0) n.MoveDelay.Current--;
                if (n.AttackDelay.Current > 0) n.AttackDelay.Current--;
            }
        }

        // Tick down player delays too
        foreach (var p in map.Players.Values)
        {
            if (p.MoveDelay.Current > 0) p.MoveDelay.Current--;
            if (p.AttackDelay.Current > 0) p.AttackDelay.Current--;
        }

        // Process monster AI (exclude town NPCs)
        foreach (var chunk in map.LoadedChunks)
        {
            foreach (var monster in chunk.Monsters)
            {
                if (monster.IsDead || !monster.Health.IsAlive) continue;

                bool canMove = monster.MoveDelay.Current <= 0;

                // Find nearest player (same Z or ±1 Z)
                int nearestDist = int.MaxValue;
                int nearestPx = 0, nearestPy = 0, nearestPz = 0;
                int nearestDistAboveOrBelow = int.MaxValue;
                int nearestAboveOrBelowPx = 0, nearestAboveOrBelowPy = 0, nearestAboveOrBelowPz = 0;

                foreach (var (px, py, pz) in playerPositions)
                {
                    int zDiff = Math.Abs(pz - monster.Z);
                    if (zDiff > 1) continue;
                    int dist = Math.Abs(monster.X - px) + Math.Abs(monster.Y - py) + zDiff;
                    if (zDiff == 0 && dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestPx = px; nearestPy = py; nearestPz = pz;
                    }
                    if (dist < nearestDistAboveOrBelow)
                    {
                        nearestDistAboveOrBelow = dist;
                        nearestAboveOrBelowPx = px; nearestAboveOrBelowPy = py; nearestAboveOrBelowPz = pz;
                    }
                }

                switch (monster.AI.StateId)
                {
                    case AIStates.Idle:
                        if (nearestDist <= DetectionRange)
                            monster.AI.StateId = AIStates.Chase;
                        break;

                    case AIStates.Chase:
                        if (nearestDistAboveOrBelow > ChaseRange)
                        {
                            monster.AI.StateId = AIStates.Idle;
                            break;
                        }
                        if (nearestDist <= 1)
                        {
                            monster.AI.StateId = AIStates.Attack;
                            break;
                        }
                        if (!canMove) break;

                        var path = AStarPathfinder.FindPath(
                            monster.X, monster.Y, monster.Z, nearestAboveOrBelowPx, nearestAboveOrBelowPy, nearestAboveOrBelowPz,
                            (x, y, z) =>
                            {
                                var tile = map.GetTile(x, y, z);
                                if (!tile.IsWalkable) return TileWalkability.None;
                                if (tile.Type == TileType.StairsUp) return TileWalkability.StairsUp;
                                if (tile.Type == TileType.StairsDown) return TileWalkability.StairsDown;
                                return TileWalkability.Walkable;
                            },
                            maxSteps: 200);
                        if (path != null && path.Count >= 2)
                        {
                            var next = path[1];
                            long nextKey = Position.PackCoord(next.X, next.Y, next.Z);
                            if (map.IsWalkable(next.X, next.Y, next.Z) && !actorPositions.Contains(nextKey))
                            {
                                actorPositions.Remove(Position.PackCoord(monster.X, monster.Y, monster.Z));
                                int oldX = monster.X, oldY = monster.Y, oldZ = monster.Z;
                                monster.X = next.X;
                                monster.Y = next.Y;
                                monster.Z = next.Z;
                                actorPositions.Add(nextKey);
                                monster.MoveDelay.Current = monster.MoveDelay.Interval;
                                map.MigrateMonsterIfNeeded(monster, oldX, oldY, oldZ);
                            }
                        }
                        break;

                    case AIStates.Attack:
                        if (nearestDist > 1)
                            monster.AI.StateId = AIStates.Chase;
                        break;
                }
            }
        }

        // Process town NPC wandering
        foreach (var chunk in map.LoadedChunks)
        {
            foreach (var npc in chunk.TownNpcs)
            {
                if (npc.IsDead || !npc.Health.IsAlive) continue;

                if (npc.NpcData.TalkTimer > 0)
                    npc.NpcData.TalkTimer--;

                if (npc.MoveDelay.Current > 0) continue;

                int dir = _rng.Next(4);
                int nx = npc.X + (dir == 0 ? 1 : dir == 1 ? -1 : 0);
                int ny = npc.Y + (dir == 2 ? 1 : dir == 3 ? -1 : 0);

                if (Math.Abs(nx - npc.NpcData.TownCenterX) > npc.NpcData.WanderRadius ||
                    Math.Abs(ny - npc.NpcData.TownCenterY) > npc.NpcData.WanderRadius)
                    continue;

                var targetTile = map.GetTile(nx, ny, npc.Z);
                if (PlaceableDefinitions.IsDoor(targetTile.PlaceableItemId) && targetTile.PlaceableItemExtra == 0)
                {
                    map.OpenDoor(nx, ny, npc.Z);
                    npc.MoveDelay.Current = npc.MoveDelay.Interval;
                    continue;
                }

                if (!map.IsWalkable(nx, ny, npc.Z)) continue;
                long nextKey = Position.PackCoord(nx, ny, npc.Z);
                if (actorPositions.Contains(nextKey)) continue;

                actorPositions.Remove(Position.PackCoord(npc.X, npc.Y, npc.Z));
                int oldNpcX = npc.X, oldNpcY = npc.Y, oldNpcZ = npc.Z;
                npc.X = nx;
                npc.Y = ny;
                actorPositions.Add(nextKey);
                npc.MoveDelay.Current = npc.MoveDelay.Interval;
                map.MigrateNpcIfNeeded(npc, oldNpcX, oldNpcY, oldNpcZ);
            }
        }
    }
}
