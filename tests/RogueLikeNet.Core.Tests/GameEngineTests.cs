using Arch.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Core.Tests;

public class GameEngineTests
{
    [Fact]
    public void SpawnPlayer_CreatesEntity()
    {
        using var engine = new GameEngine(42);
        engine.EnsureChunkLoaded(0, 0);
        var entity = engine.SpawnPlayer(1, 10, 10);
        Assert.True(engine.EcsWorld.IsAlive(entity));
        ref var pos = ref engine.EcsWorld.Get<Position>(entity);
        Assert.Equal(10, pos.X);
        Assert.Equal(10, pos.Y);
    }

    [Fact]
    public void Tick_IncrementsTickCounter()
    {
        using var engine = new GameEngine(42);
        engine.EnsureChunkLoaded(0, 0);
        Assert.Equal(0, engine.CurrentTick);
        engine.Tick();
        Assert.Equal(1, engine.CurrentTick);
    }

    [Fact]
    public void FindSpawnPosition_ReturnsFloorTile()
    {
        using var engine = new GameEngine(42);
        var (x, y) = engine.FindSpawnPosition();
        var chunk = engine.EnsureChunkLoaded(0, 0);
        Assert.Equal(TileType.Floor, chunk.Tiles[x, y].Type);
    }

    [Fact]
    public void Tick_ComputesLightingAroundPlayer()
    {
        using var engine = new GameEngine(42);
        engine.EnsureChunkLoaded(0, 0);

        var (sx, sy) = engine.FindSpawnPosition();
        engine.SpawnPlayer(1, sx, sy);
        engine.Tick();

        var chunk = engine.WorldMap.TryGetChunk(0, 0)!;
        Assert.True(chunk.Tiles[sx, sy].LightLevel > 0,
            $"Player tile ({sx},{sy}) has LightLevel={chunk.Tiles[sx, sy].LightLevel}, expected > 0");

        int litCount = 0;
        for (int dx = -3; dx <= 3; dx++)
        for (int dy = -3; dy <= 3; dy++)
        {
            int nx = sx + dx, ny = sy + dy;
            if (nx >= 0 && nx < Chunk.Size && ny >= 0 && ny < Chunk.Size)
            {
                if (chunk.Tiles[nx, ny].LightLevel > 0)
                    litCount++;
            }
        }
        Assert.True(litCount > 5, $"Only {litCount} tiles lit in 7x7 area around player, expected > 5");
    }

    [Fact]
    public void CombatProperty_ReturnsSystem()
    {
        using var engine = new GameEngine(42);
        Assert.NotNull(engine.Combat);
    }

    [Fact]
    public void InventoryProperty_ReturnsSystem()
    {
        using var engine = new GameEngine(42);
        Assert.NotNull(engine.Inventory);
    }

