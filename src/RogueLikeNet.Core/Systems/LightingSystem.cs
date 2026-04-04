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
                    FloodLight(map, elem.Position, elem.Light.Value.Radius);

        // Players emit ambient light matching their FOV
        foreach (var player in map.Players)
            FloodLight(map, player.Position, player.FOV.Radius);
    }

    private static void FloodLight(WorldMap map, Position origin, int radius)
    {
        ShadowCastFov.Compute(origin.X, origin.Y, radius,
            isOpaque: (x, y) => !map.IsTransparent(x, y, origin.Z),
            markVisible: (x, y) =>
            {
                int dx = x - origin.X;
                int dy = y - origin.Y;
                int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                int lightAmount = (radius - dist + 1) * 10 / (radius + 1);
                if (lightAmount <= 0) return;

                var (cx, cy, cz) = Chunk.WorldToChunkCoord(x, y, origin.Z);
                var chunk = map.TryGetChunk(cx, cy, cz);
                if (chunk == null) return;

                int lx = x - cx * Chunk.Size;
                int ly = y - cy * Chunk.Size;
                if (!chunk.InBounds(lx, ly)) return;

                chunk.LightLevels[lx, ly] = (short)Math.Max(chunk.LightLevels[lx, ly], lightAmount);
            });
    }
}
