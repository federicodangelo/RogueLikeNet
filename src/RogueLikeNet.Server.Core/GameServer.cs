using System.Collections.Concurrent;
using System.Diagnostics;
using RogueLikeNet.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;
using RogueLikeNet.Protocol;
using RogueLikeNet.Protocol.Messages;
using RogueLikeNet.Server.Persistence;

namespace RogueLikeNet.Server;

/// <summary>
/// The authoritative game server. Runs at 20 ticks/sec.
/// Processes client inputs, runs game systems, broadcasts deltas.
/// Supports persistence: auto-save modified chunks, player state, and chunk unloading.
/// </summary>
public class GameServer : IDisposable
{
    public const int TickRateMs = 1000 / TicksPerSecond;
    public const int TicksPerSecond = 20;

    private const int MaxChunksPerTick = 4;
    private const int MinTicksBetweenChunkResend = 10;

    /// <summary>Auto-save every 5 seconds (100 ticks at 20 tps).</summary>
    private const int AutoSaveIntervalTicks = 5 * TicksPerSecond;

    /// <summary>Unload chunks not viewed by any player for 30 seconds (600 ticks).</summary>
    private const int ChunkUnloadIdleTicks = 30 * TicksPerSecond;

    protected GameEngine _engine;
    private bool _engineStarted;

    private readonly ConcurrentDictionary<long, PlayerConnection> _connections = new();
    private readonly ConcurrentQueue<Func<Task>> _commandQueue = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _serverTask;
    private long _nextConnectionId = 1;
    private readonly TextWriter _logWriter;
    private TileUpdateMsg[] _pendingTileUpdates = [];
    private Action? _restartGameTask;

    // Persistence
    private readonly ISaveGameProvider? _saveProvider;
    private string? _currentSlotId;
    private long _lastSaveTick;
    private readonly Dictionary<long, long> _chunkLastViewedTick = new();

    public bool IsRunning => _serverTask != null && !_serverTask.IsCompleted;
    public int ConnectionCount => _connections.Count;
    public string? CurrentSlotId => _currentSlotId;
    public bool HasSaveProvider => _saveProvider != null;

    /// <summary>Debug: when true, player movement ignores tile collision.</summary>
    public bool DebugNoCollision
    {
        get => _engine.DebugNoCollision;
        set => _engine.DebugNoCollision = value;
    }

    /// <summary>Debug: when true, player cannot take damage.</summary>
    public bool DebugInvulnerable
    {
        get => _engine.DebugInvulnerable;
        set => _engine.DebugInvulnerable = value;
    }

    /// <summary>Debug: when true, player has zero move/attack delay.</summary>
    public bool DebugMaxSpeed
    {
        get => _engine.DebugMaxSpeed;
        set => _engine.DebugMaxSpeed = value;
    }

    /// <summary>Debug: when true, all tiles are treated as visible (no FOV).</summary>
    public bool DebugVisibilityOff { get; set; } = false;

    /// <summary>Debug: when true, newly spawned players receive 9999 of each resource.</summary>
    public bool DebugGiveResources { get; set; } = false;


    private readonly long _worldSeed;

    public GameServer(long worldSeed, IDungeonGenerator? generator = null, TextWriter? logWriter = null, ISaveGameProvider? saveProvider = null)
    {
        _worldSeed = worldSeed;
        _logWriter = logWriter ?? TextWriter.Null;
        _saveProvider = saveProvider;

        _engine = new GameEngine(worldSeed, generator);
        StartEngine();
    }

    public void Start()
    {
        if (IsRunning)
            throw new InvalidOperationException("Server is already running");

        _serverTask = Task.Run(() => RunServer(_cts.Token));
    }

    /// <summary>
    /// Initializes a new game in a save slot. Must be called before Start().
    /// </summary>
    public void InitializeNewGame(string slotName, long seed, string generatorId)
    {
        if (IsRunning) throw new InvalidOperationException("Cannot initialize while server is running. Use HandleSaveGameCommand instead.");
        StartNewGame(slotName, seed, generatorId);
        _restartGameTask?.Invoke();
        _restartGameTask = null;
    }

