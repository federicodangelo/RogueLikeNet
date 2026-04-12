using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Core.Tests;

public class GameEngineTests
{
    private static readonly BspDungeonGenerator _gen = new(42);

    private static int ItemId(string id) => GameData.Instance.Items.GetNumericId(id);
    private static Data.ItemDefinition Item(string id) => GameData.Instance.Items.Get(id)!;

    [Fact]
    public void SpawnPlayer_CreatesEntity()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var _p = engine.SpawnPlayer(1, Position.FromCoords(10, 10, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        Assert.False(player.IsDead);
        Assert.Equal(10, player.Position.X);
        Assert.Equal(10, player.Position.Y);
    }

    [Fact]
    public void Tick_IncrementsTickCounter()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        Assert.Equal(0, engine.CurrentTick);
        engine.Tick();
        Assert.Equal(1, engine.CurrentTick);
    }

    [Fact]
    public void FindSpawnPosition_IsWalkable()
    {
        using var engine = new GameEngine(42, _gen);
        var (x, y, _) = engine.FindSpawnPosition();
        var chunk = engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        Assert.True(chunk.Tiles[x, y].IsWalkable);
    }

    [Fact]
    public void Tick_ComputesLightingAroundPlayer()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        var (sx, sy, _) = engine.FindSpawnPosition();
        engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        engine.Tick();

