using Arch.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Protocol;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Protocol.Tests;

public class GameStateSerializerTests
{
    [Fact]
    public void EntityUpdateMsg_SameValues_DetectsEquality()
    {
        var a = new EntityUpdateMsg { Id = 1, X = 2, Y = 3, GlyphId = 64, FgColor = 0xFFFFFF, Health = 10, MaxHealth = 10 };
        var b = new EntityUpdateMsg { Id = 1, X = 2, Y = 3, GlyphId = 64, FgColor = 0xFFFFFF, Health = 10, MaxHealth = 10 };
        var c = new EntityUpdateMsg { Id = 1, X = 2, Y = 4, GlyphId = 64, FgColor = 0xFFFFFF, Health = 10, MaxHealth = 10 };

        Assert.True(a.SameValues(b));
        Assert.False(a.SameValues(c));
    }

    [Fact]
    public void EntityUpdateMsg_HasOnlyPositionChanges_DetectsDiff()
    {
        var prev = new EntityUpdateMsg { Id = 1, X = 2, Y = 3, GlyphId = 64, FgColor = 0xFFFFFF, Health = 10, MaxHealth = 10 };
        var posOnly = new EntityUpdateMsg { Id = 1, X = 5, Y = 6, GlyphId = 64, FgColor = 0xFFFFFF, Health = 8, MaxHealth = 10 };
        var full = new EntityUpdateMsg { Id = 1, X = 5, Y = 6, GlyphId = 65, FgColor = 0xFFFFFF, Health = 8, MaxHealth = 10 };

        Assert.True(posOnly.HasOnlyPositionHealthChanges(prev));
        Assert.False(full.HasOnlyPositionHealthChanges(prev));
    }

    [Fact]
    public void SerializeEntityDelta_OnlyReturnsChangedEntities()
    {
        var world = World.Create();
        var entity = world.Create(new Position(5, 5), new TileAppearance(64, 0xFFFFFF), new Health(10));

        var fov = new FOVData(10);
        fov.VisibleTiles!.Add(Position.PackCoord(5, 5));

        var previousState = new Dictionary<long, EntityUpdateMsg>();

        // First call — entity is new, should be included as full update
        var (full1, pos1, rem1) = GameStateSerializer.SerializeEntityDelta(world, fov, previousState);
        Assert.Single(full1);
        Assert.Equal(entity.Id, full1[0].Id);
        Assert.Empty(pos1);
        Assert.Empty(rem1);

        // Second call — nothing changed, should NOT be included
        var (full2, pos2, rem2) = GameStateSerializer.SerializeEntityDelta(world, fov, previousState);
        Assert.Empty(full2);
        Assert.Empty(pos2);
        Assert.Empty(rem2);

        World.Destroy(world);
    }

    [Fact]
    public void SerializeEntityDelta_PositionOnlyUpdate()
    {
        var world = World.Create();
        var entity = world.Create(new Position(5, 5), new TileAppearance(64, 0xFFFFFF));

        var fov = new FOVData(10);
        fov.VisibleTiles!.Add(Position.PackCoord(5, 5));
        fov.VisibleTiles!.Add(Position.PackCoord(6, 5));

        var previousState = new Dictionary<long, EntityUpdateMsg>();

        // Seed previous state
        GameStateSerializer.SerializeEntityDelta(world, fov, previousState);

        // Move entity — only position changed
        world.Set(entity, new Position(6, 5));

        var (full, pos, rem) = GameStateSerializer.SerializeEntityDelta(world, fov, previousState);
        Assert.Empty(full);
        Assert.Single(pos);
        Assert.Equal(6, pos[0].X);
        Assert.Empty(rem);

        World.Destroy(world);
    }

    [Fact]
    public void SerializeEntityDelta_RemovedEntitiesLeavingFOV()
    {
        var world = World.Create();
        var entity = world.Create(new Position(5, 5), new TileAppearance(64, 0xFFFFFF));

        var fovAll = new FOVData(10);
        fovAll.VisibleTiles!.Add(Position.PackCoord(5, 5));

        var previousState = new Dictionary<long, EntityUpdateMsg>();

        // Seed previous state — entity visible
        GameStateSerializer.SerializeEntityDelta(world, fov: fovAll, previousState);

        // Now entity moves out of FOV (or FOV shrinks)
        var fovNone = new FOVData(10);
        // No visible tiles

        var (full, pos, rem) = GameStateSerializer.SerializeEntityDelta(world, fovNone, previousState);
        Assert.Empty(full);
        Assert.Empty(pos);
        Assert.Single(rem);
        Assert.Equal(entity.Id, rem[0].Id);
        Assert.Empty(previousState); // Cleaned up

        World.Destroy(world);
    }

