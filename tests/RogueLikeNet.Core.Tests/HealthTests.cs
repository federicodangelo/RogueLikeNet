using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Core.Tests;

public class HealthTests
{
    [Fact]
    public void Health_InitializesAtMax()
    {
        var h = new Health(100);
        Assert.Equal(100, h.Current);
        Assert.Equal(100, h.Max);
        Assert.True(h.IsAlive);
    }

    [Fact]
    public void Health_IsAlive_FalseAtZero()
    {
        var h = new Health(100) { Current = 0 };
        Assert.False(h.IsAlive);
    }
}
