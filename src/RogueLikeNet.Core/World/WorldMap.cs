using Arch.Core;
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
    public void Update(Arch.Core.World ecsWorld)
    {
        ProcessDynamicTiles(ecsWorld);
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

    private void ProcessDynamicTiles(Arch.Core.World ecsWorld)
    {
        if (_dynamicTilesByChunk.Count == 0) return;

        // Build set of occupied positions for door blocking checks
        var occupied = new HashSet<long>();
        var posQuery = new QueryDescription().WithAll<Position, Health>();
        ecsWorld.Query(in posQuery, (ref Position p, ref Health h) =>
        {
            if (h.IsAlive)
                occupied.Add(Position.PackCoord(p.X, p.Y, p.Z));
        });

        // Snapshot all tile keys to avoid modifying collections during iteration
        var allTiles = new List<(long ChunkKey, long TileKey)>();
        foreach (var (chunkKey, tileCoords) in _dynamicTilesByChunk)
            foreach (var tileKey in tileCoords)
                allTiles.Add((chunkKey, tileKey));

        foreach (var (chunkKey, tileKey) in allTiles)
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
                else if (!occupied.Contains(tileKey))
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
