using Arch.Core;
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
        engine.EnsureChunkLoaded(0, 0);
        var entity = engine.SpawnPlayer(1, 10, 10, ClassDefinitions.Warrior);
        Assert.True(engine.EcsWorld.IsAlive(entity));
        ref var pos = ref engine.EcsWorld.Get<Position>(entity);
        Assert.Equal(10, pos.X);
        Assert.Equal(10, pos.Y);
    }

    [Fact]
    public void Tick_IncrementsTickCounter()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0);
        Assert.Equal(0, engine.CurrentTick);
        engine.Tick();
        Assert.Equal(1, engine.CurrentTick);
    }

    [Fact]
    public void FindSpawnPosition_IsWalkable()
    {
        using var engine = new GameEngine(42, _gen);
        var (x, y) = engine.FindSpawnPosition();
        var chunk = engine.EnsureChunkLoaded(0, 0);
        Assert.True(chunk.Tiles[x, y].IsWalkable);
    }

    [Fact]
    public void Tick_ComputesLightingAroundPlayer()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0);

        var (sx, sy) = engine.FindSpawnPosition();
        engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);
        engine.Tick();

        var chunk = engine.WorldMap.TryGetChunk(0, 0)!;
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
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        // Pick up an item
        var swordTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.ShortSword);
        engine.SpawnItemOnGround(swordTemplate, 0, sx, sy);

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.PickUp;
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
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var monster = engine.SpawnMonster(sx + 1, sy, new MonsterData { MonsterTypeId = 0, Health = 1, Attack = 5, Defense = 0, Speed = 8 });

        // Kill monster
        ref var health = ref engine.EcsWorld.Get<Health>(monster);
        health.Current = 0;
        engine.Tick(); // This marks dead and destroys monster

        var hud = engine.GetPlayerStateData(monster);
        Assert.Null(hud);
    }

    [Fact]
    public void GetPlayerStateData_ReturnsSkillData()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

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
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Mage);

        var hud = engine.GetPlayerStateData(player);
        Assert.NotNull(hud);
        Assert.Equal(1, hud!.Level);
        Assert.Equal(0, hud.Experience);
    }

    [Fact]
    public void EnsureChunkLoaded_SpawnsEntities()
    {
        using var engine = new GameEngine(42, _gen);
        // Loading a chunk should spawn monsters and items from generation results
        engine.EnsureChunkLoaded(0, 0);

        int entityCount = 0;
        var query = new QueryDescription();
        engine.EcsWorld.Query(in query, (Entity _) => entityCount++);

        Assert.True(entityCount > 0, "Chunk loading should spawn entities");
    }

    [Fact]
    public void SpawnItemOnGround_CreatesItemWithRarity()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0);

        var template = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.LongSword);
        var item = engine.SpawnItemOnGround(template, 3, 10, 10);

        Assert.True(engine.EcsWorld.IsAlive(item));
        ref var data = ref engine.EcsWorld.Get<ItemData>(item);
        Assert.Equal(3, data.Rarity);
        // Rarity 3 = 250% multiplier, LongSword base attack = 5
        Assert.Equal(5 * 250 / 100, data.BonusAttack);
    }

    [Fact]
    public void EnsureChunkLoaded_FarChunk_HigherDifficulty()
    {
        using var engine = new GameEngine(42, _gen);
        // Load a chunk far from origin for higher difficulty
        engine.EnsureChunkLoaded(5, 5);
        // Should not crash and should generate entities
        var chunk = engine.WorldMap.TryGetChunk(5, 5);
        Assert.NotNull(chunk);
    }

    [Fact]
    public void FindSpawnPosition_FallbackWhenNoFloor()
    {
        using var engine = new GameEngine(42, _gen);
        var chunk = engine.EnsureChunkLoaded(0, 0);
        // Set all tiles to Wall to trigger fallback
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                chunk.Tiles[x, y].Type = TileType.Wall;

        var (rx, ry) = engine.FindSpawnPosition();
        Assert.Equal(Chunk.Size / 2, rx);
        Assert.Equal(Chunk.Size / 2, ry);
    }

    [Fact]
    public void SpawnItemOnGround_CreatesGroundItemEntity()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        // Spawn a floor item
        var swordTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.ShortSword);
        engine.SpawnItemOnGround(swordTemplate, 0, sx, sy);

        // Verify entity exists with GroundItemTag and ItemData at the expected position
        int count = 0;
        var query = new QueryDescription().WithAll<Position, ItemData>();
        engine.EcsWorld.Query(in query, (ref Position pos, ref ItemData data) =>
        {
            if (pos.X == sx && pos.Y == sy && data.ItemTypeId == ItemDefinitions.ShortSword)
                count++;
        });
        Assert.Equal(1, count);
    }

    [Fact]
    public void GetPlayerStateData_SkillNames_MatchSkillDefinitions()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Mage);

        var hud = engine.GetPlayerStateData(player);
        Assert.NotNull(hud);
        Assert.Equal(4, hud!.Skills.Length);
        // Mage skills: Fireball, Heal, and two more
        Assert.Equal(SkillDefinitions.GetName(hud.Skills[0].Id), hud.Skills[0].Name);
        Assert.Equal(SkillDefinitions.GetName(hud.Skills[1].Id), hud.Skills[1].Name);
    }

    [Fact]
    public void GetPlayerStateData_EquippedNames_AfterEquip()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        // Equip weapon
        var swordTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.ShortSword);
        engine.SpawnItemOnGround(swordTemplate, 0, sx, sy);
        ref var input1 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input1.ActionType = ActionTypes.PickUp;
        engine.Tick();
        ref var input2 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input2.ActionType = ActionTypes.UseItem;
        input2.ItemSlot = 0;
        engine.Tick();

        // Equip armor
        var armorTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.LeatherArmor);
        engine.SpawnItemOnGround(armorTemplate, 0, sx, sy);
        ref var input3 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input3.ActionType = ActionTypes.PickUp;
        engine.Tick();
        ref var input4 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input4.ActionType = ActionTypes.UseItem;
        input4.ItemSlot = 0;
        engine.Tick();

        var hud = engine.GetPlayerStateData(player);
        Assert.NotNull(hud);
        Assert.NotNull(hud!.EquippedWeapon);
        Assert.Equal(ItemDefinitions.ShortSword, hud.EquippedWeapon!.Value.ItemTypeId);
        Assert.NotNull(hud.EquippedArmor);
        Assert.Equal(ItemDefinitions.LeatherArmor, hud.EquippedArmor!.Value.ItemTypeId);
    }

    [Fact]
    public void GetPlayerStateData_InventoryStackCountsAndRarities()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        var swordTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.ShortSword);
        engine.SpawnItemOnGround(swordTemplate, 1, sx, sy);
        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        var hud = engine.GetPlayerStateData(player);
        Assert.NotNull(hud);
        Assert.Single(hud!.InventoryItems);
        Assert.Equal(1, hud.InventoryItems[0].StackCount); // Sword is not stackable
        Assert.Equal(1, hud.InventoryItems[0].Rarity); // Rarity 1 (Uncommon)
    }

    [Fact]
    public void PlayerDeath_RespawnsWithHalfHealth()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        // Kill the player by reducing health to 0
        ref var health = ref engine.EcsWorld.Get<Health>(player);
        int maxHp = health.Max;
        health.Current = 0;
        engine.EcsWorld.Add(player, new DeadTag());

        engine.Tick();

        // Player should be alive with half health
        Assert.True(engine.EcsWorld.IsAlive(player));
        ref var healthAfter = ref engine.EcsWorld.Get<Health>(player);
        Assert.Equal(maxHp / 2, healthAfter.Current);
    }

    [Fact]
    public void PlayerDeath_LosesExperience()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        // Give the player some experience
        ref var classData = ref engine.EcsWorld.Get<ClassData>(player);
        classData.Experience = 100;

        // Kill the player
        ref var health = ref engine.EcsWorld.Get<Health>(player);
        health.Current = 0;
        engine.EcsWorld.Add(player, new DeadTag());

        engine.Tick();

        ref var classDataAfter = ref engine.EcsWorld.Get<ClassData>(player);
        Assert.Equal(75, classDataAfter.Experience); // Lost 25% (100 - 100/4)
    }

    [Fact]
    public void PlayerDeath_ZeroExperience_StaysZero()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        // Player starts with 0 experience
        ref var health = ref engine.EcsWorld.Get<Health>(player);
        health.Current = 0;
        engine.EcsWorld.Add(player, new DeadTag());

        engine.Tick();

        ref var classData = ref engine.EcsWorld.Get<ClassData>(player);
        Assert.Equal(0, classData.Experience);
    }

    [Fact]
    public void SpawnPlayer_HighSpeed_ZeroMoveDelay()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0);
        // Rogue has speed 4, so delay = max(0, 10 - (6+4)) = 0
        var player = engine.SpawnPlayer(1, 10, 10, ClassDefinitions.Rogue);
        ref var delay = ref engine.EcsWorld.Get<MoveDelay>(player);
        Assert.Equal(0, delay.Interval);
    }

    [Fact]
    public void FindDropPosition_OriginFree_ReturnsOrigin()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0);
        var (x, y) = GameEngine.FindDropPosition(engine.EcsWorld, 10, 10);
        Assert.Equal(10, x);
        Assert.Equal(10, y);
    }

    [Fact]
    public void FindDropPosition_OriginOccupied_FindsNearby()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();

        // Place an item at the origin
        var template = ItemDefinitions.Get(ItemDefinitions.HealthPotion);
        engine.SpawnItemOnGround(template, 0, sx, sy);

        var (x, y) = GameEngine.FindDropPosition(engine.EcsWorld, sx, sy);
        Assert.True(x != sx || y != sy, "Should find a different position when origin is occupied");
    }

    [Fact]
    public void SpawnElement_WithLight_CreatesLightSource()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0);

        var element = new DungeonElement
        {
            Position = new Position(10, 10),
            Appearance = new TileAppearance('*', 0xFFAA00),
            Light = new LightSource { Radius = 5 }
        };

        var entity = engine.SpawnElement(element);
        Assert.True(engine.EcsWorld.Has<LightSource>(entity));
        ref var light = ref engine.EcsWorld.Get<LightSource>(entity);
        Assert.Equal(5, light.Radius);
    }

    [Fact]
    public void SpawnElement_WithoutLight_NoLightSource()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0);

        var element = new DungeonElement
        {
            Position = new Position(10, 10),
            Appearance = new TileAppearance('#', 0x888888),
            Light = null
        };

        var entity = engine.SpawnElement(element);
        Assert.False(engine.EcsWorld.Has<LightSource>(entity));
    }


    [Fact]
    public void GetPlayerStateData_InventoryItemWithBonusHealth()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        // Add an item with BonusHealth to inventory directly
        ref var inv = ref engine.EcsWorld.Get<Inventory>(player);
        Assert.NotNull(inv.Items);
        inv.Items.Add(new ItemData
        {
            ItemTypeId = ItemDefinitions.HealthPotion,
            StackCount = 1,
            BonusHealth = 25,
            BonusAttack = 0,
            BonusDefense = 0,
        });

        var state = engine.GetPlayerStateData(player);
        Assert.NotNull(state);
        Assert.Single(state!.InventoryItems);
        Assert.Equal(25, state.InventoryItems[0].BonusHealth);
    }
}
