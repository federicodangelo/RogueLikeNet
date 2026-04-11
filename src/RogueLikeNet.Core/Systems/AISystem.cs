using RogueLikeNet.Core.Algorithms;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Entities;
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
    private readonly List<(int Id, Position From, Position To)> _pendingAnimalMoves = new();
    private readonly List<Position> _playerPositions = new();
    private readonly HashSet<long> _entitiesPositions = new();
    private readonly AStarPathfinder _pathfinder = new();
    private readonly List<Position> _pathfinderTargetPositions = new();

    public void Update(WorldMap map)
    {
        // Collect player positions
        CollectPlayerPositions(map);

        // Collect all actor positions (alive) for collision
        CollectEntitiesPositions(map);

        // Tick down all move and attack delays directly on chunk entities
        TickDownAttackAndMoveDelays(map);

        // Process monster AI directly on chunk entities
        ProcessMonstersAI(map);

        // Process town NPC wandering directly on chunk entities
        ProcessNpcsAI(map);

        // Process animal AI
        ProcessAnimalsAI(map);
    }

    private void CollectEntitiesPositions(WorldMap map)
    {
        _entitiesPositions.Clear();
        map.CollectEntitiesPositions(_entitiesPositions);
    }

    private void CollectPlayerPositions(WorldMap map)
    {
        _playerPositions.Clear();
        foreach (ref var p in map.Players)
            if (!p.IsDead) _playerPositions.Add(p.Position);
    }

    private void ProcessAnimalsAI(WorldMap map)
    {
        _pendingAnimalMoves.Clear();
        foreach (var chunk in map.LoadedChunks)
        {
            foreach (ref var animal in chunk.Animals)
            {
                if (animal.IsDead) continue;

                if (animal.MoveDelay.Current > 0) continue;

                int dir = _rng.Next(4);
                var nextPosition = Position.FromCoords(
                    animal.Position.X + (dir == 0 ? 1 : dir == 1 ? -1 : 0),
                    animal.Position.Y + (dir == 2 ? 1 : dir == 3 ? -1 : 0),
                    animal.Position.Z
                );

                if (!map.IsWalkable(nextPosition)) continue;
                long nextKey = nextPosition.Pack();
                if (_entitiesPositions.Contains(nextKey)) continue;

                _entitiesPositions.Remove(Position.PackCoord(animal.Position.X, animal.Position.Y, animal.Position.Z));
                _entitiesPositions.Add(nextKey);
                animal.MoveDelay.Current = animal.MoveDelay.Interval;
                _pendingAnimalMoves.Add((animal.Id, animal.Position, nextPosition));
            }
        }

        // Apply deferred NPC moves
        foreach (var (id, from, to) in _pendingAnimalMoves)
            map.MoveAnimalEntity(id, from, to);
    }

    private void ProcessNpcsAI(WorldMap map)
    {
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
                var nextPosition = Position.FromCoords(
                    npc.Position.X + (dir == 0 ? 1 : dir == 1 ? -1 : 0),
                    npc.Position.Y + (dir == 2 ? 1 : dir == 3 ? -1 : 0),
                    npc.Position.Z
                );

                if (Math.Abs(nextPosition.X - npc.NpcData.TownCenterX) > npc.NpcData.WanderRadius ||
                    Math.Abs(nextPosition.Y - npc.NpcData.TownCenterY) > npc.NpcData.WanderRadius)
                    continue;

                var targetTile = map.GetTile(nextPosition);
                if (GameData.Instance.Items.IsPlaceableDoor(targetTile.PlaceableItemId) && targetTile.PlaceableItemExtra == 0)
                {
                    map.OpenDoor(nextPosition);
                    npc.MoveDelay.Current = npc.MoveDelay.Interval;
                    continue;
                }

                if (!map.IsWalkable(nextPosition)) continue;
                long nextKey = nextPosition.Pack();
                if (_entitiesPositions.Contains(nextKey)) continue;

                _entitiesPositions.Remove(Position.PackCoord(npc.Position.X, npc.Position.Y, npc.Position.Z));
                _entitiesPositions.Add(nextKey);
                npc.MoveDelay.Current = npc.MoveDelay.Interval;
                _pendingNpcMoves.Add((npc.Id, npc.Position, nextPosition));
            }
        }

        // Apply deferred NPC moves
        foreach (var (id, from, to) in _pendingNpcMoves)
            map.MoveNpcEntity(id, from, to);
    }

    private void ProcessMonstersAI(WorldMap map)
    {
        _pendingMonsterMoves.Clear();
        foreach (var chunk in map.LoadedChunks)
        {
            foreach (ref var monster in chunk.Monsters)
            {
                if (monster.IsDead) continue;
                var monsterId = monster.Id;

                bool canMove = monster.MoveDelay.Current <= 0;

                // Find nearest player (same Z or ±1 Z)
                var nearestDist = int.MaxValue;
                var nearest = Position.Zero;
                var nearestDistAboveOrBelow = int.MaxValue;
                var nearestAboveOrBelow = Position.Zero;

                foreach (var p in _playerPositions)
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

                        _pathfinderTargetPositions.Clear();
                        var path = _pathfinder.FindPath(
                            monster.Position, nearestAboveOrBelow,
                            p =>
                            {
                                var tile = map.GetTile(p);
                                if (!tile.IsWalkable) return TileWalkability.None;
                                if (tile.Type == TileType.StairsUp) return TileWalkability.StairsUp;
                                if (tile.Type == TileType.StairsDown) return TileWalkability.StairsDown;
                                var entityRef = map.GetEntityRefAt(p);
                                if (entityRef.Type != EntityType.None)
                                {
                                    if (entityRef.Type == EntityType.Monster && entityRef.Id == monsterId)
                                        return TileWalkability.Walkable;

                                    if (entityRef.Type == EntityType.Player)
                                        return TileWalkability.Walkable;

                                    if (entityRef.Type == EntityType.GroundItem)
                                        return TileWalkability.Walkable;

                                    return TileWalkability.None;
                                }

                                return TileWalkability.Walkable;
                            },
                            maxSteps: 200,
                            _pathfinderTargetPositions
                        );
                        if (path != null && path.Count >= 2)
                        {
                            var next = path[1];
                            long nextKey = next.Pack();
                            if (map.IsWalkable(next) && !_entitiesPositions.Contains(nextKey))
                            {
                                _entitiesPositions.Remove(Position.PackCoord(monster.Position.X, monster.Position.Y, monster.Position.Z));
                                _entitiesPositions.Add(nextKey);
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
    }

    private static void TickDownAttackAndMoveDelays(WorldMap map)
    {
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
            foreach (ref var a in chunk.Animals)
            {
                if (a.MoveDelay.Current > 0) a.MoveDelay.Current--;
            }
        }

        foreach (ref var p in map.Players)
        {
            if (p.MoveDelay.Current > 0) p.MoveDelay.Current--;
            if (p.AttackDelay.Current > 0) p.AttackDelay.Current--;
        }
    }
}
