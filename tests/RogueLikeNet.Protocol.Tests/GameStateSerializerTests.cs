using Arch.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Protocol;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Protocol.Tests;

public class GameStateSerializerTests
{
    [Fact]
    public void EntitySnapshot_EqualityWorks()
    {
        var a = new EntitySnapshot(1, 2, 64, 0xFFFFFF, 10, 10);
        var b = new EntitySnapshot(1, 2, 64, 0xFFFFFF, 10, 10);
        var c = new EntitySnapshot(1, 3, 64, 0xFFFFFF, 10, 10);

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void SerializeEntities_WithFOV_OnlyReturnsVisibleEntities()
    {
        var world = World.Create();

        // Entity at (5,5) — will be visible
        world.Create(new Position(5, 5), new TileAppearance(64, 0xFFFFFF));
        // Entity at (50,50) — will NOT be visible
        world.Create(new Position(50, 50), new TileAppearance(65, 0xFFFFFF));

        var fov = new FOVData(10);
        fov.VisibleTiles!.Add(FOVData.PackCoord(5, 5));

        var result = GameStateSerializer.SerializeEntities(world, fov);

        Assert.Single(result);
        Assert.Equal(5, result[0].X);
        Assert.Equal(5, result[0].Y);
        Assert.Equal(64, result[0].GlyphId);

        World.Destroy(world);
    }

    [Fact]
    public void SerializeEntities_WithFOV_IncludesHealthData()
    {
        var world = World.Create();
        world.Create(new Position(3, 4), new TileAppearance(64, 0xFF0000), new Health(20));

        var fov = new FOVData(10);
        fov.VisibleTiles!.Add(FOVData.PackCoord(3, 4));

        var result = GameStateSerializer.SerializeEntities(world, fov);

        Assert.Single(result);
        Assert.Equal(20, result[0].Health);
        Assert.Equal(20, result[0].MaxHealth);

        World.Destroy(world);
    }

    [Fact]
    public void SerializeEntityUpdatesDelta_OnlyReturnsChangedEntities()
    {
        var world = World.Create();
        var entity = world.Create(new Position(5, 5), new TileAppearance(64, 0xFFFFFF), new Health(10));

        var fov = new FOVData(10);
        fov.VisibleTiles!.Add(FOVData.PackCoord(5, 5));

        var previousState = new Dictionary<long, EntitySnapshot>();

        // First call — entity is new, should be included
        var result1 = GameStateSerializer.SerializeEntityUpdatesDelta(world, fov, previousState);
        Assert.Single(result1);
        Assert.Equal(entity.Id, result1[0].Id);
        Assert.False(result1[0].Removed);

        // Second call — nothing changed, entity should NOT be included
        var result2 = GameStateSerializer.SerializeEntityUpdatesDelta(world, fov, previousState);
        Assert.Empty(result2);

        World.Destroy(world);
    }

    [Fact]
    public void SerializeEntityUpdatesDelta_IncludesEntityWhenPositionChanges()
    {
        var world = World.Create();
        var entity = world.Create(new Position(5, 5), new TileAppearance(64, 0xFFFFFF));

        var fov = new FOVData(10);
        fov.VisibleTiles!.Add(FOVData.PackCoord(5, 5));
        fov.VisibleTiles!.Add(FOVData.PackCoord(6, 5));

        var previousState = new Dictionary<long, EntitySnapshot>();

        // Seed previous state
        GameStateSerializer.SerializeEntityUpdatesDelta(world, fov, previousState);

        // Move entity
        world.Set(entity, new Position(6, 5));

        var result = GameStateSerializer.SerializeEntityUpdatesDelta(world, fov, previousState);
        Assert.Single(result);
        Assert.Equal(6, result[0].X);

        World.Destroy(world);
    }

    [Fact]
    public void SerializeEntityUpdatesDelta_MarksRemovedEntitiesLeavingFOV()
    {
        var world = World.Create();
        var entity = world.Create(new Position(5, 5), new TileAppearance(64, 0xFFFFFF));

        var fovAll = new FOVData(10);
        fovAll.VisibleTiles!.Add(FOVData.PackCoord(5, 5));

        var previousState = new Dictionary<long, EntitySnapshot>();

        // Seed previous state — entity visible
        GameStateSerializer.SerializeEntityUpdatesDelta(world, fovAll, previousState);

        // Now entity moves out of FOV (or FOV shrinks)
        var fovNone = new FOVData(10);
        // No visible tiles

        var result = GameStateSerializer.SerializeEntityUpdatesDelta(world, fovNone, previousState);
        Assert.Single(result);
        Assert.Equal(entity.Id, result[0].Id);
        Assert.True(result[0].Removed);
        Assert.Empty(previousState); // Cleaned up

        World.Destroy(world);
    }

    [Fact]
    public void SerializeEntities_WithoutFOV_ReturnsAllEntities()
    {
        var world = World.Create();
        world.Create(new Position(1, 1), new TileAppearance(64, 0xFFFFFF));
        world.Create(new Position(99, 99), new TileAppearance(65, 0xFFFFFF));

        var result = GameStateSerializer.SerializeEntities(world);
        Assert.Equal(2, result.Length);

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
        // Replicate the exact server→client flow: build a real snapshot, serialize, wrap, unwrap, deserialize
        var engine = new RogueLikeNet.Core.GameEngine(42, new RogueLikeNet.Core.Generation.BspDungeonGenerator());
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy);
        engine.Tick();

        ref var pos = ref engine.EcsWorld.Get<Position>(player);
        ref var fov = ref engine.EcsWorld.Get<FOVData>(player);

        var snapshot = new WorldSnapshotMsg
        {
            WorldTick = engine.CurrentTick,
            PlayerX = pos.X,
            PlayerY = pos.Y,
            Chunks = GameStateSerializer.SerializeChunksAroundPosition(engine, pos.X, pos.Y),
            Entities = GameStateSerializer.SerializeEntities(engine.EcsWorld, fov),
            PlayerHud = GameStateSerializer.BuildPlayerHud(engine, player),
        };

        // Serialize + wrap (server side)
        var payload = NetSerializer.Serialize(snapshot);
        var data = NetSerializer.WrapMessage(MessageTypes.WorldSnapshot, payload);

        // Unwrap + deserialize (client side)
        var envelope = NetSerializer.UnwrapMessage(data);
        Assert.Equal(MessageTypes.WorldSnapshot, envelope.MessageType);

        var result = NetSerializer.Deserialize<WorldSnapshotMsg>(envelope.Payload);
        Assert.Equal(snapshot.WorldTick, result.WorldTick);
        Assert.Equal(snapshot.PlayerX, result.PlayerX);
        Assert.Equal(9, result.Chunks.Length); // 3x3 around player
        Assert.True(result.Entities.Length > 0);
        Assert.NotNull(result.PlayerHud);

        engine.Dispose();
    }
}
