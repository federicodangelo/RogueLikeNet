using RogueLikeNet.Core.Data;

namespace RogueLikeNet.Core.Definitions;

/// <summary>
/// Describes the in-world behavior of a placed item: walkability, transparency,
/// appearance, and optional state (e.g. open/closed doors).
/// PlaceableItemExtra 0 = default state, 1 = alternate state (for items with HasState).
/// </summary>
public readonly record struct PlaceableDefinition(
    int ItemTypeId,
    int Category,
    int PlacedGlyphId,
    int PlacedFgColor,
    bool Walkable,
    bool Transparent,
    bool HasState,
    // Properties for the alternate state (e.g. open door)
    int AlternateGlyphId,
    bool AlternateWalkable,
    bool AlternateTransparent
);

/// <summary>
/// Lookup table for placed-item behavior. Derives data from the item registry's
/// Furniture/Block data. Keyed by numeric item type IDs.
/// </summary>
public static class PlaceableDefinitions
{
    public const int CategoryNone = 0;
    public const int CategoryDoor = 1;
    public const int CategoryWall = 2;
    public const int CategoryDecoration = 3;
    public const int CategoryFloorTile = 4;

    public static PlaceableDefinition[] All
    {
        get
        {
            var reg = GameData.Instance.Items;
            if (reg.Count == 0) return [];
            return reg.All
                .Where(d => d.IsPlaceable)
                .Select(d => FromRegistry(d))
                .ToArray();
        }
    }

    public static PlaceableDefinition Get(int itemTypeId)
    {
        var def = GameData.Instance.Items.Get(itemTypeId);
        if (def != null && def.IsPlaceable)
            return FromRegistry(def);
        return default;
    }

    private static PlaceableDefinition FromRegistry(ItemDefinition def)
    {
        if (def.Furniture != null)
        {
            var f = def.Furniture;
            return new PlaceableDefinition(
                def.NumericId,
                FurnitureCategoryFromData(f.FurnitureType),
                f.PlacedGlyphId,
                f.PlacedFgColor,
                f.Walkable,
                f.Transparent,
                f.StateType != PlaceableStateType.None,
                f.AlternateGlyphId,
                f.AlternateWalkable,
                f.AlternateTransparent
            );
        }

        if (def.Block != null)
        {
            return new PlaceableDefinition(
                def.NumericId,
                CategoryWall,
                def.GlyphId,
                def.FgColor,
                false,
                false,
                false,
                0, false, false
            );
        }

        return new PlaceableDefinition(
            def.NumericId,
            CategoryDecoration,
            def.GlyphId,
            def.FgColor,
            true,
            true,
            false,
            0, false, false
        );
    }

    private static int FurnitureCategoryFromData(FurnitureType t) => t switch
    {
        FurnitureType.Door => CategoryDoor,
        FurnitureType.Wall or FurnitureType.Window => CategoryWall,
        FurnitureType.FloorTile => CategoryFloorTile,
        _ => CategoryDecoration,
    };

    public static bool IsWalkable(int itemTypeId, int extra)
    {
        var def = Get(itemTypeId);
        if (def.ItemTypeId == 0) return true; // unknown → don't block
        return def.HasState && extra != 0 ? def.AlternateWalkable : def.Walkable;
    }

    public static bool IsTransparent(int itemTypeId, int extra)
    {
        var def = Get(itemTypeId);
        if (def.ItemTypeId == 0) return true;
        return def.HasState && extra != 0 ? def.AlternateTransparent : def.Transparent;
    }

    public static int GetGlyphId(int itemTypeId, int extra)
    {
        var def = Get(itemTypeId);
        if (def.ItemTypeId == 0) return 0;
        return def.HasState && extra != 0 ? def.AlternateGlyphId : def.PlacedGlyphId;
    }

    public static int GetFgColor(int itemTypeId, int extra)
    {
        var def = Get(itemTypeId);
        return def.PlacedFgColor; // color doesn't change with state
    }

    public static int GetCategory(int itemTypeId) => Get(itemTypeId).Category;

    public static bool IsDoor(int itemTypeId) => GetCategory(itemTypeId) == CategoryDoor;

    public static bool IsDoorOpen(int itemTypeId, int extra) => IsDoor(itemTypeId) && extra != 0;

    public static bool IsDoorClosed(int itemTypeId, int extra) => IsDoor(itemTypeId) && extra == 0;

    public static bool IsWall(int itemTypeId) => GetCategory(itemTypeId) == CategoryWall;
}
