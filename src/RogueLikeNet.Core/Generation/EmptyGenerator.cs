using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Generation;

/// <summary>
/// Generates a completely empty world — all floor tiles, no walls, no enemies, no items.
/// Useful for testing movement, rendering, and FOV without obstacles.
/// </summary>
public class EmptyGenerator : IDungeonGenerator
{
    public GenerationResult Generate(int chunkX, int chunkY, int chunkZ)
    {
        var chunk = new Chunk(chunkX, chunkY, chunkZ);
        var result = new GenerationResult(chunk);

        if (chunkZ != Position.DefaultZ)
            return result;

        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
            {
                ref var tile = ref chunk.Tiles[x, y];
                tile.Type = TileType.Floor;
                tile.GlyphId = TileDefinitions.GlyphFloor;
                tile.FgColor = TileDefinitions.ColorFloorFg;
                tile.BgColor = TileDefinitions.ColorBlack;
            }

        // Spawn point: center of the chunk
        if (chunkX == 0 && chunkY == 0)
            result.SpawnPosition = (Chunk.Size / 2, Chunk.Size / 2, chunkZ);

        return result;
    }
}