    /// <summary>
    /// Initializes from an existing save slot. Must be called before Start().
    /// </summary>
    public void InitializeFromSlot(string slotId)
    {
        if (IsRunning) throw new InvalidOperationException("Cannot initialize while server is running. Use HandleSaveGameCommand instead.");
        LoadSaveSlot(slotId);
        _restartGameTask?.Invoke();
        _restartGameTask = null;
    }

    /// <summary>
    /// Auto-initializes from the save provider: loads the latest slot, or creates a default one.
    /// Must be called before Start().
    /// </summary>
    public void InitializeFromSaveProvider()
    {
        if (IsRunning) throw new InvalidOperationException("Cannot initialize while server is running.");
        if (_saveProvider == null) return;

        var slots = _saveProvider.ListSaveSlots();
        if (slots.Count > 0)
        {
            var latest = slots.OrderByDescending(s => s.LastSavedAt).First();
            LoadSaveSlot(latest.SlotId);
        }
        else
        {
            StartNewGame("Default World", _worldSeed, GeneratorRegistry.DefaultId);
        }
    }

    // ──────────────────────────────────────────────
    // Save slot management commands
    // ──────────────────────────────────────────────

    /// <summary>
    /// Creates a new game in a new save slot and replaces the current engine.
    /// Must be called before any players connect, or after all disconnect.
    /// </summary>
    private void StartNewGame(string slotName, long seed, string generatorId)
    {
        if (_saveProvider == null) return;

        _restartGameTask = () =>
        {
            StopEngineAndDispose();

            var slot = _saveProvider.CreateSaveSlot(slotName, seed, generatorId);
            _currentSlotId = slot.SlotId;

            var baseGenerator = GeneratorRegistry.Create(generatorId, seed);
            var persistentGen = new PersistentDungeonGenerator(baseGenerator, _saveProvider, _currentSlotId);

            _engine = new GameEngine(seed, persistentGen);
            persistentGen.SetEngine(_engine);
            StartEngine();

            _saveProvider.SaveWorldMeta(_currentSlotId, new WorldSaveData
            {
                Seed = seed,
                GeneratorId = generatorId,
                CurrentTick = 0,
            });

            _logWriter.WriteLine($"[Server] New game started: slot={slot.SlotId} name={slotName} seed={seed} gen={generatorId}");
        };
    }

    /// <summary>
    /// Loads a saved game from an existing slot, replacing the current engine.
    /// </summary>
    private void LoadSaveSlot(string slotId)
    {
        if (_saveProvider == null) return;

        _restartGameTask = () =>
        {
            StopEngineAndDispose();

            var slot = _saveProvider.GetSaveSlot(slotId);
            if (slot == null)
            {
                _logWriter.WriteLine($"[Server] Save slot not found: {slotId}");
                return;
            }

            var meta = _saveProvider.LoadWorldMeta(slotId);
            if (meta == null)
            {
                _logWriter.WriteLine($"[Server] World metadata not found for slot: {slotId}");
                return;
            }

            _currentSlotId = slotId;

            var baseGenerator = GeneratorRegistry.Create(meta.GeneratorId, meta.Seed);
            var persistentGen = new PersistentDungeonGenerator(baseGenerator, _saveProvider, _currentSlotId);

            _engine = new GameEngine(meta.Seed, persistentGen);
            persistentGen.SetEngine(_engine);
            StartEngine();

            _logWriter.WriteLine($"[Server] Loaded save: slot={slotId} name={slot.Name} seed={meta.Seed} gen={meta.GeneratorId} tick={meta.CurrentTick}");
        };
    }

    private void StopEngineAndDispose()
    {
        if (!_engineStarted) return;
        _logWriter.WriteLine($"[Server] Stopping game engine...");
        _engine.Dispose();
        _chunkLastViewedTick.Clear();
        _lastSaveTick = 0;
    }

    private void StartEngine()
    {
        _logWriter.WriteLine($"[Server] Starting game engine...");
        _engine.EnsureChunkLoaded(0, 0, Position.DefaultZ);
        _engineStarted = true;
    }

    /// <summary>Deletes a save slot.</summary>
    private void DeleteSaveSlot(string slotId)
    {
        if (_saveProvider == null) return;
        _saveProvider.DeleteSaveSlot(slotId);
        _logWriter.WriteLine($"[Server] Deleted save slot: {slotId}");
    }

    /// <summary>Manually triggers a save of the current game state.</summary>
    private void SaveCurrentGame()
    {
        if (_saveProvider == null || _currentSlotId == null) return;
        PerformAutoSave();
    }

