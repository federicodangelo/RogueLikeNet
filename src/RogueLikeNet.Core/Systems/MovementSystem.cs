using Arch.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.World;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// Processes player and AI movement intents. Validates against world collision.
/// Moving into a tile occupied by an actor (entity with Health) converts to an attack.
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
                actorPositions.Add(Position.PackCoord(aPos.X, aPos.Y));
        });

        var query = new QueryDescription().WithAll<Position, PlayerInput, MoveDelay>();
        world.Query(in query, (ref Position pos, ref PlayerInput input, ref MoveDelay delay) =>
        {
            if (input.ActionType != ActionTypes.Move) return;

            // Respect player action cooldown — preserve action to execute next tick
            if (!debugMaxSpeed && delay.Current > 0) return;

            int newX = pos.X + input.TargetX;
            int newY = pos.Y + input.TargetY;

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
    }
}
