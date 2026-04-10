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
    /// <summary>Glyph ID used for tilled soil tiles.</summary>
    public const int TilledSoilGlyphId = 126; // ~ (tilde, tilled appearance)

    private static readonly (int DX, int DY)[] AdjacentOffsets =
        [(0, -1), (0, 1), (-1, 0), (1, 0)];

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
                    continue; // No valid interact action, skip processing other actions this tick

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
                case ActionTypes.Interact:
                    ResolveInteract(ref player, map);
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
        if (!IsAdjacent(player.Input.TargetX, player.Input.TargetY)) return;

        // Player must have a hoe equipped
        if (!HasEquippedToolType(ref player, ToolType.Hoe)) return;

        // Target must be a floor tile (dirt/grass)
        var tile = map.GetTile(target);
        if (tile.Type != TileType.Floor) return;
        if (tile.HasPlaceable) return;

        // Check no entity occupies the target
        if (map.IsPositionOccupiedByEntity(target)) return;

        // Convert to tilled soil (change the tile glyph/color to represent tilled soil)
        tile.GlyphId = TilledSoilGlyphId;
        tile.FgColor = TileDefinitions.ColorTilledSoil;
        map.SetTile(target, tile);
    }

    private static void ProcessPlant(ref PlayerEntity player, WorldMap map)
    {
        int slot = player.Input.ItemSlot;
        int targetX = player.Position.X + player.Input.TargetX;
        int targetY = player.Position.Y + player.Input.TargetY;
        var target = Position.FromCoords(targetX, targetY, player.Position.Z);
        player.Input.ActionType = ActionTypes.None;

        // Must be adjacent
        if (!IsAdjacent(player.Input.TargetX, player.Input.TargetY)) return;

        // Validate inventory slot
        if (slot < 0 || slot >= player.Inventory.Items.Count) return;

        var itemData = player.Inventory.Items[slot];
        var def = GameData.Instance.Items.Get(itemData.ItemTypeId);
        if (def == null || def.Category != ItemCategory.Seed || def.Seed == null) return;

        // Target must be tilled soil
        var tile = map.GetTile(target);
        if (tile.Type != TileType.Floor) return;
        if (tile.GlyphId != TilledSoilGlyphId) return;

        // Check no crop already at this position
        var chunk = map.GetChunkForWorldPos(target);
        if (chunk == null) return;
        foreach (var c in chunk.Crops)
            if (!c.IsDestroyed && c.Position == target) return;

        // Check no other entity occupies the position
        if (map.IsPositionOccupiedByEntity(target)) return;

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
    }

    private static void ProcessWater(ref PlayerEntity player, WorldMap map)
    {
        int targetX = player.Position.X + player.Input.TargetX;
        int targetY = player.Position.Y + player.Input.TargetY;
        var target = Position.FromCoords(targetX, targetY, player.Position.Z);
        player.Input.ActionType = ActionTypes.None;

        // Must be adjacent
        if (!IsAdjacent(player.Input.TargetX, player.Input.TargetY)) return;

        // Player must have a watering can equipped
        if (!HasEquippedToolType(ref player, ToolType.WateringCan)) return;

        // Find a crop at the target position
        var chunk = map.GetChunkForWorldPos(target);
        if (chunk == null) return;

        foreach (ref var crop in chunk.Crops)
        {
            if (crop.IsDestroyed || crop.Position != target) continue;
            crop.CropData.IsWatered = true;
            return;
        }
    }

    private static void ProcessHarvest(ref PlayerEntity player, WorldMap map)
    {
        int targetX = player.Position.X + player.Input.TargetX;
        int targetY = player.Position.Y + player.Input.TargetY;
        var target = Position.FromCoords(targetX, targetY, player.Position.Z);
        player.Input.ActionType = ActionTypes.None;

        // Must be adjacent
        if (!IsAdjacent(player.Input.TargetX, player.Input.TargetY)) return;

        if (player.Inventory.IsFull) return;

        // Find mature crop at target
        var chunk = map.GetChunkForWorldPos(target);
        if (chunk == null) return;

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
            return;
        }
    }

    public static TileAppearance GetCropAppearance(int growthStage) => growthStage switch
    {
        0 => new TileAppearance(TileDefinitions.GlyphCropStage0, TileDefinitions.ColorCropSeedling),
        1 => new TileAppearance(TileDefinitions.GlyphCropStage1, TileDefinitions.ColorCropGrowing),
        2 => new TileAppearance(TileDefinitions.GlyphCropStage2, TileDefinitions.ColorCropGrowing),
        _ => new TileAppearance(TileDefinitions.GlyphCropStage3, TileDefinitions.ColorCropMature),
    };

    private static bool IsAdjacent(int dx, int dy)
    {
        foreach (var (ox, oy) in AdjacentOffsets)
            if (dx == ox && dy == oy) return true;
        return false;
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

        // 2. Feed animal: target has an animal and player has feed in the selected slot
        foreach (var animal in chunk.Animals)
        {
            if (!animal.IsDead && animal.Position == target)
            {
                // Find a valid feed item in inventory
                var animalDef = GameData.Instance.Animals.Get(animal.AnimalData.AnimalTypeId);
                if (animalDef != null)
                {
                    int feedItemId = GameData.Instance.Items.GetNumericId(animalDef.FeedItemId);
                    if (feedItemId != 0)
                    {
                        for (int i = 0; i < player.Inventory.Items.Count; i++)
                        {
                            if (player.Inventory.Items[i].ItemTypeId == feedItemId)
                            {
                                player.Input.ActionType = ActionTypes.FeedAnimal;
                                player.Input.ItemSlot = i;
                                return true;
                            }
                        }
                    }
                }
            }
        }

        // 3. Water: target has a crop and player has watering can
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

        // 4. Plant: target is tilled soil and player has seeds in quick slot
        if (tile.Type == TileType.Floor && tile.GlyphId == TilledSoilGlyphId)
        {
            for (int i = 0; i < player.QuickSlots.Count; i++)
            {
                int inventoryIndex = player.QuickSlots[i];
                if (inventoryIndex < 0 || inventoryIndex >= player.Inventory.Items.Count) continue;

                var itemData = player.Inventory.Items[inventoryIndex];
                var def = GameData.Instance.Items.Get(itemData.ItemTypeId);
                if (def?.Category == ItemCategory.Seed && def.Seed != null)
                {
                    player.Input.ItemSlot = inventoryIndex;
                    player.Input.ActionType = ActionTypes.Plant;
                    return true;
                }
            }
        }

        // 5. Till: target is a regular floor and player has a hoe
        if (HasEquippedToolType(ref player, ToolType.Hoe) &&
            tile.Type == TileType.Floor &&
            tile.GlyphId != TilledSoilGlyphId)
        {
            player.Input.ActionType = ActionTypes.Till;
            return true;
        }

        return false;
    }
}
