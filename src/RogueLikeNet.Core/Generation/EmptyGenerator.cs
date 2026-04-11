using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Generation;

/// <summary>
/// Generates a completely empty world — all floor tiles, no walls, no enemies, no items.
/// Useful for testing movement, rendering, and FOV without obstacles.
/// </summary>
public class EmptyGenerator : IDungeonGenerator
{
    public bool Exists(ChunkPosition chunkPos)
    {
        var (chunkX, chunkY, chunkZ) = chunkPos;
        // Only the spawn chunk has content; all other chunks are empty floors.
        return chunkZ == Position.DefaultZ;
    }

    public GenerationResult Generate(ChunkPosition chunkPos)
    {
        var (chunkX, chunkY, chunkZ) = chunkPos;
        var chunk = new Chunk(chunkPos);
        var result = new GenerationResult(chunk);

        if (chunkZ != Position.DefaultZ)
            return result;

        int floorTileId = GameData.Instance.Tiles.GetNumericId("floor");
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                chunk.Tiles[x, y].TileId = floorTileId;

        // Spawn point: center of the chunk
        if (chunkX == 0 && chunkY == 0)
            result.SpawnPosition = Position.FromCoords(Chunk.Size / 2, Chunk.Size / 2, chunkZ);

        return result;
    }
}
