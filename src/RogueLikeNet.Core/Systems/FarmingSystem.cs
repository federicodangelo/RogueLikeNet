using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Entities;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// Processes farming actions (till, plant, water, harvest) and advances crop growth each tick.
/// Tilling requires an equipped hoe and an adjacent grass/dirt floor tile.
/// Planting requires seeds in inventory and an adjacent tilled soil tile.
/// Watering requires an equipped watering can on an adjacent crop.
/// Harvesting collects mature crops (growth stage 3) and may return seeds.
/// </summary>
public class FarmingSystem
{
    /// <summary>Numeric tile ID for tilled soil (resolved from registry).</summary>
    public static int TilledSoilTileId => GameData.Instance.Tiles.GetNumericId("tilled_soil");

    public void Update(WorldMap map)
    {
        ProcessPlayerActions(map);

        ProcessCropsGrowth(map);
    }

    private static void ProcessCropsGrowth(WorldMap map)
    {
        foreach (var chunk in map.LoadedChunks)
        {
            foreach (ref var crop in chunk.Crops)
            {
                if (crop.IsDestroyed) continue;

                var seedData = GameData.Instance.Items.Get(crop.CropData.SeedItemTypeId)?.Seed;
                if (seedData == null) continue;
                if (crop.CropData.IsFullyGrown(seedData)) continue;

                int growthIncrement = 1;
                if (crop.CropData.IsWatered && seedData.WateredGrowthMultiplierBase100 > 0)
                    growthIncrement = seedData.WateredGrowthMultiplierBase100 / 100;

                int previousStage = crop.CropData.GetGrowthStage(seedData);
                crop.CropData.GrowthTicksCurrent += growthIncrement;

                // Update appearance when growth stage changes
                int newStage = crop.CropData.GetGrowthStage(seedData);
                if (newStage != previousStage)
                {
                    crop.Appearance = GetCropAppearance(newStage);
                }
            }
        }
    }

    private static void ProcessPlayerActions(WorldMap map)
    {
        foreach (ref var player in map.Players)
        {
            if (player.IsDead) continue;

            if (player.Input.ActionType == ActionTypes.Interact)
                if (!ResolveInteract(ref player, map))
                    continue; // No valid interact action

            switch (player.Input.ActionType)
            {
                case ActionTypes.Till:
                    ProcessTill(ref player, map);
                    break;
                case ActionTypes.Plant:
                    ProcessPlant(ref player, map);
                    break;
                case ActionTypes.Water:
                    ProcessWater(ref player, map);
                    break;
                case ActionTypes.Harvest:
                    ProcessHarvest(ref player, map);
                    break;
                case ActionTypes.FeedAnimal:
                    // Handled in AnimalSystem, no additional processing needed here
                    break;
            }
        }
    }

