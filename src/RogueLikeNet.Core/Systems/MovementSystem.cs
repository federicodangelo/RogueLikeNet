using Arch.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.World;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// Processes player and AI movement intents. Validates against world collision.
/// Moving into a tile occupied by an actor (entity with Health) converts to an attack.
/// Handles door open/close: bumping a closed door opens it; doors auto-close after walk-through.
/// </summary>
public class MovementSystem
{
    // Grace period (in ticks) before an opened door can auto-close.
    // Must exceed the max MoveDelay interval so the player can walk through.
    private const int DoorGraceTicks = 20; // 1 second

    // Tracks open door positions → remaining grace ticks before auto-close is allowed.
    private readonly Dictionary<long, int> _openDoorTimers = new();

    public void Update(Arch.Core.World world, WorldMap map, bool debugNoCollision = false, bool debugMaxSpeed = false)
    {
        // Collect all actor positions (entities with Position + Health, alive)
        var actorPositions = new HashSet<long>();
        var actorQuery = new QueryDescription().WithAll<Position, Health>();
        world.Query(in actorQuery, (ref Position aPos, ref Health h) =>
        {
            if (h.IsAlive)
                actorPositions.Add(Position.PackCoord(aPos.X, aPos.Y));
        });

        var query = new QueryDescription().WithAll<Position, PlayerInput, MoveDelay>();
        world.Query(in query, (ref Position pos, ref PlayerInput input, ref MoveDelay delay) =>
        {
            if (input.ActionType != ActionTypes.Move) return;

            // Respect player action cooldown — preserve action to execute next tick
            if (!debugMaxSpeed && delay.Current > 0) return;

            int newX = pos.X + input.TargetX * (debugMaxSpeed ? 4 : 1);
            int newY = pos.Y + input.TargetY * (debugMaxSpeed ? 4 : 1);

            // Bumping into a closed door opens it
            if (!debugNoCollision)
            {
                var targetTile = map.GetTile(newX, newY);
                if (targetTile.Type == TileType.DoorClosed)
                {
                    OpenDoor(map, newX, newY, targetTile);
                    input.ActionType = ActionTypes.None;
                    delay.Current = delay.Interval;
                    return;
                }
            }

            if (!debugNoCollision && !map.IsWalkable(newX, newY))
            {
                input.ActionType = ActionTypes.None;
                return;
            }

            // Check if an actor occupies the destination (skip in debug no-collision mode)
            if (!debugNoCollision && actorPositions.Contains(Position.PackCoord(newX, newY)))
            {
                // Convert move into attack (CombatSystem will handle it)
                input.ActionType = ActionTypes.Attack;
                return;
            }

            pos.X = newX;
            pos.Y = newY;
            input.ActionType = ActionTypes.None;
            // Reset player action cooldown after moving
            delay.Current = delay.Interval;
        });

        // Process GridVelocity-based movement (for entities with velocity)
        var velQuery = new QueryDescription().WithAll<Position, GridVelocity>();
        world.Query(in velQuery, (ref Position pos, ref GridVelocity vel) =>
        {
            if (vel.DX == 0 && vel.DY == 0) return;

            int newX = pos.X + vel.DX;
            int newY = pos.Y + vel.DY;

            if (map.IsWalkable(newX, newY) && !actorPositions.Contains(Position.PackCoord(newX, newY)))
            {
                pos.X = newX;
                pos.Y = newY;
            }

            vel.DX = 0;
            vel.DY = 0;
        });

        // Auto-close open doors that are no longer occupied
        AutoCloseDoors(world, map);
    }

    private void OpenDoor(WorldMap map, int x, int y, TileInfo tile)
    {
        map.SetTile(x, y, new TileInfo
        {
            Type = TileType.Door,
            GlyphId = TileDefinitions.GlyphDoor,
            FgColor = tile.FgColor,
            BgColor = tile.BgColor,
        });
        _openDoorTimers[Position.PackCoord(x, y)] = DoorGraceTicks;
    }

    private void AutoCloseDoors(Arch.Core.World world, WorldMap map)
    {
        if (_openDoorTimers.Count == 0) return;

        // Collect current actor positions
        var occupied = new HashSet<long>();
        var posQuery = new QueryDescription().WithAll<Position, Health>();
        world.Query(in posQuery, (ref Position p, ref Health h) =>
        {
            if (h.IsAlive)
                occupied.Add(Position.PackCoord(p.X, p.Y));
        });

        var toRemove = new List<long>();
        var updates = new List<(long Key, int Ticks)>();
        foreach (var (key, ticksLeft) in _openDoorTimers)
        {
            // Verify tile is still an open door
            var (x, y) = Position.UnpackCoord(key);
            var tile = map.GetTile(x, y);
            if (tile.Type != TileType.Door) { toRemove.Add(key); continue; }

            // Don't close while occupied or grace period active
            int next = ticksLeft - 1;
            if (occupied.Contains(key) || next > 0)
            {
                updates.Add((key, Math.Max(0, next)));
                continue;
            }

            // Grace expired and unoccupied — close the door
            map.SetTile(x, y, new TileInfo
            {
                Type = TileType.DoorClosed,
                GlyphId = TileDefinitions.GlyphDoorClosed,
                FgColor = tile.FgColor,
                BgColor = tile.BgColor,
            });
            toRemove.Add(key);
        }

        foreach (var key in toRemove)
            _openDoorTimers.Remove(key);
        foreach (var (key, ticks) in updates)
            _openDoorTimers[key] = ticks;
    }
}