    /// <summary>
    /// Processes a save game command from a client and sends the response.
    /// Enqueued on the server thread for thread-safety.
    /// </summary>
    public Task HandleSaveGameCommand(long connectionId, SaveGameCommandMsg cmd)
    {
        if (!_connections.TryGetValue(connectionId, out var _))
            throw new Exception($"Connection not found: {connectionId}");

        TaskCompletionSource tcs = new TaskCompletionSource();

        EnqueueCommand(async () =>
        {
            var response = ProcessSaveCommand(cmd);
            if (_connections.TryGetValue(connectionId, out var conn))
            {
                var payload = NetSerializer.Serialize(response);
                var data = NetSerializer.WrapMessage(MessageTypes.SaveGameResponse, payload);
                await conn.SendAsync(data);
            }
            if (_restartGameTask != null)
            {
                // Wait for restart to complete, we have to do this in another thread because otherwise we
                // would lock the server thread while waiting, which would prevent the restart from completing
                _ = Task.Run(async () =>
                {
                    while (_restartGameTask != null)
                        await Task.Delay(100);
                    tcs.SetResult();
                });
            }
            else
            {
                tcs.SetResult();
            }
        });

        return tcs.Task;
    }

    /// <summary>Processes a save command synchronously and returns the response.</summary>
    private SaveGameResponseMsg ProcessSaveCommand(SaveGameCommandMsg cmd)
    {
        if (_saveProvider == null)
            return new SaveGameResponseMsg { Action = cmd.Action, Success = false, Message = "No save provider configured" };

        try
        {
            string message;
            switch (cmd.Action)
            {
                case SaveGameAction.List:
                    message = "";
                    break;

                case SaveGameAction.New:
                    StartNewGame(cmd.SlotName, cmd.Seed, cmd.GeneratorId);
                    message = $"New game started: {cmd.SlotName}";
                    break;

                case SaveGameAction.Load:
                    LoadSaveSlot(cmd.SlotId);
                    message = $"Loaded save: {cmd.SlotId}";
                    break;

                case SaveGameAction.Delete:
                    DeleteSaveSlot(cmd.SlotId);
                    message = $"Deleted save: {cmd.SlotId}";
                    break;

                case SaveGameAction.Save:
                    SaveCurrentGame();
                    message = "Game saved";
                    break;

                default:
                    return new SaveGameResponseMsg { Action = cmd.Action, Success = false, Message = $"Unknown action: {cmd.Action}" };
            }

            // Always return the refreshed slot list so clients stay in sync
            var slots = _saveProvider.ListSaveSlots();
            return new SaveGameResponseMsg
            {
                Action = cmd.Action,
                Success = true,
                Message = message,
                Slots = slots.Select(s => new SaveSlotInfoMsg
                {
                    SlotId = s.SlotId,
                    Name = s.Name,
                    Seed = s.Seed,
                    GeneratorId = s.GeneratorId,
                    CreatedAtUnixMs = new DateTimeOffset(s.CreatedAt).ToUnixTimeMilliseconds(),
                    LastSavedAtUnixMs = new DateTimeOffset(s.LastSavedAt).ToUnixTimeMilliseconds(),
                }).ToArray(),
                CurrentSlotId = _currentSlotId ?? "",
            };
        }
        catch (Exception ex)
        {
            _logWriter.WriteLine($"[Server] Save command error: {ex.Message}");
            return new SaveGameResponseMsg { Action = cmd.Action, Success = false, Message = ex.Message };
        }
    }

    // ──────────────────────────────────────────────
    // Connection management
    // ──────────────────────────────────────────────

