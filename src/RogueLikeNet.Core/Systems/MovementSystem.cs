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

        foreach (var player in map.Players.Values)
        {
            if (player.IsDead) continue;

            ref var input = ref player.Input;
            ref var delay = ref player.MoveDelay;

            if (input.ActionType == ActionTypes.UseStairs)
            {
                if (delay.Current > 0) { input.ActionType = ActionTypes.None; continue; }

                var tile = map.GetTile(player.X, player.Y, player.Z);
                int dz = 0;
                if (tile.Type == TileType.StairsDown) dz = -1;
                else if (tile.Type == TileType.StairsUp) dz = 1;

                if (dz != 0)
                {
                    int newZ = player.Z + dz;
                    if (newZ >= 0 && newZ <= 255 && map.IsWalkable(player.X, player.Y, newZ))
                    {
                        player.Z = newZ;
                        delay.Current = delay.Interval;
                    }
                }
                input.ActionType = ActionTypes.None;
                continue;
            }

            if (input.ActionType != ActionTypes.Move) continue;

            // Respect player action cooldown
            if (!debugMaxSpeed && delay.Current > 0) continue;

            int newX = player.X + input.TargetX * (debugMaxSpeed ? 4 : 1);
            int newY = player.Y + input.TargetY * (debugMaxSpeed ? 4 : 1);

            // Bumping into a closed door opens it
            if (!debugNoCollision)
            {
                var targetTile = map.GetTile(newX, newY, player.Z);
                if (PlaceableDefinitions.IsDoor(targetTile.PlaceableItemId) && PlaceableDefinitions.IsDoorClosed(targetTile.PlaceableItemId, targetTile.PlaceableItemExtra))
                {
                    map.OpenDoor(newX, newY, player.Z);
                    input.ActionType = ActionTypes.None;
                    delay.Current = delay.Interval;
                    continue;
                }
            }

            if (!debugNoCollision && !map.IsWalkable(newX, newY, player.Z))
            {
                input.ActionType = ActionTypes.None;
                continue;
            }

            // Check if an actor occupies the destination
            if (!debugNoCollision && actorPositions.Contains(Position.PackCoord(newX, newY, player.Z)))
            {
                input.ActionType = ActionTypes.Attack;
                continue;
            }

            player.X = newX;
            player.Y = newY;
            input.ActionType = ActionTypes.None;
            delay.Current = delay.Interval;
        }
    }
}