    [Fact]
    public void ChatMsg_RoundTrip()
    {
        var msg = new ChatMsg
        {
            SenderId = 42,
            SenderName = "Player1",
            Text = "Hello world!",
            Timestamp = 1234567890
        };

        var data = NetSerializer.Serialize(msg);
        var result = NetSerializer.Deserialize<ChatMsg>(data);

        Assert.Equal(42, result.SenderId);
        Assert.Equal("Player1", result.SenderName);
        Assert.Equal("Hello world!", result.Text);
        Assert.Equal(1234567890, result.Timestamp);
    }

    [Fact]
    public void FullSnapshot_SerializeDeserialize_RoundTrip()
    {
        // Replicate the exact server→client flow: build a snapshot delta, serialize, wrap, unwrap, deserialize
        var engine = new RogueLikeNet.Core.GameEngine(42, new RogueLikeNet.Core.Generation.BspDungeonGenerator(42));
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);
        engine.Tick();

        ref var pos = ref engine.EcsWorld.Get<Position>(player);
        ref var fov = ref engine.EcsWorld.Get<FOVData>(player);

        var previousState = new Dictionary<long, EntityUpdateMsg>();
        var (fullUpdates, posUpdates, removals) = GameStateSerializer.SerializeEntityDelta(engine.EcsWorld, fov, previousState);
        var snapshot = new WorldDeltaMsg
        {
            FromTick = 0,
            ToTick = engine.CurrentTick,
            IsSnapshot = true,
            Chunks = GameStateSerializer.SerializeChunksAroundPosition(engine, pos.X, pos.Y),
            EntityUpdates = fullUpdates,
            EntityPositionHealthUpdates = posUpdates,
            EntityRemovals = removals,
            PlayerState = GameStateSerializer.BuildPlayerState(engine, player),
        };

        // Serialize + wrap (server side)
        var payload = NetSerializer.Serialize(snapshot);
        var data = NetSerializer.WrapMessage(MessageTypes.WorldDelta, payload);

        // Unwrap + deserialize (client side)
        var envelope = NetSerializer.UnwrapMessage(data);
        Assert.Equal(MessageTypes.WorldDelta, envelope.MessageType);

        var result = NetSerializer.Deserialize<WorldDeltaMsg>(envelope.Payload);
        Assert.True(result.IsSnapshot);
        Assert.Equal(snapshot.ToTick, result.ToTick);
        Assert.Equal(9, result.Chunks.Length); // 3x3 around player
        Assert.True(result.EntityUpdates.Length > 0);
        Assert.NotNull(result.PlayerState);

        engine.Dispose();
    }

    [Fact]
    public void SerializeEntityDelta_PositionHealthOnlyChange_EmitsPositionHealthUpdate()
    {
        var world = World.Create();
        // Entity with Health — position + health changes should use the compressed path
        var entity = world.Create(new Position(5, 5), new TileAppearance(64, 0xFFFFFF), new Health(20));

        var fov = new FOVData(10);
        fov.VisibleTiles!.Add(Position.PackCoord(5, 5));
        fov.VisibleTiles!.Add(Position.PackCoord(7, 5));

        var previousState = new Dictionary<long, EntityUpdateMsg>();

        // Seed previous state
        GameStateSerializer.SerializeEntityDelta(world, fov, previousState);

        // Change only position and health — glyph and color stay the same
        world.Set(entity, new Position(7, 5));
        ref var health = ref world.Get<Health>(entity);
        health.Current = 15;

        var (full, pos, rem) = GameStateSerializer.SerializeEntityDelta(world, fov, previousState);
        Assert.Empty(full); // Not a full update
        Assert.Single(pos); // Should be a position-health update
        Assert.Equal(7, pos[0].X);
        Assert.Equal(5, pos[0].Y);
        Assert.Equal(15, pos[0].Health);
        Assert.Empty(rem);

        World.Destroy(world);
    }
}
