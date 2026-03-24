namespace RogueLikeNet.Core.World;

public class Chunk
{
    public const int Size = 64;

    public int ChunkX { get; }
    public int ChunkY { get; }
    public TileInfo[,] Tiles { get; }

    public Chunk(int chunkX, int chunkY)
    {
        ChunkX = chunkX;
        ChunkY = chunkY;
        Tiles = new TileInfo[Size, Size];
    }

    public ref TileInfo GetTile(int localX, int localY) => ref Tiles[localX, localY];

    public bool InBounds(int localX, int localY)
        => localX >= 0 && localX < Size && localY >= 0 && localY < Size;

    /// <summary>
    /// Converts world coordinates to local chunk coordinates.
    /// Returns false if the world coords don't belong to this chunk.
    /// </summary>
    public bool WorldToLocal(int worldX, int worldY, out int localX, out int localY)
    {
        localX = worldX - ChunkX * Size;
        localY = worldY - ChunkY * Size;
        return InBounds(localX, localY);
    }

    public static (int ChunkX, int ChunkY) WorldToChunkCoord(int worldX, int worldY)
    {
        // Use integer division that floors towards negative infinity
        int cx = worldX >= 0 ? worldX / Size : (worldX - Size + 1) / Size;
        int cy = worldY >= 0 ? worldY / Size : (worldY - Size + 1) / Size;
        return (cx, cy);
    }

    public static long PackChunkKey(int chunkX, int chunkY) => ((long)chunkX << 32) | (uint)chunkY;
}
