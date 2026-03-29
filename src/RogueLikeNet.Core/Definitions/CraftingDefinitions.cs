namespace RogueLikeNet.Core.Definitions;

public readonly record struct CraftingIngredient(int ItemTypeId, int Count);

public readonly record struct CraftingRecipe(
    int RecipeId, string Name, int ResultItemTypeId, int ResultCount,
    CraftingIngredient[] Ingredients
);

public static class CraftingDefinitions
{
    public static readonly CraftingRecipe[] All =
    [
        new(0,  "Wooden Door",   ItemDefinitions.WoodenDoor,   1, [new(ItemDefinitions.Wood, 5)]),
        new(1,  "Wooden Wall",   ItemDefinitions.WoodenWall,   1, [new(ItemDefinitions.Wood, 3)]),
        new(2,  "Wooden Window", ItemDefinitions.WoodenWindow, 1, [new(ItemDefinitions.Wood, 4)]),
        new(3,  "Copper Door",   ItemDefinitions.CopperDoor,   1, [new(ItemDefinitions.CopperOre, 5), new(ItemDefinitions.Wood, 2)]),
        new(4,  "Copper Wall",   ItemDefinitions.CopperWall,   1, [new(ItemDefinitions.CopperOre, 4)]),
        new(5,  "Iron Door",     ItemDefinitions.IronDoor,     1, [new(ItemDefinitions.IronOre, 5), new(ItemDefinitions.Wood, 2)]),
        new(6,  "Iron Wall",     ItemDefinitions.IronWall,     1, [new(ItemDefinitions.IronOre, 4)]),
        new(7,  "Gold Door",     ItemDefinitions.GoldDoor,     1, [new(ItemDefinitions.GoldOre, 5), new(ItemDefinitions.Wood, 2)]),
        new(8,  "Gold Wall",     ItemDefinitions.GoldWall,     1, [new(ItemDefinitions.GoldOre, 4)]),
        // Furniture
        new(9,  "Wooden Table",     ItemDefinitions.WoodenTable,     1, [new(ItemDefinitions.Wood, 4)]),
        new(10, "Wooden Chair",     ItemDefinitions.WoodenChair,     1, [new(ItemDefinitions.Wood, 2)]),
        new(11, "Wooden Bed",       ItemDefinitions.WoodenBed,       1, [new(ItemDefinitions.Wood, 6)]),
        new(12, "Wooden Bookshelf", ItemDefinitions.WoodenBookshelf, 1, [new(ItemDefinitions.Wood, 5)]),
        // Floor tiles
        new(13, "Wooden Floor",  ItemDefinitions.WoodenFloorTile, 4, [new(ItemDefinitions.Wood, 2)]),
        new(14, "Stone Floor",   ItemDefinitions.StoneFloorTile,  4, [new(ItemDefinitions.CopperOre, 1)]),
        new(15, "Copper Floor",  ItemDefinitions.CopperFloorTile, 4, [new(ItemDefinitions.CopperOre, 2)]),
        new(16, "Iron Floor",    ItemDefinitions.IronFloorTile,   4, [new(ItemDefinitions.IronOre, 2)]),
        new(17, "Gold Floor",    ItemDefinitions.GoldFloorTile,   4, [new(ItemDefinitions.GoldOre, 2)]),
    ];

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
}
