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
                isOpaque: (x, y) => !map.IsTransparent(x, y, p.Z),
                markVisible: (x, y) =>
                {
                    visibleTiles.Add(Position.PackCoord(x, y, p.Z));
                });
        }
    }
}
