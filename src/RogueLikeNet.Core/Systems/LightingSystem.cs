using RogueLikeNet.Core.Algorithms;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.World;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// Shadow-cast lighting from all LightSource entities and player ambient light.
/// </summary>
public class LightingSystem
{
    public void Update(WorldMap map)
    {
        foreach (var chunk in map.LoadedChunks)
            chunk.ResetLight();

        // Light sources from elements
        foreach (var chunk in map.LoadedChunks)
            foreach (var elem in chunk.Elements)
                if (elem.Light.HasValue)
                    FloodLight(map, elem.X, elem.Y, elem.Z, elem.Light.Value.Radius);

        // Players emit ambient light matching their FOV
        foreach (var player in map.Players.Values)
            FloodLight(map, player.X, player.Y, player.Z, player.FOV.Radius);
    }

    private static void FloodLight(WorldMap map, int originX, int originY, int originZ, int radius)
    {
        ShadowCastFov.Compute(originX, originY, radius,
            isOpaque: (x, y) => !map.IsTransparent(x, y, originZ),
            markVisible: (x, y) =>
            {
                int dx = x - originX;
                int dy = y - originY;
                int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                int lightAmount = (radius - dist + 1) * 10 / (radius + 1);
                if (lightAmount <= 0) return;

                var (cx, cy, cz) = Chunk.WorldToChunkCoord(x, y, originZ);
                var chunk = map.TryGetChunk(cx, cy, cz);
                if (chunk == null) return;

                int lx = x - cx * Chunk.Size;
                int ly = y - cy * Chunk.Size;
                if (!chunk.InBounds(lx, ly)) return;

                chunk.LightLevels[lx, ly] = (short)Math.Max(chunk.LightLevels[lx, ly], lightAmount);
            });
    }
}
