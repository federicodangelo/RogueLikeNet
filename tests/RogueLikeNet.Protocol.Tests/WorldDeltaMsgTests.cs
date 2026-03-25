using RogueLikeNet.Protocol;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Protocol.Tests;

public class WorldDeltaMsgTests
{
    [Fact]
    public void TileUpdateMsg_RoundTrip()
    {
        var msg = new TileUpdateMsg
        {
            X = 10,
            Y = 20,
            TileType = 3,
            GlyphId = 219,
            FgColor = 0xFFFFFF,
            BgColor = 0x000000,
            LightLevel = 75
        };
        var data = NetSerializer.Serialize(msg);
        var result = NetSerializer.Deserialize<TileUpdateMsg>(data);
        Assert.Equal(10, result.X);
        Assert.Equal(20, result.Y);
        Assert.Equal(3, result.TileType);
        Assert.Equal(219, result.GlyphId);
        Assert.Equal(0xFFFFFF, result.FgColor);
        Assert.Equal(0x000000, result.BgColor);
        Assert.Equal(75, result.LightLevel);
    }

    [Fact]
    public void TileUpdateMsg_DefaultValues()
    {
        var msg = new TileUpdateMsg();
        Assert.Equal(0, msg.X);
        Assert.Equal(0, msg.Y);
        Assert.Equal(0, msg.TileType);
        Assert.Equal(0, msg.GlyphId);
        Assert.Equal(0, msg.FgColor);
        Assert.Equal(0, msg.BgColor);
        Assert.Equal(0, msg.LightLevel);
    }

    [Fact]
    public void PlayerHudMsg_RoundTrip()
    {
        var msg = new PlayerHudMsg
        {
            Health = 80,
            MaxHealth = 100,
            Attack = 15,
            Defense = 10,
            Level = 5,
            Experience = 500,
            InventoryCount = 3,
            InventoryCapacity = 20,
            SkillIds = [1, 2, 3],
            SkillCooldowns = [0, 5, 10],
            InventoryNames = ["Sword", "Shield", "Potion"]
        };
        var data = NetSerializer.Serialize(msg);
        var result = NetSerializer.Deserialize<PlayerHudMsg>(data);
        Assert.Equal(80, result.Health);
        Assert.Equal(100, result.MaxHealth);
        Assert.Equal(15, result.Attack);
        Assert.Equal(10, result.Defense);
        Assert.Equal(5, result.Level);
        Assert.Equal(500, result.Experience);
        Assert.Equal(3, result.InventoryCount);
        Assert.Equal(20, result.InventoryCapacity);
        Assert.Equal([1, 2, 3], result.SkillIds);
        Assert.Equal([0, 5, 10], result.SkillCooldowns);
        Assert.Equal(["Sword", "Shield", "Potion"], result.InventoryNames);
    }

    [Fact]
    public void PlayerHudMsg_DefaultValues()
    {
        var msg = new PlayerHudMsg();
        Assert.Equal(0, msg.Health);
        Assert.Equal(0, msg.MaxHealth);
        Assert.Equal(0, msg.Attack);
        Assert.Equal(0, msg.Defense);
        Assert.Equal(0, msg.Level);
        Assert.Equal(0, msg.Experience);
        Assert.Equal(0, msg.InventoryCount);
        Assert.Equal(0, msg.InventoryCapacity);
        Assert.Empty(msg.SkillIds);
        Assert.Empty(msg.SkillCooldowns);
        Assert.Empty(msg.InventoryNames);
    }

    [Fact]
    public void WorldDelta_WithTileUpdates_RoundTrip()
    {
        var delta = new WorldDeltaMsg
        {
            FromTick = 5,
            ToTick = 6,
            TileUpdates = [new TileUpdateMsg { X = 1, Y = 2, TileType = 1, GlyphId = 100, FgColor = 0xFF, BgColor = 0, LightLevel = 50 }],
            PlayerHud = new PlayerHudMsg { Health = 50, MaxHealth = 100, Attack = 10, Defense = 5 }
        };
        var data = NetSerializer.Serialize(delta);
        var result = NetSerializer.Deserialize<WorldDeltaMsg>(data);
        Assert.Single(result.TileUpdates);
        Assert.Equal(1, result.TileUpdates[0].X);
        Assert.Equal(2, result.TileUpdates[0].Y);
        Assert.Equal(1, result.TileUpdates[0].TileType);
        Assert.NotNull(result.PlayerHud);
        Assert.Equal(50, result.PlayerHud.Health);
    }

    [Fact]
    public void WorldDelta_DefaultValues()
    {
        var msg = new WorldDeltaMsg();
        Assert.Equal(0, msg.FromTick);
        Assert.Equal(0, msg.ToTick);
        Assert.Empty(msg.TileUpdates);
        Assert.Empty(msg.EntityUpdates);
        Assert.Empty(msg.CombatEvents);
        Assert.Empty(msg.Chunks);
        Assert.Null(msg.PlayerHud);
    }

    [Fact]
    public void EntityUpdateMsg_RoundTrip()
    {
        var msg = new EntityUpdateMsg
        {
            Id = 42,
            X = 10,
            Y = 20,
            GlyphId = 64,
            FgColor = 0xFF0000,
            Health = 80,
            MaxHealth = 100,
            Removed = true
        };
        var data = NetSerializer.Serialize(msg);
        var result = NetSerializer.Deserialize<EntityUpdateMsg>(data);
        Assert.Equal(42, result.Id);
        Assert.Equal(10, result.X);
        Assert.Equal(20, result.Y);
        Assert.Equal(64, result.GlyphId);
        Assert.Equal(0xFF0000, result.FgColor);
        Assert.Equal(80, result.Health);
        Assert.Equal(100, result.MaxHealth);
        Assert.True(result.Removed);
    }

    [Fact]
    public void CombatEventMsg_RoundTrip()
    {
        var msg = new CombatEventMsg
        {
            AttackerX = 1,
            AttackerY = 2,
            TargetX = 3,
            TargetY = 4,
            Damage = 25,
            TargetDied = true
        };
        var data = NetSerializer.Serialize(msg);
        var result = NetSerializer.Deserialize<CombatEventMsg>(data);
        Assert.Equal(1, result.AttackerX);
        Assert.Equal(2, result.AttackerY);
        Assert.Equal(3, result.TargetX);
        Assert.Equal(4, result.TargetY);
        Assert.Equal(25, result.Damage);
        Assert.True(result.TargetDied);
    }
}
