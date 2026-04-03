using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using static RogueLikeNet.Core.Definitions.PlaceableDefinitions;

namespace RogueLikeNet.Core.World;

/// <summary>
/// Manages all chunks in the game world. Generates new chunks on demand.
/// Tracks dynamic tiles (e.g. open doors with auto-close timers) for efficient per-tick processing.
/// </summary>
public class WorldMap
{
    /// <summary>
    /// Grace period (in ticks) before an opened door auto-closes.
    /// Stored directly in TileInfo.PlaceableItemExtra so it survives save/load.
    /// </summary>
    public const int DoorGraceTicks = 20;

    private readonly Dictionary<long, Chunk> _chunks = new();
    private readonly HashSet<long> _chunksDontExist = new();
    private readonly long _seed;

    // ── Player storage (global, not per-chunk) ────────────────────────
    private readonly Dictionary<int, PlayerEntity> _players = new();
    private readonly Dictionary<long, int> _playersByConnection = new();
    private int _nextEntityId = 1;

    public IReadOnlyDictionary<int, PlayerEntity> Players => _players;

    /// <summary>
    /// Tracks world coordinates of tiles that need per-tick processing, grouped by chunk key.
    /// Used to avoid scanning every tile every tick.
    /// </summary>
    private readonly Dictionary<long, HashSet<long>> _dynamicTilesByChunk = new();

    public long Seed => _seed;

    public WorldMap(long seed)
    {
        _seed = seed;
    }

    // ── Entity ID management ──────────────────────────────────────────

    public int AllocateEntityId() => _nextEntityId++;

    /// <summary>Sets the next entity ID (used when restoring from persistence).</summary>
    public void SetNextEntityId(int nextId) => _nextEntityId = nextId;

    // ── Player management ────────────────────────────────────────────

    public void AddPlayer(PlayerEntity player)
    {
        _players[player.Id] = player;
        _playersByConnection[player.ConnectionId] = player.Id;
    }

    public void RemovePlayer(int entityId)
    {
        if (_players.TryGetValue(entityId, out var player))
        {
            _playersByConnection.Remove(player.ConnectionId);
            _players.Remove(entityId);
        }
    }

    public PlayerEntity? GetPlayer(int entityId)
        => _players.TryGetValue(entityId, out var p) ? p : null;

    public PlayerEntity? GetPlayerByConnection(long connectionId)
        => _playersByConnection.TryGetValue(connectionId, out var id) && _players.TryGetValue(id, out var p) ? p : null;

    // ── Spatial queries ──────────────────────────────────────────────

    /// <summary>Builds a set of packed coordinates for all alive entities (players, monsters, NPCs, resource nodes).</summary>
    public HashSet<long> CollectEntitiesPositions()
    {
        var set = new HashSet<long>();
        foreach (var p in _players.Values)
            if (!p.IsDead) set.Add(Position.PackCoord(p.X, p.Y, p.Z));
        foreach (var chunk in _chunks.Values)
            foreach (var m in chunk.AllSolidEntitiesWithHealth)
                if (!m.IsDead) set.Add(Position.PackCoord(m.X, m.Y, m.Z));
        return set;
    }

    /// <summary>Checks if any alive actor (player, monster, NPC) occupies the given position.</summary>
    public bool IsPositionOccupiedByEntity(int x, int y, int z)
    {
        foreach (var p in _players.Values)
            if (!p.IsDead && p.X == x && p.Y == y && p.Z == z) return true;
        var (cx, cy, cz) = Chunk.WorldToChunkCoord(x, y, z);
        var chunk = TryGetChunk(cx, cy, cz);
        if (chunk == null) return false;
        foreach (var m in chunk.AllSolidEntitiesWithHealth)
            if (!m.IsDead && m.X == x && m.Y == y && m.Z == z) return true;
        return false;
    }

    /// <summary>Convenience: get the chunk that contains the given world position.</summary>
    public Chunk? GetChunkForWorldPos(int x, int y, int z)
    {
        var (cx, cy, cz) = Chunk.WorldToChunkCoord(x, y, z);
        return TryGetChunk(cx, cy, cz);
    }

    // ── Chunk migration (for moving entities) ────────────────────────

