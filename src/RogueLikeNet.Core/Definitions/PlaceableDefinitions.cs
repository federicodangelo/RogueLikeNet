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
/// Lookup table for placed-item behavior. Keyed by ItemDefinitions item type IDs.
/// </summary>
public static class PlaceableDefinitions
{
    public const int CategoryNone = 0;
    public const int CategoryDoor = 1;
    public const int CategoryWall = 2;
    public const int CategoryDecoration = 3;
    public const int CategoryFloorTile = 4;

    private static readonly PlaceableDefinition[] _byId;

    static PlaceableDefinitions()
    {
        // Pre-build a flat array indexed by ItemTypeId for O(1) lookup.
        int maxId = All.Max(t => t.ItemTypeId);
        _byId = new PlaceableDefinition[maxId + 1];
        foreach (var d in All)
            _byId[d.ItemTypeId] = d;
    }

    public static readonly PlaceableDefinition[] All =
    [
        // Doors — closed by default (extra=0): not walkable, not transparent. Open (extra=1): walkable, transparent.
        new(ItemDefinitions.WoodenDoor,  CategoryDoor, TileDefinitions.GlyphDoorClosed, TileDefinitions.ColorWoodFg,   false, false, true, TileDefinitions.GlyphDoor, true, true),
        new(ItemDefinitions.CopperDoor,  CategoryDoor, TileDefinitions.GlyphDoorClosed, TileDefinitions.ColorCopperFg, false, false, true, TileDefinitions.GlyphDoor, true, true),
        new(ItemDefinitions.IronDoor,    CategoryDoor, TileDefinitions.GlyphDoorClosed, TileDefinitions.ColorIronFg,   false, false, true, TileDefinitions.GlyphDoor, true, true),
        new(ItemDefinitions.GoldDoor,    CategoryDoor, TileDefinitions.GlyphDoorClosed, TileDefinitions.ColorGoldFg,   false, false, true, TileDefinitions.GlyphDoor, true, true),
        // Walls — not walkable, not transparent
        new(ItemDefinitions.WoodenWall,  CategoryWall, TileDefinitions.GlyphWall, TileDefinitions.ColorWoodFg,   false, false, false, 0, false, false),
        new(ItemDefinitions.CopperWall,  CategoryWall, TileDefinitions.GlyphWall, TileDefinitions.ColorCopperFg, false, false, false, 0, false, false),
        new(ItemDefinitions.IronWall,    CategoryWall, TileDefinitions.GlyphWall, TileDefinitions.ColorIronFg,   false, false, false, 0, false, false),
        new(ItemDefinitions.GoldWall,    CategoryWall, TileDefinitions.GlyphWall, TileDefinitions.ColorGoldFg,   false, false, false, 0, false, false),
        // Windows — not walkable, but transparent
        new(ItemDefinitions.WoodenWindow, CategoryWall, TileDefinitions.GlyphWindow, TileDefinitions.ColorWindowFg, false, true, false, 0, false, false),
        // Furniture — walkable, transparent
        new(ItemDefinitions.WoodenTable,     CategoryDecoration, TileDefinitions.GlyphTable,     TileDefinitions.ColorTableFg,     true, true, false, 0, false, false),
        new(ItemDefinitions.WoodenChair,     CategoryDecoration, TileDefinitions.GlyphChair,     TileDefinitions.ColorChairFg,     true, true, false, 0, false, false),
        new(ItemDefinitions.WoodenBed,       CategoryDecoration, TileDefinitions.GlyphBed,       TileDefinitions.ColorBedFg,       true, true, false, 0, false, false),
        new(ItemDefinitions.WoodenBookshelf, CategoryDecoration, TileDefinitions.GlyphBookshelf, TileDefinitions.ColorBookshelfFg, true, true, false, 0, false, false),
        // Floor tiles — walkable, transparent
        new(ItemDefinitions.WoodenFloorTile, CategoryFloorTile, TileDefinitions.GlyphFloorTile, TileDefinitions.ColorWoodFg,      true, true, false, 0, false, false),
        new(ItemDefinitions.StoneFloorTile,  CategoryFloorTile, TileDefinitions.GlyphFloorTile, TileDefinitions.ColorStoneTileFg, true, true, false, 0, false, false),
        new(ItemDefinitions.CopperFloorTile, CategoryFloorTile, TileDefinitions.GlyphFloorTile, TileDefinitions.ColorCopperFg,    true, true, false, 0, false, false),
        new(ItemDefinitions.IronFloorTile,   CategoryFloorTile, TileDefinitions.GlyphFloorTile, TileDefinitions.ColorIronFg,      true, true, false, 0, false, false),
        new(ItemDefinitions.GoldFloorTile,   CategoryFloorTile, TileDefinitions.GlyphFloorTile, TileDefinitions.ColorGoldFg,      true, true, false, 0, false, false),
    ];

    public static PlaceableDefinition Get(int itemTypeId)
    {
        // Try new registry via LegacyItemBridge
        var newDef = LegacyItemBridge.GetNewDefinition(itemTypeId);
        if (newDef?.Furniture != null)
        {
            var f = newDef.Furniture;
            return new PlaceableDefinition(
                itemTypeId,
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

        return itemTypeId > 0 && itemTypeId < _byId.Length ? _byId[itemTypeId] : default;
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
