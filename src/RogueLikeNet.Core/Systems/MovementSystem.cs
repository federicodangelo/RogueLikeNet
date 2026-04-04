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
    private readonly HashSet<long> _actorPositions = new();
    public void Update(WorldMap map, bool debugNoCollision = false, bool debugMaxSpeed = false)
    {
        // Collect all actor positions (alive players + monsters + NPCs)
        _actorPositions.Clear();
        map.CollectEntitiesPositions(_actorPositions);

        foreach (ref var player in map.Players)
        {
            if (player.IsDead) continue;

            ref var input = ref player.Input;
            ref var delay = ref player.MoveDelay;

            if (input.ActionType == ActionTypes.UseStairs)
            {
                if (delay.Current > 0) { input.ActionType = ActionTypes.None; continue; }

                var tile = map.GetTile(player.Position);
                int dz = 0;
                if (tile.Type == TileType.StairsDown) dz = -1;
                else if (tile.Type == TileType.StairsUp) dz = 1;

                if (dz != 0)
                {
                    var newStairsPosition = Position.FromCoords(player.Position.X, player.Position.Y, player.Position.Z + dz);
                    if (newStairsPosition.Z >= 0 && newStairsPosition.Z <= 255 && map.IsWalkable(newStairsPosition))
                    {
                        player.Position = newStairsPosition;
                        delay.Current = delay.Interval;
                    }
                }
                input.ActionType = ActionTypes.None;
                continue;
            }

            if (input.ActionType != ActionTypes.Move) continue;

            // Respect player action cooldown
            if (!debugMaxSpeed && delay.Current > 0) continue;

            var newPosition = Position.FromCoords(
                player.Position.X + input.TargetX * (debugMaxSpeed ? 4 : 1),
                player.Position.Y + input.TargetY * (debugMaxSpeed ? 4 : 1),
                player.Position.Z
            );

            // Bumping into a closed door opens it
            if (!debugNoCollision)
            {
                var targetTile = map.GetTile(newPosition);
                if (PlaceableDefinitions.IsDoor(targetTile.PlaceableItemId) && PlaceableDefinitions.IsDoorClosed(targetTile.PlaceableItemId, targetTile.PlaceableItemExtra))
                {
                    map.OpenDoor(newPosition);
                    input.ActionType = ActionTypes.None;
                    delay.Current = delay.Interval;
                    continue;
                }
            }

            if (!debugNoCollision && !map.IsWalkable(newPosition))
            {
                input.ActionType = ActionTypes.None;
                continue;
            }

            // Check if an actor occupies the destination
            if (!debugNoCollision && _actorPositions.Contains(newPosition.Pack()))
            {
                input.ActionType = ActionTypes.Attack;
                continue;
            }

            player.Position = newPosition;
            input.ActionType = ActionTypes.None;
            delay.Current = delay.Interval;
        }
    }
}
