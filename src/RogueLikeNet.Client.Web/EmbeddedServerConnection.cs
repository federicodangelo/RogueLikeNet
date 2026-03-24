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

    public bool IsConnected => _connected;

    public event Action<WorldSnapshotMsg>? OnWorldSnapshot;
    public event Action<WorldDeltaMsg>? OnWorldDelta;
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
            snapshot.PlayerX = pos.X;
            snapshot.PlayerY = pos.Y;
            snapshot.PlayerEntityId = _playerEntity.Id;

            var (cx, cy) = Chunk.WorldToChunkCoord(pos.X, pos.Y);
            var chunks = new List<ChunkDataMsg>();
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                chunks.Add(SerializeChunk(_engine.EnsureChunkLoaded(cx + dx, cy + dy)));
            snapshot.Chunks = chunks.ToArray();

            var entities = new List<EntityMsg>();
            var query = new Arch.Core.QueryDescription().WithAll<Position, TileAppearance>();
            _engine.EcsWorld.Query(in query, (Arch.Core.Entity e, ref Position ePos, ref TileAppearance app) =>
            {
                var msg = new EntityMsg { Id = e.Id, X = ePos.X, Y = ePos.Y, GlyphId = app.GlyphId, FgColor = app.FgColor };
                if (_engine.EcsWorld.Has<Health>(e))
                {
                    ref var h = ref _engine.EcsWorld.Get<Health>(e);
                    msg.Health = h.Current;
                    msg.MaxHealth = h.Max;
                }
                entities.Add(msg);
            });
            snapshot.Entities = entities.ToArray();
        }
        return snapshot;
    }

    private WorldDeltaMsg BuildDelta()
    {
        var delta = new WorldDeltaMsg { FromTick = _engine.CurrentTick - 1, ToTick = _engine.CurrentTick };
        var entities = new List<EntityUpdateMsg>();
        var query = new Arch.Core.QueryDescription().WithAll<Position, TileAppearance>();
        _engine.EcsWorld.Query(in query, (Arch.Core.Entity e, ref Position ePos, ref TileAppearance app) =>
        {
            var u = new EntityUpdateMsg { Id = e.Id, X = ePos.X, Y = ePos.Y, GlyphId = app.GlyphId, FgColor = app.FgColor };
            if (_engine.EcsWorld.Has<Health>(e))
            {
                ref var h = ref _engine.EcsWorld.Get<Health>(e);
                u.Health = h.Current;
                u.MaxHealth = h.Max;
            }
            entities.Add(u);
        });
        delta.EntityUpdates = entities.ToArray();

        var combatEvents = _engine.Combat.LastTickEvents;
        if (combatEvents.Count > 0)
        {
            delta.CombatEvents = combatEvents.Select(ev => new CombatEventMsg
            {
                AttackerX = ev.AttackerX, AttackerY = ev.AttackerY,
                TargetX = ev.TargetX, TargetY = ev.TargetY,
                Damage = ev.Damage, TargetDied = ev.TargetDied,
            }).ToArray();
        }
        return delta;
    }

    private static ChunkDataMsg SerializeChunk(Chunk chunk)
    {
        int total = Chunk.Size * Chunk.Size;
        var msg = new ChunkDataMsg
        {
            ChunkX = chunk.ChunkX, ChunkY = chunk.ChunkY,
            TileTypes = new byte[total], TileGlyphs = new int[total],
            TileFgColors = new int[total], TileBgColors = new int[total],
            TileLightLevels = new int[total],
        };
        for (int x = 0; x < Chunk.Size; x++)
        for (int y = 0; y < Chunk.Size; y++)
        {
            int idx = y * Chunk.Size + x;
            ref var tile = ref chunk.Tiles[x, y];
            msg.TileTypes[idx] = (byte)tile.Type;
            msg.TileGlyphs[idx] = tile.GlyphId;
            msg.TileFgColors[idx] = tile.FgColor;
            msg.TileBgColors[idx] = tile.BgColor;
            msg.TileLightLevels[idx] = tile.LightLevel;
        }
        return msg;
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
