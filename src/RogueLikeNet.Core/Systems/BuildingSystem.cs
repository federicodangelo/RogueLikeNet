using Arch.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// Processes building placement: validates that the target tile is adjacent floor,
/// removes the buildable item from inventory, and modifies the world tile.
/// Uses InventorySystem for pickup to ensure quick-slot updates.
/// </summary>
public class BuildingSystem
{
    // Cardinal directions for adjacency check
    private static readonly (int DX, int DY)[] AdjacentOffsets =
        [(0, -1), (0, 1), (-1, 0), (1, 0)];

    public void Update(Arch.Core.World world, WorldMap map)
    {
        ProcessPlacement(world, map);
        ProcessPickUpPlaced(world, map);
    }

    private void ProcessPlacement(Arch.Core.World world, WorldMap map)
    {
        var actions = new List<(Entity Player, int Slot, int TargetX, int TargetY, int TargetZ)>();

        var query = new QueryDescription().WithAll<PlayerInput, Inventory, Position>();
        world.Query(in query, (Entity player, ref PlayerInput input, ref Position pos) =>
        {
            if (input.ActionType != ActionTypes.PlaceItem) return;

            int targetX = pos.X + input.TargetX;
            int targetY = pos.Y + input.TargetY;
            actions.Add((player, input.ItemSlot, targetX, targetY, pos.Z));
            input.ActionType = ActionTypes.None;
        });

        foreach (var (player, slot, targetX, targetY, targetZ) in actions)
        {
            if (!world.IsAlive(player)) continue;
            ref var inv = ref world.Get<Inventory>(player);
            ref var pos = ref world.Get<Position>(player);
            if (inv.Items == null || slot < 0 || slot >= inv.Items.Count) continue;

            var itemData = inv.Items[slot];
            var def = ItemDefinitions.Get(itemData.ItemTypeId);
            if (def.Category != ItemDefinitions.CategoryPlaceable) continue;

            // Validate adjacency (target must be within 1 cardinal tile of player)
            int dx = targetX - pos.X;
            int dy = targetY - pos.Y;
            bool adjacent = false;
            foreach (var (ox, oy) in AdjacentOffsets)
            {
                if (dx == ox && dy == oy) { adjacent = true; break; }
            }
            if (!adjacent) continue;

            // Target tile must be a walkable floor tile with no existing placeable
            var tile = map.GetTile(targetX, targetY, targetZ);
            if (tile.Type != TileType.Floor) continue;
            // We can't place items on top of other placeables
            if (tile.PlaceableItemId != 0) continue;

            // Check no entity occupies the target position
            bool occupied = false;
            var entityQuery = new QueryDescription().WithAll<Position, Health>();
            world.Query(in entityQuery, (ref Position ePos) =>
            {
                if (ePos.X == targetX && ePos.Y == targetY)
                    occupied = true;
            });
            if (occupied) continue;

            // Only set the placeable fields — base tile stays unchanged
            map.SetPlaceable(targetX, targetY, targetZ, itemData.ItemTypeId, 0);

            // Remove item from inventory (decrease stack or remove)
            var item = inv.Items[slot];
            item.StackCount--;
            if (item.StackCount <= 0)
            {
                inv.Items.RemoveAt(slot);
                if (world.Has<QuickSlots>(player))
                {
                    ref var qs = ref world.Get<QuickSlots>(player);
                    qs.OnItemRemoved(slot);
                }
            }
            else
            {
                inv.Items[slot] = item;
            }
        }
    }

    private void ProcessPickUpPlaced(Arch.Core.World world, WorldMap map)
    {
        var actions = new List<(Entity Player, int TargetX, int TargetY, int TargetZ)>();

        var query = new QueryDescription().WithAll<PlayerInput, Inventory, Position>();
        world.Query(in query, (Entity player, ref PlayerInput input, ref Position pos) =>
        {
            if (input.ActionType != ActionTypes.PickUpPlaced) return;

            int targetX = pos.X + input.TargetX;
            int targetY = pos.Y + input.TargetY;
            actions.Add((player, targetX, targetY, pos.Z));
            input.ActionType = ActionTypes.None;
        });

        foreach (var (player, targetX, targetY, targetZ) in actions)
        {
            if (!world.IsAlive(player)) continue;
            ref var inv = ref world.Get<Inventory>(player);
            if (inv.Items == null || inv.IsFull) continue;

            var tile = map.GetTile(targetX, targetY, targetZ);
            if (tile.PlaceableItemId == ItemDefinitions.None)
            {
                // No placeable here, but maybe there's an item on the floor to pick up?
                var itemQuery = new QueryDescription().WithAll<Position, ItemData>();
                var pickups = new List<Entity>();
                world.Query(in itemQuery, (Entity item, ref Position iPos) =>
                {
                    if (iPos.X == targetX && iPos.Y == targetY && iPos.Z == targetZ)
                        pickups.Add(item);
                });

                foreach (var item in pickups)
                {
                    var floorItemData = world.Get<ItemData>(item);
                    if (InventorySystem.AddItemToInventory(world, player, floorItemData))
                        world.Add<DeadTag>(item);
                }
                continue;
            }

            var placeableItemData = new ItemData
            {
                ItemTypeId = tile.PlaceableItemId,
                Rarity = ItemDefinitions.RarityCommon,
                StackCount = 1,
            };

            if (!InventorySystem.AddItemToInventory(world, player, placeableItemData))
                continue;

            // Only clear the placeable fields — base tile stays unchanged
            map.SetPlaceable(targetX, targetY, targetZ, 0, 0);
        }
    }
}
