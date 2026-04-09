namespace RogueLikeNet.Core.Data;

/// <summary>
/// Central access point for all game data registries.
/// Initialized once at startup via DataLoader.
/// </summary>
public sealed class GameData
{
    public ItemRegistry Items { get; } = new();
    public RecipeRegistry Recipes { get; } = new();
    public ResourceNodeRegistry ResourceNodes { get; } = new();
    public NpcRegistry Npcs { get; } = new();
    public BiomeRegistry Biomes { get; } = new();

    /// <summary>
    /// Global singleton instance. Set by DataLoader.Load().
    /// </summary>
    public static GameData Instance { get; set; } = new();
}
