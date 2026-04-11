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
        if (def.Placeable != null)
        {
            bool hasState = def.Placeable.StateType != PlaceableStateType.None;
            return hasState && extra != 0 ? def.Placeable.AlternateWalkable : def.Placeable.Walkable;
        }
        return true; // decoration
    }

    public bool IsPlaceableTransparent(int itemTypeId, int extra)
    {
        var def = Get(itemTypeId);
        if (def == null || !def.IsPlaceable) return true;
        if (def.Placeable != null)
        {
            bool hasState = def.Placeable.StateType != PlaceableStateType.None;
            return hasState && extra != 0 ? def.Placeable.AlternateTransparent : def.Placeable.Transparent;
        }
        return true; // decoration
    }

    public int GetPlaceableGlyphId(int itemTypeId, int extra)
    {
        var def = Get(itemTypeId);
        if (def == null) return 0;
        if (def.Placeable != null)
        {
            bool hasState = def.Placeable.StateType != PlaceableStateType.None;
            return hasState && extra != 0 ? def.Placeable.AlternateGlyphId : def.GlyphId;
        }
        return def.GlyphId;
    }

    public int GetPlaceableFgColor(int itemTypeId, int extra)
    {
        var def = Get(itemTypeId);
        if (def == null) return 0;
        return GetPlaceableFgColor(def, extra);
    }

    public int GetPlaceableFgColor(ItemDefinition def, int extra)
    {
        if (def.Placeable != null) return def.FgColor;
        return def.FgColor;
    }

    public bool IsPlaceableDoor(int itemTypeId) => Get(itemTypeId)?.Placeable?.PlaceableType == PlaceableType.Door;
    public bool IsPlaceableDoorOpen(int itemTypeId, int extra) => IsPlaceableDoor(itemTypeId) && extra != 0;
    public bool IsPlaceableDoorClosed(int itemTypeId, int extra) => IsPlaceableDoor(itemTypeId) && extra == 0;
    public bool IsPlaceableWall(int itemTypeId) => Get(itemTypeId) is { Category: ItemCategory.Placeable, Placeable.PlaceableType: PlaceableType.Wall };

    public bool IsPlaceableHasState(int itemTypeId)
    {
        var def = Get(itemTypeId);
        if (def?.Placeable != null) return def.Placeable.StateType != PlaceableStateType.None;
        return false;
    }

    public CraftingStationType? GetPlaceableCraftingStationType(int itemTypeId)
    {
        var def = Get(itemTypeId);
        return def?.Placeable?.CraftingStationType;
    }

    public int GetPlaceableLightRadius(int itemTypeId)
    {
        var def = Get(itemTypeId);
        return def?.Placeable?.LightRadius ?? 0;
    }

    public int GetPlaceableLightColor(int itemTypeId)
    {
        var def = Get(itemTypeId);
        return def?.Placeable?.LightColor ?? 0xFFFFFF;
    }
}
