using Arch.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.World;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// BFS flood fill lighting from all LightSource entities.
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

        // Players also emit a small amount of ambient light (from FOV)
        var playerQuery = new QueryDescription().WithAll<Position, FOVData, PlayerTag>();
        world.Query(in playerQuery, (ref Position pos, ref FOVData fov, ref PlayerTag _) =>
        {
            FloodLight(map, pos.X, pos.Y, fov.Radius);
        });
    }

    private static void FloodLight(WorldMap map, int originX, int originY, int radius)
    {
        // Simple radial attenuation: light = max(0, radius - chebyshev_distance)
        for (int dx = -radius; dx <= radius; dx++)
        for (int dy = -radius; dy <= radius; dy++)
        {
            int wx = originX + dx;
            int wy = originY + dy;
            int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
            if (dist > radius) continue;

            int lightAmount = (radius - dist + 1) * 10 / (radius + 1); // scale to 0-10
            if (lightAmount <= 0) continue;

            // Check transparency along the path (simplified: skip full raycast for perf)
            if (!map.IsTransparent(wx, wy) && dist > 0) continue;

            var (cx, cy) = Chunk.WorldToChunkCoord(wx, wy);
            var chunk = map.TryGetChunk(cx, cy);
            if (chunk == null) continue;

            int lx = wx - cx * Chunk.Size;
            int ly = wy - cy * Chunk.Size;
            if (!chunk.InBounds(lx, ly)) continue;

            ref var tile = ref chunk.Tiles[lx, ly];
            tile.LightLevel = Math.Max(tile.LightLevel, lightAmount);
        }
    }
}
