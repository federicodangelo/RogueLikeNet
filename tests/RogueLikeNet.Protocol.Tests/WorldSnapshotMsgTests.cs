using RogueLikeNet.Protocol;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Protocol.Tests;

public class WorldSnapshotMsgTests
{
    [Fact]
    public void WorldSnapshotMsg_DefaultValues()
    {
        var msg = new WorldSnapshotMsg();
        Assert.Equal(0, msg.WorldTick);
        Assert.Empty(msg.Chunks);
        Assert.Empty(msg.Entities);
        Assert.Equal(0, msg.PlayerEntityId);
        Assert.Equal(0, msg.PlayerX);
        Assert.Equal(0, msg.PlayerY);
        Assert.Null(msg.PlayerHud);
    }

    [Fact]
    public void WorldSnapshotMsg_WithPlayerHud_RoundTrip()
    {
        var snapshot = new WorldSnapshotMsg
        {
            WorldTick = 50,
            PlayerX = 10,
            PlayerY = 20,
            PlayerEntityId = 3,
            PlayerHud = new PlayerHudMsg { Health = 90, MaxHealth = 100, Level = 2 },
            Chunks = [],
            Entities = [],
        };
        var data = NetSerializer.Serialize(snapshot);
        var result = NetSerializer.Deserialize<WorldSnapshotMsg>(data);
        Assert.NotNull(result.PlayerHud);
        Assert.Equal(90, result.PlayerHud.Health);
        Assert.Equal(2, result.PlayerHud.Level);
    }

    [Fact]
    public void ChunkDataMsg_DefaultValues()
    {
        var msg = new ChunkDataMsg();
        Assert.Equal(0, msg.ChunkX);
        Assert.Equal(0, msg.ChunkY);
        Assert.Empty(msg.TileTypes);
        Assert.Empty(msg.TileGlyphs);
        Assert.Empty(msg.TileFgColors);
        Assert.Empty(msg.TileBgColors);
        Assert.Empty(msg.TileLightLevels);
    }

    [Fact]
    public void EntityMsg_DefaultValues()
    {
        var msg = new EntityMsg();
        Assert.Equal(0, msg.Id);
        Assert.Equal(0, msg.X);
        Assert.Equal(0, msg.Y);
        Assert.Equal(0, msg.GlyphId);
        Assert.Equal(0, msg.FgColor);
        Assert.Equal(0, msg.Health);
        Assert.Equal(0, msg.MaxHealth);
    }

    [Fact]
    public void EntityMsg_RoundTrip()
    {
        var msg = new EntityMsg
        {
            Id = 99,
            X = 15,
            Y = 25,
            GlyphId = 77,
            FgColor = 0x00FF00,
            Health = 50,
            MaxHealth = 75
        };
        var data = NetSerializer.Serialize(msg);
        var result = NetSerializer.Deserialize<EntityMsg>(data);
        Assert.Equal(99, result.Id);
        Assert.Equal(15, result.X);
        Assert.Equal(25, result.Y);
        Assert.Equal(77, result.GlyphId);
        Assert.Equal(0x00FF00, result.FgColor);
        Assert.Equal(50, result.Health);
        Assert.Equal(75, result.MaxHealth);
    }
}
