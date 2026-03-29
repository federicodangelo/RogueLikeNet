using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Core.World;

/// <summary>
/// Manages all chunks in the game world. Generates new chunks on demand.
/// </summary>
public class WorldMap
{
    private readonly Dictionary<long, Chunk> _chunks = new();
    private readonly long _seed;

    public long Seed => _seed;

    public WorldMap(long seed)
    {
        _seed = seed;
    }

    public (Chunk Chunk, Generation.GenerationResult? NewlyGenerated) GetOrCreateChunk(int chunkX, int chunkY, Generation.IDungeonGenerator generator)
    {
        long key = Position.PackCoord(chunkX, chunkY);
        if (_chunks.TryGetValue(key, out var chunk))
            return (chunk, null);

        var result = generator.Generate(chunkX, chunkY);
        _chunks[key] = result.Chunk;
        return (result.Chunk, result);
    }

    public Chunk? TryGetChunk(int chunkX, int chunkY)
    {
        long key = Position.PackCoord(chunkX, chunkY);
        return _chunks.TryGetValue(key, out var chunk) ? chunk : null;
    }

    public TileInfo GetTile(int worldX, int worldY)
    {
        var (cx, cy) = Chunk.WorldToChunkCoord(worldX, worldY);
        var chunk = TryGetChunk(cx, cy);
        if (chunk == null) return default;
        int lx = worldX - cx * Chunk.Size;
        int ly = worldY - cy * Chunk.Size;
        return chunk.Tiles[lx, ly];
    }

    public bool IsWalkable(int worldX, int worldY)
    {
        return GetTile(worldX, worldY).IsWalkable;
    }

    public bool IsTransparent(int worldX, int worldY)
    {
        return GetTile(worldX, worldY).IsTransparent;
    }

    public void SetTile(int worldX, int worldY, TileInfo tile)
    {
        var (cx, cy) = Chunk.WorldToChunkCoord(worldX, worldY);
        var chunk = TryGetChunk(cx, cy);
        if (chunk == null) return;
        int lx = worldX - cx * Chunk.Size;
        int ly = worldY - cy * Chunk.Size;
        chunk.Tiles[lx, ly] = tile;
        chunk.MarkTileDirty(worldX, worldY);
    }

    /// <summary>Collects all dirty tile updates from loaded chunks and clears the dirty state.</summary>
    public List<(int WorldX, int WorldY, TileInfo Tile)> FlushDirtyTiles()
    {
        var result = new List<(int, int, TileInfo)>();
        foreach (var chunk in _chunks.Values)
        {
            if (chunk.DirtyTiles.Count == 0) continue;
            foreach (var (wx, wy) in chunk.DirtyTiles)
            {
                int lx = wx - chunk.ChunkX * Chunk.Size;
                int ly = wy - chunk.ChunkY * Chunk.Size;
                result.Add((wx, wy, chunk.Tiles[lx, ly]));
            }
            chunk.ClearDirtyTiles();
        }
        return result;
    }

    public IEnumerable<Chunk> LoadedChunks => _chunks.Values;
}