    /// <summary>Moves a monster to the correct chunk if it crossed a boundary.</summary>
    public void MoveMonsterEntity(MonsterEntity monster, int newX, int newY, int newZ)
    {
        var (oldCx, oldCy, oldCz) = Chunk.WorldToChunkCoord(monster.X, monster.Y, monster.Z);
        var (newCx, newCy, newCz) = Chunk.WorldToChunkCoord(newX, newY, newZ);
        monster.X = newX;
        monster.Y = newY;
        monster.Z = newZ;
        if (oldCx == newCx && oldCy == newCy && oldCz == newCz) return;
        var oldChunk = TryGetChunk(oldCx, oldCy, oldCz);
        var newChunk = TryGetChunk(newCx, newCy, newCz);
        oldChunk?.RemoveEntity(monster);
        newChunk?.AddEntity(monster);
    }

    /// <summary>Moves a town NPC to the correct chunk if it crossed a boundary.</summary>
    public void MoveNpcEntity(TownNpcEntity npc, int newX, int newY, int newZ)
    {
        var (oldCx, oldCy, oldCz) = Chunk.WorldToChunkCoord(npc.X, npc.Y, npc.Z);
        var (newCx, newCy, newCz) = Chunk.WorldToChunkCoord(newX, newY, newZ);
        npc.X = newX;
        npc.Y = newY;
        npc.Z = newZ;
        if (oldCx == newCx && oldCy == newCy && oldCz == newCz) return;
        var oldChunk = TryGetChunk(oldCx, oldCy, oldCz);
        var newChunk = TryGetChunk(newCx, newCy, newCz);
        oldChunk?.RemoveEntity(npc);
        newChunk?.AddEntity(npc);
    }

    public bool ExistsChunk(int chunkX, int chunkY, int chunkZ, Generation.IDungeonGenerator generator)
    {
        long key = Position.PackCoord(chunkX, chunkY, chunkZ);
        if (_chunks.ContainsKey(key))
            return true;
        if (_chunksDontExist.Contains(key))
            return false;
        var exists = generator.Exists(chunkX, chunkY, chunkZ);
        if (!exists)
            _chunksDontExist.Add(key);

        return exists;
    }

    public (Chunk Chunk, Generation.GenerationResult? NewlyGenerated) GetOrCreateChunk(int chunkX, int chunkY, int chunkZ, Generation.IDungeonGenerator generator)
    {
        long key = Position.PackCoord(chunkX, chunkY, chunkZ);
        if (_chunks.TryGetValue(key, out var chunk))
            return (chunk, null);

        var result = generator.Generate(chunkX, chunkY, chunkZ);
        _chunks[key] = result.Chunk;
        ScanChunkForDynamicTiles(result.Chunk);
        return (result.Chunk, result);
    }

    public Chunk? TryGetChunk(int chunkX, int chunkY, int chunkZ)
    {
        long key = Position.PackCoord(chunkX, chunkY, chunkZ);
        return _chunks.TryGetValue(key, out var chunk) ? chunk : null;
    }

    public TileInfo GetTile(int worldX, int worldY, int worldZ)
    {
        var (cx, cy, cz) = Chunk.WorldToChunkCoord(worldX, worldY, worldZ);
        var chunk = TryGetChunk(cx, cy, cz);
        if (chunk == null) return default;
        int lx = worldX - cx * Chunk.Size;
        int ly = worldY - cy * Chunk.Size;
        return chunk.Tiles[lx, ly];
    }

    public bool IsWalkable(int worldX, int worldY, int worldZ)
    {
        return GetTile(worldX, worldY, worldZ).IsWalkable;
    }

    public bool IsTransparent(int worldX, int worldY, int worldZ)
    {
        return GetTile(worldX, worldY, worldZ).IsTransparent;
    }

    public void SetTile(int worldX, int worldY, int worldZ, TileInfo tile)
    {
        var (cx, cy, cz) = Chunk.WorldToChunkCoord(worldX, worldY, worldZ);
        var chunk = TryGetChunk(cx, cy, cz);
        if (chunk == null) return;
        int lx = worldX - cx * Chunk.Size;
        int ly = worldY - cy * Chunk.Size;

        bool wasDynamic = IsDynamicTile(chunk.Tiles[lx, ly]);
        bool isDynamic = IsDynamicTile(tile);

        chunk.Tiles[lx, ly] = tile;
        chunk.MarkTileDirty(worldX, worldY, worldZ);

        if (isDynamic && !wasDynamic)
            TrackDynamicTile(worldX, worldY, worldZ);
        else if (!isDynamic && wasDynamic)
            UntrackDynamicTile(worldX, worldY, worldZ);
    }

