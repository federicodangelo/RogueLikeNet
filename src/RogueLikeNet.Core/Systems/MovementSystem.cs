using Arch.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// Processes player and AI movement intents. Validates against world collision.
/// Moving into a tile occupied by an actor (entity with Health) converts to an attack.
/// Bumping a closed door delegates to <see cref="WorldMap.OpenDoor"/>.
/// </summary>
public class MovementSystem
{
    public void Update(Arch.Core.World world, WorldMap map, bool debugNoCollision = false, bool debugMaxSpeed = false)
    {
        // Collect all actor positions (entities with Position + Health, alive)
        var actorPositions = new HashSet<long>();
        var actorQuery = new QueryDescription().WithAll<Position, Health>();
        world.Query(in actorQuery, (ref Position aPos, ref Health h) =>
        {
            if (h.IsAlive)
                actorPositions.Add(Position.PackCoord(aPos.X, aPos.Y, aPos.Z));
        });

        var query = new QueryDescription().WithAll<Position, PlayerInput, MoveDelay>();
        world.Query(in query, (ref Position pos, ref PlayerInput input, ref MoveDelay delay) =>
        {
            if (input.ActionType == ActionTypes.UseStairs)
            {
                if (delay.Current > 0) return;

                var tile = map.GetTile(pos.X, pos.Y, pos.Z);
                int dz = 0;
                if (tile.Type == TileType.StairsDown) dz = -1;
                else if (tile.Type == TileType.StairsUp) dz = 1;

                if (dz != 0)
                {
                    int newZ = pos.Z + dz;
                    if (newZ >= 0 && newZ <= 255 && map.IsWalkable(pos.X, pos.Y, newZ))
                    {
                        pos.Z = newZ;
                        delay.Current = delay.Interval;
                    }
                }
                input.ActionType = ActionTypes.None;
                return;
            }

            if (input.ActionType != ActionTypes.Move) return;

            // Respect player action cooldown — preserve action to execute next tick
            if (!debugMaxSpeed && delay.Current > 0) return;

            int newX = pos.X + input.TargetX * (debugMaxSpeed ? 4 : 1);
            int newY = pos.Y + input.TargetY * (debugMaxSpeed ? 4 : 1);

            // Bumping into a closed door opens it
            if (!debugNoCollision)
            {
                var targetTile = map.GetTile(newX, newY, pos.Z);
                if (PlaceableDefinitions.IsDoor(targetTile.PlaceableItemId) && PlaceableDefinitions.IsDoorClosed(targetTile.PlaceableItemId, targetTile.PlaceableItemExtra))
                {
                    map.OpenDoor(newX, newY, pos.Z);
                    input.ActionType = ActionTypes.None;
                    delay.Current = delay.Interval;
                    return;
                }
            }

            if (!debugNoCollision && !map.IsWalkable(newX, newY, pos.Z))
            {
                input.ActionType = ActionTypes.None;
                return;
            }

            // Check if an actor occupies the destination (skip in debug no-collision mode)
            if (!debugNoCollision && actorPositions.Contains(Position.PackCoord(newX, newY, pos.Z)))
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

            if (map.IsWalkable(newX, newY, pos.Z) && !actorPositions.Contains(Position.PackCoord(newX, newY, pos.Z)))
            {
                pos.X = newX;
                pos.Y = newY;
            }

            vel.DX = 0;
            vel.DY = 0;
        });
    }
}
