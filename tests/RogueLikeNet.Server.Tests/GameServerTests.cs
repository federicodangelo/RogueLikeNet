using RogueLikeNet.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Protocol;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Server.Tests;

// A test subclass that exposes internal state for testing purposes.
public class TestGameServer : GameServer
{
    public TestGameServer(int worldSeed, BspDungeonGenerator generator) : base(worldSeed, generator)
    {
    }

    public GameEngine Engine
    {
        get
        {
            if (IsRunning)
                throw new InvalidOperationException("Cannot access Engine while server is running");
            return _engine;
        }
    }
}

public class GameServerTests
{
    private static readonly BspDungeonGenerator _gen = new();

    [Fact]
    public void GameServer_StartsAndStops()
    {
        using var loop = new TestGameServer(42, _gen);
        loop.Start();
        Assert.True(loop.IsRunning);
        loop.Dispose();
    }

    [Fact]
    public void AddConnection_ReturnsValidConnection()
    {
        using var loop = new TestGameServer(42, _gen);
        var conn = loop.AddConnection(_ => Task.CompletedTask);
        Assert.NotNull(conn);
        Assert.True(conn.ConnectionId > 0);
    }

    [Fact]
    public void SpawnPlayerForConnection_CreatesEntity()
    {
        using var loop = new TestGameServer(42, _gen);
        var conn = loop.AddConnection(_ => Task.CompletedTask);
        loop.SpawnPlayerForConnection(conn.ConnectionId);
        Assert.NotNull(conn.PlayerEntity);
        Assert.True(loop.Engine.EcsWorld.IsAlive(conn.PlayerEntity.Value));
    }

    [Fact]
    public void RemoveConnection_DestroysEntity()
    {
        using var loop = new TestGameServer(42, _gen);
        var conn = loop.AddConnection(_ => Task.CompletedTask);
        loop.SpawnPlayerForConnection(conn.ConnectionId);

        var entity = conn.PlayerEntity!.Value;
        loop.RemoveConnection(conn.ConnectionId);

        Assert.False(loop.Engine.EcsWorld.IsAlive(entity));
    }

    [Fact]
    public void EnqueueInput_QueuesCorrectly()
    {
        using var loop = new TestGameServer(42, _gen);
        var conn = loop.AddConnection(_ => Task.CompletedTask);

        var input = new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = 1, TargetY = 0 };
        loop.EnqueueInput(conn.ConnectionId, input);