    public void SetPlaceable(int worldX, int worldY, int worldZ, int placeableId, int extraData = 0)
    {
        var tile = GetTile(worldX, worldY, worldZ);
        tile.PlaceableItemId = placeableId;
        tile.PlaceableItemExtra = extraData;
        SetTile(worldX, worldY, worldZ, tile);
    }

    public void SetTileChunkDirty(int x, int y, int z)
    {
        var (cx, cy, cz) = Chunk.WorldToChunkCoord(x, y, z);
        TryGetChunk(cx, cy, cz)?.MarkModified();
    }


    /// <summary>Collects all dirty tile updates from loaded chunks and clears the dirty state.</summary>
    public List<(int WorldX, int WorldY, int WorldZ, TileInfo Tile)> FlushDirtyTiles()
    {
        var result = new List<(int, int, int, TileInfo)>();
        foreach (var chunk in _chunks.Values)
        {
            if (chunk.DirtyTiles.Count == 0) continue;
            foreach (var (wx, wy, wz) in chunk.DirtyTiles)
            {
                int lx = wx - chunk.ChunkX * Chunk.Size;
                int ly = wy - chunk.ChunkY * Chunk.Size;
                result.Add((wx, wy, wz, chunk.Tiles[lx, ly]));
            }
            chunk.ClearDirtyTiles();
        }
        return result;
    }

    public IEnumerable<Chunk> LoadedChunks => _chunks.Values;

    /// <summary>Exposes loaded chunks with their packed coordinate keys.</summary>
    public IReadOnlyDictionary<long, Chunk> LoadedChunksDict => _chunks;

    /// <summary>Removes a chunk from the loaded set. Does not save — caller should save before calling.</summary>
    public void UnloadChunk(int chunkX, int chunkY, int chunkZ)
    {
        long key = Position.PackCoord(chunkX, chunkY, chunkZ);
        _chunks.Remove(key);
        _dynamicTilesByChunk.Remove(key);
    }

    /// <summary>Returns all loaded chunks that have been modified since last save.</summary>
    public List<Chunk> GetModifiedChunks()
    {
        var result = new List<Chunk>();
        foreach (var chunk in _chunks.Values)
        {
            if (chunk.IsModifiedSinceLastSave)
                result.Add(chunk);
        }
        return result;
    }

    /// <summary>Adds a pre-built chunk to the loaded set (used when restoring from persistence).</summary>
    public void AddChunk(Chunk chunk)
    {
        long key = Position.PackCoord(chunk.ChunkX, chunk.ChunkY, chunk.ChunkZ);
        _chunks[key] = chunk;
        ScanChunkForDynamicTiles(chunk);
    }

    /// <summary>
    /// Opens a closed door at the given position. Sets the auto-close countdown in PlaceableItemExtra.
    /// </summary>
    public void OpenDoor(int worldX, int worldY, int worldZ)
    {
        var tile = GetTile(worldX, worldY, worldZ);
        if (!IsDoorClosed(tile.PlaceableItemId, tile.PlaceableItemExtra)) return;
        tile.PlaceableItemExtra = DoorGraceTicks; // ticks until auto-close
        SetTile(worldX, worldY, worldZ, tile);
    }

    /// <summary>
    /// Per-tick update: processes all dynamic tiles (e.g. auto-closing doors).
    /// </summary>
    public void Update()
    {
        ProcessDynamicTiles();
    }

    /// <summary>Returns true if the given world position is tracked as a dynamic tile.</summary>
    public bool IsDynamicTileTracked(int worldX, int worldY, int worldZ)
    {
        var (cx, cy, cz) = Chunk.WorldToChunkCoord(worldX, worldY, worldZ);
        long chunkKey = Position.PackCoord(cx, cy, cz);
        if (!_dynamicTilesByChunk.TryGetValue(chunkKey, out var set)) return false;
        return set.Contains(Position.PackCoord(worldX, worldY, worldZ));
    }

    /// <summary>Returns true if a tile has a stateful placeable in a non-default state (e.g. open door).</summary>
    private static bool IsDynamicTile(TileInfo tile)
    {
        if (tile.PlaceableItemId == 0) return false;
        var def = PlaceableDefinitions.Get(tile.PlaceableItemId);
        return def.HasState && tile.PlaceableItemExtra > 0;
    }

