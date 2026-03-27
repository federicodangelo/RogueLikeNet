using Arch.Core;
using RogueLikeNet.Core.Algorithms;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.World;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// Computes FOV for all entities that have FOVData (typically players).
/// Uses recursive shadow casting with integer arithmetic.
/// </summary>
public class FOVSystem
{
    public void Update(Arch.Core.World world, WorldMap map)
    {
        var query = new QueryDescription().WithAll<Position, FOVData>();
        world.Query(in query, (ref Position pos, ref FOVData fov) =>
        {
            fov.VisibleTiles ??= new HashSet<long>();
            fov.VisibleTiles.Clear();

            // Capture ref params into locals for use in lambda
            var visibleTiles = fov.VisibleTiles;
            int px = pos.X, py = pos.Y, radius = fov.Radius;

            ShadowCastFov.Compute(
                px, py, radius,
                isOpaque: (x, y) => !map.IsTransparent(x, y),
                markVisible: (x, y) =>
                {
                    visibleTiles.Add(Position.PackCoord(x, y));
                });
        });
    }
}