    public PlayerConnection AddConnection(Func<byte[], Task> sendFunc, Func<Task>? closeFunc = null)
    {
        if (closeFunc == null)
            closeFunc = () => Task.CompletedTask;
        long id = Interlocked.Increment(ref _nextConnectionId);
        var conn = new PlayerConnection(id, sendFunc, closeFunc);
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
                // Save player before destroying
                if (_saveProvider != null && _currentSlotId != null && conn.PlayerEntity.HasValue)
                {
                    var entity = conn.PlayerEntity.Value;
                    if (_engine.EcsWorld.IsAlive(entity))
                    {
                        var playerData = PlayerSerializer.SerializePlayer(_engine.EcsWorld, entity, conn.PlayerName);
                        _saveProvider.SavePlayers(_currentSlotId, [playerData]);
                    }
                }

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

    public void UpdateVisibleChunks(long connectionId, int visibleChunks)
    {
        if (_connections.TryGetValue(connectionId, out var conn))
        {
            EnqueueCommand(() =>
            {
                conn.VisibleChunks = Math.Clamp(visibleChunks, 1, ChunkTracker.MaxVisibleChunks);
                return Task.CompletedTask;
            });
        }
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
        if (!_connections.TryGetValue(connectionId, out var _))
            throw new Exception($"Connection not found: {connectionId}");

        EnqueueCommand(() => SpawnPlayerForConnectionInternal(connectionId, classId, playerName));
    }

    private async Task SpawnPlayerForConnectionInternal(long connectionId, int classId, string playerName)
    {
        if (!_connections.TryGetValue(connectionId, out var conn)) return;

        conn.PlayerName = playerName;

        // Check for saved player data
        Arch.Core.Entity entity;
        if (_saveProvider != null && _currentSlotId != null)
        {
            var savedPlayer = _saveProvider.LoadPlayer(_currentSlotId, playerName);
            if (savedPlayer != null)
            {
                // Ensure the chunk at the saved position is loaded
                var (cx, cy, cz) = Chunk.WorldToChunkCoord(savedPlayer.PositionX, savedPlayer.PositionY, savedPlayer.PositionZ);
                _engine.EnsureChunkLoaded(cx, cy, cz);

                entity = PlayerSerializer.RestorePlayer(_engine, connectionId, savedPlayer);
                conn.PlayerEntity = entity;

                if (DebugGiveResources)
                    _engine.GiveDebugResources(entity);

                _engine.Tick();
                ResetConnectionTracking(conn);
                await SendSnapshot(conn);
                return;
            }
        }

        // No saved player — spawn fresh
        var (spawnX, spawnY, spawnZ) = _engine.FindSpawnPosition();
        entity = _engine.SpawnPlayer(connectionId, spawnX, spawnY, spawnZ, classId);
        conn.PlayerEntity = entity;

        if (DebugGiveResources)
            _engine.GiveDebugResources(entity);

        _engine.Tick();
        ResetConnectionTracking(conn);
        await SendSnapshot(conn);
    }

    private void ResetConnectionTracking(PlayerConnection conn)
    {
        conn.LastSentEntities.Clear();
        conn.SentChunkTracker.Clear();
        conn.LastSentPlayerState = null;
        conn.LastAckedTick = 0;
    }

    private async Task SendSnapshot(PlayerConnection conn)
    {
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

    // ──────────────────────────────────────────────
    // Server loop
    // ──────────────────────────────────────────────

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
                if (_restartGameTask != null)
                {
                    // Auto-save current game before switching
                    if (_currentSlotId != null)
                        PerformAutoSave();

                    // Disconnect everyone before restarting the engine to avoid complications with entity references, chunk tracking, etc.
                    _logWriter.WriteLine("[Server] Disconnecting players...");
                    foreach (var conn in _connections.Values)
                    {
                        try
                        {

                            await conn.CloseAsync();
                        }
                        catch
                        {
                            /* connection closing */
                        }
                    }
                    _connections.Clear();
                    _logWriter.WriteLine("[Server] Restarting game engine...");

                    _restartGameTask.Invoke();
                    _restartGameTask = null;

                    _logWriter.WriteLine("[Server] Game engine restarted...");
                }

                await ProcessCommandsAsync();
                ProcessInputs();
                _engine.Tick();
                FlushDirtyTiles();
                await BroadcastDeltas();
                _pendingTileUpdates = [];

                // Persistence: auto-save and chunk unload
                if (_saveProvider != null && _currentSlotId != null)
                {
                    var currentTick = _engine.CurrentTick;
                    if (currentTick - _lastSaveTick >= AutoSaveIntervalTicks)
                    {
                        PerformAutoSave();
                        PerformChunkUnload();
                        _lastSaveTick = currentTick;
                    }
                }

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
            // Save on shutdown
            if (_saveProvider != null && _currentSlotId != null)
            {
                _logWriter.WriteLine("[Server] Saving game on shutdown...");
                PerformAutoSave();
            }
            StopEngineAndDispose();
            _logWriter.WriteLine("[Server] Server loop stopped...");
        }
    }

