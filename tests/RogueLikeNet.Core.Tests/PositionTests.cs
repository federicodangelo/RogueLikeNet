using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Core.Tests;

public class PositionTests
{
    [Fact]
    public void ManhattanDistance_ReturnsCorrectValue()
    {
        var a = new Position(0, 0);
        var b = new Position(3, 4);
        Assert.Equal(7, Position.ManhattanDistance(a, b));
    }

    [Fact]
    public void ChebyshevDistance_ReturnsCorrectValue()
    {
        var a = new Position(0, 0);
        var b = new Position(3, 4);
        Assert.Equal(4, Position.ChebyshevDistance(a, b));
    }

    [Fact]
    public void Equality_WorksCorrectly()
    {
        var a = new Position(5, 10);
        var b = new Position(5, 10);
        var c = new Position(5, 11);
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.True(a == b);
        Assert.True(a != c);
    }

    [Fact]
    public void ToString_FormatsCorrectly()
    {
        var p = new Position(3, 7);
        Assert.Equal("(3, 7)", p.ToString());
    }

    [Fact]
    public void Equals_NonPosition_ReturnsFalse()
    {
        var p = new Position(1, 2);
        Assert.False(p.Equals("not a position"));
        Assert.False(p.Equals(null));
    }

    [Fact]
    public void GetHashCode_EqualPositions_SameHash()
    {
        var a = new Position(5, 10);
        var b = new Position(5, 10);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentPositions_DifferentHash()
    {
        var a = new Position(5, 10);
        var b = new Position(10, 5);
        Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
    }
}
