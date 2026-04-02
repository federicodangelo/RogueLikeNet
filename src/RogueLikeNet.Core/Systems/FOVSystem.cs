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
        foreach (var player in map.Players.Values)
        {
            player.FOV.VisibleTiles ??= new HashSet<long>();
            player.FOV.VisibleTiles.Clear();

            var visibleTiles = player.FOV.VisibleTiles;
            int px = player.X, py = player.Y, pz = player.Z, radius = player.FOV.Radius;

            ShadowCastFov.Compute(
                px, py, radius,
                isOpaque: (x, y) => !map.IsTransparent(x, y, pz),
                markVisible: (x, y) =>
                {
                    visibleTiles.Add(Position.PackCoord(x, y, pz));
                });
        }
    }
}
