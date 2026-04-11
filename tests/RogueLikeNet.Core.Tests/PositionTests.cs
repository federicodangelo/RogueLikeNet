using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Core.Tests;

public class PositionTests
{
    [Fact]
    public void ManhattanDistance_ReturnsCorrectValue()
    {
        var a = Position.FromCoords(0, 0, Position.DefaultZ);
        var b = Position.FromCoords(3, 4, Position.DefaultZ);
        Assert.Equal(7, Position.ManhattanDistance(a, b));
    }

    [Fact]
    public void ChebyshevDistance_ReturnsCorrectValue()
    {
        var a = Position.FromCoords(0, 0, Position.DefaultZ);
        var b = Position.FromCoords(3, 4, Position.DefaultZ);
        Assert.Equal(4, Position.ChebyshevDistance(a, b));
    }

    [Fact]
    public void Equality_WorksCorrectly()
    {
        var a = Position.FromCoords(5, 10, Position.DefaultZ);
        var b = Position.FromCoords(5, 10, Position.DefaultZ);
        var c = Position.FromCoords(5, 11, Position.DefaultZ);
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.True(a == b);
        Assert.True(a != c);
    }

    [Fact]
    public void ToString_FormatsCorrectly()
    {
        var p = Position.FromCoords(3, 7, Position.DefaultZ);
        Assert.Equal("(3, 7, 127)", p.ToString());
    }

    [Fact]
    public void Equals_NonPosition_ReturnsFalse()
    {
        var p = Position.FromCoords(1, 2, Position.DefaultZ);
        Assert.False(p.Equals("not a position"));
        Assert.False(p.Equals(null));
    }

    [Fact]
    public void GetHashCode_EqualPositions_SameHash()
    {
        var a = Position.FromCoords(5, 10, Position.DefaultZ);
        var b = Position.FromCoords(5, 10, Position.DefaultZ);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentPositions_DifferentHash()
    {
        var a = Position.FromCoords(5, 10, Position.DefaultZ);
        var b = Position.FromCoords(10, 5, Position.DefaultZ);
        Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ManhattanDistance_IncludesZ()
    {
        var a = Position.FromCoords(0, 0, 100);
        var b = Position.FromCoords(3, 4, 105);
        // |3| + |4| + |5| = 12
        Assert.Equal(12, Position.ManhattanDistance(a, b));
    }

    [Fact]
    public void ChebyshevDistance_IncludesZ()
    {
        var a = Position.FromCoords(0, 0, 100);
        var b = Position.FromCoords(3, 4, 110);
        // max(|3|, |4|, |10|) = 10
        Assert.Equal(10, Position.ChebyshevDistance(a, b));
    }

    [Fact]
    public void ManhattanDistance_SameZ_UnchangedFromXY()
    {
        var a = Position.FromCoords(0, 0, Position.DefaultZ);
        var b = Position.FromCoords(3, 4, Position.DefaultZ);
        // Z difference is 0, so still just |3| + |4| = 7
        Assert.Equal(7, Position.ManhattanDistance(a, b));
    }

    [Fact]
    public void ChebyshevDistance_SameZ_UnchangedFromXY()
    {
        var a = Position.FromCoords(0, 0, Position.DefaultZ);
        var b = Position.FromCoords(3, 4, Position.DefaultZ);
        // Z difference is 0, so still max(|3|, |4|) = 4
        Assert.Equal(4, Position.ChebyshevDistance(a, b));
    }

    // ── PackCoord / UnpackCoord ──

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(100, 200, 127)]
    [InlineData(-100, -200, 0)]
    [InlineData(-1, 1, 255)]
    [InlineData(8388607, 8388607, 255)]   // max 24-bit signed
    [InlineData(-8388608, -8388608, 0)]   // min 24-bit signed
    public void PackCoord_UnpackCoord_Roundtrip(int x, int y, int z)
    {
        long packed = Position.PackCoord(x, y, z);
        var result = Position.UnpackCoord(packed);
        Assert.Equal(x, result.X);
        Assert.Equal(y, result.Y);
        Assert.Equal(z, result.Z);
    }

    [Fact]
    public void PackCoord_PositionOverload_MatchesXYZ()
    {
        var pos = Position.FromCoords(42, -17, 200);
        Assert.Equal(Position.PackCoord(42, -17, 200), Position.PackCoord(pos));
    }

    [Fact]
    public void Position_Pack_MatchesStaticPackCoord()
    {
        var pos = Position.FromCoords(5, -3, 100);
        Assert.Equal(Position.PackCoord(5, -3, 100), pos.Pack());
    }

    // ── FromCoords ──

    [Fact]
    public void FromCoords_CreatesCorrectPosition()
    {
        var pos = Position.FromCoords(10, 20, 30);
        Assert.Equal(10, pos.X);
        Assert.Equal(20, pos.Y);
        Assert.Equal(30, pos.Z);
    }

    // ── Zero constant ──

    [Fact]
    public void Zero_IsAllZeros()
    {
        Assert.Equal(0, Position.Zero.X);
        Assert.Equal(0, Position.Zero.Y);
        Assert.Equal(0, Position.Zero.Z);
    }

    // ── DefaultZ ──

    [Fact]
    public void DefaultZ_Is127()
    {
        Assert.Equal(127, Position.DefaultZ);
    }
}