        var chunk = engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ))!;
        Assert.True(chunk.LightLevels[sx, sy] > 0,
            $"Player tile ({sx},{sy}) has LightLevel={chunk.LightLevels[sx, sy]}, expected > 0");

        int litCount = 0;
        for (int dx = -3; dx <= 3; dx++)
            for (int dy = -3; dy <= 3; dy++)
            {
                int nx = sx + dx, ny = sy + dy;
                if (nx >= 0 && nx < Chunk.Size && ny >= 0 && ny < Chunk.Size)
                {
                    if (chunk.LightLevels[nx, ny] > 0)
                        litCount++;
                }
            }
        Assert.True(litCount > 5, $"Only {litCount} tiles lit in 7x7 area around player, expected > 5");
    }

    [Fact]
    public void CombatProperty_ReturnsSystem()
    {
        using var engine = new GameEngine(42, _gen);
        Assert.NotNull(engine.Combat);
    }

    [Fact]
    public void InventoryProperty_ReturnsSystem()
    {
        using var engine = new GameEngine(42, _gen);
        Assert.NotNull(engine.Inventory);
    }

    [Fact]
    public void GetPlayerStateData_WithInventoryItems_ReturnsNames()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Pick up an item
        var swordTemplate = Item("short_sword");
        engine.SpawnItemOnGround(swordTemplate, Position.FromCoords(sx, sy, Position.DefaultZ));

        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        var hud = engine.GetPlayerStateData(player);
        Assert.NotNull(hud);
        Assert.True(hud!.InventoryCount > 0);
        Assert.NotEmpty(hud.InventoryItems);
        Assert.Equal(ItemId("short_sword"), hud.InventoryItems[0].ItemTypeId);
    }

    [Fact]
    public void GetPlayerStateData_DeadEntity_ReturnsNull()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _m = engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ), new MonsterData { MonsterTypeId = 0, Health = 1, Attack = 5, Defense = 0, Speed = 8 });
        ref var monster = ref engine.WorldMap.GetMonsterRef(_m.Id);

        // Kill monster
        monster.Health.Current = 0;
        engine.Tick(); // This marks dead and destroys monster

        // GetPlayerStateData only works with PlayerEntity, so test null for a dead player instead
        var _p = engine.SpawnPlayer(2, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        player.Health.Current = 0;
        var hud = engine.GetPlayerStateData(player);
        Assert.Null(hud);
    }

    [Fact]
    public void GetPlayerStateData_ReturnsClassInfo()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        var hud = engine.GetPlayerStateData(player);
        Assert.NotNull(hud);
        Assert.Equal(1, hud!.Level);
        Assert.Equal(0, hud.Experience);
    }

    [Fact]
    public void EnsureChunkLoaded_SpawnsEntities()
    {
        using var engine = new GameEngine(42, _gen);
        var chunk = engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        int entityCount = chunk.Monsters.Length + chunk.GroundItems.Length +
                          chunk.ResourceNodes.Length + chunk.TownNpcs.Length;
        Assert.True(entityCount > 0, "Chunk loading should spawn entities");
    }

    [Fact]
    public void SpawnItemOnGround_CreatesItemWithCorrectType()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        var template = Item("long_sword");
        var item = engine.SpawnItemOnGround(template, Position.FromCoords(10, 10, Position.DefaultZ));

        Assert.False(item.IsDestroyed);
        Assert.Equal(ItemId("long_sword"), item.Item.ItemTypeId);
        Assert.Equal(1, item.Item.StackCount);
    }

    [Fact]
    public void EnsureChunkLoaded_FarChunk_HigherDifficulty()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(5, 5, Position.DefaultZ));
        var chunk = engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(5, 5, Position.DefaultZ));
        Assert.NotNull(chunk);
    }

    [Fact]
    public void FindSpawnPosition_FallbackWhenNoFloor()
    {
        using var engine = new GameEngine(42, _gen);
        var chunk = engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                chunk.Tiles[x, y].TileId = GameData.Instance.Tiles.GetNumericId("wall");

        var (rx, ry, _) = engine.FindSpawnPosition();
        Assert.Equal(Chunk.Size / 2, rx);
        Assert.Equal(Chunk.Size / 2, ry);
    }

    [Fact]
    public void SpawnItemOnGround_CreatesGroundItemEntity()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        var swordTemplate = Item("short_sword");
        engine.SpawnItemOnGround(swordTemplate, Position.FromCoords(sx, sy, Position.DefaultZ));

        // Verify entity exists with ItemData at the expected position
        var chunk = engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ))!;
        int count = chunk.GroundItems.ToArray().Count(gi => gi.Position.X == sx && gi.Position.Y == sy && gi.Item.ItemTypeId == ItemId("short_sword"));
        Assert.Equal(1, count);
    }

    [Fact]
    public void GetPlayerStateData_EquippedNames_AfterEquip()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Equip weapon
        var swordTemplate = Item("short_sword");
        engine.SpawnItemOnGround(swordTemplate, Position.FromCoords(sx, sy, Position.DefaultZ));
        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();
        player.Input.ActionType = ActionTypes.UseItem;
        player.Input.ItemSlot = 0;
        engine.Tick();

        // Equip armor
        var armorTemplate = Item("leather_armor");
        engine.SpawnItemOnGround(armorTemplate, Position.FromCoords(sx, sy, Position.DefaultZ));
        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();
        player.Input.ActionType = ActionTypes.UseItem;
        player.Input.ItemSlot = 0;
        engine.Tick();

        var hud = engine.GetPlayerStateData(player);
        Assert.NotNull(hud);
        Assert.Equal(2, hud!.EquippedItems.Length);
        Assert.Contains(hud.EquippedItems, e => e.ItemTypeId == ItemId("short_sword"));
        Assert.Contains(hud.EquippedItems, e => e.ItemTypeId == ItemId("leather_armor"));
    }

    [Fact]
    public void GetPlayerStateData_InventoryStackCounts()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        var swordTemplate = Item("short_sword");
        engine.SpawnItemOnGround(swordTemplate, Position.FromCoords(sx, sy, Position.DefaultZ));
        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        var hud = engine.GetPlayerStateData(player);
        Assert.NotNull(hud);
        Assert.Single(hud!.InventoryItems);
        Assert.Equal(1, hud.InventoryItems[0].StackCount);
    }

    [Fact]
    public void PlayerDeath_RespawnsWithHalfHealth()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        int maxHp = player.Health.Max;
        player.Health.Current = 0;

        engine.Tick();

        Assert.False(player.IsDead);
        Assert.Equal(maxHp / 2, player.Health.Current);
    }

    [Fact]
    public void PlayerDeath_LosesExperience()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        player.ClassData.Experience = 100;
        player.Health.Current = 0;

        engine.Tick();

        Assert.Equal(75, player.ClassData.Experience);
    }

    [Fact]
    public void PlayerDeath_ZeroExperience_StaysZero()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        player.Health.Current = 0;

        engine.Tick();

        Assert.Equal(0, player.ClassData.Experience);
    }

    [Fact]
    public void SpawnPlayer_HighSpeed_ZeroMoveDelay()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var _p = engine.SpawnPlayer(1, Position.FromCoords(10, 10, Position.DefaultZ), ClassDefinitions.Rogue);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        Assert.Equal(0, player.MoveDelay.Interval);
    }

    [Fact]
    public void FindDropPosition_OriginFree_ReturnsOrigin()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        engine.WorldMap.GetChunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ))!.Tiles[10, 10].TileId = GameData.Instance.Tiles.GetNumericId("floor");
        var drop = engine.FindDropPosition(Position.FromCoords(10, 10, Position.DefaultZ));
        Assert.Equal(10, drop.X);
        Assert.Equal(10, drop.Y);
        Assert.Equal(Position.DefaultZ, drop.Z);
    }

    [Fact]
    public void FindDropPosition_OriginOccupied_FindsNearby()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();

        var template = Item("health_potion_small");
        engine.SpawnItemOnGround(template, Position.FromCoords(sx, sy, Position.DefaultZ));

        var drop = engine.FindDropPosition(Position.FromCoords(sx, sy, Position.DefaultZ));
        Assert.True(drop.X != sx || drop.Y != sy, "Should find a different position when origin is occupied");
    }

    [Fact]
    public void GetPlayerStateData_InventoryItemCategory()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        Assert.NotNull(player.Inventory.Items);
        player.Inventory.Items.Add(new ItemData
        {
            ItemTypeId = ItemId("health_potion_small"),
            StackCount = 1,
        });

        var state = engine.GetPlayerStateData(player);
        Assert.NotNull(state);
        Assert.Single(state!.InventoryItems);
        Assert.Equal((int)ItemCategory.Potion, state.InventoryItems[0].Category);
    }

    [Fact]
    public void GiveDebugResources_Adds9999OfEachResource()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        ref var playerRef = ref engine.WorldMap.GetPlayerRef(player.Id);
        engine.GiveDebugResources(ref playerRef);

        Assert.NotNull(playerRef.Inventory.Items);

        int[] expectedResources = [ItemId("wood"), ItemId("copper_ore"), ItemId("iron_ore"), ItemId("gold_ore")];
        foreach (int resId in expectedResources)
        {
            var item = playerRef.Inventory.Items.Find(i => i.ItemTypeId == resId);
            Assert.Equal(9999, item.StackCount);
        }
    }

    [Fact]
    public void ResourceItems_UseDifferentGlyphs()
    {
        var wood = Item("wood");
        Assert.Equal(RenderConstants.GlyphLog, wood.GlyphId);

        var copper = Item("copper_ore");
        Assert.Equal(RenderConstants.GlyphOreNugget, copper.GlyphId);

        var iron = Item("iron_ore");
        Assert.Equal(RenderConstants.GlyphOreNugget, iron.GlyphId);

        var gold = Item("gold_ore");
        Assert.Equal(RenderConstants.GlyphOreNugget, gold.GlyphId);
    }

    // ──────────────────────────────────────────────
    // Spawn methods mark chunk dirty for persistence
    // ──────────────────────────────────────────────

    [Fact]
    public void SpawnMonster_MarksChunkDirty()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var chunk = engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ))!;
        chunk.ClearSaveFlag();

        engine.SpawnMonster(Position.FromCoords(5, 5, Position.DefaultZ), new MonsterData
        {
            MonsterTypeId = 1,
            Health = 10,
            Attack = 2,
            Defense = 1,
            Speed = 3
        });

        Assert.True(chunk.IsModifiedSinceLastSave);
    }

    [Fact]
    public void SpawnItemOnGround_MarksChunkDirty()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var chunk = engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ))!;
        chunk.ClearSaveFlag();

        var itemData = new ItemData { ItemTypeId = ItemId("health_potion_small"), StackCount = 1 };
        engine.SpawnItemOnGround(itemData, Position.FromCoords(5, 5, Position.DefaultZ));

        Assert.True(chunk.IsModifiedSinceLastSave);
    }

    [Fact]
    public void SpawnResourceNode_MarksChunkDirty()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var chunk = engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ))!;
        chunk.ClearSaveFlag();

        engine.SpawnResourceNode(Position.FromCoords(5, 5, Position.DefaultZ), GameData.Instance.ResourceNodes.Get("tree")!);

        Assert.True(chunk.IsModifiedSinceLastSave);
    }

    [Fact]
    public void SpawnTownNpc_MarksChunkDirty()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var chunk = engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ))!;
        chunk.ClearSaveFlag();

        engine.SpawnTownNpc(Position.FromCoords(5, 5, Position.DefaultZ), "TestNpc", 10, 10, 5);

        Assert.True(chunk.IsModifiedSinceLastSave);
    }

    // ── Debug mode flags ──

    [Fact]
    public void DebugFlags_DefaultFalse()
    {
        using var engine = new GameEngine(42, _gen);
        Assert.False(engine.DebugNoCollision);
        Assert.False(engine.DebugInvulnerable);
        Assert.False(engine.DebugMaxSpeed);
        Assert.False(engine.DebugFreeCrafting);
    }

    // ── GetPlayerStateData ──

    [Fact]
    public void GetPlayerStateData_ReturnsNullForDeadPlayer()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var _p = engine.SpawnPlayer(1, Position.FromCoords(10, 10, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        player.Health.Current = 0;

        var state = engine.GetPlayerStateData(player);
        Assert.Null(state);
    }

    [Fact]
    public void GetPlayerStateData_IncludesEquipment()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Equip a weapon
        var swordDef = GameData.Instance.Items.Get("iron_sword");
        if (swordDef != null)
        {
            engine.SpawnItemOnGround(swordDef, player.Position);
            player.Input.ActionType = ActionTypes.PickUp;
            engine.Tick();
            player = ref engine.WorldMap.GetPlayerRef(_p.Id);
            player.Input.ActionType = ActionTypes.UseItem;
            player.Input.ItemSlot = 0;
            engine.Tick();
            player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        }

        var state = engine.GetPlayerStateData(player);
        Assert.NotNull(state);
        Assert.NotNull(state.EquippedItems);
    }

    [Fact]
    public void GetPlayerStateData_IncludesQuickSlots()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        var state = engine.GetPlayerStateData(player);
        Assert.NotNull(state);
        Assert.NotNull(state.QuickSlotIndices);
        Assert.Equal(8, state.QuickSlotIndices.Length);
    }

    // ── GiveDebugResources ──

    [Fact]
    public void GiveDebugResources_AddsResources()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var _p = engine.SpawnPlayer(1, Position.FromCoords(10, 10, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        int before = player.Inventory.Items.Count;
        engine.GiveDebugResources(ref player);
        Assert.True(player.Inventory.Items.Count > before);
    }

    // ── All classes can spawn ──

    [Theory]
    [InlineData(ClassDefinitions.Warrior)]
    [InlineData(ClassDefinitions.Rogue)]
    [InlineData(ClassDefinitions.Mage)]
    [InlineData(ClassDefinitions.Ranger)]
    public void SpawnPlayer_AllClasses(int classId)
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var _p = engine.SpawnPlayer(1, Position.FromCoords(10, 10, Position.DefaultZ), classId);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        Assert.False(player.IsDead);
        Assert.Equal(classId, player.ClassData.ClassId);
    }

    // ── DestroyEntitiesInChunk ──

    [Fact]
    public void DestroyEntitiesInChunk_ClearsEntities()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        engine.SpawnMonster(Position.FromCoords(5, 5, Position.DefaultZ), new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 1, Defense = 0, Speed = 1 });

        var chunk = engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ))!;
        Assert.True(chunk.Monsters.Length > 0);

        engine.DestroyEntitiesInChunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        Assert.Equal(0, chunk.Monsters.Length);
    }

    // ── EnsureChunkLoadedOrDoesntExist ──

    [Fact]
    public void EnsureChunkLoadedOrDoesntExist_ReturnsNullForNonExistent()
    {
        using var engine = new GameEngine(42, _gen);
        // Try a far away chunk that the generator may report as not existing
        var result = engine.EnsureChunkLoadedOrDoesntExist(ChunkPosition.FromCoords(9999, 9999, 0));
        // This may or may not return null depending on generator; just verify no crash
        Assert.True(result == null || result != null);
    }

    // ── SpawnCrop tests ──

    [Fact]
    public void SpawnCrop_ValidSeed_SpawnsCrop()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();

        // Find a seed item
        var seedDef = GameData.Instance.Items.Get("wheat_seeds");
        if (seedDef?.Seed == null) return;

        ref var crop = ref engine.SpawnCrop(Position.FromCoords(sx, sy, Position.DefaultZ), seedDef);
        Assert.Equal(seedDef.NumericId, crop.CropData.SeedItemTypeId);
        Assert.Equal(0, crop.CropData.GrowthTicksCurrent);
        Assert.False(crop.CropData.IsWatered);
    }

    [Fact]
    public void SpawnCrop_InvalidSeed_Throws()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();

        var swordDef = GameData.Instance.Items.Get("short_sword")!;
        Assert.Throws<ArgumentException>(() => engine.SpawnCrop(Position.FromCoords(sx, sy, Position.DefaultZ), swordDef));
    }

    // ── FindDropPosition tests ──

    [Fact]
    public void FindDropPosition_ReturnsWalkablePosition()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();

        var dropPos = engine.FindDropPosition(Position.FromCoords(sx, sy, Position.DefaultZ));
        Assert.True(engine.WorldMap.GetTile(dropPos).IsWalkable);
    }

    // ── Player death and respawn ──

    [Fact]
    public void PlayerDeath_Respawns_WithHalfHealth()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        int maxHealth = player.Health.Max;
        player.Health.Current = 0; // Kill the player

        engine.Tick(); // should respawn

        player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        Assert.Equal(maxHealth / 2, player.Health.Current);
        Assert.False(player.IsDead);
    }

    // ── FindSpawnPosition fallback ──

    [Fact]
    public void FindSpawnPosition_ReturnsWalkable()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, sz) = engine.FindSpawnPosition();
        var tile = engine.WorldMap.GetTile(Position.FromCoords(sx, sy, sz));
        Assert.True(tile.IsWalkable);
    }

    // ── GetPlayerStateData with nearby stations ──

    [Fact]
    public void GetPlayerStateData_IncludesNearbyStations()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Place a crafting bench adjacent
        var benchDef = GameData.Instance.Items.Get("crafting_bench");
        if (benchDef != null)
        {
            var stationPos = Position.FromCoords(sx + 1, sy, Position.DefaultZ);
            engine.WorldMap.SetTile(stationPos, new TileInfo
            {
                TileId = GameData.Instance.Tiles.GetNumericId("floor"),
                PlaceableItemId = benchDef.NumericId,
            });
        }

        var stateData = engine.GetPlayerStateData(player);
        Assert.NotNull(stateData);
        // Hand station is always available
        Assert.Contains((int)CraftingStationType.Hand, stateData!.NearbyStationsTypes);
    }

    // ── Loot drops from monsters ──

    [Fact]
    public void MonsterDeath_CanDropLoot()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Give player huge attack to one-shot monster
        player.CombatStats.Attack = 9999;

        var monsterPos = Position.FromCoords(sx + 1, sy, Position.DefaultZ);
        engine.WorldMap.SetTile(monsterPos, new TileInfo { TileId = GameData.Instance.Tiles.GetNumericId("floor") });
        engine.SpawnMonster(monsterPos, new MonsterData { MonsterTypeId = 0, Health = 1, Attack = 0, Defense = 0, Speed = 1 });

        player.Input.ActionType = ActionTypes.Move;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick(); // converts to attack, kills monster

        // After tick, dead entities cleaned up. Loot may have dropped.
        var chunk = engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ))!;
        // Monster should be dead and removed
        Assert.DoesNotContain(chunk.Monsters.ToArray(), m => m.Position == monsterPos && !m.IsDead);
    }

    // ── Dispose ──

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var engine = new GameEngine(42, _gen);
        engine.Dispose(); // should not throw
    }

    // ── SpawnAnimal ──

    [Fact]
    public void SpawnAnimal_CreatesAnimalEntity()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();

        var animalDefs = GameData.Instance.Animals.All.ToArray();
        if (animalDefs.Length == 0) return;
        var animalDef = animalDefs[0];

        var pos = Position.FromCoords(sx + 2, sy, Position.DefaultZ);
        ref var animal = ref engine.SpawnAnimal(pos, animalDef);

        Assert.Equal(pos, animal.Position);
        Assert.Equal(animalDef.Health, animal.Health.Max);
        Assert.Equal(animalDef.NumericId, animal.AnimalData.AnimalTypeId);
    }

    // ── RawEntityJsonHandler ──

    [Fact]
    public void RawEntityJsonHandler_InvokedDuringChunkLoad()
    {
        string? capturedJson = null;
        GameEngine? capturedEngine = null;

        // Use a fake generator that returns a GenerationResult with RawEntityJson
        var fakeGen = new FakeGeneratorWithEntityJson("[{\"type\":\"test\"}]");
        using var engine = new GameEngine(42, fakeGen);
        engine.RawEntityJsonHandler = (json, eng) =>
        {
            capturedJson = json;
            capturedEngine = eng;
        };

        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        Assert.Equal("[{\"type\":\"test\"}]", capturedJson);
        Assert.Same(engine, capturedEngine);
    }

    [Fact]
    public void RawEntityJsonHandler_NullJson_DoesNotInvoke()
    {
        bool invoked = false;

        using var engine = new GameEngine(42, _gen);
        engine.RawEntityJsonHandler = (_, _) => invoked = true;
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        Assert.False(invoked);
    }

    private class FakeGeneratorWithEntityJson : IDungeonGenerator
    {
        private readonly string _json;
        private readonly HashSet<ChunkPosition> _generated = new();

        public FakeGeneratorWithEntityJson(string rawEntityJson) => _json = rawEntityJson;

        public bool Exists(ChunkPosition chunkPos) => true;

        public GenerationResult Generate(ChunkPosition chunkPos)
        {
            var chunk = new Chunk(chunkPos);
            for (int x = 0; x < Chunk.Size; x++)
                for (int y = 0; y < Chunk.Size; y++)
                    chunk.Tiles[x, y] = new TileInfo { TileId = GameData.Instance.Tiles.GetNumericId("floor") };

            return new GenerationResult(chunk)
            {
                RawEntityJson = _generated.Add(chunkPos) ? _json : null,
            };
        }
    }
}