    [Fact]
    public void GetPlayerHudData_WithInventoryItems_ReturnsNames()
    {
        using var engine = new GameEngine(42);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        // Pick up an item
        var swordTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.ShortSword);
        engine.SpawnItemOnGround(swordTemplate, 0, sx, sy);

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        var hud = engine.GetPlayerHudData(player);
        Assert.NotNull(hud);
        Assert.True(hud!.InventoryCount > 0);
        Assert.NotEmpty(hud.InventoryNames);
        Assert.Equal("Short Sword", hud.InventoryNames[0]);
    }

    [Fact]
    public void GetPlayerHudData_DeadEntity_ReturnsNull()
    {
        using var engine = new GameEngine(42);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var monster = engine.SpawnMonster(0, sx + 1, sy, 103, 0x00FF00, 1, 5, 0, 8);

        // Kill monster
        ref var health = ref engine.EcsWorld.Get<Health>(monster);
        health.Current = 0;
        engine.Tick(); // This marks dead and destroys monster

        var hud = engine.GetPlayerHudData(monster);
        Assert.Null(hud);
    }

    [Fact]
    public void GetPlayerHudData_ReturnsSkillData()
    {
        using var engine = new GameEngine(42);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        var hud = engine.GetPlayerHudData(player);
        Assert.NotNull(hud);
        Assert.Equal(4, hud!.SkillIds.Length);
        Assert.Equal(4, hud.SkillCooldowns.Length);
        Assert.Equal(SkillDefinitions.PowerStrike, hud.SkillIds[0]);
        Assert.Equal(SkillDefinitions.ShieldBash, hud.SkillIds[1]);
    }

    [Fact]
    public void GetPlayerHudData_ReturnsClassInfo()
    {
        using var engine = new GameEngine(42);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Mage);

        var hud = engine.GetPlayerHudData(player);
        Assert.NotNull(hud);
        Assert.Equal(1, hud!.Level);
        Assert.Equal(0, hud.Experience);
    }

    [Fact]
    public void EnsureChunkLoaded_SpawnsEntities()
    {
        using var engine = new GameEngine(42);
        // Loading a chunk should spawn monsters and items from generation results
        engine.EnsureChunkLoaded(0, 0);

        int entityCount = 0;
        var query = new QueryDescription();
        engine.EcsWorld.Query(in query, (Entity _) => entityCount++);

        Assert.True(entityCount > 0, "Chunk loading should spawn entities");
    }

    [Fact]
    public void SpawnTorch_CreatesLightSource()
    {
        using var engine = new GameEngine(42);
        engine.EnsureChunkLoaded(0, 0);
        var torch = engine.SpawnTorch(10, 10);

        Assert.True(engine.EcsWorld.IsAlive(torch));
        Assert.True(engine.EcsWorld.Has<LightSource>(torch));
        Assert.True(engine.EcsWorld.Has<Position>(torch));
    }

    [Fact]
    public void SpawnItemOnGround_CreatesItemWithRarity()
    {
        using var engine = new GameEngine(42);
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
        using var engine = new GameEngine(42);
        // Load a chunk far from origin for higher difficulty
        engine.EnsureChunkLoaded(5, 5);
        // Should not crash and should generate entities
        var chunk = engine.WorldMap.TryGetChunk(5, 5);
        Assert.NotNull(chunk);
    }

    [Fact]
    public void FindSpawnPosition_FallbackWhenNoFloor()
    {
        using var engine = new GameEngine(42);
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
    public void GetPlayerHudData_FloorItems_ReturnsNames()
    {
        using var engine = new GameEngine(42);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        // Spawn a floor item
        var swordTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.ShortSword);
        engine.SpawnItemOnGround(swordTemplate, 0, sx, sy);

        var hud = engine.GetPlayerHudData(player);
        Assert.NotNull(hud);
        Assert.NotEmpty(hud!.FloorItemNames);
        Assert.Equal("Short Sword", hud.FloorItemNames[0]);
    }

    [Fact]
    public void GetPlayerHudData_SkillNames_MatchSkillDefinitions()
    {
        using var engine = new GameEngine(42);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Mage);

        var hud = engine.GetPlayerHudData(player);
        Assert.NotNull(hud);
        Assert.Equal(4, hud!.SkillNames.Length);
        // Mage skills: Fireball, Heal, and two more
        Assert.Equal(SkillDefinitions.GetName(hud.SkillIds[0]), hud.SkillNames[0]);
        Assert.Equal(SkillDefinitions.GetName(hud.SkillIds[1]), hud.SkillNames[1]);
    }

    [Fact]
    public void GetPlayerHudData_EquippedNames_AfterEquip()
    {
        using var engine = new GameEngine(42);
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

        var hud = engine.GetPlayerHudData(player);
        Assert.NotNull(hud);
        Assert.Equal("Short Sword", hud!.EquippedWeaponName);
        Assert.Equal("Leather Armor", hud.EquippedArmorName);
    }

    [Fact]
    public void GetPlayerHudData_InventoryStackCountsAndRarities()
    {
        using var engine = new GameEngine(42);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        var swordTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.ShortSword);
        engine.SpawnItemOnGround(swordTemplate, 1, sx, sy);
        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        var hud = engine.GetPlayerHudData(player);
        Assert.NotNull(hud);
        Assert.Single(hud!.InventoryStackCounts);
        Assert.Equal(1, hud.InventoryStackCounts[0]); // Sword is not stackable
        Assert.Single(hud.InventoryRarities);
        Assert.Equal(1, hud.InventoryRarities[0]); // Rarity 1 (Uncommon)
    }
}
