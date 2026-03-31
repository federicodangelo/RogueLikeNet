using Arch.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using static RogueLikeNet.Core.Definitions.PlaceableDefinitions;

namespace RogueLikeNet.Core.World;

/// <summary>
/// Manages all chunks in the game world. Generates new chunks on demand.
/// Owns door auto-close timers.
/// </summary>
public class WorldMap
{
    // Grace period (in ticks) before an opened door can auto-close.
    private const int DoorGraceTicks = 20;

    private readonly Dictionary<long, Chunk> _chunks = new();
    private readonly Dictionary<long, int> _openDoorTimers = new();
    private readonly long _seed;

    public long Seed => _seed;

    public WorldMap(long seed)
    {
        _seed = seed;
    }

    public (Chunk Chunk, Generation.GenerationResult? NewlyGenerated) GetOrCreateChunk(int chunkX, int chunkY, int chunkZ, Generation.IDungeonGenerator generator)
    {
        long key = Position.PackCoord(chunkX, chunkY, chunkZ);
        if (_chunks.TryGetValue(key, out var chunk))
            return (chunk, null);

        var result = generator.Generate(chunkX, chunkY, chunkZ);
        _chunks[key] = result.Chunk;
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
        chunk.Tiles[lx, ly] = tile;
        chunk.MarkTileDirty(worldX, worldY, worldZ);
    }

    public void SetPlaceable(int worldX, int worldY, int worldZ, int placeableId, int extraData = 0)
    {
        var tile = GetTile(worldX, worldY, worldZ);
        tile.PlaceableItemId = placeableId;
        tile.PlaceableItemExtra = extraData;
        SetTile(worldX, worldY, worldZ, tile);
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

    /// <summary>
    /// Opens a closed door at the given position. Starts the auto-close grace timer.
    /// </summary>
    public void OpenDoor(int worldX, int worldY, int worldZ)
    {
        var tile = GetTile(worldX, worldY, worldZ);
        if (!IsDoorClosed(tile.PlaceableItemId, tile.PlaceableItemExtra)) return;
        tile.PlaceableItemExtra = 1; // open
        SetTile(worldX, worldY, worldZ, tile);
        _openDoorTimers[Position.PackCoord(worldX, worldY, worldZ)] = DoorGraceTicks;
    }

    /// <summary>
    /// Per-tick update: auto-closes doors that are no longer occupied.
    /// </summary>
    public void Update(Arch.Core.World ecsWorld)
    {
        ProcessDoorTimers(ecsWorld);
    }

    private void ProcessDoorTimers(Arch.Core.World ecsWorld)
    {
        if (_openDoorTimers.Count == 0) return;

        var occupied = new HashSet<long>();
        var posQuery = new QueryDescription().WithAll<Position, Health>();
        ecsWorld.Query(in posQuery, (ref Position p, ref Health h) =>
        {
            if (h.IsAlive)
                occupied.Add(Position.PackCoord(p.X, p.Y, p.Z));
        });

        var toRemove = new List<long>();
        var updates = new List<(long Key, int Ticks)>();
        foreach (var (key, ticksLeft) in _openDoorTimers)
        {
            var (x, y, z) = Position.UnpackCoord(key);
            var tile = GetTile(x, y, z);
            if (!IsDoor(tile.PlaceableItemId) || tile.PlaceableItemExtra != 1) { toRemove.Add(key); continue; }

            int next = ticksLeft - 1;
            if (occupied.Contains(key) || next > 0)
            {
                updates.Add((key, Math.Max(0, next)));
                continue;
            }

            // Grace expired and unoccupied — close the door
            tile.PlaceableItemExtra = 0; // closed
            SetTile(x, y, z, tile);
            toRemove.Add(key);
        }

        foreach (var key in toRemove)
            _openDoorTimers.Remove(key);
        foreach (var (key, ticks) in updates)
            _openDoorTimers[key] = ticks;
    }
}
