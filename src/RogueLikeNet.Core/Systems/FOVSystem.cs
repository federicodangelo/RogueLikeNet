using RogueLikeNet.Core.Algorithms;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// Computes FOV for all players using recursive shadow casting.
/// </summary>
public class FOVSystem
{
    public void Update(WorldMap map)
    {
        foreach (ref var player in map.Players)
        {
            player.FOV.VisibleTiles ??= new HashSet<long>();
            player.FOV.VisibleTiles.Clear();

            var visibleTiles = player.FOV.VisibleTiles;
            var p = player.Position;
            var radius = player.FOV.Radius;

            ShadowCastFov.Compute(
                p.X, p.Y, radius,
                isOpaque: (x, y) => !map.IsTransparent(Position.FromCoords(x, y, p.Z)),
                markVisible: (x, y) =>
                {
                    visibleTiles.Add(Position.PackCoord(x, y, p.Z));
                });

            // Update per-player explored tiles bitmask on each chunk
            Chunk? lastChunk = null;
            ChunkPosition lastChunkPos = default;
            foreach (var packed in visibleTiles)
            {
                var pos = Position.UnpackCoord(packed);
                var chunkPos = Chunk.WorldToChunkCoord(pos);
                if (chunkPos != lastChunkPos || lastChunk == null)
                {
                    // Fast path if we're still in the same chunk as previous tile
                    // (which is often the case since FOV expands outwards from player position)
                    // We can skip the TryGetChunk call and just update the same chunk's explored data.
                    lastChunkPos = chunkPos;
                    lastChunk = map.TryGetChunk(chunkPos);
                }
                var chunk = lastChunk;
                if (chunk == null) continue;

                chunk.SetTileExploredByServerPlayerId(pos, player.ServerPlayerId);
            }
        }
    }
}
