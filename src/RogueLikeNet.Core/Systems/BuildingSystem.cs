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

    public void Update(WorldMap map)
    {
        ProcessPlacement(map);
        ProcessPickUpPlaced(map);
    }

    private void ProcessPlacement(WorldMap map)
    {
        foreach (ref var player in map.Players)
        {
            if (player.IsDead) continue;
            if (player.Input.ActionType != ActionTypes.PlaceItem) continue;

            int slot = player.Input.ItemSlot;
            int targetX = player.Position.X + player.Input.TargetX;
            int targetY = player.Position.Y + player.Input.TargetY;
            var target = Position.FromCoords(targetX, targetY, player.Position.Z);
            player.Input.ActionType = ActionTypes.None;

            if (player.Inventory.Items == null || slot < 0 || slot >= player.Inventory.Items.Count) continue;

            var itemData = player.Inventory.Items[slot];
            var def = ItemDefinitions.Get(itemData.ItemTypeId);
            if (def.Category != ItemDefinitions.CategoryPlaceable) continue;

            int dx = target.X - player.Position.X;
            int dy = target.Y - player.Position.Y;
            bool adjacent = false;
            foreach (var (ox, oy) in AdjacentOffsets)
            {
                if (dx == ox && dy == oy) { adjacent = true; break; }
            }
            if (!adjacent) continue;

            var tile = map.GetTile(target);
            if (tile.Type != TileType.Floor) continue;
            if (tile.PlaceableItemId != 0) continue;

            // Check no entity occupies the target position
            if (map.IsPositionOccupiedByEntity(target)) continue;

            map.SetPlaceable(target, itemData.ItemTypeId, 0);

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

    private void ProcessPickUpPlaced(WorldMap map)
    {
        foreach (ref var player in map.Players)
        {
            if (player.IsDead) continue;
            if (player.Input.ActionType != ActionTypes.PickUpPlaced) continue;

            int targetX = player.Position.X + player.Input.TargetX;
            int targetY = player.Position.Y + player.Input.TargetY;
            var target = Position.FromCoords(targetX, targetY, player.Position.Z);
            player.Input.ActionType = ActionTypes.None;

            if (player.Inventory.Items == null || player.Inventory.IsFull) continue;

            var tile = map.GetTile(target);
            if (tile.PlaceableItemId == ItemDefinitions.None)
            {
                // Maybe there's an item on the floor
                var chunk = map.GetChunkForWorldPos(target);
                if (chunk != null)
                {
                    foreach (ref var gi in chunk.GroundItems)
                    {
                        if (gi.IsDestroyed || gi.Position != target) continue;
                        if (InventorySystem.AddItemToInventory(ref player, gi.Item))
                            gi.IsDestroyed = true;
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

            if (!InventorySystem.AddItemToInventory(ref player, placeableItemData))
                continue;

            map.SetPlaceable(target, 0, 0);
        }
    }
}
