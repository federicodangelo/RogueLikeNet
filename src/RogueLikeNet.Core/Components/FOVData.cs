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

    public static long PackCoord(int x, int y) => ((long)x << 32) | (uint)y;
    public static (int X, int Y) UnpackCoord(long packed) => ((int)(packed >> 32), (int)(packed & 0xFFFFFFFF));

    public bool IsVisible(int x, int y) => VisibleTiles?.Contains(PackCoord(x, y)) ?? false;
}
