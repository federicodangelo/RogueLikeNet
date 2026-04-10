namespace RogueLikeNet.Core.Data;

/// <summary>
/// Central access point for all game data registries.
/// Initialized once at startup via DataLoader.
/// </summary>
public sealed class GameData
{
    public readonly ItemRegistry Items = new();
    public readonly RecipeRegistry Recipes;
    public readonly ResourceNodeRegistry ResourceNodes = new();
    public readonly NpcRegistry Npcs = new();
    public readonly BiomeRegistry Biomes = new();

    public GameData()
    {
        Recipes = new RecipeRegistry(Items);
    }

    /// <summary>
    /// Global singleton instance. Set by DataLoader.Load().
    /// </summary>
    public static GameData Instance { get; set; } = new();
}
