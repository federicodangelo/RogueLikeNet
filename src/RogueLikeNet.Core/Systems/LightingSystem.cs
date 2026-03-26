using Arch.Core;
using RogueLikeNet.Core.Algorithms;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.World;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// Shadow-cast lighting from all LightSource entities.
/// Computes integer light levels per tile in the world map.
/// </summary>
public class LightingSystem
{
    public void Update(Arch.Core.World world, WorldMap map)
    {
        // Reset light levels for all loaded chunks
        foreach (var chunk in map.LoadedChunks)
        {
            for (int x = 0; x < Chunk.Size; x++)
                for (int y = 0; y < Chunk.Size; y++)
                    chunk.Tiles[x, y].LightLevel = 0;
        }

        // Gather all light sources
        var query = new QueryDescription().WithAll<Position, LightSource>();
        world.Query(in query, (ref Position pos, ref LightSource light) =>
        {
            FloodLight(map, pos.X, pos.Y, light.Radius);
        });

        // Players also emit ambient light matching their FOV
        var playerQuery = new QueryDescription().WithAll<Position, FOVData, PlayerTag>();
        world.Query(in playerQuery, (ref Position pos, ref FOVData fov, ref PlayerTag _) =>
        {
            FloodLight(map, pos.X, pos.Y, fov.Radius);
        });
    }

    private static void FloodLight(WorldMap map, int originX, int originY, int radius)
    {
        // Use shadow casting so light doesn't bleed through walls
        ShadowCastFov.Compute(originX, originY, radius,
            isOpaque: (x, y) => !map.IsTransparent(x, y),
            markVisible: (x, y) =>
            {
                int dx = x - originX;
                int dy = y - originY;
                int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                int lightAmount = (radius - dist + 1) * 10 / (radius + 1);
                if (lightAmount <= 0) return;

                var (cx, cy) = Chunk.WorldToChunkCoord(x, y);
                var chunk = map.TryGetChunk(cx, cy);
                if (chunk == null) return;

                int lx = x - cx * Chunk.Size;
                int ly = y - cy * Chunk.Size;
                if (!chunk.InBounds(lx, ly)) return;

                ref var tile = ref chunk.Tiles[lx, ly];
                tile.LightLevel = Math.Max(tile.LightLevel, lightAmount);
            });
    }
}
