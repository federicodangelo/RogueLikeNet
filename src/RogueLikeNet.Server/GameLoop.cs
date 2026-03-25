using System.Collections.Concurrent;
using Arch.Core;
using RogueLikeNet.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Protocol;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Server;

/// <summary>
/// The authoritative game loop. Runs at 20 ticks/sec.
/// Processes client inputs, runs game systems, broadcasts deltas.
/// </summary>
public class GameLoop : IDisposable
{
    private const int TickRateMs = 50; // 20 ticks/sec

    private readonly GameEngine _engine;
    private readonly ConcurrentDictionary<long, PlayerConnection> _connections = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _loopTask;
    private long _nextConnectionId = 1;

    public GameEngine Engine => _engine;
    public bool IsRunning => _loopTask != null && !_loopTask.IsCompleted;

    public GameLoop(long worldSeed)
    {
        _engine = new GameEngine(worldSeed);
        // Pre-generate spawn chunk
        _engine.EnsureChunkLoaded(0, 0);
    }

    public void Start()
    {
        _loopTask = Task.Run(() => RunLoop(_cts.Token));
    }

    public PlayerConnection AddConnection(Func<byte[], Task> sendFunc)
    {
        long id = Interlocked.Increment(ref _nextConnectionId);
        var conn = new PlayerConnection(id, sendFunc);
        _connections[id] = conn;
        return conn;
    }

    public void RemoveConnection(long connectionId)
    {
        if (_connections.TryRemove(connectionId, out var conn) && conn.PlayerEntity.HasValue)
        {
            // Remove player entity when they disconnect
            if (_engine.EcsWorld.IsAlive(conn.PlayerEntity.Value))
                _engine.EcsWorld.Destroy(conn.PlayerEntity.Value);
        }
    }

    public void EnqueueInput(long connectionId, ClientInputMsg input)
    {
        if (_connections.TryGetValue(connectionId, out var conn))
            conn.InputQueue.Enqueue(input);
    }

    public async Task BroadcastChat(long senderConnectionId, string text)
    {
        string senderName = $"Player {senderConnectionId}";
        var chat = new ChatMsg
        {
            SenderId = senderConnectionId,
            SenderName = senderName,
            Text = text,
            Timestamp = _engine.CurrentTick,
        };
        var payload = NetSerializer.Serialize(chat);
        var data = NetSerializer.WrapMessage(MessageTypes.ChatReceive, payload);

        foreach (var conn in _connections.Values)
        {
            try { await conn.SendAsync(data); }
            catch { /* connection closing */ }
        }
    }

    public async Task SpawnPlayerForConnection(long connectionId)
    {
        if (!_connections.TryGetValue(connectionId, out var conn)) return;

        var (spawnX, spawnY) = _engine.FindSpawnPosition();
        var entity = _engine.SpawnPlayer(connectionId, spawnX, spawnY);
        conn.PlayerEntity = entity;

        // Run a tick so lighting is computed before the initial snapshot
        _engine.Tick();

        // Send initial world snapshot
        var snapshot = BuildSnapshot(conn);
        var payload = NetSerializer.Serialize(snapshot);
        var data = NetSerializer.WrapMessage(MessageTypes.WorldSnapshot, payload);
        await conn.SendAsync(data);
    }

    private async Task RunLoop(CancellationToken ct)
    {
        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(TickRateMs));
        while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct))
        {
            ProcessInputs();
            _engine.Tick();
            await BroadcastDeltas();
        }
    }

    private void ProcessInputs()
    {
        foreach (var conn in _connections.Values)
        {
            if (!conn.PlayerEntity.HasValue) continue;
            if (!_engine.EcsWorld.IsAlive(conn.PlayerEntity.Value)) continue;

            // Process only ONE input per tick to prevent action skipping
            if (conn.InputQueue.TryDequeue(out var input))
            {
                ref var playerInput = ref _engine.EcsWorld.Get<PlayerInput>(conn.PlayerEntity.Value);
                playerInput.ActionType = input.ActionType;
                playerInput.TargetX = input.TargetX;
                playerInput.TargetY = input.TargetY;
                playerInput.ItemSlot = input.ItemSlot;
                playerInput.TargetSlot = input.TargetSlot;
            }
        }
    }

    private async Task BroadcastDeltas()
    {
        foreach (var conn in _connections.Values)
        {
            if (!conn.PlayerEntity.HasValue) continue;
            if (!_engine.EcsWorld.IsAlive(conn.PlayerEntity.Value)) continue;

            try
            {
                var delta = BuildDelta(conn);
                var payload = NetSerializer.Serialize(delta);
                var data = NetSerializer.WrapMessage(MessageTypes.WorldDelta, payload);
                await conn.SendAsync(data);
                conn.LastAckedTick = _engine.CurrentTick;
            }
            catch
            {
                // Connection likely closed — will be cleaned up
            }
        }
    }

    private WorldSnapshotMsg BuildSnapshot(PlayerConnection conn)
    {
        // Caller (SpawnPlayerForConnection) guarantees conn.PlayerEntity is set and alive
        var entity = conn.PlayerEntity!.Value;
        ref var pos = ref _engine.EcsWorld.Get<Position>(entity);
        ref var fov = ref _engine.EcsWorld.Get<FOVData>(entity);

        var snapshot = new WorldSnapshotMsg
        {
            WorldTick = _engine.CurrentTick,
            PlayerX = pos.X,
            PlayerY = pos.Y,
        };

        snapshot.Chunks = GameStateSerializer.SerializeChunksAroundPosition(_engine, pos.X, pos.Y);
        snapshot.Entities = GameStateSerializer.SerializeEntities(_engine.EcsWorld, fov);
        snapshot.PlayerHud = GameStateSerializer.BuildPlayerHud(_engine, entity);

        // Seed delta tracking from snapshot so first delta is already compressed
        conn.LastSentEntities.Clear();
        foreach (var e in snapshot.Entities)
            conn.LastSentEntities[e.Id] = new EntitySnapshot(e.X, e.Y, e.GlyphId, e.FgColor, e.Health, e.MaxHealth);

        return snapshot;
    }

    private WorldDeltaMsg BuildDelta(PlayerConnection conn)
    {
        // Caller (BroadcastDeltas) guarantees conn.PlayerEntity is set and alive
        var playerEntity = conn.PlayerEntity!.Value;
        ref var playerPos = ref _engine.EcsWorld.Get<Position>(playerEntity);
        ref var fov = ref _engine.EcsWorld.Get<FOVData>(playerEntity);

        var delta = new WorldDeltaMsg
        {
            FromTick = conn.LastAckedTick,
            ToTick = _engine.CurrentTick,
            Chunks = GameStateSerializer.SerializeChunksAroundPosition(_engine, playerPos.X, playerPos.Y),
            EntityUpdates = GameStateSerializer.SerializeEntityUpdatesDelta(_engine.EcsWorld, fov, conn.LastSentEntities),
            CombatEvents = GameStateSerializer.SerializeCombatEvents(_engine),
            PlayerHud = GameStateSerializer.BuildPlayerHud(_engine, playerEntity)
        };
        return delta;
    }

    public void Dispose()
    {
        try { _cts.Cancel(); } catch (ObjectDisposedException) { }
        try { _loopTask?.Wait(TimeSpan.FromSeconds(2)); } catch (AggregateException) { }
        _engine.Dispose();
        try { _cts.Dispose(); } catch (ObjectDisposedException) { }
    }
}
