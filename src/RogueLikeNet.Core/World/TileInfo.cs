using RogueLikeNet.Core.Data;

namespace RogueLikeNet.Core.World;

public enum TileType : byte
{
    Void = 0,
    Floor = 1,
    /// <summary>Indestructible terrain wall (cannot be picked up, unlike placeable walls).</summary>
    Blocked = 2,
    StairsDown = 4,
    StairsUp = 5,
    Water = 6,
    Lava = 7,
}

public struct TileInfo
{
    public int TileId;
    public int PlaceableItemId;
    public int PlaceableItemExtra;

    public readonly TileType Type => GameData.Instance.Tiles.GetTileType(TileId);
    public readonly int GlyphId => GameData.Instance.Tiles.GetGlyphId(TileId);
    public readonly int FgColor => GameData.Instance.Tiles.GetFgColor(TileId);
    public readonly int BgColor => GameData.Instance.Tiles.GetBgColor(TileId);

    public readonly bool IsWalkable
    {
        get
        {
            if (!GameData.Instance.Tiles.IsWalkable(TileId)) return false;
            if (PlaceableItemId != 0)
                return GameData.Instance.Items.IsPlaceableWalkable(PlaceableItemId, PlaceableItemExtra);
            return true;
        }
    }

    public readonly bool IsTransparent
    {
        get
        {
            if (!GameData.Instance.Tiles.IsTransparent(TileId)) return false;
            if (PlaceableItemId != 0)
                return GameData.Instance.Items.IsPlaceableTransparent(PlaceableItemId, PlaceableItemExtra);
            return true;
        }
    }

    public readonly bool HasPlaceable => PlaceableItemId != 0;
}