    // ──────────────────────────────────────────────
    // Persistence: auto-save and chunk unload
    // ──────────────────────────────────────────────

    private void PerformAutoSave()
    {
        if (_saveProvider == null || _currentSlotId == null) return;

        try
        {
            // Save modified chunks
            var modifiedChunks = _engine.WorldMap.GetModifiedChunks();
            if (modifiedChunks.Count > 0)
            {
                var chunkEntries = new List<ChunkSaveEntry>(modifiedChunks.Count);
                foreach (var chunk in modifiedChunks)
                {
                    chunkEntries.Add(new ChunkSaveEntry
                    {
                        ChunkX = chunk.ChunkX,
                        ChunkY = chunk.ChunkY,
                        ChunkZ = chunk.ChunkZ,
                        TileData = ChunkSerializer.SerializeTiles(chunk.Tiles),
                        EntityData = EntitySerializer.SerializeEntities(_engine.EcsWorld, chunk.ChunkX, chunk.ChunkY, chunk.ChunkZ),
                    });
                    chunk.ClearSaveFlag();
                }
                _saveProvider.SaveChunks(_currentSlotId, chunkEntries);
            }

            // Save all connected players
            var playerEntries = new List<PlayerSaveData>();
            foreach (var conn in _connections.Values)
            {
                if (conn.PlayerEntity.HasValue && _engine.EcsWorld.IsAlive(conn.PlayerEntity.Value))
                {
                    playerEntries.Add(PlayerSerializer.SerializePlayer(_engine.EcsWorld, conn.PlayerEntity.Value, conn.PlayerName));
                }
            }
            if (playerEntries.Count > 0)
                _saveProvider.SavePlayers(_currentSlotId, playerEntries);

            // Save world metadata
            _saveProvider.SaveWorldMeta(_currentSlotId, new WorldSaveData
            {
                Seed = _engine.WorldMap.Seed,
                GeneratorId = GetCurrentGeneratorId(),
                CurrentTick = _engine.CurrentTick,
            });
        }
        catch (Exception ex)
        {
            _logWriter.WriteLine($"[Server] Auto-save error: {ex.Message}");
        }
    }

    private string GetCurrentGeneratorId()
    {
        if (_saveProvider != null && _currentSlotId != null)
        {
            var slot = _saveProvider.GetSaveSlot(_currentSlotId);
            if (slot != null) return slot.GeneratorId;
        }
        return GeneratorRegistry.DefaultId;
    }

    private void PerformChunkUnload()
    {
        if (_saveProvider == null || _currentSlotId == null) return;

        var currentTick = _engine.CurrentTick;

        // Update viewed chunks from connected players
        UpdateChunkViewTracking(currentTick);

        // Find chunks to unload
        var toUnload = new List<(int cx, int cy, int cz, long key)>();
        foreach (var (key, lastViewed) in _chunkLastViewedTick)
        {
            if (currentTick - lastViewed >= ChunkUnloadIdleTicks)
            {
                var (cx, cy, cz) = Position.UnpackCoord(key);
                toUnload.Add((cx, cy, cz, key));
            }
        }

        foreach (var (cx, cy, cz, key) in toUnload)
        {
            var chunk = _engine.WorldMap.TryGetChunk(cx, cy, cz);
            if (chunk == null)
            {
                _chunkLastViewedTick.Remove(key);
                continue;
            }

            // Save before unloading
            var entry = new ChunkSaveEntry
            {
                ChunkX = cx,
                ChunkY = cy,
                ChunkZ = cz,
                TileData = ChunkSerializer.SerializeTiles(chunk.Tiles),
                EntityData = EntitySerializer.SerializeEntities(_engine.EcsWorld, cx, cy, cz),
            };
            _saveProvider.SaveChunks(_currentSlotId, [entry]);

            // Destroy entities in chunk, then unload
            _engine.DestroyEntitiesInChunk(cx, cy, cz);
            _engine.WorldMap.UnloadChunk(cx, cy, cz);
            _chunkLastViewedTick.Remove(key);

            _logWriter.WriteLine($"[Server] Unloaded chunk ({cx},{cy},{cz})");
        }
    }

