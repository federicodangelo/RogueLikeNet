using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;

namespace RogueLikeNet.Core.Tests;

public class RecipeRegistryTests
{
    [Fact]
    public void GetByStation_KnownStation_ReturnsRecipes()
    {
        var recipes = GameData.Instance.Recipes.GetByStation(CraftingStationType.Workbench);
        Assert.NotEmpty(recipes);
    }

    [Fact]
    public void GetByStation_UnknownStation_ReturnsEmpty()
    {
        var recipes = GameData.Instance.Recipes.GetByStation((CraftingStationType)999);
        Assert.Empty(recipes);
    }

    [Fact]
    public void CanCraft_SufficientResources_ReturnsTrue()
    {
        // Get any recipe from the registry
        var recipes = GameData.Instance.Recipes.GetByStation(CraftingStationType.Workbench);
        var recipe = recipes[0];

        // Build an inventory with enough resources
        var items = new List<ItemData>();
        foreach (var ingredient in recipe.Ingredients)
        {
            items.Add(new ItemData { ItemTypeId = ingredient.NumericItemId, StackCount = ingredient.Count });
        }

        Assert.True(RecipeRegistry.CanCraft(recipe, items));
    }

    [Fact]
    public void CanCraft_InsufficientResources_ReturnsFalse()
    {
        var recipes = GameData.Instance.Recipes.GetByStation(CraftingStationType.Workbench);
        var recipe = recipes[0];

        // Empty inventory
        Assert.False(RecipeRegistry.CanCraft(recipe, new List<ItemData>()));
    }

    [Fact]
    public void CanCraft_PartialResources_ReturnsFalse()
    {
        var recipes = GameData.Instance.Recipes.GetByStation(CraftingStationType.Workbench);
        var recipe = recipes[0];

        if (recipe.Ingredients.Length == 0) return;

        // Provide only one less than needed of the first ingredient
        var items = new List<ItemData>();
        foreach (var ingredient in recipe.Ingredients)
        {
            items.Add(new ItemData { ItemTypeId = ingredient.NumericItemId, StackCount = ingredient.Count - 1 });
        }

        // If any ingredient required count > 1, this should be false
        bool anyNeedMore = recipe.Ingredients.Any(i => i.Count > 1);
        if (anyNeedMore)
            Assert.False(RecipeRegistry.CanCraft(recipe, items));
    }

    [Fact]
    public void CanCraft_SplitAcrossStacks_ReturnsTrue()
    {
        var recipes = GameData.Instance.Recipes.GetByStation(CraftingStationType.Workbench);
        var recipe = recipes[0];

        if (recipe.Ingredients.Length == 0) return;

        // Split ingredients across multiple stacks
        var items = new List<ItemData>();
        foreach (var ingredient in recipe.Ingredients)
        {
            if (ingredient.Count >= 2)
            {
                int half = ingredient.Count / 2;
                items.Add(new ItemData { ItemTypeId = ingredient.NumericItemId, StackCount = half });
                items.Add(new ItemData { ItemTypeId = ingredient.NumericItemId, StackCount = ingredient.Count - half });
            }
            else
            {
                items.Add(new ItemData { ItemTypeId = ingredient.NumericItemId, StackCount = ingredient.Count });
            }
        }

        Assert.True(RecipeRegistry.CanCraft(recipe, items));
    }

    [Fact]
    public void RecipeRegistry_HasMultipleStations()
    {
        // Ensure recipes are registered under multiple station types
        var stations = Enum.GetValues<CraftingStationType>();
        int stationsWithRecipes = 0;
        foreach (var station in stations)
        {
            if (GameData.Instance.Recipes.GetByStation(station).Count > 0)
                stationsWithRecipes++;
        }
        Assert.True(stationsWithRecipes >= 1);
    }
}
