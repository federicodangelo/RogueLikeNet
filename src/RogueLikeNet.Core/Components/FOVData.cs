namespace RogueLikeNet.Core.Components;

public struct FOVData
{
    public int Radius;
    public HashSet<long>? VisibleTiles;

    public FOVData(int radius)
    {
        Radius = radius;
        VisibleTiles = new HashSet<long>();
    }

    public readonly bool IsVisible(Position pos) => VisibleTiles?.Contains(pos.Pack()) ?? false;
}
