namespace RogueLikeNet.Core.World;

public enum TileType : byte
{
    Void = 0,
    Floor = 1,
    Wall = 2,
    Door = 3,
    StairsDown = 4,
    StairsUp = 5,
    Water = 6,
    Lava = 7,
    Decoration = 8,
}

public struct TileInfo
{
    public TileType Type;
    public int GlyphId;
    public int FgColor;
    public int BgColor;

    public bool IsWalkable => Type is TileType.Floor or TileType.Door or TileType.StairsDown or TileType.StairsUp or TileType.Decoration;
    public bool IsTransparent => Type is not TileType.Wall;
}
