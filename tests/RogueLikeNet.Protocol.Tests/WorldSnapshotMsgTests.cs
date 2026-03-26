using RogueLikeNet.Protocol;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Protocol.Tests;

public class WorldSnapshotMsgTests
{
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
    }

    [Fact]
    public void WorldDeltaMsg_IsSnapshot_RoundTrip()
    {
        var msg = new WorldDeltaMsg
        {
            FromTick = 0,
            ToTick = 50,
            IsSnapshot = true,
            Chunks = [],
            EntityUpdates = [new EntityUpdateMsg { Id = 1, X = 10, Y = 20, GlyphId = 64, FgColor = 0xFFFFFF, Health = 100, MaxHealth = 100 }],
            PlayerState = new PlayerStateMsg { Health = 90, MaxHealth = 100, Level = 2, PlayerEntityId = 3 },
        };
        var data = NetSerializer.Serialize(msg);
        var result = NetSerializer.Deserialize<WorldDeltaMsg>(data);
        Assert.True(result.IsSnapshot);
        Assert.Equal(0, result.FromTick);
        Assert.Equal(50, result.ToTick);
        Assert.NotNull(result.PlayerState);
        Assert.Equal(90, result.PlayerState.Health);
        Assert.Equal(2, result.PlayerState.Level);
        Assert.Single(result.EntityUpdates);
        Assert.Equal(64, result.EntityUpdates[0].GlyphId);
    }
}