    private void UpdateChunkViewTracking(long currentTick)
    {
        foreach (var conn in _connections.Values)
        {
            if (!conn.PlayerEntity.HasValue || !_engine.EcsWorld.IsAlive(conn.PlayerEntity.Value))
                continue;

            ref var pos = ref _engine.EcsWorld.Get<Position>(conn.PlayerEntity.Value);
            var (pcx, pcy, pcz) = Chunk.WorldToChunkCoord(pos.X, pos.Y, pos.Z);
            int radius = conn.VisibleChunks;

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    var key = Position.PackCoord(pcx + dx, pcy + dy, pcz);
                    _chunkLastViewedTick[key] = currentTick;
                }
            }
        }
    }

    // ──────────────────────────────────────────────
    // Tick processing
    // ──────────────────────────────────────────────

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

    private void FlushDirtyTiles()
    {
        var dirty = _engine.WorldMap.FlushDirtyTiles();
        if (dirty.Count == 0) return;
        _pendingTileUpdates = new TileUpdateMsg[dirty.Count];
        for (int i = 0; i < dirty.Count; i++)
        {
            var (wx, wy, wz, tile) = dirty[i];
            _pendingTileUpdates[i] = new TileUpdateMsg
            {
                X = wx,
                Y = wy,
                Z = wz,
                TileType = (byte)tile.Type,
                GlyphId = tile.GlyphId,
                FgColor = tile.FgColor,
                BgColor = tile.BgColor,
                PlaceableItemId = tile.PlaceableItemId,
                PlaceableItemExtra = tile.PlaceableItemExtra,
            };
        }
    }

    private WorldDeltaMsg BuildDelta(PlayerConnection conn, bool isSnapshot = false)
    {
        var playerEntity = conn.PlayerEntity!.Value;
        ref var playerPos = ref _engine.EcsWorld.Get<Position>(playerEntity);
        ref var fov = ref _engine.EcsWorld.Get<FOVData>(playerEntity);

        var ticksSinceLastSentChunks = _engine.CurrentTick - conn.LastSentChunksTick;
        var shouldSendChunks = isSnapshot || ticksSinceLastSentChunks >= MinTicksBetweenChunkResend;

        var chunkDelta =
            shouldSendChunks ?
                GameStateSerializer.SerializeChunksDelta(_engine, playerPos.X, playerPos.Y, playerPos.Z, conn.SentChunkTracker, conn.VisibleChunks, isSnapshot ? int.MaxValue : MaxChunksPerTick) :
                new ChunkDeltaResult { NewChunks = [], DiscardedKeys = [] };

        if (chunkDelta.NewChunks.Length > 0)
            conn.LastSentChunksTick = _engine.CurrentTick;

        // State delta compression: always send on snapshot, otherwise only when changed
        var playerState = GameStateSerializer.SerializePlayerStateDelta(_engine, playerEntity, conn.LastSentPlayerState);
        if (playerState != null || isSnapshot)
            conn.LastSentPlayerState = playerState;

        var serializedEntityData = GameStateSerializer.SerializeEntityDelta(_engine.EcsWorld, fov, conn.LastSentEntities, DebugVisibilityOff);

        return new WorldDeltaMsg
        {
            FromTick = conn.LastAckedTick,
            ToTick = _engine.CurrentTick,
            IsSnapshot = isSnapshot,
            Chunks = chunkDelta.NewChunks,
            DiscardedChunkKeys = chunkDelta.DiscardedKeys,
            TileUpdates = _pendingTileUpdates,
            EntityUpdates = serializedEntityData.FullUpdates,
            EntityPositionHealthUpdates = serializedEntityData.PositionHealthUpdates,
            EntityRemovals = serializedEntityData.Removals,
            CombatEvents = GameStateSerializer.SerializeCombatEvents(_engine),
            NpcDialogueEvents = GameStateSerializer.SerializeNpcDialogueEvents(_engine),
            PlayerState = playerState,
        };
    }

    public void Dispose()
    {
        if (!IsRunning)
        {
            StopEngineAndDispose();
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

        StopEngineAndDispose();

        try
        {
            _cts.Dispose();
        }
        catch (ObjectDisposedException)
        {
            _logWriter.WriteLine("[Server] Cancellation token already disposed");
        }

        _logWriter.WriteLine("[Server] Server disposed");
    }
}
