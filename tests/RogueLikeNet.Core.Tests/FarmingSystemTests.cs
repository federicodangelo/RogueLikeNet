using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Entities;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.Systems;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Tests;

public class FarmingSystemTests
{
    private static readonly BspDungeonGenerator _gen = new(42);

    private static int ItemId(string id) => GameData.Instance.Items.GetNumericId(id);

    private GameEngine CreateEngine()
    {
        var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        return engine;
    }

    private (int PlayerId, Position TargetPos) SpawnPlayerWithHoeAndSeeds(GameEngine engine, string seedId = "wheat_seeds")
    {
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Equip a hoe
        player.Equipment[(int)EquipSlot.Hand] = new ItemData
        {
            ItemTypeId = ItemId("wooden_hoe"),
            StackCount = 1,
        };

        // Add seeds to inventory
        player.Inventory.Items.Add(new ItemData
        {
            ItemTypeId = ItemId(seedId),
            StackCount = 10,
        });

        // Set seed in quick slot
        player.QuickSlots.Slot0 = player.Inventory.Items.Count - 1; // last inventory slot (where we added seeds)

        // Set up adjacent floor tile
        var targetPos = Position.FromCoords(sx + 1, sy, Position.DefaultZ);
        engine.WorldMap.SetTile(targetPos, new TileInfo
        {
            TileId = GameData.Instance.Tiles.GetNumericId("floor"),
        });

        return (_p.Id, targetPos);
    }

    // ── Tilling ──

