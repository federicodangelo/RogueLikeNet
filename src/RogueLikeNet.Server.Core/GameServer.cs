using System.Collections.Concurrent;
using System.Diagnostics;
using RogueLikeNet.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Protocol;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Server;

/// <summary>
/// The authoritative game server. Runs at 20 ticks/sec.
/// Processes client inputs, runs game systems, broadcasts deltas.
/// </summary>
public class GameServer : IDisposable
{
    private const int TickRateMs = 50; // 20 ticks/sec
    protected readonly GameEngine _engine;
    private readonly ConcurrentDictionary<long, PlayerConnection> _connections = new();
    private readonly ConcurrentQueue<Func<Task>> _commandQueue = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _serverTask;
    private long _nextConnectionId = 1;
    private readonly TextWriter _logWriter;

    public bool IsRunning => _serverTask != null && !_serverTask.IsCompleted;
    public int ConnectionCount => _connections.Count;

    public GameServer(long worldSeed, IDungeonGenerator? generator = null, TextWriter? logWriter = null)
    {
        _logWriter = logWriter ?? TextWriter.Null;

        _engine = new GameEngine(worldSeed, generator);
        // Pre-generate spawn chunk
        _engine.EnsureChunkLoaded(0, 0);
    }

    public void Start()
    {
        if (IsRunning)
            throw new InvalidOperationException("Server is already running");

        _serverTask = Task.Run(() => RunServer(_cts.Token));
    }

    public PlayerConnection AddConnection(Func<byte[], Task> sendFunc)
    {
        long id = Interlocked.Increment(ref _nextConnectionId);
        var conn = new PlayerConnection(id, sendFunc);
        if (!_connections.TryAdd(id, conn))
            throw new Exception("Failed to add connection");
        return conn;
    }

    public void RemoveConnection(long connectionId)
    {
        if (_connections.TryRemove(connectionId, out var conn))
        {
            EnqueueCommand(() =>
            {
                if (conn.PlayerEntity.HasValue)
                {
                    var entity = conn.PlayerEntity.Value;
                    if (_engine.EcsWorld.IsAlive(entity))
                        _engine.EcsWorld.Destroy(entity);
                }
                return Task.CompletedTask;
            });
        }
    }

    public void EnqueueInput(long connectionId, ClientInputMsg input)
    {
        if (_connections.TryGetValue(connectionId, out var conn))
            conn.InputQueue.Enqueue(input);
    }

    public void BroadcastChat(long senderConnectionId, string text)
    {
        EnqueueCommand(() => BroadcastChatInternal(senderConnectionId, text));
    }

