using RogueLikeNet.Core.Data;

namespace RogueLikeNet.Core.Definitions;

public readonly record struct CraftingIngredient(int ItemTypeId, int Count);

public readonly record struct CraftingRecipe(
    int RecipeId, string Name, int ResultItemTypeId, int ResultCount,
    CraftingIngredient[] Ingredients
);

public static class CraftingDefinitions
{
    private static CraftingRecipe[]? _cachedAll;

    /// <summary>
    /// All available recipes, built from JSON recipe registry.
    /// </summary>
    public static CraftingRecipe[] All
    {
        get
        {
            if (_cachedAll != null) return _cachedAll;

            var data = GameData.Instance;
            if (data.Recipes.Count == 0) return [];

            var sorted = data.Recipes.All.OrderBy(r => r.NumericId).ToArray();
            var result = new CraftingRecipe[sorted.Length];
            for (int i = 0; i < sorted.Length; i++)
            {
                var r = sorted[i];
                var ingredients = new CraftingIngredient[r.Ingredients.Length];
                for (int j = 0; j < r.Ingredients.Length; j++)
                    ingredients[j] = new CraftingIngredient(data.Items.GetNumericId(r.Ingredients[j].ItemId), r.Ingredients[j].Count);

                result[i] = new CraftingRecipe(
                    r.NumericId,
                    r.Name,
                    data.Items.GetNumericId(r.Result.ItemId),
                    r.Result.Count,
                    ingredients);
            }
            _cachedAll = result;
            return result;
        }
    }

    public static CraftingRecipe Get(int recipeId) =>
        Array.Find(All, r => r.RecipeId == recipeId);

    /// <summary>
    /// Returns true if the player inventory has enough resources to craft the recipe.
    /// </summary>
    public static bool CanCraft(CraftingRecipe recipe, IReadOnlyList<Components.ItemData> items)
    {
        foreach (var ingredient in recipe.Ingredients)
        {
            int have = 0;
            foreach (var item in items)
            {
                if (item.ItemTypeId == ingredient.ItemTypeId)
                    have += item.StackCount;
            }
            if (have < ingredient.Count) return false;
        }
        return true;
    }

    /// <summary>
    /// Clears the cached recipe array (for testing when GameData changes).
    /// </summary>
    public static void InvalidateCache() => _cachedAll = null;
}
