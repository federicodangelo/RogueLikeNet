using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Tests;

public class PlayerStateDataTests
{
    private static readonly BspDungeonGenerator _gen = new(42);

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

        var swordTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.LongSword);
        engine.SpawnItemOnGround(swordTemplate, 0, Position.FromCoords(sx, sy, Position.DefaultZ));

        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        player.Input.ActionType = ActionTypes.UseItem;
        player.Input.ItemSlot = 0;
        engine.Tick();

        var hud = engine.GetPlayerStateData(player);
        Assert.NotNull(hud);
        Assert.NotNull(hud!.EquippedWeapon);
        Assert.Equal(ItemDefinitions.LongSword, hud.EquippedWeapon!.Value.ItemTypeId);
    }

    [Fact]
    public void GetPlayerStateData_EquippedArmorName_AfterEquip()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        var armorTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.ChainMail);
        engine.SpawnItemOnGround(armorTemplate, 0, Position.FromCoords(sx, sy, Position.DefaultZ));

        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        player.Input.ActionType = ActionTypes.UseItem;
        player.Input.ItemSlot = 0;
        engine.Tick();

        var hud = engine.GetPlayerStateData(player);
        Assert.NotNull(hud);
        Assert.NotNull(hud!.EquippedArmor);
        Assert.Equal(ItemDefinitions.ChainMail, hud.EquippedArmor!.Value.ItemTypeId);
    }

    [Fact]
    public void GetPlayerStateData_InventoryStackCounts_Populated()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        var potionTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.HealthPotion);
        engine.SpawnItemOnGround(potionTemplate, 0, Position.FromCoords(sx, sy, Position.DefaultZ));

        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        var hud = engine.GetPlayerStateData(player);
        Assert.NotNull(hud);
        Assert.Single(hud!.InventoryItems);
        Assert.True(hud.InventoryItems[0].StackCount >= 1);
    }

    [Fact]
    public void GetPlayerStateData_InventoryRarities_Populated()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        var swordTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.ShortSword);
        engine.SpawnItemOnGround(swordTemplate, 2, Position.FromCoords(sx, sy, Position.DefaultZ));

        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        var hud = engine.GetPlayerStateData(player);
        Assert.NotNull(hud);
        Assert.Single(hud!.InventoryItems);
        Assert.Equal(2, hud.InventoryItems[0].Rarity);
    }

    [Fact]
    public void GetPlayerStateData_NoEquipment_EmptyNames()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        var hud = engine.GetPlayerStateData(player);
        Assert.NotNull(hud);
        Assert.Null(hud!.EquippedWeapon);
        Assert.Null(hud.EquippedArmor);
    }
}
