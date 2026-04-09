namespace RogueLikeNet.Core.Data;

/// <summary>
/// Holds all loaded item definitions and provides O(1) lookup by numeric ID or string ID.
/// </summary>
public sealed class ItemRegistry
{
    public const int PlaceableCategoryNone = 0;
    public const int PlaceableCategoryDoor = 1;
    public const int PlaceableCategoryWall = 2;
    public const int PlaceableCategoryDecoration = 3;
    public const int PlaceableCategoryFloorTile = 4;

    private readonly Dictionary<string, ItemDefinition> _byStringId = new();
    private ItemDefinition?[] _byNumericId = [];

    public IReadOnlyCollection<ItemDefinition> All => _byStringId.Values;

    public void Register(IEnumerable<ItemDefinition> items)
    {
        // Sort by string ID for deterministic numeric ID assignment
        var sorted = items.OrderBy(i => i.Id, StringComparer.Ordinal).ToList();

        // Assign sequential IDs starting from 1 (0 is reserved for None)
        int nextId = 1;
        foreach (var item in sorted)
        {
            item.NumericId = nextId++;
            _byStringId[item.Id] = item;
        }

        int maxId = sorted.Max(i => i.NumericId);
        _byNumericId = new ItemDefinition?[maxId + 1];
        foreach (var item in sorted)
            _byNumericId[item.NumericId] = item;
    }

    public ItemDefinition? Get(string id) =>
        _byStringId.GetValueOrDefault(id);

    public ItemDefinition? Get(int numericId) =>
        numericId > 0 && numericId < _byNumericId.Length ? _byNumericId[numericId] : null;

    public int GetNumericId(string id) =>
        _byStringId.TryGetValue(id, out var def) ? def.NumericId : 0;

    public int Count => _byStringId.Count;

    // ── Placeable helpers ────────────────────────────────────────────

    public ItemDefinition[] GetAllPlaceables()
    {
        if (Count == 0) return [];
        return All.Where(d => d.IsPlaceable).ToArray();
    }

    public int GetPlaceableCategory(int itemTypeId)
    {
        var def = Get(itemTypeId);
        if (def == null) return PlaceableCategoryNone;
        if (def.Furniture != null)
            return FurnitureCategoryFromData(def.Furniture.FurnitureType);
        if (def.Block != null)
            return PlaceableCategoryWall;
        if (def.IsPlaceable)
            return PlaceableCategoryDecoration;
        return PlaceableCategoryNone;
    }

    public bool IsPlaceableWalkable(int itemTypeId, int extra)
    {
        var def = Get(itemTypeId);
        if (def == null || !def.IsPlaceable) return true; // unknown → don't block
        if (def.Furniture != null)
        {
            bool hasState = def.Furniture.StateType != PlaceableStateType.None;
            return hasState && extra != 0 ? def.Furniture.AlternateWalkable : def.Furniture.Walkable;
        }
        if (def.Block != null) return false;
        return true; // decoration
    }

    public bool IsPlaceableTransparent(int itemTypeId, int extra)
    {
        var def = Get(itemTypeId);
        if (def == null || !def.IsPlaceable) return true;
        if (def.Furniture != null)
        {
            bool hasState = def.Furniture.StateType != PlaceableStateType.None;
            return hasState && extra != 0 ? def.Furniture.AlternateTransparent : def.Furniture.Transparent;
        }
        if (def.Block != null) return false;
        return true; // decoration
    }

    public int GetPlaceableGlyphId(int itemTypeId, int extra)
    {
        var def = Get(itemTypeId);
        if (def == null) return 0;
        if (def.Furniture != null)
        {
            bool hasState = def.Furniture.StateType != PlaceableStateType.None;
            return hasState && extra != 0 ? def.Furniture.AlternateGlyphId : def.Furniture.PlacedGlyphId;
        }
        return def.GlyphId;
    }

    public int GetPlaceableFgColor(int itemTypeId, int extra)
    {
        var def = Get(itemTypeId);
        if (def == null) return 0;
        if (def.Furniture != null) return def.Furniture.PlacedFgColor;
        return def.FgColor;
    }

    public bool IsPlaceableDoor(int itemTypeId) => GetPlaceableCategory(itemTypeId) == PlaceableCategoryDoor;
    public bool IsPlaceableDoorOpen(int itemTypeId, int extra) => IsPlaceableDoor(itemTypeId) && extra != 0;
    public bool IsPlaceableDoorClosed(int itemTypeId, int extra) => IsPlaceableDoor(itemTypeId) && extra == 0;
    public bool IsPlaceableWall(int itemTypeId) => GetPlaceableCategory(itemTypeId) == PlaceableCategoryWall;

    public bool IsPlaceableHasState(int itemTypeId)
    {
        var def = Get(itemTypeId);
        if (def?.Furniture != null) return def.Furniture.StateType != PlaceableStateType.None;
        return false;
    }

    private static int FurnitureCategoryFromData(FurnitureType t) => t switch
    {
        FurnitureType.Door => PlaceableCategoryDoor,
        FurnitureType.Wall or FurnitureType.Window => PlaceableCategoryWall,
        FurnitureType.FloorTile => PlaceableCategoryFloorTile,
        _ => PlaceableCategoryDecoration,
    };
}
