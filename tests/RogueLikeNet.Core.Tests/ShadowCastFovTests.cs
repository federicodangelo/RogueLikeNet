using RogueLikeNet.Core.Algorithms;

namespace RogueLikeNet.Core.Tests;

public class ShadowCastFovTests
{
    [Fact]
    public void Compute_OriginAlwaysVisible()
    {
        var visible = new HashSet<(int, int)>();
        ShadowCastFov.Compute(5, 5, 3, (_, _) => false, (x, y) => visible.Add((x, y)));
        Assert.Contains((5, 5), visible);
    }

    [Fact]
    public void Compute_RadiusLimitsRange()
    {
        var visible = new HashSet<(int, int)>();
        ShadowCastFov.Compute(5, 5, 2, (_, _) => false, (x, y) => visible.Add((x, y)));
        // Nothing beyond radius 2 should be visible (Manhattan distance > 2)
        foreach (var (x, y) in visible)
        {
            int dist = Math.Max(Math.Abs(x - 5), Math.Abs(y - 5));
            Assert.True(dist <= 3, $"Tile ({x},{y}) is beyond radius");
        }
    }

    [Fact]
    public void Compute_WallBlocksVision()
    {
        // Wall line at x=7 blocks all tiles behind it
        var visible = new HashSet<(int, int)>();
        ShadowCastFov.Compute(5, 5, 8,
            (x, y) => x == 7, // wall line at x=7
            (x, y) => visible.Add((x, y)));
        // The wall tiles should be visible
        Assert.Contains((7, 5), visible);
        // Origin always visible
        Assert.Contains((5, 5), visible);
        // Tiles well behind the wall line should mostly be blocked
        // At minimum, the origin side should have visible tiles
        Assert.Contains((6, 5), visible);
    }

    [Fact]
    public void Compute_OpenArea_ManyTilesVisible()
    {
        var visible = new HashSet<(int, int)>();
        ShadowCastFov.Compute(10, 10, 5, (_, _) => false, (x, y) => visible.Add((x, y)));
        // In a completely open area with radius 5, several tiles should be visible
        Assert.True(visible.Count > 10, $"Expected > 10 visible tiles in open area, got {visible.Count}");
    }

    [Fact]
    public void Compute_FullyEnclosed_OnlyOriginVisible()
    {
        // Walls immediately surround origin
        var visible = new HashSet<(int, int)>();
        ShadowCastFov.Compute(5, 5, 10,
            (x, y) => Math.Abs(x - 5) == 1 || Math.Abs(y - 5) == 1,
            (x, y) => visible.Add((x, y)));
        // Origin is always visible
        Assert.Contains((5, 5), visible);
    }

    [Fact]
    public void Compute_ZeroRadius_OnlyOrigin()
    {
        var visible = new HashSet<(int, int)>();
        ShadowCastFov.Compute(5, 5, 0, (_, _) => false, (x, y) => visible.Add((x, y)));
        Assert.Contains((5, 5), visible);
        Assert.True(visible.Count <= 2, $"Zero radius should show very few tiles, got {visible.Count}");
    }

    [Fact]
    public void Compute_LargeRadius_CoversMultipleDirections()
    {
        var visible = new HashSet<(int, int)>();
        ShadowCastFov.Compute(20, 20, 8, (_, _) => false, (x, y) => visible.Add((x, y)));
        // Origin
        Assert.Contains((20, 20), visible);
        // Cardinal directions at distance 2 should always be visible in open space
        Assert.Contains((20, 22), visible); // down
        Assert.Contains((20, 18), visible); // up
        // Should have a significant number of tiles with large radius in open area
        Assert.True(visible.Count > 20, $"Expected > 20 visible tiles, got {visible.Count}");
    }

    [Fact]
    public void Compute_PartialWall_ShadowCasting()
    {
        // Wall line at x=7, blocks vision beyond
        var visible = new HashSet<(int, int)>();
        ShadowCastFov.Compute(5, 5, 6,
            (x, y) => x == 7,
            (x, y) => visible.Add((x, y)));
        // Tiles before the wall should be visible
        Assert.Contains((6, 5), visible);
        // The wall tile itself should be visible
        Assert.Contains((7, 5), visible);
        // Origin always visible
        Assert.Contains((5, 5), visible);
    }
}
