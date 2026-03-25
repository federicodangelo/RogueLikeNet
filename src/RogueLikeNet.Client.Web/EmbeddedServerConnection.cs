using RogueLikeNet.Client.Core.Networking;
using RogueLikeNet.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.World;
using RogueLikeNet.Protocol;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Web;

/// <summary>
/// In-browser local game loop + connection for offline WASM mode.
/// Runs the game engine directly (no ASP.NET server).
/// </summary>
public class LocalGameConnection : IGameServerConnection
{
    private readonly GameEngine _engine;
    private Arch.Core.Entity _playerEntity;
    private CancellationTokenSource? _loopCts;
    private bool _connected;
    private readonly Dictionary<long, EntitySnapshot> _lastSentEntities = new();
    private readonly HashSet<long> _sentChunkKeys = new();

    public bool IsConnected => _connected;
    public long BytesSent => 0;
    public long BytesReceived => 0;

    public event Action<WorldSnapshotMsg>? OnWorldSnapshot;
    public event Action<WorldDeltaMsg>? OnWorldDelta;
    public event Action<ChatMsg>? OnChatReceived;
    public event Action? OnDisconnected;

    public LocalGameConnection(long seed)
    {
        _engine = new GameEngine(seed);
        _engine.EnsureChunkLoaded(0, 0);
    }

    public Task ConnectAsync(string uri, CancellationToken ct = default)
    {
        _connected = true;

        var (sx, sy) = _engine.FindSpawnPosition();
        _playerEntity = _engine.SpawnPlayer(1, sx, sy);

        // Run a tick so lighting is computed before the initial snapshot
        _engine.Tick();

        // Send initial snapshot
        var snapshot = BuildSnapshot();
        OnWorldSnapshot?.Invoke(snapshot);

        // Start the local game loop
        _loopCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = Task.Run(() => RunLoop(_loopCts.Token));

        return Task.CompletedTask;
    }

    public Task SendInputAsync(ClientInputMsg input, CancellationToken ct = default)
    {
        if (!_connected || !_engine.EcsWorld.IsAlive(_playerEntity)) return Task.CompletedTask;

        ref var playerInput = ref _engine.EcsWorld.Get<PlayerInput>(_playerEntity);
        playerInput.ActionType = input.ActionType;
        playerInput.TargetX = input.TargetX;
        playerInput.TargetY = input.TargetY;
        playerInput.ItemSlot = input.ItemSlot;
        playerInput.TargetSlot = input.TargetSlot;

        return Task.CompletedTask;
    }

    public Task SendChatAsync(string text, CancellationToken ct = default)
    {
        // Single-player: no chat target
        return Task.CompletedTask;
    }

    private async Task RunLoop(CancellationToken ct)
    {
        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50)); // 20 ticks/sec
        while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                _engine.Tick();
                var delta = BuildDelta();
                OnWorldDelta?.Invoke(delta);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { Console.Error.WriteLine($"Local loop error: {ex.Message}"); }
        }
    }

    private WorldSnapshotMsg BuildSnapshot()
    {
        var snapshot = new WorldSnapshotMsg { WorldTick = _engine.CurrentTick };

        if (_engine.EcsWorld.IsAlive(_playerEntity))
        {
            ref var pos = ref _engine.EcsWorld.Get<Position>(_playerEntity);
            ref var fov = ref _engine.EcsWorld.Get<FOVData>(_playerEntity);
            snapshot.PlayerX = pos.X;
            snapshot.PlayerY = pos.Y;
            snapshot.PlayerEntityId = _playerEntity.Id;
            snapshot.Chunks = GameStateSerializer.SerializeChunksAroundPosition(_engine, pos.X, pos.Y);
            snapshot.Entities = GameStateSerializer.SerializeEntities(_engine.EcsWorld, fov);

            _lastSentEntities.Clear();
            foreach (var e in snapshot.Entities)
                _lastSentEntities[e.Id] = new EntitySnapshot(e.X, e.Y, e.GlyphId, e.FgColor, e.Health, e.MaxHealth, e.LightRadius);

            // Seed chunk tracking
            _sentChunkKeys.Clear();
            var (cx, cy) = Chunk.WorldToChunkCoord(pos.X, pos.Y);
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                int ccx = cx + dx, ccy = cy + dy;
                long key = Chunk.PackChunkKey(ccx, ccy);
                _sentChunkKeys.Add(key);
            }
        }

        snapshot.PlayerHud = GameStateSerializer.BuildPlayerHud(_engine, _playerEntity);
        return snapshot;
    }

    private WorldDeltaMsg BuildDelta()
    {
        var delta = new WorldDeltaMsg { FromTick = _engine.CurrentTick - 1, ToTick = _engine.CurrentTick };

        if (_engine.EcsWorld.IsAlive(_playerEntity))
        {
            ref var playerPos = ref _engine.EcsWorld.Get<Position>(_playerEntity);
            ref var fov = ref _engine.EcsWorld.Get<FOVData>(_playerEntity);
            var newChunks = GameStateSerializer.SerializeChunksDelta(
                _engine, playerPos.X, playerPos.Y, _sentChunkKeys);
            delta.Chunks = newChunks;
            delta.EntityUpdates = GameStateSerializer.SerializeEntityUpdatesDelta(_engine.EcsWorld, fov, _lastSentEntities);
        }

        delta.CombatEvents = GameStateSerializer.SerializeCombatEvents(_engine);
        delta.PlayerHud = GameStateSerializer.BuildPlayerHud(_engine, _playerEntity);
        return delta;
    }

    public ValueTask DisposeAsync()
    {
        _connected = false;
        _loopCts?.Cancel();
        _engine.Dispose();
        OnDisconnected?.Invoke();
        return ValueTask.CompletedTask;
    }
}
