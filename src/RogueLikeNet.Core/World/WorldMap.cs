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

        chunk = new Chunk(chunkX, chunkY);
        long chunkSeed = _seed ^ (((long)chunkX * 0x45D9F3B) + ((long)chunkY * 0x12345678));
        var result = generator.Generate(chunk, chunkSeed);
        _chunks[key] = chunk;
        return (chunk, result);
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

    public IEnumerable<Chunk> LoadedChunks => _chunks.Values;
}
