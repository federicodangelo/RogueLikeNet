using RogueLikeNet.Protocol;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Protocol.Tests;

public class NetSerializerTests
{
    [Fact]
    public void Serialize_Deserialize_RoundTrip()
    {
        var msg = new ClientInputMsg
        {
            Tick = 42,
            ActionType = 1,
            TargetX = 3,
            TargetY = -5,
            ItemSlot = 2
        };

        var data = NetSerializer.Serialize(msg);
        var result = NetSerializer.Deserialize<ClientInputMsg>(data);

        Assert.Equal(42, result.Tick);
        Assert.Equal(1, result.ActionType);
        Assert.Equal(3, result.TargetX);
        Assert.Equal(-5, result.TargetY);
        Assert.Equal(2, result.ItemSlot);
    }

    [Fact]
    public void WrapUnwrap_PreservesMessageType()
    {
        var msg = new ClientInputMsg { Tick = 1, ActionType = 2 };
        var payload = NetSerializer.Serialize(msg);
        var wrapped = NetSerializer.WrapMessage(MessageTypes.ClientInput, payload);

        var envelope = NetSerializer.UnwrapMessage(wrapped);
        Assert.Equal(MessageTypes.ClientInput, envelope.MessageType);

        var unwrapped = NetSerializer.Deserialize<ClientInputMsg>(envelope.Payload);
        Assert.Equal(1, unwrapped.Tick);
        Assert.Equal(2, unwrapped.ActionType);
    }

    [Fact]
    public void WorldSnapshot_RoundTrip()
    {
        var snapshot = new WorldSnapshotMsg
        {
            WorldTick = 100,
            PlayerX = 32,
            PlayerY = 16,
            PlayerEntityId = 5,
            Chunks = [new ChunkDataMsg
            {
                ChunkX = 0, ChunkY = 0,
                TileTypes = [1, 2, 3],
                TileGlyphs = [250, 219, 43],
                TileFgColors = [0xFFFFFF, 0x808080, 0],
                TileBgColors = [0, 0, 0],
            }],
            Entities = [new EntityMsg { Id = 1, X = 10, Y = 10, GlyphId = 64, FgColor = 0xFFFFFF, Health = 100, MaxHealth = 100 }],
        };

        var data = NetSerializer.Serialize(snapshot);
        var result = NetSerializer.Deserialize<WorldSnapshotMsg>(data);

        Assert.Equal(100, result.WorldTick);
        Assert.Equal(32, result.PlayerX);
        Assert.Single(result.Chunks);
        Assert.Equal(3, result.Chunks[0].TileTypes.Length);
        Assert.Single(result.Entities);
        Assert.Equal(64, result.Entities[0].GlyphId);
    }

    [Fact]
    public void WorldDelta_RoundTrip()
    {
        var delta = new WorldDeltaMsg
        {
            FromTick = 10,
            ToTick = 11,
            EntityUpdates = [new EntityUpdateMsg { Id = 1, X = 5, Y = 6, Health = 80, MaxHealth = 100 }],
            CombatEvents = [new CombatEventMsg { AttackerX = 5, AttackerY = 5, TargetX = 5, TargetY = 6, Damage = 20, TargetDied = false }],
        };

        var data = NetSerializer.Serialize(delta);
        var result = NetSerializer.Deserialize<WorldDeltaMsg>(data);

        Assert.Equal(10, result.FromTick);
        Assert.Equal(11, result.ToTick);
        Assert.Single(result.EntityUpdates);
        Assert.Equal(80, result.EntityUpdates[0].Health);
        Assert.Single(result.CombatEvents);
        Assert.Equal(20, result.CombatEvents[0].Damage);
    }

