using Arch.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.World;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// Processes player and AI movement intents. Validates against world collision.
/// </summary>
public class MovementSystem
{
    public void Update(Arch.Core.World world, WorldMap map)
    {
        var query = new QueryDescription().WithAll<Position, PlayerInput>();
        world.Query(in query, (ref Position pos, ref PlayerInput input) =>
        {
            if (input.ActionType != ActionTypes.Move) return;

            int newX = pos.X + input.TargetX;
            int newY = pos.Y + input.TargetY;

            if (map.IsWalkable(newX, newY))
            {
                pos.X = newX;
                pos.Y = newY;
            }

            // Clear input after processing
            input.ActionType = ActionTypes.None;
        });

        // Process GridVelocity-based movement (for entities with velocity)
        var velQuery = new QueryDescription().WithAll<Position, GridVelocity>();
        world.Query(in velQuery, (ref Position pos, ref GridVelocity vel) =>
        {
            if (vel.DX == 0 && vel.DY == 0) return;

            int newX = pos.X + vel.DX;
            int newY = pos.Y + vel.DY;

            if (map.IsWalkable(newX, newY))
            {
                pos.X = newX;
                pos.Y = newY;
            }

            vel.DX = 0;
            vel.DY = 0;
        });
    }
}