    private async Task BroadcastChatInternal(long senderConnectionId, string text)
    {
        string senderName = "Player";
        if (_connections.TryGetValue(senderConnectionId, out var sender))
            senderName = sender.PlayerName.Length > 0 ? sender.PlayerName : $"Player {senderConnectionId}";
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
            try
            {
                await conn.SendAsync(data);
            }
            catch
            {
                /* connection closing */
            }
        }
    }

    public void SpawnPlayerForConnection(long connectionId, int classId = 0, string playerName = "")
    {
        EnqueueCommand(() => SpawnPlayerForConnectionInternal(connectionId, classId, playerName));
    }

    private async Task SpawnPlayerForConnectionInternal(long connectionId, int classId, string playerName)
    {
        if (!_connections.TryGetValue(connectionId, out var conn)) return;

        conn.PlayerName = playerName;

        var (spawnX, spawnY) = _engine.FindSpawnPosition();
        var entity = _engine.SpawnPlayer(connectionId, spawnX, spawnY, classId);
        conn.PlayerEntity = entity;

        // Run a tick so lighting is computed before the initial snapshot
        _engine.Tick();

        // Reset per-connection tracking so BuildDelta treats this as a full snapshot
        conn.LastSentEntities.Clear();
        conn.SentChunkKeys.Clear();
        conn.LastSentHudBytes = null;
        conn.LastAckedTick = 0;

        var snapshot = BuildDelta(conn, isSnapshot: true);
        var payload = NetSerializer.Serialize(snapshot);
        var data = NetSerializer.WrapMessage(MessageTypes.WorldDelta, payload);
        await conn.SendAsync(data);
    }

    /// <summary>
    /// Enqueues a command for execution on the server thread.
    /// If the server loop is not running (e.g. in tests), executes immediately.
    /// </summary>
    private void EnqueueCommand(Func<Task> command)
    {
        if (IsRunning)
            _commandQueue.Enqueue(command);
        else
            command().GetAwaiter().GetResult();
    }

    private async Task ProcessCommandsAsync()
    {
        while (_commandQueue.TryDequeue(out var command))
            await command();
    }

    private async Task RunServer(CancellationToken ct)
    {
        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(TickRateMs));
        var statsStopwatch = Stopwatch.StartNew();
        long lastTotalSent = 0;
        long lastTotalRecv = 0;

        _logWriter.WriteLine("[Server] Starting server loop...");

        try
        {
            while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct))
            {
                await ProcessCommandsAsync();
                ProcessInputs();
                _engine.Tick();
                await BroadcastDeltas();

                if (statsStopwatch.ElapsedMilliseconds >= 5000)
                {
                    long totalSent = 0, totalRecv = 0;
                    foreach (var c in _connections.Values)
                    {
                        totalSent += c.BytesSent;
                        totalRecv += c.BytesReceived;
                    }
                    double elapsed = statsStopwatch.Elapsed.TotalSeconds;
                    double sentKBps = (totalSent - lastTotalSent) / 1024.0 / elapsed;
                    double recvKBps = (totalRecv - lastTotalRecv) / 1024.0 / elapsed;
                    lastTotalSent = totalSent;
                    lastTotalRecv = totalRecv;

                    _logWriter.WriteLine($"[Server] tick={_engine.CurrentTick} players={_connections.Count} out={sentKBps:F1}KB/s in={recvKBps:F1}KB/s");
                    statsStopwatch.Restart();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (Exception ex)
        {
            _logWriter.WriteLine("[Server] Exception in server loop: " + ex);
        }
        finally
        {
            _logWriter.WriteLine("[Server] Shutting down server loop...");
        }
    }

    private void ProcessInputs()
    {
        foreach (var conn in _connections.Values)
        {
            if (!conn.PlayerEntity.HasValue) continue;
            if (!_engine.EcsWorld.IsAlive(conn.PlayerEntity.Value)) continue;

            // Drain all queued inputs; keep only the latest one
            ClientInputMsg? latestInput = null;
            while (conn.InputQueue.TryDequeue(out var queued))
                latestInput = queued;

            if (latestInput != null)
            {
                var input = latestInput;
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

    private WorldDeltaMsg BuildDelta(PlayerConnection conn, bool isSnapshot = false)
    {
        var playerEntity = conn.PlayerEntity!.Value;
        ref var playerPos = ref _engine.EcsWorld.Get<Position>(playerEntity);
        ref var fov = ref _engine.EcsWorld.Get<FOVData>(playerEntity);

        var newChunks = GameStateSerializer.SerializeChunksDelta(
            _engine, playerPos.X, playerPos.Y, conn.SentChunkKeys);

        // State delta compression: always send on snapshot, otherwise only when changed
        var state = GameStateSerializer.BuildPlayerState(_engine, playerEntity);
        byte[]? stateBytes = state != null ? NetSerializer.Serialize(state) : null;
        PlayerStateMsg? deltaState;
        if (stateBytes != null && conn.LastSentHudBytes != null
            && stateBytes.AsSpan().SequenceEqual(conn.LastSentHudBytes))
        {
            deltaState = null;
        }
        else
        {
            deltaState = state;
            conn.LastSentHudBytes = stateBytes;
        }

        var serializedEntityData = GameStateSerializer.SerializeEntityDelta(_engine.EcsWorld, fov, conn.LastSentEntities);

        return new WorldDeltaMsg
        {
            FromTick = conn.LastAckedTick,
            ToTick = _engine.CurrentTick,
            IsSnapshot = isSnapshot,
            Chunks = newChunks,
            EntityUpdates = serializedEntityData.FullUpdates,
            EntityPositionHealthUpdates = serializedEntityData.PositionHealthUpdates,
            EntityRemovals = serializedEntityData.Removals,
            CombatEvents = GameStateSerializer.SerializeCombatEvents(_engine),
            PlayerState = deltaState,
        };
    }

    public void Dispose()
    {
        if (!IsRunning)
        {
            return;
        }

        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            _logWriter.WriteLine("[Server] Cancellation token already disposed");
        }

        try
        {
            _serverTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException e)
        {
            _logWriter.WriteLine("[Server] Server task did not shut down gracefully: " + e.ToString());
        }

        _engine.Dispose();

        try
        {
            _cts.Dispose();
        }
        catch (ObjectDisposedException)
        {
            _logWriter.WriteLine("[Server] Cancellation token already disposed");
        }
    }
}