    [Fact]
    public void NetworkEnvelope_DifferentMessageTypes()
    {
        var input = new ClientInputMsg { ActionType = 5 };
        var inputPayload = NetSerializer.Serialize(input);
        var inputWrapped = NetSerializer.WrapMessage(MessageTypes.ClientInput, inputPayload);

        var snapshot = new WorldSnapshotMsg { WorldTick = 99 };
        var snapshotPayload = NetSerializer.Serialize(snapshot);
        var snapshotWrapped = NetSerializer.WrapMessage(MessageTypes.WorldSnapshot, snapshotPayload);

        var env1 = NetSerializer.UnwrapMessage(inputWrapped);
        var env2 = NetSerializer.UnwrapMessage(snapshotWrapped);

        Assert.Equal(MessageTypes.ClientInput, env1.MessageType);
        Assert.Equal(MessageTypes.WorldSnapshot, env2.MessageType);
    }

    [Fact]
    public void SmallPayload_NotCompressed()
    {
        // A small payload (<= 4096 bytes) must NOT be compressed
        var msg = new ClientInputMsg { Tick = 1, ActionType = 2 };
        var payload = NetSerializer.Serialize(msg);
        var wrapped = NetSerializer.WrapMessage(MessageTypes.ClientInput, payload);

        var envelope = NetSerializer.UnwrapMessage(wrapped);
        // Payload should be identical to original after round-trip
        Assert.Equal(MessageTypes.ClientInput, envelope.MessageType);
        Assert.Equal(0, envelope.IsCompressed);
        var result = NetSerializer.Deserialize<ClientInputMsg>(envelope.Payload);
        Assert.Equal(1, result.Tick);
        Assert.Equal(2, result.ActionType);
    }

    [Fact]
    public void LargePayload_CompressedAndDecompressedTransparently()
    {
        // Build a payload larger than 4096 bytes by creating a snapshot with many chunks
        var chunks = new ChunkDataMsg[50];
        for (int i = 0; i < chunks.Length; i++)
        {
            chunks[i] = new ChunkDataMsg
            {
                ChunkX = i,
                ChunkY = i,
                TileTypes = new byte[256],
                TileGlyphs = new int[256],
                TileFgColors = new int[256],
                TileBgColors = new int[256],
            };
        }
        var snapshot = new WorldSnapshotMsg { WorldTick = 42, PlayerX = 10, PlayerY = 10, Chunks = chunks, Entities = [] };
        var payload = NetSerializer.Serialize(snapshot);
        Assert.True(payload.Length > 4096, "Test payload must exceed compression threshold");

        var wrapped = NetSerializer.WrapMessage(MessageTypes.WorldSnapshot, payload);

        // Unwrap is transparent — caller receives decompressed payload
        var envelope = NetSerializer.UnwrapMessage(wrapped);
        Assert.Equal(MessageTypes.WorldSnapshot, envelope.MessageType);
        Assert.Equal(0, envelope.IsCompressed); // already decompressed by UnwrapMessage

        var result = NetSerializer.Deserialize<WorldSnapshotMsg>(envelope.Payload);
        Assert.Equal(42, result.WorldTick);
        Assert.Equal(50, result.Chunks.Length);
    }

    [Fact]
    public void LargePayload_WireFormatIsActuallySmaller()
    {
        // Verify the wire bytes are compressed (smaller than uncompressed envelope)
        var chunks = new ChunkDataMsg[50];
        for (int i = 0; i < chunks.Length; i++)
        {
            chunks[i] = new ChunkDataMsg
            {
                ChunkX = i,
                ChunkY = i,
                TileTypes = new byte[256],
                TileGlyphs = new int[256],
                TileFgColors = new int[256],
                TileBgColors = new int[256],
            };
        }
        var snapshot = new WorldSnapshotMsg { WorldTick = 1, Chunks = chunks, Entities = [] };
        var payload = NetSerializer.Serialize(snapshot);

        var compressedWire = NetSerializer.WrapMessage(MessageTypes.WorldSnapshot, payload);
        Assert.True(compressedWire.Length < payload.Length, "Compressed wire frame should be smaller than raw payload");
    }
}
