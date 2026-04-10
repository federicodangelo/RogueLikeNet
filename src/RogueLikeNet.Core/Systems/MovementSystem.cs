using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
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
                        delay.Current = GetEffectiveDelay(ref player);
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
                if (GameData.Instance.Items.IsPlaceableDoor(targetTile.PlaceableItemId) && GameData.Instance.Items.IsPlaceableDoorClosed(targetTile.PlaceableItemId, targetTile.PlaceableItemExtra))
                {
                    map.OpenDoor(newPosition);
                    input.ActionType = ActionTypes.None;
                    delay.Current = GetEffectiveDelay(ref player);
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
            delay.Current = GetEffectiveDelay(ref player);
        }
    }

    /// <summary>
    /// Computes the effective move delay, applying any speed multiplier from active effects.
    /// A speed multiplier of 100 = normal, 50 = half speed (double delay), etc.
    /// </summary>
    private static int GetEffectiveDelay(ref Entities.PlayerEntity player)
    {
        int baseDelay = player.MoveDelay.Interval;
        int speedMult = player.ActiveEffects.CombinedSpeedMultiplierBase100;

        if (speedMult < 100 && speedMult > 0)
        {
            // Slowdown: e.g. 50% speed means double delay, with a value of 1 at least
            return Math.Max((baseDelay + 1) * 100 / speedMult - 1, 1);
        }

        if (speedMult > 100)
        {
            // Speedup: e.g. 200% speed means half delay, with a value of at least 0
            return Math.Max(baseDelay * 100 / speedMult, 0);
        }

        return baseDelay;
    }
}
