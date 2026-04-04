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

    private readonly List<(int Id, Position From, Position To)> _pendingMonsterMoves = new();
    private readonly List<(int Id, Position From, Position To)> _pendingNpcMoves = new();

    public void Update(WorldMap map)
    {
        // Collect player positions
        var playerPositions = new List<Position>();
        foreach (ref var p in map.Players)
            if (!p.IsDead) playerPositions.Add(p.Position);

        if (playerPositions.Count == 0) return;

        // Collect all actor positions (alive) for collision
        var actorPositions = map.CollectEntitiesPositions();

        // Tick down all move and attack delays directly on chunk entities
        foreach (var chunk in map.LoadedChunks)
        {
            foreach (ref var m in chunk.Monsters)
            {
                if (m.MoveDelay.Current > 0) m.MoveDelay.Current--;
                if (m.AttackDelay.Current > 0) m.AttackDelay.Current--;
            }
            foreach (ref var n in chunk.TownNpcs)
            {
                if (n.MoveDelay.Current > 0) n.MoveDelay.Current--;
                if (n.AttackDelay.Current > 0) n.AttackDelay.Current--;
            }
        }

        // Tick down player delays too
        foreach (ref var p in map.Players)
        {
            if (p.MoveDelay.Current > 0) p.MoveDelay.Current--;
            if (p.AttackDelay.Current > 0) p.AttackDelay.Current--;
        }

        // Process monster AI directly on chunk entities, defer moves
        _pendingMonsterMoves.Clear();
        foreach (var chunk in map.LoadedChunks)
        {
            foreach (ref var monster in chunk.Monsters)
            {
                if (monster.IsDead) continue;

                bool canMove = monster.MoveDelay.Current <= 0;

                // Find nearest player (same Z or ±1 Z)
                var nearestDist = int.MaxValue;
                var nearest = Position.Zero;
                var nearestDistAboveOrBelow = int.MaxValue;
                var nearestAboveOrBelow = Position.Zero;

                foreach (var p in playerPositions)
                {
                    int zDiff = Math.Abs(p.Z - monster.Position.Z);
                    if (zDiff > 1) continue;
                    int dist = Math.Abs(monster.Position.X - p.X) + Math.Abs(monster.Position.Y - p.Y) + zDiff;
                    if (zDiff == 0 && dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearest = p;
                    }
                    if (dist < nearestDistAboveOrBelow)
                    {
                        nearestDistAboveOrBelow = dist;
                        nearestAboveOrBelow = p;
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
                            monster.Position, nearestAboveOrBelow,
                            p =>
                            {
                                var tile = map.GetTile(p);
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
                                actorPositions.Remove(Position.PackCoord(monster.Position.X, monster.Position.Y, monster.Position.Z));
                                actorPositions.Add(nextKey);
                                monster.MoveDelay.Current = monster.MoveDelay.Interval;
                                _pendingMonsterMoves.Add((monster.Id, monster.Position, next));
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

        // Apply deferred monster moves
        foreach (var (id, from, to) in _pendingMonsterMoves)
            map.MoveMonsterEntity(id, from, to);

        // Process town NPC wandering directly on chunk entities, defer moves
        _pendingNpcMoves.Clear();
        foreach (var chunk in map.LoadedChunks)
        {
            foreach (ref var npc in chunk.TownNpcs)
            {
                if (npc.IsDead) continue;

                if (npc.NpcData.TalkTimer > 0)
                    npc.NpcData.TalkTimer--;

                if (npc.MoveDelay.Current > 0) continue;

                int dir = _rng.Next(4);
                int nx = npc.Position.X + (dir == 0 ? 1 : dir == 1 ? -1 : 0);
                int ny = npc.Position.Y + (dir == 2 ? 1 : dir == 3 ? -1 : 0);

                if (Math.Abs(nx - npc.NpcData.TownCenterX) > npc.NpcData.WanderRadius ||
                    Math.Abs(ny - npc.NpcData.TownCenterY) > npc.NpcData.WanderRadius)
                    continue;

                var targetTile = map.GetTile(nx, ny, npc.Position.Z);
                if (PlaceableDefinitions.IsDoor(targetTile.PlaceableItemId) && targetTile.PlaceableItemExtra == 0)
                {
                    map.OpenDoor(nx, ny, npc.Position.Z);
                    npc.MoveDelay.Current = npc.MoveDelay.Interval;
                    continue;
                }

                if (!map.IsWalkable(nx, ny, npc.Position.Z)) continue;
                long nextKey = Position.PackCoord(nx, ny, npc.Position.Z);
                if (actorPositions.Contains(nextKey)) continue;

                actorPositions.Remove(Position.PackCoord(npc.Position.X, npc.Position.Y, npc.Position.Z));
                actorPositions.Add(nextKey);
                npc.MoveDelay.Current = npc.MoveDelay.Interval;
                _pendingNpcMoves.Add((npc.Id, npc.Position, Position.FromCoords(nx, ny, npc.Position.Z)));
            }
        }

        // Apply deferred NPC moves
        foreach (var (id, from, to) in _pendingNpcMoves)
            map.MoveNpcEntity(id, from, to);
    }
}
