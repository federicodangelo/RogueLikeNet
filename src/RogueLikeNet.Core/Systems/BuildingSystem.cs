using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// Processes building placement: validates that the target tile is adjacent floor,
/// removes the buildable item from inventory, and modifies the world tile.
/// </summary>
public class BuildingSystem
{
    private static readonly (int DX, int DY)[] AdjacentOffsets =
        [(0, -1), (0, 1), (-1, 0), (1, 0)];

    public void Update(WorldMap map, GameEngine engine)
    {
        ProcessPlacement(map);
        ProcessPickUpPlaced(map, engine);
    }

    private void ProcessPlacement(WorldMap map)
    {
        var actions = new List<(PlayerEntity Player, int Slot, int TargetX, int TargetY, int TargetZ)>();

        foreach (var player in map.Players.Values)
        {
            if (player.IsDead) continue;
            if (player.Input.ActionType != ActionTypes.PlaceItem) continue;

            int targetX = player.X + player.Input.TargetX;
            int targetY = player.Y + player.Input.TargetY;
            actions.Add((player, player.Input.ItemSlot, targetX, targetY, player.Z));
            player.Input.ActionType = ActionTypes.None;
        }

        foreach (var (player, slot, targetX, targetY, targetZ) in actions)
        {
            if (player.Inventory.Items == null || slot < 0 || slot >= player.Inventory.Items.Count) continue;

            var itemData = player.Inventory.Items[slot];
            var def = ItemDefinitions.Get(itemData.ItemTypeId);
            if (def.Category != ItemDefinitions.CategoryPlaceable) continue;

            int dx = targetX - player.X;
            int dy = targetY - player.Y;
            bool adjacent = false;
            foreach (var (ox, oy) in AdjacentOffsets)
            {
                if (dx == ox && dy == oy) { adjacent = true; break; }
            }
            if (!adjacent) continue;

            var tile = map.GetTile(targetX, targetY, targetZ);
            if (tile.Type != TileType.Floor) continue;
            if (tile.PlaceableItemId != 0) continue;

            // Check no entity occupies the target position
            if (map.IsPositionOccupiedByEntity(targetX, targetY, targetZ)) continue;

            map.SetPlaceable(targetX, targetY, targetZ, itemData.ItemTypeId, 0);

            var item = player.Inventory.Items[slot];
            item.StackCount--;
            if (item.StackCount <= 0)
            {
                player.Inventory.Items.RemoveAt(slot);
                player.QuickSlots.OnItemRemoved(slot);
            }
            else
            {
                player.Inventory.Items[slot] = item;
            }
        }
    }

    private void ProcessPickUpPlaced(WorldMap map, GameEngine engine)
    {
        var actions = new List<(PlayerEntity Player, int TargetX, int TargetY, int TargetZ)>();

        foreach (var player in map.Players.Values)
        {
            if (player.IsDead) continue;
            if (player.Input.ActionType != ActionTypes.PickUpPlaced) continue;

            int targetX = player.X + player.Input.TargetX;
            int targetY = player.Y + player.Input.TargetY;
            actions.Add((player, targetX, targetY, player.Z));
            player.Input.ActionType = ActionTypes.None;
        }

        foreach (var (player, targetX, targetY, targetZ) in actions)
        {
            if (player.Inventory.Items == null || player.Inventory.IsFull) continue;

            var tile = map.GetTile(targetX, targetY, targetZ);
            if (tile.PlaceableItemId == ItemDefinitions.None)
            {
                // Maybe there's an item on the floor
                var chunk = map.GetChunkForWorldPos(targetX, targetY, targetZ);
                if (chunk != null)
                {
                    foreach (var gi in chunk.GroundItems)
                    {
                        if (gi.IsDead || gi.X != targetX || gi.Y != targetY || gi.Z != targetZ) continue;
                        if (InventorySystem.AddItemToInventory(player, gi.Item))
                            gi.IsDead = true;
                    }
                }
                continue;
            }

            var placeableItemData = new ItemData
            {
                ItemTypeId = tile.PlaceableItemId,
                Rarity = ItemDefinitions.RarityCommon,
                StackCount = 1,
            };

            if (!InventorySystem.AddItemToInventory(player, placeableItemData))
                continue;

            map.SetPlaceable(targetX, targetY, targetZ, 0, 0);
        }
    }
}
