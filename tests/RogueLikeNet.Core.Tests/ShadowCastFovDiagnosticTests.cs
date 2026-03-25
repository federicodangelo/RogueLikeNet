using RogueLikeNet.Core.Algorithms;

namespace RogueLikeNet.Core.Tests;

/// <summary>Tests for diagonal visibility and shadow correctness in the FOV algorithm.</summary>
public class ShadowCastFovDiagnosticTests
{
    [Fact]
    public void DiagonalTiles_ShouldBeVisible_InOpenArea()
    {
        var visible = new HashSet<(int, int)>();
        ShadowCastFov.Compute(5, 5, 10, (_, _) => false, (x, y) => visible.Add((x, y)));

        // Adjacent diagonals (distance 1)
        Assert.Contains((6, 6), visible);  // SE
        Assert.Contains((4, 4), visible);  // NW
        Assert.Contains((6, 4), visible);  // NE
        Assert.Contains((4, 6), visible);  // SW
    }

    [Fact]
    public void NearDiagonalTiles_ShouldBeVisible()
    {
        var visible = new HashSet<(int, int)>();
        ShadowCastFov.Compute(5, 5, 10, (_, _) => false, (x, y) => visible.Add((x, y)));

        // Distance 2 diagonals
        Assert.Contains((7, 7), visible);
        Assert.Contains((3, 3), visible);
        Assert.Contains((7, 3), visible);
        Assert.Contains((3, 7), visible);

        // Off-axis tiles (1 off cardinal at distance 2)
        Assert.Contains((6, 7), visible);
        Assert.Contains((7, 6), visible);
        Assert.Contains((4, 3), visible);
        Assert.Contains((3, 4), visible);
    }

    [Fact]
    public void CardinalTiles_ShouldBeVisible()
    {
        var visible = new HashSet<(int, int)>();
        ShadowCastFov.Compute(5, 5, 10, (_, _) => false, (x, y) => visible.Add((x, y)));

        // Cardinal directions
        Assert.Contains((5, 6), visible);  // S
        Assert.Contains((5, 4), visible);  // N
        Assert.Contains((6, 5), visible);  // E
        Assert.Contains((4, 5), visible);  // W
    }

    [Fact]
    public void OpenArea_VisibleTileCountIsReasonable()
    {
        var visible = new HashSet<(int, int)>();
        ShadowCastFov.Compute(10, 10, 5, (_, _) => false, (x, y) => visible.Add((x, y)));

        // A radius-5 circle should have π*5² ≈ 78 tiles; with shadow casting ≈ 60-90
        Assert.True(visible.Count > 50, $"Expected > 50 visible tiles in r=5 open area, got {visible.Count}");
    }

    [Fact]
    public void WallLineBlocksVisionBehindIt()
    {
        // Solid wall line at y=7 should block vision to y=8+
        var visible = new HashSet<(int, int)>();
        ShadowCastFov.Compute(5, 5, 10,
            (x, y) => y == 7,
            (x, y) => visible.Add((x, y)));

        // Wall tiles should be visible
        Assert.Contains((5, 7), visible);
        // Tiles behind the wall should be blocked
        Assert.DoesNotContain((5, 9), visible);
        Assert.DoesNotContain((5, 10), visible);
    }

    [Fact]
    public void WallBlocksCardinalVision()
    {
        // Solid wall line at x=7 blocks tiles well behind it
        var visible = new HashSet<(int, int)>();
        ShadowCastFov.Compute(5, 5, 10,
            (x, y) => x == 7,
            (x, y) => visible.Add((x, y)));

        Assert.Contains((7, 5), visible);
        Assert.DoesNotContain((9, 5), visible);
    }

    [Fact]
    public void ItemNextToPlayer_AlwaysVisible_EvenDiagonal()
    {
        // This tests the specific user-reported bug: items at distance 1 diagonal disappearing
        var visible = new HashSet<(int, int)>();
        ShadowCastFov.Compute(10, 10, 10, (_, _) => false, (x, y) => visible.Add((x, y)));

        // All 8 neighbors should be visible
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            Assert.Contains((10 + dx, 10 + dy), visible);
        }
    }

    [Fact]
    public void FullRoomCorner_DiagonalItemsVisible()
    {
        // Simulate a room corner at (3,3) to (7,7) with walls on edges
        // Player at (5,5), items at all nearby positions
        var visible = new HashSet<(int, int)>();
        ShadowCastFov.Compute(5, 5, 10,
            (x, y) => x == 3 || x == 7 || y == 3 || y == 7, // room walls
            (x, y) => visible.Add((x, y)));

        // Items at distance 1-2 inside the room should all be visible
        Assert.Contains((6, 6), visible);
        Assert.Contains((4, 4), visible);
        Assert.Contains((6, 4), visible);
        Assert.Contains((4, 6), visible);
    }
}