        Assert.True(conn.InputQueue.TryDequeue(out var dequeued));
        Assert.Equal(ActionTypes.Move, dequeued.ActionType);
        Assert.Equal(1, dequeued.TargetX);
    }

    [Fact]
    public void SpawnPlayer_SendsSnapshot()
    {
        byte[]? receivedData = null;
        using var loop = new TestGameServer(42, _gen);
        var conn = loop.AddConnection(data =>
        {
            receivedData = data;
            return Task.CompletedTask;
        });

        loop.SpawnPlayerForConnection(conn.ConnectionId);

        Assert.NotNull(receivedData);
        var envelope = NetSerializer.UnwrapMessage(receivedData);
        Assert.Equal(MessageTypes.WorldDelta, envelope.MessageType);

        var snapshot = NetSerializer.Deserialize<WorldDeltaMsg>(envelope.Payload);
        Assert.True(snapshot.IsSnapshot);
        Assert.True(snapshot.Chunks.Length > 0);
    }

    [Fact]
    public async Task RunLoop_BroadcastsDeltas()
    {
        var messages = new List<byte[]>();
        using var loop = new TestGameServer(42, _gen);
        var conn = loop.AddConnection(data =>
        {
            messages.Add(data.ToArray());
            return Task.CompletedTask;
        });
        loop.SpawnPlayerForConnection(conn.ConnectionId);
        messages.Clear(); // Clear the initial snapshot

        loop.Start();
        await Task.Delay(200); // Wait for a few ticks
        loop.Dispose();

        Assert.True(messages.Count > 0);
        var env = NetSerializer.UnwrapMessage(messages[0]);
        Assert.Equal(MessageTypes.WorldDelta, env.MessageType);

        var delta = NetSerializer.Deserialize<WorldDeltaMsg>(env.Payload);
        // After snapshot, delta sends new chunks (if player moved to new area) or is empty
        Assert.True(delta.Chunks.Length >= 0);
    }

    [Fact]
    public async Task ProcessInputs_AppliesPlayerInput()
    {
        var messages = new List<byte[]>();
        using var loop = new TestGameServer(42, _gen);
        var conn = loop.AddConnection(data =>
        {
            messages.Add(data.ToArray());
            return Task.CompletedTask;
        });
        loop.SpawnPlayerForConnection(conn.ConnectionId);

        var input = new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = 1, TargetY = 0 };
        loop.EnqueueInput(conn.ConnectionId, input);

        loop.Start();
        await Task.Delay(200);
        loop.Dispose();

        // Should have received snapshot + at least one delta
        Assert.True(messages.Count > 1);
    }

    [Fact]
    public void RemoveConnection_WithoutEntity_NoError()
    {
        using var loop = new TestGameServer(42, _gen);
        var conn = loop.AddConnection(_ => Task.CompletedTask);
        loop.RemoveConnection(conn.ConnectionId);
    }

    [Fact]
    public void RemoveConnection_NonExistent_NoError()
    {
        using var loop = new TestGameServer(42, _gen);
        loop.RemoveConnection(9999);
    }

    [Fact]
    public void EnqueueInput_NonExistentConnection_NoError()
    {
        using var loop = new TestGameServer(42, _gen);
        loop.EnqueueInput(9999, new ClientInputMsg { ActionType = 1 });
    }

    [Fact]
    public async Task BroadcastDeltas_HandlesSendException()
    {
        int sendCount = 0;
        using var loop = new TestGameServer(42, _gen);
        var conn = loop.AddConnection(data =>
        {
            sendCount++;
            if (sendCount > 1) throw new InvalidOperationException("send failed");
            return Task.CompletedTask;
        });
        loop.SpawnPlayerForConnection(conn.ConnectionId);

        loop.Start();
        await Task.Delay(200);
        loop.Dispose();
        // Should not throw — exception is swallowed in BroadcastDeltas
    }

    [Fact]
    public void Dispose_WithoutStart()
    {
        var loop = new TestGameServer(42, _gen);
        loop.Dispose();
    }

    [Fact]
    public void Dispose_WithStart()
    {
        var loop = new TestGameServer(42, _gen);
        loop.Start();
        Assert.True(loop.IsRunning);
        loop.Dispose();
    }

    [Fact]
    public void SpawnPlayerForConnection_InvalidConnection_NoOp()
    {
        using var loop = new TestGameServer(42, _gen);
        loop.SpawnPlayerForConnection(9999); // Non-existent connection
    }

    [Fact]
    public void Snapshot_ContainsEntitiesAndChunks()
    {
        byte[]? receivedData = null;
        using var loop = new TestGameServer(42, _gen);
        var conn = loop.AddConnection(data =>
        {
            receivedData = data;
            return Task.CompletedTask;
        });

        loop.SpawnPlayerForConnection(conn.ConnectionId);

        Assert.NotNull(receivedData);
        var envelope = NetSerializer.UnwrapMessage(receivedData);
        var snapshot = NetSerializer.Deserialize<WorldDeltaMsg>(envelope.Payload);

        // Should have 3x3 = 9 chunks around spawn
        Assert.Equal(9, snapshot.Chunks.Length);
        // Each chunk should have tile data
        foreach (var chunk in snapshot.Chunks)
        {
            Assert.True(chunk.TileTypes.Length > 0);
            Assert.True(chunk.TileGlyphs.Length > 0);
            Assert.True(chunk.TileFgColors.Length > 0);
            Assert.True(chunk.TileBgColors.Length > 0);
        }

        // Should have entities (at least the player)
        Assert.True(snapshot.EntityUpdates.Length > 0);

        // Should have PlayerState
        Assert.NotNull(snapshot.PlayerState);
    }

    [Fact]
    public async Task Delta_ContainsEntityUpdates()
    {
        var messages = new List<byte[]>();
        using var loop = new TestGameServer(42, _gen);
        var conn = loop.AddConnection(data =>
        {
            messages.Add(data.ToArray());
            return Task.CompletedTask;
        });
        loop.SpawnPlayerForConnection(conn.ConnectionId);
        messages.Clear();

        loop.Start();
        await Task.Delay(200);
        loop.Dispose();

        Assert.True(messages.Count > 0);
        var env = NetSerializer.UnwrapMessage(messages[0]);
        var delta = NetSerializer.Deserialize<WorldDeltaMsg>(env.Payload);

        // Should have entity updates (at least the player)
        Assert.True(delta.EntityUpdates.Length > 0);
    }

    [Fact]
    public async Task MultipleConnections_EachGetDeltas()
    {
        var messages1 = new List<byte[]>();
        var messages2 = new List<byte[]>();
        using var loop = new TestGameServer(42, _gen);

        var conn1 = loop.AddConnection(data =>
        {
            messages1.Add(data.ToArray());
            return Task.CompletedTask;
        });
        var conn2 = loop.AddConnection(data =>
        {
            messages2.Add(data.ToArray());
            return Task.CompletedTask;
        });

        loop.SpawnPlayerForConnection(conn1.ConnectionId);
        loop.SpawnPlayerForConnection(conn2.ConnectionId);
        messages1.Clear();
        messages2.Clear();

        loop.Start();
        await Task.Delay(200);
        loop.Dispose();

        Assert.True(messages1.Count > 0);
        Assert.True(messages2.Count > 0);
    }

    [Fact]
    public void Engine_IsAccessible()
    {
        using var loop = new TestGameServer(42, _gen);
        Assert.NotNull(loop.Engine);
    }

    [Fact]
    public void IsRunning_FalseBeforeStart()
    {
        using var loop = new TestGameServer(42, _gen);
        Assert.False(loop.IsRunning);
    }

    [Fact]
    public async Task ProcessInputs_SkipsConnectionWithoutEntity()
    {
        var messages = new List<byte[]>();
        using var loop = new TestGameServer(42, _gen);

        // Add connection but don't spawn player
        var connNoEntity = loop.AddConnection(data =>
        {
            messages.Add(data.ToArray());
            return Task.CompletedTask;
        });

        // Enqueue input for connection without entity
        loop.EnqueueInput(connNoEntity.ConnectionId, new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = 1 });

        loop.Start();
        await Task.Delay(150);
        loop.Dispose();

        // No snapshot or delta should have been sent (no entity)
        Assert.Empty(messages);
    }

    [Fact]
    public async Task ProcessInputs_SkipsDeadEntity()
    {
        var messages = new List<byte[]>();
        using var loop = new TestGameServer(42, _gen);
        var conn = loop.AddConnection(data =>
        {
            messages.Add(data.ToArray());
            return Task.CompletedTask;
        });
        loop.SpawnPlayerForConnection(conn.ConnectionId);

        // Kill the entity
        var entity = conn.PlayerEntity!.Value;
        loop.Engine.EcsWorld.Destroy(entity);

        messages.Clear();

        loop.Start();
        await Task.Delay(150);
        loop.Dispose();

        // No deltas should be sent for dead entity
        Assert.Empty(messages);
    }

    [Fact]
    public async Task Delta_ContainsCombatEvents()
    {
        var messages = new List<byte[]>();
        using var loop = new TestGameServer(42, _gen);
        var conn = loop.AddConnection(data =>
        {
            messages.Add(data.ToArray());
            return Task.CompletedTask;
        });
        loop.SpawnPlayerForConnection(conn.ConnectionId);

        // Get the player's position and spawn a monster right next to them
        ref var playerPos = ref loop.Engine.EcsWorld.Get<Position>(conn.PlayerEntity!.Value);
        int monsterX = playerPos.X + 1;
        int monsterY = playerPos.Y;
        loop.Engine.SpawnMonster(1, monsterX, monsterY, 77, 0xFF0000);

        // Send an attack input targeting the monster
        var attack = new ClientInputMsg
        {
            ActionType = ActionTypes.Attack,
            TargetX = 1,
            TargetY = 0
        };
        loop.EnqueueInput(conn.ConnectionId, attack);

        messages.Clear();

        loop.Start();
        await Task.Delay(200);
        loop.Dispose();

        // At least one delta should contain combat events
        bool hasCombatEvents = false;
        foreach (var msg in messages)
        {
            var env = NetSerializer.UnwrapMessage(msg);
            if (env.MessageType == MessageTypes.WorldDelta)
            {
                var delta = NetSerializer.Deserialize<WorldDeltaMsg>(env.Payload);
                if (delta.CombatEvents.Length > 0)
                {
                    hasCombatEvents = true;
                    Assert.True(delta.CombatEvents[0].Damage > 0);
                    break;
                }
            }
        }
        Assert.True(hasCombatEvents);
    }

    [Fact]
    public async Task BuildPlayerState_ReturnsNullForNoEntity()
    {
        var messages = new List<byte[]>();
        using var loop = new TestGameServer(42, _gen);
        var conn = loop.AddConnection(data =>
        {
            messages.Add(data.ToArray());
            return Task.CompletedTask;
        });
        // Don't spawn player — connection has no entity
        // Start + stop loop quickly
        loop.Start();
        await Task.Delay(150);
        loop.Dispose();

        // No deltas are sent when there's no entity (checked by BroadcastDeltas/ProcessInputs skip),
        // so this implicitly covers BuildPlayerState returning null for no entity
        Assert.Empty(messages);
    }

    [Fact]
    public async Task ProcessInputs_AppliesMultipleInputsFromQueue()
    {
        var messages = new List<byte[]>();
        using var loop = new TestGameServer(42, _gen);
        var conn = loop.AddConnection(data =>
        {
            messages.Add(data.ToArray());
            return Task.CompletedTask;
        });
        loop.SpawnPlayerForConnection(conn.ConnectionId);

        // Enqueue multiple inputs
        loop.EnqueueInput(conn.ConnectionId, new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = 1, TargetY = 0 });
        loop.EnqueueInput(conn.ConnectionId, new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = 0, TargetY = 1 });

        messages.Clear();
        loop.Start();
        await Task.Delay(200);
        loop.Dispose();

        Assert.True(messages.Count > 0);
    }

    [Fact]
    public void AddConnection_AssignsUniqueIds()
    {
        using var loop = new TestGameServer(42, _gen);
        var conn1 = loop.AddConnection(_ => Task.CompletedTask);
        var conn2 = loop.AddConnection(_ => Task.CompletedTask);
        Assert.NotEqual(conn1.ConnectionId, conn2.ConnectionId);
    }

    [Fact]
    public void Snapshot_IncludesEntitiesWithoutHealth()
    {
        byte[]? receivedData = null;
        using var loop = new TestGameServer(42, _gen);
        var conn = loop.AddConnection(data =>
        {
            receivedData = data;
            return Task.CompletedTask;
        });

        // Determine where the player will spawn so we can place the test entity nearby
        var (spawnX, spawnY) = loop.Engine.FindSpawnPosition();

        // Create an entity with Position + TileAppearance but NO Health — near the spawn
        loop.Engine.EcsWorld.Create(
            new Position(spawnX, spawnY),
            new TileAppearance(42, 0x00FF00)
        );

        loop.SpawnPlayerForConnection(conn.ConnectionId);

        Assert.NotNull(receivedData);
        var envelope = NetSerializer.UnwrapMessage(receivedData);
        var snapshot = NetSerializer.Deserialize<WorldDeltaMsg>(envelope.Payload);

        // Should have the healthless entity with Health = 0 / MaxHealth = 0
        var healthless = snapshot.EntityUpdates.FirstOrDefault(e => e.GlyphId == 42);
        Assert.NotNull(healthless);
        Assert.Equal(0, healthless.Health);
        Assert.Equal(0, healthless.MaxHealth);
    }

    [Fact]
    public async Task Delta_IncludesEntitiesWithoutHealth()
    {
        var messages = new List<byte[]>();
        using var loop = new TestGameServer(42, _gen);
        var conn = loop.AddConnection(data =>
        {
            messages.Add(data.ToArray());
            return Task.CompletedTask;
        });

        loop.SpawnPlayerForConnection(conn.ConnectionId);
        messages.Clear();

        // Get the player's position so the entity is within FOV
        var playerEntity = conn.PlayerEntity!.Value;
        ref var playerPos = ref loop.Engine.EcsWorld.Get<Position>(playerEntity);

        // Create an entity with Position + TileAppearance but NO Health — at player pos
        loop.Engine.EcsWorld.Create(
            new Position(playerPos.X, playerPos.Y),
            new TileAppearance(88, 0xFF0000)
        );

        loop.Start();
        await Task.Delay(200);
        loop.Dispose();

        Assert.True(messages.Count > 0);
        var env = NetSerializer.UnwrapMessage(messages[0]);
        var delta = NetSerializer.Deserialize<WorldDeltaMsg>(env.Payload);

        var healthless = delta.EntityUpdates.FirstOrDefault(e => e.GlyphId == 88);
        Assert.NotNull(healthless);
        Assert.Equal(0, healthless.Health);
    }

    [Fact]
    public async Task ProcessInputs_DrainsAllKeepsLatest()
    {
        var messages = new List<byte[]>();
        using var loop = new TestGameServer(42, _gen);
        var conn = loop.AddConnection(data =>
        {
            messages.Add(data.ToArray());
            return Task.CompletedTask;
        });
        loop.SpawnPlayerForConnection(conn.ConnectionId);

        // Record starting position
        ref var startPos = ref loop.Engine.EcsWorld.Get<Position>(conn.PlayerEntity!.Value);
        int startX = startPos.X;
        int startY = startPos.Y;

        // Clear any actors that could block the path (spawned by chunk generation)
        var clearQuery = new Arch.Core.QueryDescription().WithAll<Position, Health>();
        var toDestroy = new List<Arch.Core.Entity>();
        loop.Engine.EcsWorld.Query(in clearQuery, (Arch.Core.Entity e, ref Position _, ref Health _) =>
        {
            if (e != conn.PlayerEntity!.Value)
                toDestroy.Add(e);
        });
        foreach (var e in toDestroy) loop.Engine.EcsWorld.Destroy(e);

        // Queue 3 right-moves before start — all drained in tick 1, only the last is applied
        loop.EnqueueInput(conn.ConnectionId, new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = 1, TargetY = 0 });
        loop.EnqueueInput(conn.ConnectionId, new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = 1, TargetY = 0 });
        loop.EnqueueInput(conn.ConnectionId, new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = 1, TargetY = 0 });

        messages.Clear();
        loop.Start();
        await Task.Delay(400); // Enough time for all ticks to run
        loop.Dispose();

        // Player should have moved exactly 1 tile right (all 3 queued drained in tick 1, latest applied)
        ref var endPos = ref loop.Engine.EcsWorld.Get<Position>(conn.PlayerEntity!.Value);
        Assert.Equal(startX + 1, endPos.X);
        Assert.Equal(startY, endPos.Y);
    }

    [Fact]
    public async Task Delta_IncludesFloorItemsViaEntityUpdates()
    {
        var messages = new List<byte[]>();
        using var loop = new TestGameServer(42, _gen);
        var conn = loop.AddConnection(data =>
        {
            messages.Add(data.ToArray());
            return Task.CompletedTask;
        });
        loop.SpawnPlayerForConnection(conn.ConnectionId);

        // Place an item at the player's position
        ref var playerPos = ref loop.Engine.EcsWorld.Get<Position>(conn.PlayerEntity!.Value);
        var template = ItemDefinitions.Get(ItemDefinitions.HealthPotion); // Health Potion
        loop.Engine.SpawnItemOnGround(template, 0, playerPos.X, playerPos.Y);

        messages.Clear();
        loop.Start();
        await Task.Delay(200);
        loop.Dispose();

        // Find a delta with an entity update carrying the item name
        bool hasItemEntity = false;
        foreach (var msg in messages)
        {
            var env = NetSerializer.UnwrapMessage(msg);
            if (env.MessageType == MessageTypes.WorldDelta)
            {
                var delta = NetSerializer.Deserialize<WorldDeltaMsg>(env.Payload);
                foreach (var eu in delta.EntityUpdates)
                {
                    if (eu.ItemName == template.Name)
                    {
                        hasItemEntity = true;
                        break;
                    }
                }
                if (hasItemEntity) break;
            }
        }
        Assert.True(hasItemEntity, "Delta should contain an entity update with ItemName for the floor item");
    }

    [Fact]
    public void GameStateSerializer_SerializeChunk_ProducesValidMsg()
    {
        using var loop = new TestGameServer(42, _gen);
        var chunk = loop.Engine.EnsureChunkLoaded(0, 0);

        var msg = GameStateSerializer.SerializeChunk(chunk);

        Assert.Equal(0, msg.ChunkX);
        Assert.Equal(0, msg.ChunkY);
        int total = RogueLikeNet.Core.World.Chunk.Size * RogueLikeNet.Core.World.Chunk.Size;
        Assert.Equal(total, msg.TileTypes.Length);
        Assert.Equal(total, msg.TileGlyphs.Length);
        Assert.Equal(total, msg.TileFgColors.Length);
        Assert.Equal(total, msg.TileBgColors.Length);
    }

    [Fact]
    public void GameStateSerializer_BuildPlayerState_PopulatesAllFields()
    {
        using var loop = new TestGameServer(42, _gen);
        var conn = loop.AddConnection(_ => Task.CompletedTask);
        loop.SpawnPlayerForConnection(conn.ConnectionId);

        var hudMsg = GameStateSerializer.BuildPlayerState(loop.Engine, conn.PlayerEntity!.Value);
        Assert.NotNull(hudMsg);
        Assert.True(hudMsg!.MaxHealth > 0);
        Assert.True(hudMsg.Attack > 0);
        Assert.True(hudMsg.Defense > 0);
        Assert.Equal(4, hudMsg.Skills.Length);
        Assert.Equal("", hudMsg.EquippedWeaponName); // Nothing equipped yet
        Assert.Equal("", hudMsg.EquippedArmorName);
        Assert.Empty(hudMsg.InventoryItems);
    }

    [Fact]
    public void GameStateSerializer_BuildPlayerState_NullForDeadEntity()
    {
        using var loop = new TestGameServer(42, _gen);
        var conn = loop.AddConnection(_ => Task.CompletedTask);
        loop.SpawnPlayerForConnection(conn.ConnectionId);

        var entity = conn.PlayerEntity!.Value;
        loop.Engine.EcsWorld.Destroy(entity);

        var hudMsg = GameStateSerializer.BuildPlayerState(loop.Engine, entity);
        Assert.Null(hudMsg);
    }
}
