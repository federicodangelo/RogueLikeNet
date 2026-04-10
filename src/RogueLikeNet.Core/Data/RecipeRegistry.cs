using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Core.Data;

/// <summary>
/// Holds all loaded recipe definitions and provides lookups by station type or numeric ID.
/// </summary>
public sealed class RecipeRegistry : BaseRegistry<RecipeDefinition>
{
    private readonly Dictionary<CraftingStationType, List<RecipeDefinition>> _byStation = new();

    private readonly ItemRegistry _itemRegistry;

    public RecipeRegistry(ItemRegistry itemRegistry)
    {
        _itemRegistry = itemRegistry;
    }

    protected override void ExtraRegister(RecipeDefinition recipe)
    {
        // Resolve string item IDs to numeric IDs
        recipe.Result.NumericItemId = _itemRegistry.GetNumericId(recipe.Result.ItemId);
        foreach (var ingredient in recipe.Ingredients)
            ingredient.NumericItemId = _itemRegistry.GetNumericId(ingredient.ItemId);

        if (!_byStation.TryGetValue(recipe.Station, out var list))
        {
            list = new List<RecipeDefinition>();
            _byStation[recipe.Station] = list;
        }
        list.Add(recipe);
    }

    public IReadOnlyList<RecipeDefinition> GetByStation(CraftingStationType station) =>
        _byStation.TryGetValue(station, out var list) ? list : [];

    /// <summary>
    /// Returns true if the player inventory has enough resources to craft the recipe.
    /// </summary>
    public static bool CanCraft(RecipeDefinition recipe, IReadOnlyList<ItemData> items)
    {
        foreach (var ingredient in recipe.Ingredients)
        {
            int have = 0;
            foreach (var item in items)
            {
                if (item.ItemTypeId == ingredient.NumericItemId)
                    have += item.StackCount;
            }
            if (have < ingredient.Count) return false;
        }
        return true;
    }
}
