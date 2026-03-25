using System.Collections.Concurrent;
using Arch.Core;
using RogueLikeNet.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;
using RogueLikeNet.Protocol;
using RogueLikeNet.Protocol.Messages;
using Chunk = RogueLikeNet.Core.World.Chunk;

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

            while (conn.InputQueue.TryDequeue(out var input))
            {
                ref var playerInput = ref _engine.EcsWorld.Get<PlayerInput>(conn.PlayerEntity.Value);
                playerInput.ActionType = input.ActionType;
                playerInput.TargetX = input.TargetX;
                playerInput.TargetY = input.TargetY;
                playerInput.ItemSlot = input.ItemSlot;
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

        var snapshot = new WorldSnapshotMsg
        {
            WorldTick = _engine.CurrentTick,
            PlayerX = pos.X,
            PlayerY = pos.Y,
        };

        // Load chunks around player
        var (cx, cy) = Chunk.WorldToChunkCoord(pos.X, pos.Y);
        var chunks = new List<ChunkDataMsg>();
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            var chunk = _engine.EnsureChunkLoaded(cx + dx, cy + dy);
            chunks.Add(SerializeChunk(chunk));
        }
        snapshot.Chunks = chunks.ToArray();

        // Serialize visible entities
        var entities = new List<EntityMsg>();
        var entityQuery = new QueryDescription().WithAll<Position, TileAppearance>();
        _engine.EcsWorld.Query(in entityQuery, (Entity e, ref Position ePos, ref TileAppearance appearance) =>
        {
            var eMsg = new EntityMsg
            {
                Id = e.Id,
                X = ePos.X,
                Y = ePos.Y,
                GlyphId = appearance.GlyphId,
                FgColor = appearance.FgColor,
            };
            if (_engine.EcsWorld.Has<Health>(e))
            {
                ref var health = ref _engine.EcsWorld.Get<Health>(e);
                eMsg.Health = health.Current;
                eMsg.MaxHealth = health.Max;
            }
            entities.Add(eMsg);
        });
        snapshot.Entities = entities.ToArray();

        snapshot.PlayerHud = BuildPlayerHud(conn);
        return snapshot;
    }

    private WorldDeltaMsg BuildDelta(PlayerConnection conn)
    {
        // Caller (BroadcastDeltas) guarantees conn.PlayerEntity is set and alive
        var playerEntity = conn.PlayerEntity!.Value;
        ref var playerPos = ref _engine.EcsWorld.Get<Position>(playerEntity);

        var delta = new WorldDeltaMsg
        {
            FromTick = conn.LastAckedTick,
            ToTick = _engine.CurrentTick,
        };

        // Include chunks around the player with updated light levels
        var (cx, cy) = Chunk.WorldToChunkCoord(playerPos.X, playerPos.Y);
        var chunks = new List<ChunkDataMsg>();
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            var chunk = _engine.EnsureChunkLoaded(cx + dx, cy + dy);
            chunks.Add(SerializeChunk(chunk));
        }
        delta.Chunks = chunks.ToArray();

        // Entity updates (send all visible entities each tick for now; optimize later with true delta tracking)
        var entities = new List<EntityUpdateMsg>();
        var entityQuery = new QueryDescription().WithAll<Position, TileAppearance>();
        _engine.EcsWorld.Query(in entityQuery, (Entity entity, ref Position ePos, ref TileAppearance appearance) =>
        {
            var update = new EntityUpdateMsg
            {
                Id = entity.Id,
                X = ePos.X,
                Y = ePos.Y,
                GlyphId = appearance.GlyphId,
                FgColor = appearance.FgColor,
            };
            if (_engine.EcsWorld.Has<Health>(entity))
            {
                ref var health = ref _engine.EcsWorld.Get<Health>(entity);
                update.Health = health.Current;
                update.MaxHealth = health.Max;
            }
            entities.Add(update);
        });
        delta.EntityUpdates = entities.ToArray();

        // Combat events
        var combatEvents = _engine.Combat.LastTickEvents;
        if (combatEvents.Count > 0)
        {
            delta.CombatEvents = combatEvents.Select(e => new CombatEventMsg
            {
                AttackerX = e.AttackerX,
                AttackerY = e.AttackerY,
                TargetX = e.TargetX,
                TargetY = e.TargetY,
                Damage = e.Damage,
                TargetDied = e.TargetDied,
            }).ToArray();
        }

        delta.PlayerHud = BuildPlayerHud(conn);
        return delta;
    }

    private PlayerHudMsg? BuildPlayerHud(PlayerConnection conn)
    {
        if (!conn.PlayerEntity.HasValue || !_engine.EcsWorld.IsAlive(conn.PlayerEntity.Value)) return null;
        var hudData = _engine.GetPlayerHudData(conn.PlayerEntity.Value);
        return new PlayerHudMsg
        {
            Health = hudData.Health,
            MaxHealth = hudData.MaxHealth,
            Attack = hudData.Attack,
            Defense = hudData.Defense,
            Level = hudData.Level,
            Experience = hudData.Experience,
            InventoryCount = hudData.InventoryCount,
            InventoryCapacity = hudData.InventoryCapacity,
            SkillIds = hudData.SkillIds,
            SkillCooldowns = hudData.SkillCooldowns,
            InventoryNames = hudData.InventoryNames,
        };
    }

    private static ChunkDataMsg SerializeChunk(Chunk chunk)
    {
        int total = Chunk.Size * Chunk.Size;
        var msg = new ChunkDataMsg
        {
            ChunkX = chunk.ChunkX,
            ChunkY = chunk.ChunkY,
            TileTypes = new byte[total],
            TileGlyphs = new int[total],
            TileFgColors = new int[total],
            TileBgColors = new int[total],
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

    public void Dispose()
    {
        try { _cts.Cancel(); } catch (ObjectDisposedException) { }
        try { _loopTask?.Wait(TimeSpan.FromSeconds(2)); } catch (AggregateException) { }
        _engine.Dispose();
        try { _cts.Dispose(); } catch (ObjectDisposedException) { }
    }
}
