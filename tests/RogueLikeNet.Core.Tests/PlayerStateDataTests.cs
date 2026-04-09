using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Tests;

public class PlayerStateDataTests
{
    private static readonly BspDungeonGenerator _gen = new(42);

    private static int ItemId(string id) => GameData.Instance.Items.GetNumericId(id);
    private static Data.ItemDefinition Item(string id) => GameData.Instance.Items.Get(id)!;

    [Fact]
    public void GetPlayerStateData_ReturnsValidData()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        var hud = engine.GetPlayerStateData(player);
        Assert.NotNull(hud);
        Assert.True(hud!.MaxHealth > 0);
        Assert.Equal(hud.Health, hud.MaxHealth);
        Assert.True(hud.Attack > 0);
        Assert.True(hud.Defense > 0);
        Assert.Equal(4, hud.Skills.Length);
    }

    [Fact]
    public void GetPlayerStateData_SkillNames_Populated()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        var hud = engine.GetPlayerStateData(player);
        Assert.NotNull(hud);
        Assert.Equal(4, hud!.Skills.Length);
        Assert.Equal("Power Strike", hud.Skills[0].Name);
        Assert.Equal("Shield Bash", hud.Skills[1].Name);
    }

    [Fact]
    public void GetPlayerStateData_EquippedWeaponName_AfterEquip()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        var swordTemplate = Item("long_sword");
        engine.SpawnItemOnGround(swordTemplate, 0, Position.FromCoords(sx, sy, Position.DefaultZ));

        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        player.Input.ActionType = ActionTypes.UseItem;
        player.Input.ItemSlot = 0;
        engine.Tick();

        var hud = engine.GetPlayerStateData(player);
        Assert.NotNull(hud);
        Assert.NotEmpty(hud!.EquippedItems);
        Assert.Contains(hud.EquippedItems, e => e.ItemTypeId == ItemId("long_sword"));
    }

    [Fact]
    public void GetPlayerStateData_EquippedArmorName_AfterEquip()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        var armorTemplate = Item("chain_mail");
        engine.SpawnItemOnGround(armorTemplate, 0, Position.FromCoords(sx, sy, Position.DefaultZ));

        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        player.Input.ActionType = ActionTypes.UseItem;
        player.Input.ItemSlot = 0;
        engine.Tick();

        var hud = engine.GetPlayerStateData(player);
        Assert.NotNull(hud);
        Assert.NotEmpty(hud!.EquippedItems);
        Assert.Contains(hud.EquippedItems, e => e.ItemTypeId == ItemId("chain_mail"));
    }

    [Fact]
    public void GetPlayerStateData_InventoryStackCounts_Populated()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        var potionTemplate = Item("health_potion_small");
        engine.SpawnItemOnGround(potionTemplate, 0, Position.FromCoords(sx, sy, Position.DefaultZ));

        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        var hud = engine.GetPlayerStateData(player);
        Assert.NotNull(hud);
        Assert.Single(hud!.InventoryItems);
        Assert.True(hud.InventoryItems[0].StackCount >= 1);
    }

    [Fact]
    public void GetPlayerStateData_InventoryCategories_Populated()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        var swordTemplate = Item("short_sword");
        engine.SpawnItemOnGround(swordTemplate, 0, Position.FromCoords(sx, sy, Position.DefaultZ));

        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        var hud = engine.GetPlayerStateData(player);
        Assert.NotNull(hud);
        Assert.Single(hud!.InventoryItems);
        Assert.Equal((int)ItemCategory.Weapon, hud.InventoryItems[0].Category);
    }

    [Fact]
    public void GetPlayerStateData_NoEquipment_EmptyItems()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        var hud = engine.GetPlayerStateData(player);
        Assert.NotNull(hud);
        Assert.Empty(hud!.EquippedItems);
    }
}
