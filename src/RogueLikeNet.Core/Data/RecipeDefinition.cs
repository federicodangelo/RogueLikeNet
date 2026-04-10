using System.Text.Json.Serialization;

namespace RogueLikeNet.Core.Data;

public enum CraftingStationType
{
    Hand = 0,
    Workbench = 1,
    Forge = 2,
    Anvil = 3,
    Furnace = 4,
    CookingPot = 5,
    Alchemy = 6,
    Loom = 7,
    TanningRack = 8,
    StoneCutter = 9,
    Sawmill = 10,
}

/// <summary>
/// Defines a crafting recipe. Loaded from JSON data files.
/// </summary>
public sealed class RecipeDefinition : BaseDefinition
{
    public CraftingStationType Station { get; set; }
    public RecipeIngredient[] Ingredients { get; set; } = [];
    public RecipeResult Result { get; set; } = new();
}

public sealed class RecipeIngredient
{
    public string ItemId { get; set; } = "";
    public int Count { get; set; } = 1;

    [JsonIgnore]
    public int NumericItemId { get; set; }
}

public sealed class RecipeResult
{
    public string ItemId { get; set; } = "";
    public int Count { get; set; } = 1;

    [JsonIgnore]
    public int NumericItemId { get; set; }
}
