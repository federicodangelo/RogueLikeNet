using RogueLikeNet.Core.Definitions;

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
    public TileType Type;
    public int GlyphId;
    public int FgColor;
    public int BgColor;

    /// <summary>
    /// The placeable item on this tile. 0 (ItemDefinitions.None) means nothing is placed.
    /// The base tile (Type/GlyphId/FgColor/BgColor) represents the underlying terrain;
    /// the placed item's appearance is derived from PlaceableDefinitions at render time.
    /// </summary>
    public int PlaceableItemId;

    /// <summary>
    /// Extra state for the placed item (e.g. 0 = closed door, 1 = open door).
    /// Interpretation depends on the specific placeable item type.
    /// </summary>
    public int PlaceableItemExtra;

    public bool IsWalkable
    {
        get
        {
            bool baseWalkable = Type is TileType.Floor or TileType.StairsDown or TileType.StairsUp;
            if (!baseWalkable) return false;
            if (PlaceableItemId != 0)
                return PlaceableDefinitions.IsWalkable(PlaceableItemId, PlaceableItemExtra);
            return true;
        }
    }

    public bool IsTransparent
    {
        get
        {
            bool baseTransparent = Type is not TileType.Blocked;
            if (!baseTransparent) return false;
            if (PlaceableItemId != 0)
                return PlaceableDefinitions.IsTransparent(PlaceableItemId, PlaceableItemExtra);
            return true;
        }
    }
}