    [Fact]
    public void Till_ConvertsTileToTilledSoil()
    {
        using var engine = CreateEngine();
        var (pid, target) = SpawnPlayerWithHoeAndSeeds(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        player.Input.ActionType = ActionTypes.Till;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;

        engine.Tick();

        var tile = engine.WorldMap.GetTile(target);
        Assert.Equal(FarmingSystem.TilledSoilTileId, tile.TileId);
    }

    [Fact]
    public void Till_RequiresHoeEquipped()
    {
        using var engine = CreateEngine();
        var (pid, target) = SpawnPlayerWithHoeAndSeeds(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        // Unequip the hoe
        player.Equipment[(int)EquipSlot.Hand] = default;

        player.Input.ActionType = ActionTypes.Till;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;

        engine.Tick();

        var tile = engine.WorldMap.GetTile(target);
        Assert.NotEqual(FarmingSystem.TilledSoilTileId, tile.TileId);
    }

    [Fact]
    public void Till_RequiresFloorTile()
    {
        using var engine = CreateEngine();
        var (pid, target) = SpawnPlayerWithHoeAndSeeds(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        // Set target to blocked
        engine.WorldMap.SetTile(target, new TileInfo
        {
            TileId = GameData.Instance.Tiles.GetNumericId("wall"),
        });

        player.Input.ActionType = ActionTypes.Till;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;

        engine.Tick();

        var tile = engine.WorldMap.GetTile(target);
        Assert.Equal(TileType.Blocked, tile.Type);
    }

    [Fact]
    public void Till_RequiresAdjacentTile()
    {
        using var engine = CreateEngine();
        var (pid, target) = SpawnPlayerWithHoeAndSeeds(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        // Try to till a non-adjacent tile (2 away)
        player.Input.ActionType = ActionTypes.Till;
        player.Input.TargetX = 2;
        player.Input.TargetY = 0;

        engine.Tick();

        // The tile at distance 2 should be unchanged
        var farTarget = Position.FromCoords(player.Position.X + 2, player.Position.Y, Position.DefaultZ);
        engine.WorldMap.SetTile(farTarget, new TileInfo
        {
            TileId = GameData.Instance.Tiles.GetNumericId("floor"),
        });
        var tile = engine.WorldMap.GetTile(farTarget);
        Assert.NotEqual(FarmingSystem.TilledSoilTileId, tile.TileId);
    }

    // ── Planting ──

    [Fact]
    public void Plant_PlacesCropOnTilledSoil()
    {
        using var engine = CreateEngine();
        var (pid, target) = SpawnPlayerWithHoeAndSeeds(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        // First till
        player.Input.ActionType = ActionTypes.Till;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        // Then plant
        player = ref engine.WorldMap.GetPlayerRef(pid);
        player.Input.ActionType = ActionTypes.Plant;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        player.Input.ItemSlot = 0; // seeds slot
        engine.Tick();

        // Verify crop entity exists at target
        var chunk = engine.WorldMap.GetChunkForWorldPos(target)!;
        var foundCrop = false;
        foreach (var crop in chunk.Crops)
        {
            if (crop.Position == target && !crop.IsDestroyed)
            {
                foundCrop = true;
                Assert.Equal(ItemId("wheat_seeds"), crop.CropData.SeedItemTypeId);
                // Growth is 1 because the farming system update also runs during the plant tick
                Assert.Equal(1, crop.CropData.GrowthTicksCurrent);
                var seedDef = GameData.Instance.Items.Get("wheat_seeds")!.Seed!;
                Assert.False(crop.CropData.IsFullyGrown(seedDef));
                break;
            }
        }
        Assert.True(foundCrop, "Expected a crop entity at the target position");

        // Seeds should be consumed (10 - 1 = 9)
        player = ref engine.WorldMap.GetPlayerRef(pid);
        Assert.Single(player.Inventory.Items);
        Assert.Equal(9, player.Inventory.Items[0].StackCount);
    }

    [Fact]
    public void Plant_RequiresTilledSoil()
    {
        using var engine = CreateEngine();
        var (pid, target) = SpawnPlayerWithHoeAndSeeds(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        // Try to plant on untilled soil
        player.Input.ActionType = ActionTypes.Plant;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        player.Input.ItemSlot = 0;
        engine.Tick();

        // No crop should be placed
        var chunk = engine.WorldMap.GetChunkForWorldPos(target)!;
        var hasCrop = false;
        foreach (var crop in chunk.Crops)
            if (crop.Position == target && !crop.IsDestroyed) hasCrop = true;
        Assert.False(hasCrop);

        // Seeds should not be consumed
        player = ref engine.WorldMap.GetPlayerRef(pid);
        Assert.Equal(10, player.Inventory.Items[0].StackCount);
    }

    [Fact]
    public void Plant_RequiresSeedItem()
    {
        using var engine = CreateEngine();
        var (pid, target) = SpawnPlayerWithHoeAndSeeds(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        // Replace seeds with non-seed item
        player.Inventory.Items[0] = new ItemData
        {
            ItemTypeId = ItemId("wood"),
            StackCount = 5,
        };

        // Till first
        player.Input.ActionType = ActionTypes.Till;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        // Try to plant non-seed
        player = ref engine.WorldMap.GetPlayerRef(pid);
        player.Input.ActionType = ActionTypes.Plant;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        player.Input.ItemSlot = 0;
        engine.Tick();

        var chunk = engine.WorldMap.GetChunkForWorldPos(target)!;
        var hasCrop = false;
        foreach (var crop in chunk.Crops)
            if (crop.Position == target && !crop.IsDestroyed) hasCrop = true;
        Assert.False(hasCrop);
    }

    // ── Crop Growth ──

    [Fact]
    public void CropGrowth_AdvancesEachTick()
    {
        using var engine = CreateEngine();
        var (pid, target) = SpawnPlayerWithHoeAndSeeds(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        // Till and plant
        player.Input.ActionType = ActionTypes.Till;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();
        player = ref engine.WorldMap.GetPlayerRef(pid);
        player.Input.ActionType = ActionTypes.Plant;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        player.Input.ItemSlot = 0;
        engine.Tick();

        // Run a few ticks
        for (int i = 0; i < 10; i++)
            engine.Tick();

        var chunk = engine.WorldMap.GetChunkForWorldPos(target)!;
        foreach (var crop in chunk.Crops)
        {
            if (crop.Position == target && !crop.IsDestroyed)
            {
                // 1 from plant tick + 10 additional ticks = 11
                Assert.Equal(11, crop.CropData.GrowthTicksCurrent);
                return;
            }
        }
        Assert.Fail("Expected crop at target position");
    }

    [Fact]
    public void CropGrowth_StopsWhenFullyGrown()
    {
        using var engine = CreateEngine();
        var (pid, target) = SpawnPlayerWithHoeAndSeeds(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        // Till and plant
        player.Input.ActionType = ActionTypes.Till;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();
        player = ref engine.WorldMap.GetPlayerRef(pid);
        player.Input.ActionType = ActionTypes.Plant;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        player.Input.ItemSlot = 0;
        engine.Tick();

        // Fast-forward: set growth near max
        var chunk = engine.WorldMap.GetChunkForWorldPos(target)!;
        foreach (ref var crop in chunk.Crops)
        {
            if (crop.Position == target && !crop.IsDestroyed)
            {
                var seedDef = GameData.Instance.Items.Get("wheat_seeds")!.Seed!;
                crop.CropData.GrowthTicksCurrent = seedDef.GrowthTicks - 1;
                break;
            }
        }

        // One more tick should make it fully grown
        engine.Tick();

        foreach (var crop in chunk.Crops)
        {
            if (crop.Position == target && !crop.IsDestroyed)
            {
                var seedDef2 = GameData.Instance.Items.Get("wheat_seeds")!.Seed!;
                Assert.True(crop.CropData.IsFullyGrown(seedDef2));
                int growthBefore = crop.CropData.GrowthTicksCurrent;

                // Additional ticks should not increase growth
                engine.Tick();
                foreach (var crop2 in chunk.Crops)
                {
                    if (crop2.Position == target && !crop2.IsDestroyed)
                    {
                        Assert.Equal(growthBefore, crop2.CropData.GrowthTicksCurrent);
                        return;
                    }
                }
            }
        }
    }

    [Fact]
    public void CropGrowth_GlyphChangesWithStage()
    {
        using var engine = CreateEngine();
        var (pid, target) = SpawnPlayerWithHoeAndSeeds(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        // Till and plant
        player.Input.ActionType = ActionTypes.Till;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();
        player = ref engine.WorldMap.GetPlayerRef(pid);
        player.Input.ActionType = ActionTypes.Plant;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        player.Input.ItemSlot = 0;
        engine.Tick();

        // Get the crop and check initial appearance
        var chunk = engine.WorldMap.GetChunkForWorldPos(target)!;
        ref var cropRef = ref GetCropAt(chunk, target);
        Assert.Equal(RenderConstants.GlyphCropStage0, cropRef.Appearance.GlyphId);

        // Stage thresholds for wheat (600 ticks): stage 1 at 198 (0.33), stage 2 at 396 (0.66), stage 3 at 600
        // Set GrowthTicksCurrent so that after tick (+1) it crosses into the next stage.
        var seed = GameData.Instance.Items.Get("wheat_seeds")!;
        int stage1Threshold = (int)(seed.Seed!.GrowthTicks * 0.33f); // 198

        // Advance to stage 1: set to threshold-1 so after tick it becomes exactly threshold
        cropRef.CropData.GrowthTicksCurrent = stage1Threshold - 1; // 197 → stage 0
        engine.Tick(); // 197+1=198 → stage 1

        cropRef = ref GetCropAt(chunk, target);
        Assert.Equal(RenderConstants.GlyphCropStage1, cropRef.Appearance.GlyphId);

        // Advance to stage 2
        int stage2Threshold = (int)(seed.Seed.GrowthTicks * 0.66f); // 396
        cropRef.CropData.GrowthTicksCurrent = stage2Threshold - 1;
        engine.Tick();

        cropRef = ref GetCropAt(chunk, target);
        Assert.Equal(RenderConstants.GlyphCropStage2, cropRef.Appearance.GlyphId);

        // Advance to stage 3 (fully grown)
        cropRef.CropData.GrowthTicksCurrent = seed.Seed.GrowthTicks - 1;
        engine.Tick();

        cropRef = ref GetCropAt(chunk, target);
        Assert.Equal(RenderConstants.GlyphCropStage3, cropRef.Appearance.GlyphId);
    }

    // ── Watering ──

    [Fact]
    public void Water_SetsCropWatered()
    {
        using var engine = CreateEngine();
        var (pid, target) = SpawnPlayerWithHoeAndSeeds(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        // Till and plant
        player.Input.ActionType = ActionTypes.Till;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();
        player = ref engine.WorldMap.GetPlayerRef(pid);
        player.Input.ActionType = ActionTypes.Plant;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        player.Input.ItemSlot = 0;
        engine.Tick();

        // Equip watering can
        player = ref engine.WorldMap.GetPlayerRef(pid);
        player.Equipment[(int)EquipSlot.Hand] = new ItemData
        {
            ItemTypeId = ItemId("watering_can"),
            StackCount = 1,
        };

        // Water the crop
        player.Input.ActionType = ActionTypes.Water;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        var chunk = engine.WorldMap.GetChunkForWorldPos(target)!;
        foreach (var crop in chunk.Crops)
        {
            if (crop.Position == target && !crop.IsDestroyed)
            {
                Assert.True(crop.CropData.IsWatered);
                return;
            }
        }
        Assert.Fail("Expected watered crop at target");
    }

    [Fact]
    public void Water_RequiresWateringCanEquipped()
    {
        using var engine = CreateEngine();
        var (pid, target) = SpawnPlayerWithHoeAndSeeds(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        // Till and plant
        player.Input.ActionType = ActionTypes.Till;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();
        player = ref engine.WorldMap.GetPlayerRef(pid);
        player.Input.ActionType = ActionTypes.Plant;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        player.Input.ItemSlot = 0;
        engine.Tick();

        // Try water without watering can (hoe still equipped)
        player = ref engine.WorldMap.GetPlayerRef(pid);
        player.Input.ActionType = ActionTypes.Water;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        var chunk = engine.WorldMap.GetChunkForWorldPos(target)!;
        foreach (var crop in chunk.Crops)
        {
            if (crop.Position == target && !crop.IsDestroyed)
            {
                Assert.False(crop.CropData.IsWatered);
                return;
            }
        }
    }

    // ── Harvesting ──

    [Fact]
    public void Harvest_FullyGrownCrop_AddsToInventory()
    {
        using var engine = CreateEngine();
        var (pid, target) = SpawnPlayerWithHoeAndSeeds(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        // Till and plant
        player.Input.ActionType = ActionTypes.Till;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();
        player = ref engine.WorldMap.GetPlayerRef(pid);
        player.Input.ActionType = ActionTypes.Plant;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        player.Input.ItemSlot = 0;
        engine.Tick();

        // Force crop to fully grown
        var chunk = engine.WorldMap.GetChunkForWorldPos(target)!;
        foreach (ref var crop in chunk.Crops)
        {
            if (crop.Position == target && !crop.IsDestroyed)
            {
                var wheatSeed = GameData.Instance.Items.Get("wheat_seeds")!.Seed!;
                crop.CropData.GrowthTicksCurrent = wheatSeed.GrowthTicks;
                break;
            }
        }

        // Harvest
        player = ref engine.WorldMap.GetPlayerRef(pid);
        int invCountBefore = player.Inventory.Items.Count;
        player.Input.ActionType = ActionTypes.Harvest;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        // Crop should be destroyed
        var hasCrop = false;
        foreach (var crop in chunk.Crops)
            if (crop.Position == target && !crop.IsDestroyed) hasCrop = true;
        Assert.False(hasCrop, "Crop should be destroyed after harvest");

        // Inventory should have new items
        player = ref engine.WorldMap.GetPlayerRef(pid);
        Assert.True(player.Inventory.Items.Count > invCountBefore ||
                     player.Inventory.Items.Any(i => i.ItemTypeId == ItemId("wheat")),
                     "Expected wheat in inventory after harvest");
    }

    [Fact]
    public void Harvest_ImmatureCrop_DoesNothing()
    {
        using var engine = CreateEngine();
        var (pid, target) = SpawnPlayerWithHoeAndSeeds(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        // Till and plant
        player.Input.ActionType = ActionTypes.Till;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();
        player = ref engine.WorldMap.GetPlayerRef(pid);
        player.Input.ActionType = ActionTypes.Plant;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        player.Input.ItemSlot = 0;
        engine.Tick();

        // Try to harvest immature crop
        player = ref engine.WorldMap.GetPlayerRef(pid);
        player.Input.ActionType = ActionTypes.Harvest;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        // Crop should still exist
        var chunk = engine.WorldMap.GetChunkForWorldPos(target)!;
        var hasCrop = false;
        foreach (var crop in chunk.Crops)
            if (crop.Position == target && !crop.IsDestroyed) hasCrop = true;
        Assert.True(hasCrop, "Immature crop should not be destroyed");
    }

    // ── CropData unit tests ──

    [Fact]
    public void CropData_GrowthStage_ReturnsCorrectStage()
    {
        var crop = new CropData { GrowthTicksCurrent = 0 };
        var def = new SeedData { GrowthTicks = 100 };
        Assert.Equal(0, crop.GetGrowthStage(def));

        crop.GrowthTicksCurrent = 33;
        Assert.Equal(1, crop.GetGrowthStage(def));

        crop.GrowthTicksCurrent = 66;
        Assert.Equal(2, crop.GetGrowthStage(def));

        crop.GrowthTicksCurrent = 100;
        Assert.Equal(3, crop.GetGrowthStage(def));
    }

    [Fact]
    public void CropData_IsFullyGrown_WhenGrowthComplete()
    {
        var crop = new CropData { GrowthTicksCurrent = 99 };
        var def = new SeedData { GrowthTicks = 100 };
        Assert.False(crop.IsFullyGrown(def));

        crop.GrowthTicksCurrent = 100;
        Assert.True(crop.IsFullyGrown(def));

        crop.GrowthTicksCurrent = 150;
        Assert.True(crop.IsFullyGrown(def));
    }

    [Fact]
    public void GetCropAppearance_ReturnsCorrectGlyphs()
    {
        Assert.Equal(RenderConstants.GlyphCropStage0, FarmingSystem.GetCropAppearance(0).GlyphId);
        Assert.Equal(RenderConstants.GlyphCropStage1, FarmingSystem.GetCropAppearance(1).GlyphId);
        Assert.Equal(RenderConstants.GlyphCropStage2, FarmingSystem.GetCropAppearance(2).GlyphId);
        Assert.Equal(RenderConstants.GlyphCropStage3, FarmingSystem.GetCropAppearance(3).GlyphId);
    }

    private static ref CropEntity GetCropAt(Chunk chunk, Position pos)
    {
        for (int i = 0; i < chunk.Crops.Length; i++)
        {
            if (chunk.Crops[i].Position == pos && !chunk.Crops[i].IsDestroyed)
                return ref chunk.Crops[i];
        }
        throw new InvalidOperationException("No crop entity found at position");
    }

    // ── Interact (context-sensitive) tests ──

    [Fact]
    public void Interact_WithHoeOnFloor_Tills()
    {
        using var engine = CreateEngine();
        var (pid, target) = SpawnPlayerWithHoeAndSeeds(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        player.Input.ActionType = ActionTypes.Interact;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        var tile = engine.WorldMap.GetTile(target);
        Assert.Equal(FarmingSystem.TilledSoilTileId, tile.TileId);
    }

    [Fact]
    public void Interact_OnTilledSoilWithSeeds_Plants()
    {
        using var engine = CreateEngine();
        var (pid, target) = SpawnPlayerWithHoeAndSeeds(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        // Till first
        player.Input.ActionType = ActionTypes.Till;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        // Unequip hoe (so interact won't try to till again)
        player = ref engine.WorldMap.GetPlayerRef(pid);
        player.Equipment[(int)EquipSlot.Hand] = default;

        // Interact should plant
        player.Input.ActionType = ActionTypes.Interact;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        var chunk = engine.WorldMap.GetChunkForWorldPos(target)!;
        var hasCrop = false;
        foreach (var crop in chunk.Crops)
            if (crop.Position == target && !crop.IsDestroyed) hasCrop = true;
        Assert.True(hasCrop, "Expected crop to be planted via Interact");
    }

    [Fact]
    public void Interact_OnMatureCrop_Harvests()
    {
        using var engine = CreateEngine();
        var (pid, target) = SpawnPlayerWithHoeAndSeeds(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        // Till + plant
        player.Input.ActionType = ActionTypes.Till;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();
        player = ref engine.WorldMap.GetPlayerRef(pid);
        player.Input.ActionType = ActionTypes.Plant;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        player.Input.ItemSlot = 0;
        engine.Tick();

        // Force mature
        var chunk = engine.WorldMap.GetChunkForWorldPos(target)!;
        foreach (ref var crop in chunk.Crops)
            if (crop.Position == target && !crop.IsDestroyed)
            {
                var wheatSeed = GameData.Instance.Items.Get("wheat_seeds")!.Seed!;
                crop.CropData.GrowthTicksCurrent = wheatSeed.GrowthTicks;
            }

        // Interact should harvest
        player = ref engine.WorldMap.GetPlayerRef(pid);
        player.Input.ActionType = ActionTypes.Interact;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        var hasCropAfter = false;
        foreach (var crop in chunk.Crops)
            if (crop.Position == target && !crop.IsDestroyed) hasCropAfter = true;
        Assert.False(hasCropAfter, "Mature crop should be harvested via Interact");
    }

    [Fact]
    public void Interact_OnAnimalWithFeed_FeedsAnimal()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // Set up adjacent tile
        var animalPos = Position.FromCoords(sx + 1, sy, Position.DefaultZ);
        engine.WorldMap.SetTile(animalPos, new TileInfo
        {
            TileId = GameData.Instance.Tiles.GetNumericId("floor"),
        });

        // Spawn animal
        var chickenDef = GameData.Instance.Animals.Get("chicken")!;
        ref var testAnimal = ref engine.SpawnAnimal(animalPos, chickenDef);
        testAnimal.MoveDelay.Current = 9999; // prevent moving so it stays in place for the test

        // Give player animal feed
        player.Inventory.Items.Add(new ItemData
        {
            ItemTypeId = ItemId("animal_feed"),
            StackCount = 5,
        });

        // Interact should feed
        player.Input.ActionType = ActionTypes.Interact;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        var chunk = engine.WorldMap.GetChunkForWorldPos(animalPos)!;
        foreach (var animal in chunk.Animals)
        {
            if (!animal.IsDead && animal.Position == animalPos)
            {
                Assert.True(animal.AnimalData.IsFed, "Animal should be fed via Interact");
                return;
            }
        }
        Assert.Fail("Expected animal at position");
    }

    [Fact]
    public void Interact_WithWateringCanOnCrop_Waters()
    {
        using var engine = CreateEngine();
        var (pid, target) = SpawnPlayerWithHoeAndSeeds(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        // Till + plant
        player.Input.ActionType = ActionTypes.Till;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();
        player = ref engine.WorldMap.GetPlayerRef(pid);
        player.Input.ActionType = ActionTypes.Plant;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        player.Input.ItemSlot = 0;
        engine.Tick();

        // Equip watering can
        player = ref engine.WorldMap.GetPlayerRef(pid);
        player.Equipment[(int)EquipSlot.Hand] = new ItemData
        {
            ItemTypeId = ItemId("watering_can"),
            StackCount = 1,
        };

        // Interact should water
        player.Input.ActionType = ActionTypes.Interact;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        var chunk = engine.WorldMap.GetChunkForWorldPos(target)!;
        foreach (var crop in chunk.Crops)
        {
            if (crop.Position == target && !crop.IsDestroyed)
            {
                Assert.True(crop.CropData.IsWatered, "Crop should be watered via Interact");
                return;
            }
        }
    }

    // ── Interact priority tests ──

    [Fact]
    public void Interact_HarvestTakesPriorityOverWater()
    {
        using var engine = CreateEngine();
        var (pid, target) = SpawnPlayerWithHoeAndSeeds(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        // Till + plant
        player.Input.ActionType = ActionTypes.Till;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();
        player = ref engine.WorldMap.GetPlayerRef(pid);
        player.Input.ActionType = ActionTypes.Plant;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        player.Input.ItemSlot = 0;
        engine.Tick();

        // Force crop mature (not watered)
        var chunk = engine.WorldMap.GetChunkForWorldPos(target)!;
        ref var crop = ref GetCropAt(chunk, target);
        var wheatSeed = GameData.Instance.Items.Get("wheat_seeds")!.Seed!;
        crop.CropData.GrowthTicksCurrent = wheatSeed.GrowthTicks;

        // Equip watering can — both harvest and water could apply
        player = ref engine.WorldMap.GetPlayerRef(pid);
        player.Equipment[(int)EquipSlot.Hand] = new ItemData
        {
            ItemTypeId = ItemId("watering_can"),
            StackCount = 1,
        };

        // Interact should harvest (priority over water)
        player.Input.ActionType = ActionTypes.Interact;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        var hasCrop = false;
        foreach (var c in chunk.Crops)
            if (c.Position == target && !c.IsDestroyed) hasCrop = true;
        Assert.False(hasCrop, "Harvest should take priority over water — crop should be destroyed");
    }

    [Fact]
    public void Interact_WaterTakesPriorityOverPlant()
    {
        using var engine = CreateEngine();
        var (pid, target) = SpawnPlayerWithHoeAndSeeds(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        // Till + plant
        player.Input.ActionType = ActionTypes.Till;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();
        player = ref engine.WorldMap.GetPlayerRef(pid);
        player.Input.ActionType = ActionTypes.Plant;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        player.Input.ItemSlot = 0;
        engine.Tick();

        // Equip watering can (player still has seeds)
        player = ref engine.WorldMap.GetPlayerRef(pid);
        player.Equipment[(int)EquipSlot.Hand] = new ItemData
        {
            ItemTypeId = ItemId("watering_can"),
            StackCount = 1,
        };

        // Crop exists and is unwatered → Water should take priority over Plant
        player.Input.ActionType = ActionTypes.Interact;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        var chunk = engine.WorldMap.GetChunkForWorldPos(target)!;
        ref var crop = ref GetCropAt(chunk, target);
        Assert.True(crop.CropData.IsWatered, "Water should take priority over plant when crop exists and is unwatered");
    }

    [Fact]
    public void Interact_PlantTakesPriorityOverTill()
    {
        using var engine = CreateEngine();
        var (pid, target) = SpawnPlayerWithHoeAndSeeds(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        // Till to create tilled soil
        player.Input.ActionType = ActionTypes.Till;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        // Player has hoe equipped AND seeds — Interact should plant (not re-till)
        player = ref engine.WorldMap.GetPlayerRef(pid);
        player.Input.ActionType = ActionTypes.Interact;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        var chunk = engine.WorldMap.GetChunkForWorldPos(target)!;
        var hasCrop = false;
        foreach (var c in chunk.Crops)
            if (c.Position == target && !c.IsDestroyed) hasCrop = true;
        Assert.True(hasCrop, "Plant should take priority over till on tilled soil with seeds");
    }

    [Fact]
    public void Interact_HarvestTakesPriorityOverTill()
    {
        using var engine = CreateEngine();
        var (pid, target) = SpawnPlayerWithHoeAndSeeds(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        // Till + plant
        player.Input.ActionType = ActionTypes.Till;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();
        player = ref engine.WorldMap.GetPlayerRef(pid);
        player.Input.ActionType = ActionTypes.Plant;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        player.Input.ItemSlot = 0;
        engine.Tick();

        // Force crop mature
        var chunk = engine.WorldMap.GetChunkForWorldPos(target)!;
        ref var crop = ref GetCropAt(chunk, target);
        var wheatSeed = GameData.Instance.Items.Get("wheat_seeds")!.Seed!;
        crop.CropData.GrowthTicksCurrent = wheatSeed.GrowthTicks;

        // Player has hoe equipped — Interact should harvest (not till)
        player = ref engine.WorldMap.GetPlayerRef(pid);
        player.Input.ActionType = ActionTypes.Interact;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        var hasCrop = false;
        foreach (var c in chunk.Crops)
            if (c.Position == target && !c.IsDestroyed) hasCrop = true;
        Assert.False(hasCrop, "Harvest should take priority over till — crop should be destroyed");
    }

    // ── Interact edge-case tests ──

    [Fact]
    public void Interact_NonAdjacentTarget_DoesNothing()
    {
        using var engine = CreateEngine();
        var (pid, _) = SpawnPlayerWithHoeAndSeeds(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        // Target 2 tiles away (non-adjacent)
        player.Input.ActionType = ActionTypes.Interact;
        player.Input.TargetX = 2;
        player.Input.TargetY = 0;
        engine.Tick();

        // Floor tile 2 tiles away should remain untouched
        var farTarget = Position.FromCoords(player.Position.X + 2, player.Position.Y, player.Position.Z);
        var tile = engine.WorldMap.GetTile(farTarget);
        Assert.NotEqual(FarmingSystem.TilledSoilTileId, tile.TileId);
    }

    [Fact]
    public void Interact_NoValidAction_DoesNothing()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // No tools, no seeds, target is wall
        var targetPos = Position.FromCoords(sx + 1, sy, Position.DefaultZ);
        engine.WorldMap.SetTile(targetPos, new TileInfo
        {
            TileId = GameData.Instance.Tiles.GetNumericId("wall"),
        });

        int inventoryCountBefore = player.Inventory.Items.Count;

        player.Input.ActionType = ActionTypes.Interact;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        // Nothing should change
        player = ref engine.WorldMap.GetPlayerRef(p.Id);
        Assert.Equal(inventoryCountBefore, player.Inventory.Items.Count);
        var tile = engine.WorldMap.GetTile(targetPos);
        Assert.Equal(TileType.Blocked, tile.Type);
    }

    [Fact]
    public void Interact_AlreadyWateredCrop_SkipsWater_FallsToNothing()
    {
        using var engine = CreateEngine();
        var (pid, target) = SpawnPlayerWithHoeAndSeeds(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        // Till + plant
        player.Input.ActionType = ActionTypes.Till;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();
        player = ref engine.WorldMap.GetPlayerRef(pid);
        player.Input.ActionType = ActionTypes.Plant;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        player.Input.ItemSlot = 0;
        engine.Tick();

        // Mark crop as already watered
        var chunk = engine.WorldMap.GetChunkForWorldPos(target)!;
        ref var crop = ref GetCropAt(chunk, target);
        crop.CropData.IsWatered = true;

        // Equip watering can, remove hoe/seeds so no other action applies
        player = ref engine.WorldMap.GetPlayerRef(pid);
        player.Equipment[(int)EquipSlot.Hand] = new ItemData
        {
            ItemTypeId = ItemId("watering_can"),
            StackCount = 1,
        };
        player.Inventory.Items.Clear();

        int seedCount = 0;
        foreach (var item in player.Inventory.Items)
        {
            var def = GameData.Instance.Items.Get(item.ItemTypeId);
            if (def?.Category == ItemCategory.Seed) seedCount++;
        }

        // Interact should do nothing (crop already watered, no seeds, can't till)
        player.Input.ActionType = ActionTypes.Interact;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        // Crop should still be watered and not harvested
        crop = ref GetCropAt(chunk, target);
        Assert.True(crop.CropData.IsWatered);
        Assert.Equal(0, seedCount);
    }

    [Fact]
    public void Interact_PlantsFromInventory_WhenNotInQuickSlot()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // Set up target as tilled soil
        var targetPos = Position.FromCoords(sx + 1, sy, Position.DefaultZ);
        engine.WorldMap.SetTile(targetPos, new TileInfo
        {
            TileId = FarmingSystem.TilledSoilTileId,
        });

        // Add some non-seed items first, then seeds — seeds NOT in quick slot
        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("wood"), StackCount = 5 });
        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("wheat_seeds"), StackCount = 3 });
        // Quick slots are empty (no seed assigned)

        // Interact should find seeds in inventory even without quick slot
        player.Input.ActionType = ActionTypes.Interact;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        var chunk = engine.WorldMap.GetChunkForWorldPos(targetPos)!;
        var hasCrop = false;
        foreach (var c in chunk.Crops)
            if (c.Position == targetPos && !c.IsDestroyed) hasCrop = true;
        Assert.True(hasCrop, "Interact should plant from inventory when no seeds in quick slots");

        // Seed count should decrease
        player = ref engine.WorldMap.GetPlayerRef(p.Id);
        Assert.Equal(2, player.Inventory.Items[1].StackCount);
    }

    [Fact]
    public void Interact_FullFarmingCycle_TillPlantWaterHarvest()
    {
        using var engine = CreateEngine();
        var (pid, target) = SpawnPlayerWithHoeAndSeeds(engine);

        // Step 1: Interact on floor with hoe → tills
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);
        player.Input.ActionType = ActionTypes.Interact;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();
        Assert.Equal(FarmingSystem.TilledSoilTileId, engine.WorldMap.GetTile(target).TileId);

        // Step 2: Interact on tilled soil with seeds → plants
        player = ref engine.WorldMap.GetPlayerRef(pid);
        player.Equipment[(int)EquipSlot.Hand] = default; // unequip hoe
        player.Input.ActionType = ActionTypes.Interact;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();
        var chunk = engine.WorldMap.GetChunkForWorldPos(target)!;
        var hasCrop = false;
        foreach (var c in chunk.Crops)
            if (c.Position == target && !c.IsDestroyed) hasCrop = true;
        Assert.True(hasCrop, "Step 2: Should plant via Interact");

        // Step 3: Interact with watering can → waters
        player = ref engine.WorldMap.GetPlayerRef(pid);
        player.Equipment[(int)EquipSlot.Hand] = new ItemData
        {
            ItemTypeId = ItemId("watering_can"),
            StackCount = 1,
        };
        player.Input.ActionType = ActionTypes.Interact;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();
        ref var crop = ref GetCropAt(chunk, target);
        Assert.True(crop.CropData.IsWatered, "Step 3: Should water via Interact");

        // Force crop mature
        var wheatSeed = GameData.Instance.Items.Get("wheat_seeds")!.Seed!;
        crop.CropData.GrowthTicksCurrent = wheatSeed.GrowthTicks;

        // Step 4: Interact on mature crop → harvests
        player = ref engine.WorldMap.GetPlayerRef(pid);
        player.Input.ActionType = ActionTypes.Interact;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();
        var hasCropAfter = false;
        foreach (var c in chunk.Crops)
            if (c.Position == target && !c.IsDestroyed) hasCropAfter = true;
        Assert.False(hasCropAfter, "Step 4: Should harvest via Interact");
    }
}
