using System.Text.Json;
using System.Text.Json.Serialization;

namespace RogueLikeNet.Core.Data;

/// <summary>
/// Loads all game data from JSON files into the GameData registries.
/// Uses System.Text.Json with source generators for AOT compatibility.
/// </summary>
public static class DataLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>
    /// Loads all data from JSON files in the given base directory and populates GameData.Instance.
    /// </summary>
    public static GameData Load(string dataDir)
    {
        var data = new GameData();

        // Load items from all JSON files in data/items/
        var items = new List<ItemDefinition>();
        var itemsDir = Path.Combine(dataDir, "items");
        if (Directory.Exists(itemsDir))
        {
            foreach (var file in Directory.GetFiles(itemsDir, "*.json"))
            {
                var loaded = DeserializeFile<ItemDefinition[]>(file);
                if (loaded != null)
                    items.AddRange(loaded);
            }
        }
        data.Items.Register(items, LegacyItemBridge.GetLegacyId);

        // Load recipes from all JSON files in data/recipes/
        var recipes = new List<RecipeDefinition>();
        var recipesDir = Path.Combine(dataDir, "recipes");
        if (Directory.Exists(recipesDir))
        {
            foreach (var file in Directory.GetFiles(recipesDir, "*.json"))
            {
                var loaded = DeserializeFile<RecipeDefinition[]>(file);
                if (loaded != null)
                    recipes.AddRange(loaded);
            }
        }
        data.Recipes.Register(recipes, data.Items);

        // Load resource nodes
        var nodesFile = Path.Combine(dataDir, "entities", "resource_nodes.json");
        if (File.Exists(nodesFile))
        {
            var nodes = DeserializeFile<ResourceNodeDefinition[]>(nodesFile);
            if (nodes != null)
                data.ResourceNodes.Register(nodes, LegacyItemBridge.GetLegacyNodeId);
        }

        // Load NPCs/monsters
        var monstersFile = Path.Combine(dataDir, "entities", "monsters.json");
        if (File.Exists(monstersFile))
        {
            var npcs = DeserializeFile<NpcDefinition[]>(monstersFile);
            if (npcs != null)
                data.Npcs.Register(npcs, LegacyItemBridge.GetLegacyNpcId);
        }

        // Load biomes
        var biomesFile = Path.Combine(dataDir, "biomes", "biomes.json");
        if (File.Exists(biomesFile))
        {
            var biomes = DeserializeFile<BiomeDefinition[]>(biomesFile);
            if (biomes != null)
                data.Biomes.Register(biomes);
        }

        GameData.Instance = data;
        return data;
    }

    /// <summary>
    /// Loads data from in-memory JSON strings (for testing or embedded resources).
    /// </summary>
    public static GameData LoadFromJson(
        string? itemsJson = null,
        string? recipesJson = null,
        string? resourceNodesJson = null,
        string? monstersJson = null,
        string? biomesJson = null)
    {
        var data = new GameData();

        if (itemsJson != null)
        {
            var items = JsonSerializer.Deserialize<ItemDefinition[]>(itemsJson, JsonOptions);
            if (items != null)
                data.Items.Register(items);
        }

        if (recipesJson != null)
        {
            var recipes = JsonSerializer.Deserialize<RecipeDefinition[]>(recipesJson, JsonOptions);
            if (recipes != null)
                data.Recipes.Register(recipes, data.Items);
        }

        if (resourceNodesJson != null)
        {
            var nodes = JsonSerializer.Deserialize<ResourceNodeDefinition[]>(resourceNodesJson, JsonOptions);
            if (nodes != null)
                data.ResourceNodes.Register(nodes);
        }

        if (monstersJson != null)
        {
            var npcs = JsonSerializer.Deserialize<NpcDefinition[]>(monstersJson, JsonOptions);
            if (npcs != null)
                data.Npcs.Register(npcs);
        }

        if (biomesJson != null)
        {
            var biomes = JsonSerializer.Deserialize<BiomeDefinition[]>(biomesJson, JsonOptions);
            if (biomes != null)
                data.Biomes.Register(biomes);
        }

        GameData.Instance = data;
        return data;
    }

    private static T? DeserializeFile<T>(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }
}
