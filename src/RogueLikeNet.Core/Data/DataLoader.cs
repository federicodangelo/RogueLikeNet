using System.Reflection;
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
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>
    /// Loads all data from JSON files in the given base directory
    /// </summary>
    public static GameData Load(string dataDir)
    {
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

        // Load resource nodes
        var nodesFile = Path.Combine(dataDir, "entities", "resource_nodes.json");
        var nodes = Array.Empty<ResourceNodeDefinition>();
        if (File.Exists(nodesFile))
        {
            nodes = DeserializeFile<ResourceNodeDefinition[]>(nodesFile);
        }

        // Load NPCs/monsters
        var monstersFile = Path.Combine(dataDir, "entities", "monsters.json");
        var npcs = Array.Empty<NpcDefinition>();
        if (File.Exists(monstersFile))
        {
            npcs = DeserializeFile<NpcDefinition[]>(monstersFile);
        }

        // Load biomes
        var biomesFile = Path.Combine(dataDir, "biomes", "biomes.json");
        var biomes = Array.Empty<BiomeDefinition>();
        if (File.Exists(biomesFile))
        {
            biomes = DeserializeFile<BiomeDefinition[]>(biomesFile);
        }

        return Load(items, recipes, nodes ?? [], npcs ?? [], biomes ?? []);
    }


    /// <summary>
    /// Loads all game data from embedded resources in the RogueLikeNet.Core assembly.
    /// Used as fallback when the filesystem data directory is unavailable (e.g. browser-wasm).
    /// </summary>
    public static GameData LoadFromEmbeddedResources()
    {
        var assembly = typeof(DataLoader).Assembly;
        var resourceNames = assembly.GetManifestResourceNames();

        // Load items from all data/items/*.json resources
        var items = new List<ItemDefinition>();
        foreach (var name in resourceNames)
        {
            if (name.StartsWith("data/items/", StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal))
            {
                var loaded = DeserializeResource<ItemDefinition[]>(assembly, name);
                if (loaded != null)
                    items.AddRange(loaded);
            }
        }

        // Load recipes from all data/recipes/*.json resources
        var recipes = new List<RecipeDefinition>();
        foreach (var name in resourceNames)
        {
            if (name.StartsWith("data/recipes/", StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal))
            {
                var loaded = DeserializeResource<RecipeDefinition[]>(assembly, name);
                if (loaded != null)
                    recipes.AddRange(loaded);
            }
        }

        // Load resource nodes
        var nodes = DeserializeResource<ResourceNodeDefinition[]>(assembly, "data/entities/resource_nodes.json");

        // Load NPCs/monsters
        var npcs = DeserializeResource<NpcDefinition[]>(assembly, "data/entities/monsters.json");

        // Load biomes
        var biomes = DeserializeResource<BiomeDefinition[]>(assembly, "data/biomes/biomes.json");

        return Load(items, recipes, nodes ?? [], npcs ?? [], biomes ?? []);
    }

    private static GameData Load(IEnumerable<ItemDefinition> items, IEnumerable<RecipeDefinition> recipes, IEnumerable<ResourceNodeDefinition> nodes, IEnumerable<NpcDefinition> npcs, IEnumerable<BiomeDefinition> biomes)
    {
        var data = new GameData();

        data.Items.Register(items);
        data.Recipes.Register(recipes);
        data.ResourceNodes.Register(nodes);
        data.Npcs.Register(npcs);
        data.Biomes.Register(biomes);

        Validate(data);

        return data;
    }

    /// <summary>
    /// Loads data from in-memory JSON strings (for testing or embedded resources).
    /// </summary>
    public static GameData LoadFromJsonForTests(
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
    private static void Validate(GameData data)
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

            // The category of each item must be consistent with its given properties (e.g. placeable, furniture, block)
            switch (item.Category)
            {
                case ItemCategory.Weapon:
                    if (item.Weapon == null)
                        errors.Add($"Item '{item.Id}': category is Weapon but Weapon data is missing.");
                    if (item.Armor != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Armor data is present.");
                    if (item.Tool != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Tool data is present.");
                    if (item.Food != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Food data is present.");
                    if (item.Potion != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Potion data is present.");
                    if (item.Block != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Block data is present.");
                    if (item.Seed != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Seed data is present.");
                    if (item.Placeable != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Furniture data is present.");
                    if (item.Ammo != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Ammo data is present.");
                    break;
                case ItemCategory.Armor:
                    if (item.Armor == null)
                        errors.Add($"Item '{item.Id}': category is Armor but Armor data is missing.");
                    if (item.Weapon != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Weapon data is present.");
                    if (item.Tool != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Tool data is present.");
                    if (item.Food != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Food data is present.");
                    if (item.Potion != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Potion data is present.");
                    if (item.Block != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Block data is present.");
                    if (item.Seed != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Seed data is present.");
                    if (item.Placeable != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Furniture data is present.");
                    if (item.Ammo != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Ammo data is present.");
                    break;
                case ItemCategory.Tool:
                    if (item.Tool == null)
                        errors.Add($"Item '{item.Id}': category is Tool but Tool data is missing.");
                    if (item.Weapon != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Weapon data is present.");
                    if (item.Armor != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Armor data is present.");
                    if (item.Food != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Food data is present.");
                    if (item.Potion != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Potion data is present.");
                    if (item.Block != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Block data is present.");
                    if (item.Seed != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Seed data is present.");
                    if (item.Placeable != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Furniture data is present.");
                    if (item.Ammo != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Ammo data is present.");
                    break;
                case ItemCategory.Food:
                    if (item.Food == null)
                        errors.Add($"Item '{item.Id}': category is Food but Food data is missing.");
                    if (item.Weapon != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Weapon data is present.");
                    if (item.Armor != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Armor data is present.");
                    if (item.Tool != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Tool data is present.");
                    if (item.Potion != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Potion data is present.");
                    if (item.Block != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Block data is present.");
                    if (item.Seed != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Seed data is present.");
                    if (item.Placeable != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Furniture data is present.");
                    if (item.Ammo != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Ammo data is present.");
                    break;
                case ItemCategory.Potion:
                    if (item.Potion == null)
                        errors.Add($"Item '{item.Id}': category is Potion but Potion data is missing.");
                    if (item.Weapon != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Weapon data is present.");
                    if (item.Armor != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Armor data is present.");
                    if (item.Tool != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Tool data is present.");
                    if (item.Food != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Food data is present.");
                    if (item.Block != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Block data is present.");
                    if (item.Seed != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Seed data is present.");
                    if (item.Placeable != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Furniture data is present.");
                    if (item.Ammo != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Ammo data is present.");
                    break;
                case ItemCategory.Block:
                    if (item.Block == null)
                        errors.Add($"Item '{item.Id}': category is Block but Block data is missing.");
                    if (item.Weapon != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Weapon data is present.");
                    if (item.Armor != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Armor data is present.");
                    if (item.Tool != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Tool data is present.");
                    if (item.Food != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Food data is present.");
                    if (item.Potion != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Potion data is present.");
                    if (item.Seed != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Seed data is present.");
                    if (item.Placeable != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Furniture data is present.");
                    if (item.Ammo != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Ammo data is present.");
                    break;
                case ItemCategory.Seed:
                    if (item.Seed == null)
                        errors.Add($"Item '{item.Id}': category is Seed but Seed data is missing.");
                    if (item.Weapon != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Weapon data is present.");
                    if (item.Armor != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Armor data is present.");
                    if (item.Tool != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Tool data is present.");
                    if (item.Food != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Food data is present.");
                    if (item.Potion != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Potion data is present.");
                    if (item.Block != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Block data is present.");
                    if (item.Placeable != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Furniture data is present.");
                    if (item.Ammo != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Ammo data is present.");
                    break;
                case ItemCategory.Placeable:
                    if (item.Placeable == null)
                        errors.Add($"Item '{item.Id}': category is Placeable but Placeable data is missing.");
                    if (item.Weapon != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Weapon data is present.");
                    if (item.Armor != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Armor data is present.");
                    if (item.Tool != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Tool data is present.");
                    if (item.Food != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Food data is present.");
                    if (item.Potion != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Potion data is present.");
                    if (item.Block != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Block data is present.");
                    if (item.Seed != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Seed data is present.");
                    if (item.Ammo != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Ammo data is present.");
                    break;
                case ItemCategory.Ammo:
                    if (item.Ammo == null)
                        errors.Add($"Item '{item.Id}': category is Ammo but Ammo data is missing.");
                    if (item.Weapon != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Weapon data is present.");
                    if (item.Armor != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Armor data is present.");
                    if (item.Tool != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Tool data is present.");
                    if (item.Food != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Food data is present.");
                    if (item.Potion != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Potion data is present.");
                    if (item.Block != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Block data is present.");
                    if (item.Seed != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Seed data is present.");
                    if (item.Placeable != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Furniture data is present.");
                    break;

                case ItemCategory.Magic:
                case ItemCategory.Misc:
                    // All category-specific data should be null for these categories
                    if (item.Weapon != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Weapon data is present.");
                    if (item.Armor != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Armor data is present.");
                    if (item.Tool != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Tool data is present.");
                    if (item.Food != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Food data is present.");
                    if (item.Potion != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Potion data is present.");
                    if (item.Block != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Block data is present.");
                    if (item.Seed != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Seed data is present.");
                    if (item.Placeable != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Furniture data is present.");
                    if (item.Ammo != null)
                        errors.Add($"Item '{item.Id}': category is {item.Category} but Ammo data is present.");
                    break;
            }

            if (item.IsEquippable)
            {
                if (item.EquipSlot == null)
                    errors.Add($"Item '{item.Id}': is equippable but EquipSlot is not set.");
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

    private static T? DeserializeResource<T>(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null) return default;
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }
}
