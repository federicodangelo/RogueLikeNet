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
    public void PlayerStateMsg_RoundTrip()
    {
        var msg = new PlayerStateMsg
        {
            Health = 80,
            MaxHealth = 100,
            Attack = 15,
            Defense = 10,
            Level = 5,
            Experience = 500,
            InventoryCount = 3,
            InventoryCapacity = 20,
            Skills = [
                new SkillSlotMsg { Id = 1, Cooldown = 0, Name = "" },
                new SkillSlotMsg { Id = 2, Cooldown = 5, Name = "" },
                new SkillSlotMsg { Id = 3, Cooldown = 10, Name = "" },
            ],
            InventoryItems = [
                new ItemDataMsg { ItemTypeId = 1, StackCount = 1, Category = 0 },
                new ItemDataMsg { ItemTypeId = 2, StackCount = 1, Category = 1 },
                new ItemDataMsg { ItemTypeId = 3, StackCount = 1, Category = 4 },
            ],
        };
        var data = NetSerializer.Serialize(msg);
        var result = NetSerializer.Deserialize<PlayerStateMsg>(data);
        Assert.Equal(80, result.Health);
        Assert.Equal(100, result.MaxHealth);
        Assert.Equal(15, result.Attack);
        Assert.Equal(10, result.Defense);
        Assert.Equal(5, result.Level);
        Assert.Equal(500, result.Experience);
        Assert.Equal(3, result.InventoryCount);
        Assert.Equal(20, result.InventoryCapacity);
        Assert.Equal(3, result.Skills.Length);
        Assert.Equal(1, result.Skills[0].Id);
        Assert.Equal(5, result.Skills[1].Cooldown);
        Assert.Equal(3, result.InventoryItems.Length);
        Assert.Equal(1, result.InventoryItems[0].ItemTypeId);
        Assert.Equal(1, result.InventoryItems[1].Category);
    }

    [Fact]
    public void PlayerStateMsg_DefaultValues()
    {
        var msg = new PlayerStateMsg();
        Assert.Equal(0, msg.Health);
        Assert.Equal(0, msg.MaxHealth);
        Assert.Equal(0, msg.Attack);
        Assert.Equal(0, msg.Defense);
        Assert.Equal(0, msg.Level);
        Assert.Equal(0, msg.Experience);
        Assert.Equal(0, msg.InventoryCount);
        Assert.Equal(0, msg.InventoryCapacity);
        Assert.Empty(msg.Skills);
        Assert.Empty(msg.InventoryItems);
    }

    [Fact]
    public void WorldDelta_WithTileUpdates_RoundTrip()
    {
        var delta = new WorldDeltaMsg
        {
            FromTick = 5,
            ToTick = 6,
            TileUpdates = [new TileUpdateMsg { X = 1, Y = 2, TileType = 1, GlyphId = 100, FgColor = 0xFF, BgColor = 0, LightLevel = 50 }],
            PlayerState = new PlayerStateMsg { Health = 50, MaxHealth = 100, Attack = 10, Defense = 5 }
        };
        var data = NetSerializer.Serialize(delta);
        var result = NetSerializer.Deserialize<WorldDeltaMsg>(data);
        Assert.Single(result.TileUpdates);
        Assert.Equal(1, result.TileUpdates[0].X);
        Assert.Equal(2, result.TileUpdates[0].Y);
        Assert.Equal(1, result.TileUpdates[0].TileType);
        Assert.NotNull(result.PlayerState);
        Assert.Equal(50, result.PlayerState.Health);
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
        Assert.Null(msg.PlayerState);
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
            Item = new ItemDataMsg { ItemTypeId = 5, Category = 1, StackCount = 1 },
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
        Assert.NotNull(result.Item);
        Assert.Equal(5, result.Item.ItemTypeId);
        Assert.Equal(1, result.Item.Category);
    }

    [Fact]
    public void EntityPositionMsg_RoundTrip()
    {
        var msg = new EntityPositionHealthMsg { Id = 7, X = 3, Y = 4, Health = 50 };
        var data = NetSerializer.Serialize(msg);
        var result = NetSerializer.Deserialize<EntityPositionHealthMsg>(data);
        Assert.Equal(7, result.Id);
        Assert.Equal(3, result.X);
        Assert.Equal(4, result.Y);
        Assert.Equal(50, result.Health);
    }

    [Fact]
    public void EntityRemovedMsg_RoundTrip()
    {
        var msg = new EntityRemovedMsg { Id = 99 };
        var data = NetSerializer.Serialize(msg);
        var result = NetSerializer.Deserialize<EntityRemovedMsg>(data);
        Assert.Equal(99, result.Id);
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

    [Fact]
    public void CombatEventMsg_Blocked_RoundTrip()
    {
        var msg = new CombatEventMsg
        {
            AttackerX = 5,
            AttackerY = 6,
            TargetX = 7,
            TargetY = 8,
            Damage = 0,
            TargetDied = false,
            Blocked = true
        };
        var data = NetSerializer.Serialize(msg);
        var result = NetSerializer.Deserialize<CombatEventMsg>(data);
        Assert.Equal(0, result.Damage);
        Assert.False(result.TargetDied);
        Assert.True(result.Blocked);
    }

    [Fact]
    public void PlayerStateMsg_NewFields_RoundTrip()
    {
        var msg = new PlayerStateMsg
        {
            Health = 80,
            MaxHealth = 100,
            Attack = 15,
            Defense = 10,
            Level = 5,
            Experience = 500,
            InventoryCount = 2,
            InventoryCapacity = 20,
            Skills = [
                new SkillSlotMsg { Id = 1, Cooldown = 0, Name = "Power Strike" },
                new SkillSlotMsg { Id = 2, Cooldown = 5, Name = "Shield Bash" },
                new SkillSlotMsg { Id = 3, Cooldown = 10, Name = "Backstab" },
                new SkillSlotMsg { Id = 4, Cooldown = 0, Name = "Dodge" },
            ],
            InventoryItems = [
                new ItemDataMsg { ItemTypeId = 10, StackCount = 1, Category = 0 },
                new ItemDataMsg { ItemTypeId = 11, StackCount = 5, Category = 4 },
            ],
            EquippedItems = [
                new ItemDataMsg { ItemTypeId = 10, Category = 0, EquipSlot = 5 },
                new ItemDataMsg { ItemTypeId = 12, Category = 1, EquipSlot = 1 },
            ],
        };
        var data = NetSerializer.Serialize(msg);
        var result = NetSerializer.Deserialize<PlayerStateMsg>(data);
        Assert.Equal(4, result.Skills.Length);
        Assert.Equal("Power Strike", result.Skills[0].Name);
        Assert.Equal("Dodge", result.Skills[3].Name);
        Assert.Equal(2, result.EquippedItems.Length);
        Assert.Equal(10, result.EquippedItems[0].ItemTypeId);
        Assert.Equal(12, result.EquippedItems[1].ItemTypeId);
        Assert.Equal(2, result.InventoryItems.Length);
        Assert.Equal(1, result.InventoryItems[0].StackCount);
        Assert.Equal(5, result.InventoryItems[1].StackCount);
    }

    [Fact]
    public void PlayerStateMsg_NewFields_DefaultValues()
    {
        var msg = new PlayerStateMsg();
        Assert.Empty(msg.Skills);
        Assert.Empty(msg.EquippedItems);
        Assert.Empty(msg.InventoryItems);
    }
}
