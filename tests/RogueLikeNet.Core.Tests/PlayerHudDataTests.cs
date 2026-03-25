using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Tests;

public class PlayerHudDataTests
{
    [Fact]
    public void GetPlayerHudData_ReturnsValidData()
    {
        using var engine = new GameEngine(42);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassIds.Warrior);

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
        using var engine = new GameEngine(42);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassIds.Warrior);

        var hud = engine.GetPlayerHudData(player);
        Assert.NotNull(hud);
        Assert.Empty(hud!.FloorItemNames);
    }

    [Fact]
    public void GetPlayerHudData_FloorItemNames_ReturnsItemsAtPlayerPosition()
    {
        using var engine = new GameEngine(42);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassIds.Warrior);

        // Place a Health Potion at player's position
        var template = ItemDefinitions.Templates[8]; // Health Potion
        engine.SpawnItemOnGround(template, 0, sx, sy);

        var hud = engine.GetPlayerHudData(player);
        Assert.NotNull(hud);
        Assert.Single(hud!.FloorItemNames);
        Assert.Equal("Health Potion", hud.FloorItemNames[0]);
    }

    [Fact]
    public void GetPlayerHudData_FloorItemNames_IgnoresItemsElsewhere()
    {
        using var engine = new GameEngine(42);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassIds.Warrior);

        // Place an item far from player
        var template = ItemDefinitions.Templates[0]; // Short Sword
        engine.SpawnItemOnGround(template, 0, sx + 5, sy + 5);

        var hud = engine.GetPlayerHudData(player);
        Assert.NotNull(hud);
        Assert.Empty(hud!.FloorItemNames);
    }
}
