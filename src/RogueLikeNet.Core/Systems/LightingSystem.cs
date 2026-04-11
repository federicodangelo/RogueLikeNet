using RogueLikeNet.Core.Algorithms;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.World;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// Shadow-cast lighting from all light-emitting placeables and player ambient light.
/// </summary>
public class LightingSystem
{
    public void Update(WorldMap map)
    {
        foreach (var chunk in map.LoadedChunks)
            chunk.ResetLight();

        // Light sources from placeables
        var items = GameData.Instance.Items;
        foreach (var chunk in map.LoadedChunks)
            foreach (var packed in chunk.LightEmittingTiles)
            {
                var worldPos = Position.UnpackCoord(packed);
                if (!chunk.WorldToLocal(worldPos.X, worldPos.Y, out var lx, out var ly))
                    continue;
                int itemId = chunk.Tiles[lx, ly].PlaceableItemId;
                int radius = items.GetPlaceableLightRadius(itemId);
                if (radius > 0)
                    FloodLight(map, worldPos, radius);
            }

        // Players emit ambient light matching their FOV
        foreach (var player in map.Players)
            FloodLight(map, player.Position, player.FOV.Radius);
    }

    private static void FloodLight(WorldMap map, Position origin, int radius)
    {
        ShadowCastFov.Compute(origin.X, origin.Y, radius,
            isOpaque: (x, y) => !map.IsTransparent(Position.FromCoords(x, y, origin.Z)),
            markVisible: (x, y) =>
            {
                int dx = x - origin.X;
                int dy = y - origin.Y;
                int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                int lightAmount = (radius - dist + 1) * 10 / (radius + 1);
                if (lightAmount <= 0) return;

                var c = Chunk.WorldToChunkCoord(Position.FromCoords(x, y, origin.Z));
                var chunk = map.TryGetChunk(c);
                if (chunk == null) return;

                if (chunk.WorldToLocal(x, y, out var lx, out var ly))
                    chunk.LightLevels[lx, ly] = (short)Math.Max(chunk.LightLevels[lx, ly], lightAmount);
            });
    }
}
