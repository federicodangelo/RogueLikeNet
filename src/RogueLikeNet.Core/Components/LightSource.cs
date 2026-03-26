using Arch.AOT.SourceGenerator;

namespace RogueLikeNet.Core.Components;

[Component]
public struct LightSource
{
    public int Radius;
    /// <summary>Packed 0xRRGGBB</summary>
    public int ColorRgb;

    public LightSource(int radius, int colorRgb = 0xFFCC66)
    {
        Radius = radius;
        ColorRgb = colorRgb;
    }
}
