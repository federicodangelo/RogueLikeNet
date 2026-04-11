using Engine.Core;
using RogueLikeNet.Core.Utilities;

namespace RogueLikeNet.Core.Tests;

public class ColorUtilsTests
{
    [Fact]
    public void ApplyBrightness_ScalesRGB()
    {
        var color = new Color4(100, 200, 50, 255);
        var result = ColorUtils.ApplyBrightness(color, 0.5f);
        Assert.Equal(50, result.R);
        Assert.Equal(100, result.G);
        Assert.Equal(25, result.B);
        Assert.Equal(255, result.A);
    }

    [Fact]
    public void ApplyBrightness_ClampsTo255()
    {
        var color = new Color4(200, 200, 200, 255);
        var result = ColorUtils.ApplyBrightness(color, 2.0f);
        Assert.Equal(255, result.R);
        Assert.Equal(255, result.G);
        Assert.Equal(255, result.B);
    }

    [Fact]
    public void IntToColor4_And_Color4ToInt_Roundtrip()
    {
        int packed = 0xFF8040;
        var c = ColorUtils.IntToColor4(packed);
        Assert.Equal(255, c.R);
        Assert.Equal(128, c.G);
        Assert.Equal(64, c.B);
        Assert.Equal(packed, ColorUtils.Color4ToInt(c));
    }

    [Fact]
    public void ScaleColor_AppliesPerChannel()
    {
        var c = new Color4(100, 100, 100, 255);
        var r = ColorUtils.ScaleColor(c, 50, 100, 200);
        Assert.Equal(50, r.R);
        Assert.Equal(100, r.G);
        Assert.Equal(200, r.B);
    }

    [Fact]
    public void ScaleColor_ClampsTo255()
    {
        var c = new Color4(200, 200, 200, 255);
        var r = ColorUtils.ScaleColor(c, 200, 200, 200);
        Assert.Equal(255, r.R);
        Assert.Equal(255, r.G);
        Assert.Equal(255, r.B);
    }

    [Fact]
    public void IntToColor4_Black()
    {
        var c = ColorUtils.IntToColor4(0x000000);
        Assert.Equal(0, c.R);
        Assert.Equal(0, c.G);
        Assert.Equal(0, c.B);
        Assert.Equal(255, c.A);
    }

    [Fact]
    public void IntToColor4_White()
    {
        var c = ColorUtils.IntToColor4(0xFFFFFF);
        Assert.Equal(255, c.R);
        Assert.Equal(255, c.G);
        Assert.Equal(255, c.B);
    }

    [Fact]
    public void Color4ToInt_Black()
    {
        Assert.Equal(0, ColorUtils.Color4ToInt(new Color4(0, 0, 0, 255)));
    }

    [Fact]
    public void ApplyBrightness_ZeroBrightness_ReturnsBlack()
    {
        var color = new Color4(100, 200, 50, 255);
        var result = ColorUtils.ApplyBrightness(color, 0f);
        Assert.Equal(0, result.R);
        Assert.Equal(0, result.G);
        Assert.Equal(0, result.B);
    }
}
