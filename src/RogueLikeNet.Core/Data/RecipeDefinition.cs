namespace RogueLikeNet.Core.Data;

/// <summary>
/// Defines a crafting recipe. Loaded from JSON data files.
/// </summary>
public sealed class RecipeDefinition
{
    public string Id { get; set; } = "";
    public int NumericId { get; set; }
    public string Name { get; set; } = "";
    public CraftingStationType Station { get; set; }
    public RecipeIngredient[] Ingredients { get; set; } = [];
    public RecipeResult Result { get; set; } = new();
}

public sealed class RecipeIngredient
{
    public string ItemId { get; set; } = "";
    public int Count { get; set; } = 1;
}

public sealed class RecipeResult
{
    public string ItemId { get; set; } = "";
    public int Count { get; set; } = 1;
}
