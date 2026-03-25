using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Tests;

public class PlayerHudDataTests
{
    private static readonly BspDungeonGenerator _gen = new();

    [Fact]
    public void GetPlayerHudData_ReturnsValidData()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        var hud = engine.GetPlayerHudData(player);
        Assert.NotNull(hud);
        Assert.True(hud!.MaxHealth > 0);
        Assert.Equal(hud.Health, hud.MaxHealth);
        Assert.True(hud.Attack > 0);
        Assert.True(hud.Defense > 0);
        Assert.Equal(4, hud.SkillIds.Length);
    }

    [Fact]
    public void GetPlayerHudData_FloorItemNames_Empty_WhenNoItems()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        var hud = engine.GetPlayerHudData(player);
        Assert.NotNull(hud);
        Assert.Empty(hud!.FloorItemNames);
    }

    [Fact]
    public void GetPlayerHudData_FloorItemNames_ReturnsItemsAtPlayerPosition()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        // Place a Health Potion at player's position
        var template = ItemDefinitions.All[8]; // Health Potion
        engine.SpawnItemOnGround(template, 0, sx, sy);

        var hud = engine.GetPlayerHudData(player);
        Assert.NotNull(hud);
        Assert.Single(hud!.FloorItemNames);
        Assert.Equal("Health Potion", hud.FloorItemNames[0]);
    }

    [Fact]
    public void GetPlayerHudData_FloorItemNames_IgnoresItemsElsewhere()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        // Place an item far from player
        var template = ItemDefinitions.All[0]; // Short Sword
        engine.SpawnItemOnGround(template, 0, sx + 5, sy + 5);

        var hud = engine.GetPlayerHudData(player);
        Assert.NotNull(hud);
        Assert.Empty(hud!.FloorItemNames);
    }

    [Fact]
    public void GetPlayerHudData_SkillNames_Populated()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        var hud = engine.GetPlayerHudData(player);
        Assert.NotNull(hud);
        Assert.Equal(4, hud!.SkillNames.Length);
        Assert.Equal("Power Strike", hud.SkillNames[0]);
        Assert.Equal("Shield Bash", hud.SkillNames[1]);
    }

    [Fact]
    public void GetPlayerHudData_EquippedWeaponName_AfterEquip()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        var swordTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.LongSword);
        engine.SpawnItemOnGround(swordTemplate, 0, sx, sy);

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        ref var input2 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input2.ActionType = ActionTypes.UseItem;
        input2.ItemSlot = 0;
        engine.Tick();

        var hud = engine.GetPlayerHudData(player);
        Assert.NotNull(hud);
        Assert.Equal("Long Sword", hud!.EquippedWeaponName);
    }

    [Fact]
    public void GetPlayerHudData_EquippedArmorName_AfterEquip()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        var armorTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.ChainMail);
        engine.SpawnItemOnGround(armorTemplate, 0, sx, sy);

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        ref var input2 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input2.ActionType = ActionTypes.UseItem;
        input2.ItemSlot = 0;
        engine.Tick();

        var hud = engine.GetPlayerHudData(player);
        Assert.NotNull(hud);
        Assert.Equal("Chain Mail", hud!.EquippedArmorName);
    }

    [Fact]
    public void GetPlayerHudData_InventoryStackCounts_Populated()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        var potionTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.HealthPotion);
        engine.SpawnItemOnGround(potionTemplate, 0, sx, sy);

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        var hud = engine.GetPlayerHudData(player);
        Assert.NotNull(hud);
        Assert.Single(hud!.InventoryStackCounts);
        Assert.True(hud.InventoryStackCounts[0] >= 1);
    }

    [Fact]
    public void GetPlayerHudData_InventoryRarities_Populated()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        var swordTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.ShortSword);
        engine.SpawnItemOnGround(swordTemplate, 2, sx, sy);

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        var hud = engine.GetPlayerHudData(player);
        Assert.NotNull(hud);
        Assert.Single(hud!.InventoryRarities);
        Assert.Equal(2, hud.InventoryRarities[0]);
    }

    [Fact]
    public void GetPlayerHudData_NoEquipment_EmptyNames()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        var hud = engine.GetPlayerHudData(player);
        Assert.NotNull(hud);
        Assert.Equal("", hud!.EquippedWeaponName);
        Assert.Equal("", hud.EquippedArmorName);
    }
}
