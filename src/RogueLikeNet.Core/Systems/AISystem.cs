using Arch.Core;
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

    public void Update(Arch.Core.World world, WorldMap map)
    {
        // First, collect player positions
        var playerPositions = new List<(int X, int Y)>();
        var playerQuery = new QueryDescription().WithAll<Position, PlayerTag>();
        world.Query(in playerQuery, (ref Position pos, ref PlayerTag _) =>
        {
            playerPositions.Add((pos.X, pos.Y));
        });

        if (playerPositions.Count == 0) return;

        // Collect all actor positions (alive) for collision
        var actorPositions = new HashSet<long>();
        var actorQuery = new QueryDescription().WithAll<Position, Health>();
        world.Query(in actorQuery, (ref Position aPos, ref Health h) =>
        {
            if (h.IsAlive)
                actorPositions.Add(Position.PackCoord(aPos.X, aPos.Y));
        });

        // Tick down move delays
        var delayQuery = new QueryDescription().WithAll<MoveDelay>();
        world.Query(in delayQuery, (ref MoveDelay delay) =>
        {
            if (delay.Current > 0)
                delay.Current--;
        });

        // Tick down attack delays
        var attackDelayQuery = new QueryDescription().WithAll<AttackDelay>();
        world.Query(in attackDelayQuery, (ref AttackDelay attackDelay) =>
        {
            if (attackDelay.Current > 0)
                attackDelay.Current--;
        });

        // Process monster AI (exclude town NPCs — they have their own wandering logic)
        var aiQuery = new QueryDescription().WithAll<Position, AIState, CombatStats, Health>().WithNone<DeadTag, TownNpcTag>();
        world.Query(in aiQuery, (Entity entity, ref Position pos, ref AIState ai, ref CombatStats stats, ref Health health) =>
        {
            if (!health.IsAlive) return;

            // Check move delay — skip movement (not state transitions) if on cooldown
            bool canMove = true;
            if (world.Has<MoveDelay>(entity))
            {
                ref var delay = ref world.Get<MoveDelay>(entity);
                if (delay.Current > 0)
                    canMove = false;
            }

            // Find nearest player
            int nearestDist = int.MaxValue;
            int nearestPx = 0, nearestPy = 0;
            foreach (var (px, py) in playerPositions)
            {
                int dist = Math.Abs(pos.X - px) + Math.Abs(pos.Y - py);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestPx = px;
                    nearestPy = py;
                }
            }

            switch (ai.StateId)
            {
                case AIStates.Idle:
                    if (nearestDist <= DetectionRange)
                        ai.StateId = AIStates.Chase;
                    break;

                case AIStates.Chase:
                    if (nearestDist > ChaseRange)
                    {
                        ai.StateId = AIStates.Idle;
                        break;
                    }
                    if (nearestDist <= 1)
                    {
                        ai.StateId = AIStates.Attack;
                        break;
                    }
                    if (!canMove) break; // Wait for move delay
                    // Move towards player using A*
                    var path = AStarPathfinder.FindPath(
                        pos.X, pos.Y, nearestPx, nearestPy,
                        (x, y) => map.IsWalkable(x, y),
                        maxSteps: 200);
                    if (path != null && path.Count >= 2)
                    {
                        var next = path[1];
                        long nextKey = Position.PackCoord(next.X, next.Y);
                        if (map.IsWalkable(next.X, next.Y) && !actorPositions.Contains(nextKey))
                        {
                            // Remove old position, move, add new position
                            actorPositions.Remove(Position.PackCoord(pos.X, pos.Y));
                            pos.X = next.X;
                            pos.Y = next.Y;
                            actorPositions.Add(nextKey);

                            // Reset move delay after moving
                            if (world.Has<MoveDelay>(entity))
                            {
                                ref var delay = ref world.Get<MoveDelay>(entity);
                                delay.Current = delay.Interval;
                            }
                        }
                    }
                    break;

                case AIStates.Attack:
                    if (nearestDist > 1)
                        ai.StateId = AIStates.Chase;
                    // Actual damage dealt by CombatSystem
                    break;
            }
        });

        // Process town NPC wandering
        var npcQuery = new QueryDescription().WithAll<Position, TownNpcTag, MoveDelay, Health>().WithNone<DeadTag>();
        world.Query(in npcQuery, (Entity entity, ref Position pos, ref TownNpcTag npc, ref MoveDelay delay, ref Health health) =>
        {
            if (!health.IsAlive) return;

            // Tick down talk timer
            if (npc.TalkTimer > 0)
                npc.TalkTimer--;

            // Wait for move delay
            if (delay.Current > 0) return;

            // Random walk: pick a cardinal direction
            int dir = _rng.Next(4);
            int nx = pos.X + (dir == 0 ? 1 : dir == 1 ? -1 : 0);
            int ny = pos.Y + (dir == 2 ? 1 : dir == 3 ? -1 : 0);

            // Stay within wander radius of town center
            if (Math.Abs(nx - npc.TownCenterX) > npc.WanderRadius ||
                Math.Abs(ny - npc.TownCenterY) > npc.WanderRadius)
                return;

            // Check for closed doors — NPCs can open them
            var targetTile = map.GetTile(nx, ny);
            if (PlaceableDefinitions.IsDoor(targetTile.PlaceableItemId) && targetTile.PlaceableItemExtra == 0)
            {
                map.OpenDoor(nx, ny);
                delay.Current = delay.Interval;
                return; // Spend this turn opening the door, move through next turn
            }

            // Check walkability and no collision
            if (!map.IsWalkable(nx, ny)) return;
            long nextKey = Position.PackCoord(nx, ny);
            if (actorPositions.Contains(nextKey)) return;

            actorPositions.Remove(Position.PackCoord(pos.X, pos.Y));
            pos.X = nx;
            pos.Y = ny;
            actorPositions.Add(nextKey);
            delay.Current = delay.Interval;
        });
    }
}
