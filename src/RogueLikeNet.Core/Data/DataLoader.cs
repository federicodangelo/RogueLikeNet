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
        TypeInfoResolver = DataJsonContext.Default,
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
        data.Items.Register(items);

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
        data.Recipes.Register(recipes);

        // Load resource nodes
        var nodesFile = Path.Combine(dataDir, "entities", "resource_nodes.json");
        if (File.Exists(nodesFile))
        {
            var nodes = DeserializeFile<ResourceNodeDefinition[]>(nodesFile);
            if (nodes != null)
                data.ResourceNodes.Register(nodes);
        }

        // Load NPCs/monsters
        var monstersFile = Path.Combine(dataDir, "entities", "monsters.json");
        if (File.Exists(monstersFile))
        {
            var npcs = DeserializeFile<NpcDefinition[]>(monstersFile);
            if (npcs != null)
                data.Npcs.Register(npcs);
        }

        // Load biomes
        var biomesFile = Path.Combine(dataDir, "biomes", "biomes.json");
        if (File.Exists(biomesFile))
        {
            var biomes = DeserializeFile<BiomeDefinition[]>(biomesFile);
            if (biomes != null)
                data.Biomes.Register(biomes);
        }

        Validate(data);

        GameData.Instance = data;
        return data;
    }

    /// <summary>
    /// Loads data from in-memory JSON strings (for testing or embedded resources).
    /// Does NOT set GameData.Instance — caller is responsible if needed.
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
                data.Recipes.Register(recipes);
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

        return data;
    }

    /// <summary>
    /// Validates the consistency of all loaded data. Throws if any referenced ID is missing.
    /// </summary>
    public static void Validate(GameData data)
    {
        var errors = new List<string>();

        // Validate recipes
        foreach (var recipe in data.Recipes.All)
        {
            if (data.Items.Get(recipe.Result.ItemId) == null)
                errors.Add($"Recipe '{recipe.Id}': result item '{recipe.Result.ItemId}' not found.");
            foreach (var ing in recipe.Ingredients)
            {
                if (data.Items.Get(ing.ItemId) == null)
                    errors.Add($"Recipe '{recipe.Id}': ingredient item '{ing.ItemId}' not found.");
            }
        }

        // Validate resource nodes
        foreach (var node in data.ResourceNodes.All)
        {
            if (!string.IsNullOrEmpty(node.DropItemId) && data.Items.Get(node.DropItemId) == null)
                errors.Add($"Resource node '{node.Id}': drop item '{node.DropItemId}' not found.");
        }

        // Validate NPCs
        foreach (var npc in data.Npcs.All)
        {
            foreach (var loot in npc.LootTable)
            {
                if (data.Items.Get(loot.ItemId) == null)
                    errors.Add($"NPC '{npc.Id}': loot item '{loot.ItemId}' not found.");
            }
        }

        // Validate biomes
        foreach (var biome in data.Biomes.All)
        {
            if (biome.EnemySpawns != null)
            {
                foreach (var spawn in biome.EnemySpawns)
                {
                    if (data.Npcs.Get(spawn.NpcId) == null)
                        errors.Add($"Biome '{biome.Id}': enemy NPC '{spawn.NpcId}' not found.");
                }
            }
            if (biome.ResourceWeights != null)
            {
                foreach (var rw in biome.ResourceWeights)
                {
                    if (data.ResourceNodes.Get(rw.NodeId) == null)
                        errors.Add($"Biome '{biome.Id}': resource node '{rw.NodeId}' not found.");
                }
            }
        }

        // Validate seed harvest items
        foreach (var item in data.Items.All)
        {
            if (item.Seed != null && !string.IsNullOrEmpty(item.Seed.HarvestItemId))
            {
                if (data.Items.Get(item.Seed.HarvestItemId) == null)
                    errors.Add($"Item '{item.Id}': seed harvest item '{item.Seed.HarvestItemId}' not found.");
            }
        }

        if (errors.Count > 0)
            throw new InvalidOperationException(
                $"Data validation failed with {errors.Count} error(s):\n" + string.Join("\n", errors));
    }

    private static T? DeserializeFile<T>(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }
}
