using Arch.AOT.SourceGenerator;

namespace RogueLikeNet.Core.Components;

[Component]
public struct FOVData
{
    public int Radius;
    public HashSet<long>? VisibleTiles;

    public FOVData(int radius)
    {
        Radius = radius;
        VisibleTiles = new HashSet<long>();
    }

    public bool IsVisible(int x, int y) => VisibleTiles?.Contains(Position.PackCoord(x, y)) ?? false;
}
