using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Core.Tests;

public class FOVDataTests
{
    [Fact]
    public void PackCoord_UnpackCoord_RoundTrips()
    {
        long packed = Position.PackCoord(10, 20);
        var (x, y) = Position.UnpackCoord(packed);
        Assert.Equal(10, x);
        Assert.Equal(20, y);
    }

    [Fact]
    public void PackCoord_UnpackCoord_NegativeValues()
    {
        long packed = Position.PackCoord(-5, -10);
        var (x, y) = Position.UnpackCoord(packed);
        Assert.Equal(-5, x);
        Assert.Equal(-10, y);
    }

    [Fact]
    public void IsVisible_ReturnsTrueForVisibleTile()
    {
        var fov = new FOVData(5);
        fov.VisibleTiles!.Add(Position.PackCoord(3, 4));
        Assert.True(fov.IsVisible(3, 4));
        Assert.False(fov.IsVisible(5, 6));
    }

    [Fact]
    public void IsVisible_NullTiles_ReturnsFalse()
    {
        var fov = new FOVData { Radius = 5, VisibleTiles = null };
        Assert.False(fov.IsVisible(0, 0));
    }

    [Fact]
    public void Constructor_InitializesVisibleTiles()
    {
        var fov = new FOVData(8);
        Assert.Equal(8, fov.Radius);
        Assert.NotNull(fov.VisibleTiles);
        Assert.Empty(fov.VisibleTiles);
    }
}