    private void TrackDynamicTile(int worldX, int worldY, int worldZ)
    {
        var (cx, cy, cz) = Chunk.WorldToChunkCoord(worldX, worldY, worldZ);
        long chunkKey = Position.PackCoord(cx, cy, cz);
        if (!_dynamicTilesByChunk.TryGetValue(chunkKey, out var set))
        {
            set = new HashSet<long>();
            _dynamicTilesByChunk[chunkKey] = set;
        }
        set.Add(Position.PackCoord(worldX, worldY, worldZ));
    }

    private void UntrackDynamicTile(int worldX, int worldY, int worldZ)
    {
        var (cx, cy, cz) = Chunk.WorldToChunkCoord(worldX, worldY, worldZ);
        long chunkKey = Position.PackCoord(cx, cy, cz);
        if (_dynamicTilesByChunk.TryGetValue(chunkKey, out var set))
        {
            set.Remove(Position.PackCoord(worldX, worldY, worldZ));
            if (set.Count == 0)
                _dynamicTilesByChunk.Remove(chunkKey);
        }
    }

    private void ScanChunkForDynamicTiles(Chunk chunk)
    {
        for (int lx = 0; lx < Chunk.Size; lx++)
        {
            for (int ly = 0; ly < Chunk.Size; ly++)
            {
                if (IsDynamicTile(chunk.Tiles[lx, ly]))
                {
                    int wx = chunk.ChunkX * Chunk.Size + lx;
                    int wy = chunk.ChunkY * Chunk.Size + ly;
                    TrackDynamicTile(wx, wy, chunk.ChunkZ);
                }
            }
        }
    }

    private readonly List<(long ChunkKey, long TileKey)> _tmpTileKeys = new();
    private readonly HashSet<long> _tmpOccupiedPositions = new();

    private void ProcessDynamicTiles()
    {
        if (_dynamicTilesByChunk.Count == 0) return;

        // Build set of occupied positions for door blocking checks
        _tmpOccupiedPositions.Clear();
        foreach (var p in _players.Values)
            if (!p.IsDead) _tmpOccupiedPositions.Add(Position.PackCoord(p.X, p.Y, p.Z));
        foreach (var chunk in _chunks.Values)
        {
            foreach (var m in chunk.Monsters)
                if (!m.IsDead) _tmpOccupiedPositions.Add(Position.PackCoord(m.X, m.Y, m.Z));
            foreach (var n in chunk.TownNpcs)
                if (!n.IsDead) _tmpOccupiedPositions.Add(Position.PackCoord(n.X, n.Y, n.Z));
        }

        // Snapshot all tile keys to avoid modifying collections during iteration
        _tmpTileKeys.Clear();
        foreach (var (chunkKey, tileCoords) in _dynamicTilesByChunk)
            foreach (var tileKey in tileCoords)
                _tmpTileKeys.Add((chunkKey, tileKey));

        foreach (var (chunkKey, tileKey) in _tmpTileKeys)
        {
            var (x, y, z) = Position.UnpackCoord(tileKey);
            var (cx, cy, cz) = Chunk.WorldToChunkCoord(x, y, z);
            var chunk = TryGetChunk(cx, cy, cz);
            if (chunk == null) { UntrackDynamicTile(x, y, z); continue; }

            int lx = x - cx * Chunk.Size;
            int ly = y - cy * Chunk.Size;
            ref var tile = ref chunk.Tiles[lx, ly];

            if (IsDoor(tile.PlaceableItemId) && tile.PlaceableItemExtra > 0)
            {
                int next = tile.PlaceableItemExtra - 1;
                if (next > 0)
                {
                    // Still counting down — quiet update (no network sync needed)
                    tile.PlaceableItemExtra = next;
                    chunk.MarkModified();
                }
                else if (!_tmpOccupiedPositions.Contains(tileKey))
                {
                    // Grace expired and unoccupied — close the door
                    tile.PlaceableItemExtra = 0;
                    chunk.MarkTileDirty(x, y, z);
                    UntrackDynamicTile(x, y, z);
                }
                else
                {
                    // Occupied — keep door open at minimum countdown
                    if (tile.PlaceableItemExtra != 1)
                    {
                        tile.PlaceableItemExtra = 1;
                        chunk.MarkModified();
                    }
                }
            }
            else
            {
                // No longer a dynamic tile
                UntrackDynamicTile(x, y, z);
            }
        }
    }
}
