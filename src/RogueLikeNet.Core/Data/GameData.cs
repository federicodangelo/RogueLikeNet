namespace RogueLikeNet.Core.Data;

/// <summary>
/// Central access point for all game data registries.
/// Initialized once at startup via DataLoader.
/// </summary>
public sealed class GameData
{
    public readonly TilesRegistry Tiles = new();
    public readonly ItemRegistry Items = new();
    public readonly RecipeRegistry Recipes;
    public readonly ResourceNodeRegistry ResourceNodes = new();
    public readonly NpcRegistry Npcs = new();
    public readonly BiomeRegistry Biomes;
    public readonly AnimalRegistry Animals = new();
    public readonly ClassRegistry Classes = new();
    public readonly PlayerLevelTable PlayerLevels = new();
    public readonly StructureRegistry Structures = new();
    public readonly TownRegistry Towns = new();

    public GameData()
    {
        Recipes = new RecipeRegistry(Items);
        Biomes = new BiomeRegistry(Tiles, Items);
    }

    /// <summary>
    /// Global singleton instance. Set by DataLoader.Load().
    /// </summary>
    public static GameData Instance { get; set; } = new();

    public void LogLoadedData(TextWriter output)
    {
        output.WriteLine($"[GameData] Loaded {Tiles.Count} tiles");
        output.WriteLine($"[GameData] Loaded {Items.Count} items");
        output.WriteLine($"[GameData] Loaded {Recipes.Count} recipes");
        output.WriteLine($"[GameData] Loaded {ResourceNodes.Count} resource nodes");
        output.WriteLine($"[GameData] Loaded {Npcs.Count} NPCs");
        output.WriteLine($"[GameData] Loaded {Biomes.Count} biomes");
        output.WriteLine($"[GameData] Loaded {Animals.Count} animals");
        output.WriteLine($"[GameData] Loaded {Classes.Count} classes");
        output.WriteLine($"[GameData] Loaded {PlayerLevels.Count} player levels");
        output.WriteLine($"[GameData] Loaded {Structures.Count} structures");
        output.WriteLine($"[GameData] Loaded {Towns.Count} town types");
    }
}
