using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// Processes player movement intents. Validates against world collision.
/// Moving into a tile occupied by an actor converts to an attack.
/// Bumping a closed door delegates to <see cref="WorldMap.OpenDoor"/>.
/// </summary>
public class MovementSystem
{
    public void Update(WorldMap map, bool debugNoCollision = false, bool debugMaxSpeed = false)
    {
        // Collect all actor positions (alive players + monsters + NPCs)
        var actorPositions = map.CollectEntitiesPositions();

        foreach (ref var player in map.Players)
        {
            if (player.IsDead) continue;

            ref var input = ref player.Input;
            ref var delay = ref player.MoveDelay;

            if (input.ActionType == ActionTypes.UseStairs)
            {
                if (delay.Current > 0) { input.ActionType = ActionTypes.None; continue; }

                var tile = map.GetTile(player.Position.X, player.Position.Y, player.Position.Z);
                int dz = 0;
                if (tile.Type == TileType.StairsDown) dz = -1;
                else if (tile.Type == TileType.StairsUp) dz = 1;

                if (dz != 0)
                {
                    int newZ = player.Position.Z + dz;
                    if (newZ >= 0 && newZ <= 255 && map.IsWalkable(player.Position.X, player.Position.Y, newZ))
                    {
                        player.Position.Z = newZ;
                        delay.Current = delay.Interval;
                    }
                }
                input.ActionType = ActionTypes.None;
                continue;
            }

            if (input.ActionType != ActionTypes.Move) continue;

            // Respect player action cooldown
            if (!debugMaxSpeed && delay.Current > 0) continue;

            int newX = player.Position.X + input.TargetX * (debugMaxSpeed ? 4 : 1);
            int newY = player.Position.Y + input.TargetY * (debugMaxSpeed ? 4 : 1);

            // Bumping into a closed door opens it
            if (!debugNoCollision)
            {
                var targetTile = map.GetTile(newX, newY, player.Position.Z);
                if (PlaceableDefinitions.IsDoor(targetTile.PlaceableItemId) && PlaceableDefinitions.IsDoorClosed(targetTile.PlaceableItemId, targetTile.PlaceableItemExtra))
                {
                    map.OpenDoor(newX, newY, player.Position.Z);
                    input.ActionType = ActionTypes.None;
                    delay.Current = delay.Interval;
                    continue;
                }
            }

            if (!debugNoCollision && !map.IsWalkable(newX, newY, player.Position.Z))
            {
                input.ActionType = ActionTypes.None;
                continue;
            }

            // Check if an actor occupies the destination
            if (!debugNoCollision && actorPositions.Contains(Position.PackCoord(newX, newY, player.Position.Z)))
            {
                input.ActionType = ActionTypes.Attack;
                continue;
            }

            player.Position.X = newX;
            player.Position.Y = newY;
            input.ActionType = ActionTypes.None;
            delay.Current = delay.Interval;
        }
    }
}