    private static void ProcessTill(ref PlayerEntity player, WorldMap map)
    {
        int targetX = player.Position.X + player.Input.TargetX;
        int targetY = player.Position.Y + player.Input.TargetY;
        var target = Position.FromCoords(targetX, targetY, player.Position.Z);
        player.Input.ActionType = ActionTypes.None;

        // Must be adjacent
        if (!IsAdjacent(player.Input.TargetX, player.Input.TargetY))
        {
            player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.Till, Failed = true });
            return;
        }

        // Player must have a hoe equipped
        if (!HasEquippedToolType(ref player, ToolType.Hoe))
        {
            player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.Till, Failed = true });
            return;
        }

        // Target must be a floor tile (dirt/grass)
        var tile = map.GetTile(target);
        if (tile.Type != TileType.Floor || tile.HasPlaceable || map.IsPositionOccupiedByEntity(target))
        {
            player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.Till, Failed = true });
            return;
        }

        // Convert to tilled soil
        tile.TileId = TilledSoilTileId;
        map.SetTile(target, tile);
        player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.Till });
    }

    private static void ProcessPlant(ref PlayerEntity player, WorldMap map)
    {
        int slot = player.Input.ItemSlot;
        int targetX = player.Position.X + player.Input.TargetX;
        int targetY = player.Position.Y + player.Input.TargetY;
        var target = Position.FromCoords(targetX, targetY, player.Position.Z);
        player.Input.ActionType = ActionTypes.None;

        // Must be adjacent
        if (!IsAdjacent(player.Input.TargetX, player.Input.TargetY))
        {
            player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.Plant, Failed = true });
            return;
        }

        // Validate inventory slot
        if (slot < 0 || slot >= player.Inventory.Items.Count)
        {
            player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.Plant, Failed = true });
            return;
        }

        var itemData = player.Inventory.Items[slot];
        var def = GameData.Instance.Items.Get(itemData.ItemTypeId);
        if (def == null || def.Category != ItemCategory.Seed || def.Seed == null)
        {
            player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.Plant, ItemTypeId = itemData.ItemTypeId, Failed = true });
            return;
        }

        // Target must be tilled soil
        var tile = map.GetTile(target);
        if (tile.Type != TileType.Floor || tile.TileId != TilledSoilTileId)
        {
            player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.Plant, ItemTypeId = itemData.ItemTypeId, Failed = true });
            return;
        }

        // Check no crop already at this position
        var chunk = map.GetChunkForWorldPos(target);
        if (chunk == null)
        {
            player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.Plant, ItemTypeId = itemData.ItemTypeId, Failed = true });
            return;
        }
        foreach (var c in chunk.Crops)
        {
            if (!c.IsDestroyed && c.Position == target)
            {
                player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.Plant, ItemTypeId = itemData.ItemTypeId, Failed = true });
                return;
            }
        }

        // Check no other entity occupies the position
        if (map.IsPositionOccupiedByEntity(target))
        {
            player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.Plant, ItemTypeId = itemData.ItemTypeId, Failed = true });
            return;
        }

        // Consume seed from inventory
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

        // Spawn the crop entity
        var cropEntity = new CropEntity(map.AllocateEntityId())
        {
            Position = target,
            Appearance = GetCropAppearance(0),
            CropData = new CropData
            {
                SeedItemTypeId = itemData.ItemTypeId,
                GrowthTicksCurrent = 0,
                IsWatered = false,
            },
        };
        chunk.AddEntity(cropEntity);
        player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.Plant, ItemTypeId = itemData.ItemTypeId });
    }

    private static void ProcessWater(ref PlayerEntity player, WorldMap map)
    {
        int targetX = player.Position.X + player.Input.TargetX;
        int targetY = player.Position.Y + player.Input.TargetY;
        var target = Position.FromCoords(targetX, targetY, player.Position.Z);
        player.Input.ActionType = ActionTypes.None;

        // Must be adjacent
        if (!IsAdjacent(player.Input.TargetX, player.Input.TargetY))
        {
            player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.Water, Failed = true });
            return;
        }

        // Player must have a watering can equipped
        if (!HasEquippedToolType(ref player, ToolType.WateringCan))
        {
            player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.Water, Failed = true });
            return;
        }

        // Find a crop at the target position
        var chunk = map.GetChunkForWorldPos(target);
        if (chunk == null)
        {
            player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.Water, Failed = true });
            return;
        }

        foreach (ref var crop in chunk.Crops)
        {
            if (crop.IsDestroyed || crop.Position != target) continue;
            crop.CropData.IsWatered = true;
            player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.Water });
            return;
        }

        player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.Water, Failed = true });
    }

    private static void ProcessHarvest(ref PlayerEntity player, WorldMap map)
    {
        int targetX = player.Position.X + player.Input.TargetX;
        int targetY = player.Position.Y + player.Input.TargetY;
        var target = Position.FromCoords(targetX, targetY, player.Position.Z);
        player.Input.ActionType = ActionTypes.None;

        // Must be adjacent
        if (!IsAdjacent(player.Input.TargetX, player.Input.TargetY))
        {
            player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.Harvest, Failed = true });
            return;
        }

        if (player.Inventory.IsFull)
        {
            player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.Harvest, Failed = true });
            return;
        }

        // Find mature crop at target
        var chunk = map.GetChunkForWorldPos(target);
        if (chunk == null)
        {
            player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.Harvest, Failed = true });
            return;
        }

        foreach (ref var crop in chunk.Crops)
        {
            if (crop.IsDestroyed || crop.Position != target) continue;

            var seedData = GameData.Instance.Items.Get(crop.CropData.SeedItemTypeId)?.Seed;
            if (seedData == null) continue;
            if (!crop.CropData.IsFullyGrown(seedData)) continue;

            // Calculate harvest amount
            int harvestItemId = GameData.Instance.Items.GetNumericId(seedData.HarvestItemId);
            int harvestCount = seedData.HarvestMin;
            if (seedData.HarvestMax > seedData.HarvestMin)
                harvestCount += Random.Shared.Next(seedData.HarvestMax - seedData.HarvestMin + 1);

            // Add harvested items to inventory
            var harvestItem = new ItemData
            {
                ItemTypeId = harvestItemId,
                StackCount = harvestCount,
            };
            InventorySystem.AddItemToInventory(ref player, harvestItem);

            // Seed return chance
            int seedReturnChanceBase100 = (int)(seedData.SeedReturnChance * 100);
            if (seedReturnChanceBase100 > 0 && Random.Shared.Next(100) < seedReturnChanceBase100)
            {
                if (!player.Inventory.IsFull)
                {
                    var seedReturn = new ItemData
                    {
                        ItemTypeId = crop.CropData.SeedItemTypeId,
                        StackCount = 1,
                    };
                    InventorySystem.AddItemToInventory(ref player, seedReturn);
                }
            }

            // Destroy the crop
            crop.IsDestroyed = true;
            player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.Harvest, ItemTypeId = harvestItemId, StackCount = harvestCount });
            return;
        }

        player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.Harvest, Failed = true });
    }

    public static TileAppearance GetCropAppearance(int growthStage) => growthStage switch
    {
        0 => new TileAppearance(RenderConstants.GlyphCropStage0, RenderConstants.ColorCropSeedling),
        1 => new TileAppearance(RenderConstants.GlyphCropStage1, RenderConstants.ColorCropGrowing),
        2 => new TileAppearance(RenderConstants.GlyphCropStage2, RenderConstants.ColorCropGrowing),
        _ => new TileAppearance(RenderConstants.GlyphCropStage3, RenderConstants.ColorCropMature),
    };

    private static bool IsAdjacent(int dx, int dy)
    {
        return Math.Abs(dx) + Math.Abs(dy) == 1;
    }

    private static bool HasEquippedToolType(ref PlayerEntity player, ToolType toolType)
    {
        if (!player.Equipment.HasItem((int)EquipSlot.Hand)) return false;
        var equippedItem = player.Equipment[(int)EquipSlot.Hand];
        var equipDef = GameData.Instance.Items.Get(equippedItem.ItemTypeId);
        return equipDef?.Tool?.ToolType == toolType;
    }

    /// <summary>
    /// Context-sensitive interact: resolves to the most appropriate farming action
    /// based on equipped item, target tile, and target entities.
    /// Priority: Harvest > FeedAnimal > Water > Plant > Till
    /// </summary>
    private static bool ResolveInteract(ref PlayerEntity player, WorldMap map)
    {
        int targetX = player.Position.X + player.Input.TargetX;
        int targetY = player.Position.Y + player.Input.TargetY;
        var target = Position.FromCoords(targetX, targetY, player.Position.Z);

        if (!IsAdjacent(player.Input.TargetX, player.Input.TargetY))
        {
            return false;
        }

        var chunk = map.GetChunkForWorldPos(target);

        if (chunk == null)
        {
            return false;
        }

        var tile = map.GetTile(target);

        // 1. Harvest: target has a mature crop
        foreach (var crop in chunk.Crops)
        {
            if (crop.IsDestroyed || crop.Position != target) continue;
            var seedData = GameData.Instance.Items.Get(crop.CropData.SeedItemTypeId)?.Seed;
            if (seedData != null && crop.CropData.IsFullyGrown(seedData))
            {
                player.Input.ActionType = ActionTypes.Harvest;
                return true;
            }
        }

        // 2. Water: target has a crop and player has watering can
        if (HasEquippedToolType(ref player, ToolType.WateringCan))
        {
            foreach (var crop in chunk.Crops)
            {
                if (!crop.IsDestroyed && crop.Position == target && !crop.CropData.IsWatered)
                {
                    player.Input.ActionType = ActionTypes.Water;
                    return true;
                }
            }
        }

        // 3. Plant: target is tilled soil and player has seeds in quick slot or inventory
        if (tile.Type == TileType.Floor && tile.TileId == TilledSoilTileId)
        {
            int seedInventoryIndex = -1;

            for (int i = 0; i < player.QuickSlots.Count; i++)
            {
                int inventoryIndex = player.QuickSlots[i];
                if (inventoryIndex < 0 || inventoryIndex >= player.Inventory.Items.Count) continue;

                var itemData = player.Inventory.Items[inventoryIndex];
                var def = GameData.Instance.Items.Get(itemData.ItemTypeId);
                if (def?.Category == ItemCategory.Seed)
                {
                    seedInventoryIndex = inventoryIndex;
                    break;
                }
            }

            if (seedInventoryIndex == -1)
            {
                var inventoryIndex = player.Inventory.Items.FindIndex(item =>
                {
                    var def = GameData.Instance.Items.Get(item.ItemTypeId);
                    return def?.Category == ItemCategory.Seed;
                });

                if (inventoryIndex != -1)
                {
                    seedInventoryIndex = inventoryIndex;
                }
            }

            if (seedInventoryIndex != -1)
            {
                player.Input.ItemSlot = seedInventoryIndex;
                player.Input.ActionType = ActionTypes.Plant;
                return true;
            }
        }

        // 4. Till: target is a regular floor and player has a hoe
        if (HasEquippedToolType(ref player, ToolType.Hoe) &&
            tile.Type == TileType.Floor &&
            tile.TileId != TilledSoilTileId)
        {
            player.Input.ActionType = ActionTypes.Till;
            return true;
        }

        return false;
    }
}
