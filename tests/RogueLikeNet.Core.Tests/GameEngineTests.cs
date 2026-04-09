using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Core.Tests;

public class GameEngineTests
{
    private static readonly BspDungeonGenerator _gen = new(42);

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
        var swordTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.ShortSword);
        engine.SpawnItemOnGround(swordTemplate, 0, Position.FromCoords(sx, sy, Position.DefaultZ));

        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        var hud = engine.GetPlayerStateData(player);
        Assert.NotNull(hud);
        Assert.True(hud!.InventoryCount > 0);
        Assert.NotEmpty(hud.InventoryItems);
        Assert.Equal(ItemDefinitions.ShortSword, hud.InventoryItems[0].ItemTypeId);
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
    public void GetPlayerStateData_ReturnsSkillData()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        var hud = engine.GetPlayerStateData(player);
        Assert.NotNull(hud);
        Assert.Equal(4, hud!.Skills.Length);
        Assert.Equal(SkillDefinitions.PowerStrike, hud.Skills[0].Id);
        Assert.Equal(SkillDefinitions.ShieldBash, hud.Skills[1].Id);
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
                          chunk.ResourceNodes.Length + chunk.TownNpcs.Length + chunk.Elements.Length;
        Assert.True(entityCount > 0, "Chunk loading should spawn entities");
    }

    [Fact]
    public void SpawnItemOnGround_CreatesItemWithCorrectType()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        var template = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.LongSword);
        var item = engine.SpawnItemOnGround(template, 0, Position.FromCoords(10, 10, Position.DefaultZ));

        Assert.False(item.IsDestroyed);
        Assert.Equal(ItemDefinitions.LongSword, item.Item.ItemTypeId);
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
                chunk.Tiles[x, y].Type = TileType.Blocked;

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

        var swordTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.ShortSword);
        engine.SpawnItemOnGround(swordTemplate, 0, Position.FromCoords(sx, sy, Position.DefaultZ));

        // Verify entity exists with ItemData at the expected position
        var chunk = engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ))!;
        int count = chunk.GroundItems.ToArray().Count(gi => gi.Position.X == sx && gi.Position.Y == sy && gi.Item.ItemTypeId == ItemDefinitions.ShortSword);
        Assert.Equal(1, count);
    }

    [Fact]
    public void GetPlayerStateData_SkillNames_MatchSkillDefinitions()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        var hud = engine.GetPlayerStateData(player);
        Assert.NotNull(hud);
        Assert.Equal(4, hud!.Skills.Length);
        Assert.Equal(SkillDefinitions.GetName(hud.Skills[0].Id), hud.Skills[0].Name);
        Assert.Equal(SkillDefinitions.GetName(hud.Skills[1].Id), hud.Skills[1].Name);
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
        var swordTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.ShortSword);
        engine.SpawnItemOnGround(swordTemplate, 0, Position.FromCoords(sx, sy, Position.DefaultZ));
        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();
        player.Input.ActionType = ActionTypes.UseItem;
        player.Input.ItemSlot = 0;
        engine.Tick();

        // Equip armor
        var armorTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.LeatherArmor);
        engine.SpawnItemOnGround(armorTemplate, 0, Position.FromCoords(sx, sy, Position.DefaultZ));
        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();
        player.Input.ActionType = ActionTypes.UseItem;
        player.Input.ItemSlot = 0;
        engine.Tick();

        var hud = engine.GetPlayerStateData(player);
        Assert.NotNull(hud);
        Assert.Equal(2, hud!.EquippedItems.Length);
        Assert.Contains(hud.EquippedItems, e => e.ItemTypeId == ItemDefinitions.ShortSword);
        Assert.Contains(hud.EquippedItems, e => e.ItemTypeId == ItemDefinitions.LeatherArmor);
    }

    [Fact]
    public void GetPlayerStateData_InventoryStackCounts()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        var swordTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.ShortSword);
        engine.SpawnItemOnGround(swordTemplate, 0, Position.FromCoords(sx, sy, Position.DefaultZ));
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
        engine.WorldMap.GetChunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ))!.Tiles[10, 10].Type = TileType.Floor;
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

        var template = ItemDefinitions.Get(ItemDefinitions.HealthPotion);
        engine.SpawnItemOnGround(template, 0, Position.FromCoords(sx, sy, Position.DefaultZ));

        var drop = engine.FindDropPosition(Position.FromCoords(sx, sy, Position.DefaultZ));
        Assert.True(drop.X != sx || drop.Y != sy, "Should find a different position when origin is occupied");
    }

    [Fact]
    public void SpawnElement_WithLight_CreatesLightSource()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        var element = new DungeonElement
        {
            Position = Position.FromCoords(10, 10, Position.DefaultZ),
            Appearance = new TileAppearance('*', 0xFFAA00),
            Light = new LightSource { Radius = 5 }
        };

        var entity = engine.SpawnElement(element);
        Assert.NotNull(entity.Light);
        Assert.Equal(5, entity.Light!.Value.Radius);
    }

    [Fact]
    public void SpawnElement_WithoutLight_NoLightSource()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        var element = new DungeonElement
        {
            Position = Position.FromCoords(10, 10, Position.DefaultZ),
            Appearance = new TileAppearance('#', 0x888888),
            Light = null
        };

        var entity = engine.SpawnElement(element);
        Assert.Null(entity.Light);
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
            ItemTypeId = ItemDefinitions.HealthPotion,
            StackCount = 1,
        });

        var state = engine.GetPlayerStateData(player);
        Assert.NotNull(state);
        Assert.Single(state!.InventoryItems);
        Assert.Equal(ItemDefinitions.CategoryPotion, state.InventoryItems[0].Category);
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

        int[] expectedResources = [ItemDefinitions.Wood, ItemDefinitions.CopperOre, ItemDefinitions.IronOre, ItemDefinitions.GoldOre];
        foreach (int resId in expectedResources)
        {
            var item = playerRef.Inventory.Items.Find(i => i.ItemTypeId == resId);
            Assert.Equal(9999, item.StackCount);
        }
    }

    [Fact]
    public void ResourceItems_UseDifferentGlyphs()
    {
        var wood = ItemDefinitions.Get(ItemDefinitions.Wood);
        Assert.Equal(TileDefinitions.GlyphLog, wood.GlyphId);

        var copper = ItemDefinitions.Get(ItemDefinitions.CopperOre);
        Assert.Equal(TileDefinitions.GlyphOreNugget, copper.GlyphId);

        var iron = ItemDefinitions.Get(ItemDefinitions.IronOre);
        Assert.Equal(TileDefinitions.GlyphOreNugget, iron.GlyphId);

        var gold = ItemDefinitions.Get(ItemDefinitions.GoldOre);
        Assert.Equal(TileDefinitions.GlyphOreNugget, gold.GlyphId);
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

        var itemData = new ItemData { ItemTypeId = ItemDefinitions.HealthPotion, StackCount = 1 };
        engine.SpawnItemOnGround(itemData, Position.FromCoords(5, 5, Position.DefaultZ));

        Assert.True(chunk.IsModifiedSinceLastSave);
    }

    [Fact]
    public void SpawnElement_MarksChunkDirty()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var chunk = engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ))!;
        chunk.ClearSaveFlag();

        engine.SpawnElement(new DungeonElement
        {
            Position = Position.FromCoords(5, 5, Position.DefaultZ),
            Appearance = new TileAppearance('#', 0x888888),
        });

        Assert.True(chunk.IsModifiedSinceLastSave);
    }

    [Fact]
    public void SpawnResourceNode_MarksChunkDirty()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var chunk = engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ))!;
        chunk.ClearSaveFlag();

        engine.SpawnResourceNode(Position.FromCoords(5, 5, Position.DefaultZ), ResourceNodeDefinitions.Get(ResourceNodeDefinitions.Tree));

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
}
