namespace RogueLikeNet.Core.Data;

/// <summary>
/// Holds all loaded item definitions and provides O(1) lookup by numeric ID or string ID.
/// </summary>
public sealed class ItemRegistry : BaseRegistry<ItemDefinition>
{
    // ── Placeable helpers ────────────────────────────────────────────

    public ItemDefinition[] GetAllPlaceables()
    {
        if (Count == 0) return [];
        return All.Where(d => d.IsPlaceable).ToArray();
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

    public bool IsPlaceableDoor(int itemTypeId) => Get(itemTypeId)?.Furniture?.FurnitureType == FurnitureType.Door;
    public bool IsPlaceableDoorOpen(int itemTypeId, int extra) => IsPlaceableDoor(itemTypeId) && extra != 0;
    public bool IsPlaceableDoorClosed(int itemTypeId, int extra) => IsPlaceableDoor(itemTypeId) && extra == 0;
    public bool IsPlaceableWall(int itemTypeId) => Get(itemTypeId) is { Category: ItemCategory.Furniture, Furniture.FurnitureType: FurnitureType.Wall } or { Category: ItemCategory.Block };

    public bool IsPlaceableHasState(int itemTypeId)
    {
        var def = Get(itemTypeId);
        if (def?.Furniture != null) return def.Furniture.StateType != PlaceableStateType.None;
        return false;
    }
}
