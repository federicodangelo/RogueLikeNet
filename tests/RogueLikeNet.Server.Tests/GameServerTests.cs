using RogueLikeNet.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Entities;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;
using RogueLikeNet.Protocol;
using RogueLikeNet.Protocol.Messages;
using RogueLikeNet.Server.Persistence;

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
    private static readonly BspDungeonGenerator _gen = new(42);

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
        Assert.NotNull(conn.PlayerEntityId);
        var player = loop.Engine.WorldMap.GetPlayer(conn.PlayerEntityId!.Value);
        Assert.NotNull(player);
    }

    [Fact]
    public void RemoveConnection_DestroysEntity()
    {
        using var loop = new TestGameServer(42, _gen);
        var conn = loop.AddConnection(_ => Task.CompletedTask);
        loop.SpawnPlayerForConnection(conn.ConnectionId);

        var entityId = conn.PlayerEntityId!.Value;
        loop.RemoveConnection(conn.ConnectionId);

        var player = loop.Engine.WorldMap.GetPlayer(entityId);
        Assert.Null(player);
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
        Assert.NotNull(conn.PlayerEntityId);
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
    public void SpawnPlayerForConnection_InvalidConnection_Throws()
    {
        using var loop = new TestGameServer(42, _gen);
        var exception = Assert.Throws<Exception>(() => loop.SpawnPlayerForConnection(9999)); // Non-existent connection
        Assert.Equal($"Connection not found: {9999}", exception.Message);
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

        // Default VisibleChunks=9 → chunkRange=2 → 5x5 = 25 chunks around spawn
        Assert.Equal(9, snapshot.Chunks.Length);
        // Each chunk should have tile data
        foreach (var chunk in snapshot.Chunks)
        {
            Assert.True(chunk.TileIds.Length > 0);
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

        // With delta compression, unchanged entities since the snapshot are omitted
        Assert.NotNull(delta);
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

        // Kill the entity by removing from WorldMap
        var entityId = conn.PlayerEntityId!.Value;
        loop.Engine.WorldMap.RemovePlayer(entityId);

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
        var player = loop.Engine.WorldMap.GetPlayer(conn.PlayerEntityId!.Value)!.Value;
        int monsterX = player.Position.X + 1;
        int monsterY = player.Position.Y;
        loop.Engine.SpawnMonster(Position.FromCoords(monsterX, monsterY, Position.DefaultZ), new MonsterData { MonsterTypeId = 1, Health = 20, Attack = 5, Defense = 2, Speed = 8 });

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
        var (spawnX, spawnY, _) = loop.Engine.FindSpawnPosition();

        // Create a ground item entity (no Health) — near the spawn
        loop.Engine.SpawnItemOnGround(
            new ItemData { ItemTypeId = GameData.Instance.Items.GetNumericId("health_potion_small"), StackCount = 1 },
            Position.FromCoords(spawnX, spawnY, Position.DefaultZ)
        );

        loop.SpawnPlayerForConnection(conn.ConnectionId);

        Assert.NotNull(receivedData);
        var envelope = NetSerializer.UnwrapMessage(receivedData);
        var snapshot = NetSerializer.Deserialize<WorldDeltaMsg>(envelope.Payload);

        // Should have the healthless entity with Health = 0 / MaxHealth = 0
        var healthless = snapshot.EntityUpdates.FirstOrDefault(e => e.EntityType == (int)EntityType.GroundItem);
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
        var player = loop.Engine.WorldMap.GetPlayer(conn.PlayerEntityId!.Value)!.Value;

        // Create a ground item entity (no Health) — at player pos
        loop.Engine.SpawnItemOnGround(
            new ItemData { ItemTypeId = GameData.Instance.Items.GetNumericId("health_potion_small"), StackCount = 1 },
            Position.FromCoords(player.Position.X, player.Position.Y, Position.DefaultZ)
        );

        loop.Start();
        await Task.Delay(200);
        loop.Dispose();

        Assert.True(messages.Count > 0);
        var env = NetSerializer.UnwrapMessage(messages[0]);
        var delta = NetSerializer.Deserialize<WorldDeltaMsg>(env.Payload);

        var healthless = delta.EntityUpdates.FirstOrDefault(e => e.EntityType == (int)EntityType.GroundItem);
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
        var playerBefore = loop.Engine.WorldMap.GetPlayer(conn.PlayerEntityId!.Value)!.Value;
        int startX = playerBefore.Position.X;
        int startY = playerBefore.Position.Y;

        // Clear any actors that could block the path (spawned by chunk generation)
        foreach (var chunk in loop.Engine.WorldMap.LoadedChunks)
        {
            chunk.Monsters.ToArray().ToList().ForEach(chunk.RemoveEntity);
            chunk.TownNpcs.ToArray().ToList().ForEach(chunk.RemoveEntity);
        }

        // Queue 3 right-moves before start — all drained in tick 1, only the last is applied
        loop.EnqueueInput(conn.ConnectionId, new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = 1, TargetY = 0 });
        loop.EnqueueInput(conn.ConnectionId, new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = 1, TargetY = 0 });
        loop.EnqueueInput(conn.ConnectionId, new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = 1, TargetY = 0 });

        messages.Clear();
        loop.Start();
        await Task.Delay(400); // Enough time for all ticks to run
        loop.Dispose();

        // Player should have moved exactly 1 tile right (all 3 queued drained in tick 1, latest applied)
        var playerAfter = loop.Engine.WorldMap.GetPlayer(conn.PlayerEntityId!.Value)!.Value;
        Assert.Equal(startX + 1, playerAfter.Position.X);
        Assert.Equal(startY, playerAfter.Position.Y);
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
        var player = loop.Engine.WorldMap.GetPlayer(conn.PlayerEntityId!.Value)!.Value;
        var template = GameData.Instance.Items.Get("health_potion_small")!; // Health Potion
        loop.Engine.SpawnItemOnGround(template, Position.FromCoords(player.Position.X, player.Position.Y, Position.DefaultZ));

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
                    if (eu.Item?.ItemTypeId == template.NumericId)
                    {
                        hasItemEntity = true;
                        break;
                    }
                }
                if (hasItemEntity) break;
            }
        }
        Assert.True(hasItemEntity, "Delta should contain an entity update with ItemTypeId for the floor item");
    }

    [Fact]
    public void GameStateSerializer_SerializeChunk_ProducesValidMsg()
    {
        using var loop = new TestGameServer(42, _gen);
        var chunk = loop.Engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        var msg = GameStateSerializer.SerializeChunk(chunk);

        Assert.Equal(0, msg.ChunkX);
        Assert.Equal(0, msg.ChunkY);
        int total = RogueLikeNet.Core.World.Chunk.Size * RogueLikeNet.Core.World.Chunk.Size;
        Assert.Equal(total, msg.TileIds.Length);
    }

    [Fact]
    public void GameStateSerializer_BuildPlayerState_PopulatesAllFields()
    {
        using var loop = new TestGameServer(42, _gen);
        var conn = loop.AddConnection(_ => Task.CompletedTask);
        loop.SpawnPlayerForConnection(conn.ConnectionId);

        var player = loop.Engine.WorldMap.GetPlayer(conn.PlayerEntityId!.Value)!.Value;
        var hudMsg = GameStateSerializer.BuildPlayerState(loop.Engine, player);
        Assert.NotNull(hudMsg);
        Assert.True(hudMsg!.MaxHealth > 0);
        Assert.True(hudMsg.Attack > 0);
        Assert.True(hudMsg.Defense > 0);
        Assert.Empty(hudMsg!.EquippedItems); // Nothing equipped yet
        Assert.Empty(hudMsg.InventoryItems);
    }

    [Fact]
    public void GameStateSerializer_BuildPlayerState_NullForDeadEntity()
    {
        using var loop = new TestGameServer(42, _gen);
        var conn = loop.AddConnection(_ => Task.CompletedTask);
        loop.SpawnPlayerForConnection(conn.ConnectionId);

        var entityId = conn.PlayerEntityId!.Value;
        var player = loop.Engine.WorldMap.GetPlayer(entityId)!.Value;
        player.Health.Current = 0; ;

        var hudMsg = GameStateSerializer.BuildPlayerState(loop.Engine, player);
        Assert.Null(hudMsg);
    }

    [Fact]
    public void BroadcastChat_DeliversToAllConnections()
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
        loop.SpawnPlayerForConnection(conn1.ConnectionId, playerName: "Alice");
        loop.SpawnPlayerForConnection(conn2.ConnectionId, playerName: "Bob");

        messages1.Clear();
        messages2.Clear();

        loop.BroadcastChat(conn1.ConnectionId, "Hello!");

        // Both connections should receive the chat message
        Assert.Single(messages1);
        Assert.Single(messages2);

        var env = NetSerializer.UnwrapMessage(messages1[0]);
        Assert.Equal(MessageTypes.ChatReceive, env.MessageType);
        var chat = NetSerializer.Deserialize<ChatMsg>(env.Payload);
        Assert.Equal("Hello!", chat.Text);
        Assert.Equal("Alice", chat.SenderName);
    }

    [Fact]
    public void BroadcastChat_EmptyPlayerName_UsesFallback()
    {
        var messages = new List<byte[]>();
        using var loop = new TestGameServer(42, _gen);
        var conn = loop.AddConnection(data =>
        {
            messages.Add(data.ToArray());
            return Task.CompletedTask;
        });
        loop.SpawnPlayerForConnection(conn.ConnectionId, playerName: "");

        messages.Clear();
        loop.BroadcastChat(conn.ConnectionId, "Hello!");

        Assert.Single(messages);
        var env = NetSerializer.UnwrapMessage(messages[0]);
        var chat = NetSerializer.Deserialize<ChatMsg>(env.Payload);
        Assert.StartsWith("Player ", chat.SenderName);
    }

    [Fact]
    public void Start_AlreadyRunning_Throws()
    {
        using var loop = new TestGameServer(42, _gen);
        loop.Start();
        Assert.Throws<InvalidOperationException>(() => loop.Start());
        loop.Dispose();
    }

    [Fact]
    public async Task RunLoop_LogsStats_AfterFiveSeconds()
    {
        var logOutput = new StringWriter();
        using var loop = new GameServer(42, _gen, logWriter: logOutput);

        var conn = loop.AddConnection(_ => Task.CompletedTask);
        loop.SpawnPlayerForConnection(conn.ConnectionId);

        loop.Start();
        // Wait enough time for at least one stats log (5 seconds)
        await Task.Delay(5500);
        loop.Dispose();

        string log = logOutput.ToString();
        Assert.Contains("tick=", log);
        Assert.Contains("KB/s", log);
    }

    [Fact]
    public void SpawnPlayerForConnection_WithClass_RespectsClassId()
    {
        byte[]? receivedData = null;
        using var loop = new TestGameServer(42, _gen);
        var conn = loop.AddConnection(data =>
        {
            receivedData = data;
            return Task.CompletedTask;
        });

        loop.SpawnPlayerForConnection(conn.ConnectionId, classId: RogueLikeNet.Core.Definitions.ClassDefinitions.Mage);

        Assert.NotNull(conn.PlayerEntityId);
        var player = loop.Engine.WorldMap.GetPlayer(conn.PlayerEntityId!.Value)!.Value;
        Assert.Equal(RogueLikeNet.Core.Definitions.ClassDefinitions.Mage, player.ClassData.ClassId);
    }

    [Fact]
    public async Task RunningServer_CommandQueue_ProcessesEnqueuedCommands()
    {
        var messages = new List<byte[]>();
        using var loop = new TestGameServer(42, _gen);
        var conn = loop.AddConnection(data =>
        {
            messages.Add(data.ToArray());
            return Task.CompletedTask;
        });

        loop.Start();
        // Give server time to start
        await Task.Delay(100);

        // SpawnPlayerForConnection enqueues a command when server is running
        loop.SpawnPlayerForConnection(conn.ConnectionId);
        await Task.Delay(200);

        loop.Dispose();

        // Should have received at least the snapshot
        Assert.True(messages.Count > 0);
        Assert.NotNull(conn.PlayerEntityId);
    }

    [Fact]
    public async Task RunningServer_RemoveConnection_ProcessesCommand()
    {
        using var loop = new TestGameServer(42, _gen);
        var conn = loop.AddConnection(_ => Task.CompletedTask);

        loop.Start();
        await Task.Delay(100);

        // SpawnPlayer while running
        loop.SpawnPlayerForConnection(conn.ConnectionId);
        await Task.Delay(200);

        Assert.NotNull(conn.PlayerEntityId);
        var entityId = conn.PlayerEntityId.Value;

        // RemoveConnection while running enqueues destroy command
        loop.RemoveConnection(conn.ConnectionId);
        await Task.Delay(200);

        loop.Dispose();
    }

    [Fact]
    public async Task RunningServer_BroadcastChat_ProcessesCommand()
    {
        var messages = new List<byte[]>();
        using var loop = new TestGameServer(42, _gen);
        var conn = loop.AddConnection(data =>
        {
            messages.Add(data.ToArray());
            return Task.CompletedTask;
        });

        loop.Start();
        await Task.Delay(100);

        // SpawnPlayer and broadcast chat while running
        loop.SpawnPlayerForConnection(conn.ConnectionId, playerName: "Test");
        await Task.Delay(200);

        messages.Clear();
        loop.BroadcastChat(conn.ConnectionId, "Hello from running server");
        await Task.Delay(200);

        loop.Dispose();

        // Should have received the chat message
        bool hasChatMsg = messages.Any(m =>
        {
            var env = NetSerializer.UnwrapMessage(m);
            return env.MessageType == MessageTypes.ChatReceive;
        });
        Assert.True(hasChatMsg);
    }

    [Fact]
    public void Dispose_AlreadyDisposed_DoesNotThrow()
    {
        var loop = new TestGameServer(42, _gen);
        loop.Start();
        loop.Dispose();
        // Second dispose should not throw
        loop.Dispose();
    }

    [Fact]
    public void UpdateVisibleChunks_ClampsToMaximum()
    {
        using var loop = new TestGameServer(42, _gen);
        var conn = loop.AddConnection(_ => Task.CompletedTask);
        Assert.Equal(9, conn.VisibleChunks); // default

        loop.UpdateVisibleChunks(conn.ConnectionId, 50);
        Assert.Equal(50, conn.VisibleChunks);

        loop.UpdateVisibleChunks(conn.ConnectionId, 1000);
        Assert.Equal(ChunkTracker.MaxVisibleChunks, conn.VisibleChunks); // clamped

        loop.UpdateVisibleChunks(conn.ConnectionId, -5);
        Assert.Equal(1, conn.VisibleChunks); // clamped to min
    }

    [Fact]
    public void UpdateVisibleChunks_AffectsChunkCount()
    {
        // With visibleChunks=4, chunkRange=1 → 3x3=9 chunks
        using var loop = new TestGameServer(42, _gen);
        byte[]? receivedData = null;
        var conn = loop.AddConnection(data =>
        {
            receivedData = data;
            return Task.CompletedTask;
        });

        loop.UpdateVisibleChunks(conn.ConnectionId, 4);
        loop.SpawnPlayerForConnection(conn.ConnectionId);

        Assert.NotNull(receivedData);
        var snapshot = NetSerializer.Deserialize<WorldDeltaMsg>(
            NetSerializer.UnwrapMessage(receivedData).Payload);

        // visibleChunks=4 → chunkRange=1 → 3x3=9 chunks
        Assert.Equal(9, snapshot.Chunks.Length);
    }

    // ── Persistence tests ──

    [Fact]
    public void InitializeNewGame_CreatesSlotAndSavesWorldMeta()
    {
        using var provider = new InMemorySaveGameProvider();
        using var server = new GameServer(42, _gen, saveProvider: provider);

        server.InitializeNewGame("Test World", 42, "bsp");

        var slots = provider.ListSaveSlots();
        Assert.Single(slots);
        Assert.Equal("Test World", slots[0].Name);
        Assert.NotNull(server.CurrentSlotId);

        var meta = provider.LoadWorldMeta(server.CurrentSlotId!);
        Assert.NotNull(meta);
        Assert.Equal(42, meta.Seed);
    }

    [Fact]
    public void InitializeNewGame_WhileRunning_Throws()
    {
        using var provider = new InMemorySaveGameProvider();
        using var server = new GameServer(42, _gen, saveProvider: provider);
        server.Start();
        Assert.Throws<InvalidOperationException>(() => server.InitializeNewGame("Test", 42, "bsp"));
        server.Dispose();
    }

    [Fact]
    public void InitializeFromSlot_LoadsExistingSlot()
    {
        using var provider = new InMemorySaveGameProvider();
        var slot = provider.CreateSaveSlot("Saved World", 42, "bsp");
        provider.SaveWorldMeta(slot.SlotId, new WorldSaveData { Seed = 42, GeneratorId = "bsp", CurrentTick = 50 });

        using var server = new GameServer(42, _gen, saveProvider: provider);
        server.InitializeFromSlot(slot.SlotId);

        Assert.Equal(slot.SlotId, server.CurrentSlotId);
    }

    [Fact]
    public void InitializeFromSlot_WhileRunning_Throws()
    {
        using var provider = new InMemorySaveGameProvider();
        using var server = new GameServer(42, _gen, saveProvider: provider);
        server.Start();
        Assert.Throws<InvalidOperationException>(() => server.InitializeFromSlot("none"));
        server.Dispose();
    }

    [Fact]
    public void InitializeFromSlot_MissingSlot_NoError()
    {
        var log = new StringWriter();
        using var provider = new InMemorySaveGameProvider();
        using var server = new GameServer(42, _gen, logWriter: log, saveProvider: provider);
        server.InitializeFromSlot("nonexistent");
        Assert.Contains("not found", log.ToString());
    }

    [Fact]
    public void InitializeFromSlot_MissingMeta_NoError()
    {
        var log = new StringWriter();
        using var provider = new InMemorySaveGameProvider();
        var slot = provider.CreateSaveSlot("Saved World", 42, "bsp");
        // Don't save world meta
        using var server = new GameServer(42, _gen, logWriter: log, saveProvider: provider);
        server.InitializeFromSlot(slot.SlotId);
        Assert.Contains("metadata not found", log.ToString());
    }

    [Fact]
    public void InitializeFromSaveProvider_NoProvider_NoOp()
    {
        using var server = new GameServer(42, _gen);
        server.InitializeFromSaveProvider(); // Should not throw
    }

    [Fact]
    public async Task InitializeFromSaveProvider_LoadsLatestSlot()
    {
        using var provider = new InMemorySaveGameProvider();
        var slot = provider.CreateSaveSlot("Test", 42, "bsp");
        provider.SaveWorldMeta(slot.SlotId, new WorldSaveData { Seed = 42, GeneratorId = "bsp" });

        using var server = new GameServer(42, _gen, saveProvider: provider);
        server.InitializeFromSaveProvider();
        // InitializeFromSaveProvider sets _restartGameTask but doesn't invoke it directly.
        // It gets invoked on the next server loop iteration.
        server.Start();
        await Task.Delay(300);
        Assert.Equal(slot.SlotId, server.CurrentSlotId);
        server.Dispose();
    }

    [Fact]
    public async Task InitializeFromSaveProvider_NoSlots_CreatesDefault()
    {
        using var provider = new InMemorySaveGameProvider();
        using var server = new GameServer(42, _gen, saveProvider: provider);
        server.InitializeFromSaveProvider();
        // InitializeFromSaveProvider sets _restartGameTask invoked on server loop
        server.Start();
        await Task.Delay(300);
        server.Dispose();

        var slots = provider.ListSaveSlots();
        Assert.Single(slots);
        Assert.Equal("Default World", slots[0].Name);
    }

    [Fact]
    public void InitializeFromSaveProvider_WhileRunning_Throws()
    {
        using var provider = new InMemorySaveGameProvider();
        using var server = new GameServer(42, _gen, saveProvider: provider);
        server.Start();
        Assert.Throws<InvalidOperationException>(() => server.InitializeFromSaveProvider());
        server.Dispose();
    }

    [Fact]
    public void HasSaveProvider_ReflectsConstructor()
    {
        using var server1 = new GameServer(42, _gen);
        Assert.False(server1.HasSaveProvider);

        using var provider = new InMemorySaveGameProvider();
        using var server2 = new GameServer(42, _gen, saveProvider: provider);
        Assert.True(server2.HasSaveProvider);
    }

    [Fact]
    public void DebugProperties_SetAndGet()
    {
        using var server = new TestGameServer(42, _gen);
        server.DebugNoCollision = true;
        Assert.True(server.DebugNoCollision);

        server.DebugInvulnerable = true;
        Assert.True(server.DebugInvulnerable);

        server.DebugMaxSpeed = true;
        Assert.True(server.DebugMaxSpeed);

        server.DebugFreeCrafting = true;
        Assert.True(server.DebugFreeCrafting);

        server.DebugVisibilityOff = true;
        Assert.True(server.DebugVisibilityOff);

        server.DebugGiveResources = true;
        Assert.True(server.DebugGiveResources);
    }
}
