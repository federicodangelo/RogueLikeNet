using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Engine.Rendering.Base;
using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Data;

public class HexConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            return reader.GetInt32();

        var value = reader.GetString();
        if (value == null)
            return 0;
        // If the string starts with "#" or "0x", parse it as hex; otherwise parse as decimal
        if (value.StartsWith("#"))
            return int.Parse(value[1..], System.Globalization.NumberStyles.HexNumber);
        else if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return int.Parse(value.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber);
        else
            return int.Parse(value);
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        writer.WriteStringValue($"#{value:X}");
    }
}

/// <summary>
/// Converts between CP437 glyph indices (int) and their Unicode character representation (string).
/// In JSON, glyphs are stored as single-character strings (e.g. "♣" for CP437 index 5).
/// Falls back to integer parsing for backward compatibility.
/// </summary>
public class GlyphConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            return reader.GetInt32();

        var value = reader.GetString();
        if (value == null || value.Length == 0)
            return 0;

        // Single character → look up CP437 index
        if (value.Length == 1 && MiniBitmapFont.UnicodeToCp437.TryGetValue(value[0], out var cp437Index))
            return cp437Index;

        // Fall back to integer parsing for backward compatibility
        if (int.TryParse(value, out var intValue))
            return intValue;

        return 0;
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        if (value > 0 && value < MiniBitmapFont.Cp437ToUnicode.Length)
            writer.WriteStringValue(MiniBitmapFont.Cp437ToUnicode[value].ToString());
        else
            writer.WriteNumberValue(value);
    }
}

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
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase), new HexConverter() },
        TypeInfoResolver = DataJsonContext.Default,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>
    /// Loads all data from JSON files in the given base directory
    /// </summary>
    public static GameData Load(string dataDir)
    {
        // Load tiles from data/tiles/
        var tiles = new List<TileDefinition>();
        var tilesDir = Path.Combine(dataDir, "tiles");
        if (Directory.Exists(tilesDir))
        {
            foreach (var file in Directory.GetFiles(tilesDir, "*.json"))
            {
                var loaded = DeserializeFile<TileDefinition[]>(file);
                if (loaded != null)
                    tiles.AddRange(loaded);
            }
        }

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

        // Load animals
        var animalsFile = Path.Combine(dataDir, "entities", "animals.json");
        var animals = Array.Empty<AnimalDefinition>();
        if (File.Exists(animalsFile))
        {
            animals = DeserializeFile<AnimalDefinition[]>(animalsFile);
        }

        // Load classes from data/classes/
        var classes = new List<ClassDataDefinition>();
        var classesDir = Path.Combine(dataDir, "classes");
        if (Directory.Exists(classesDir))
        {
            foreach (var file in Directory.GetFiles(classesDir, "*.json"))
            {
                var loaded = DeserializeFile<ClassDataDefinition>(file);
                if (loaded != null)
                    classes.Add(loaded);
            }
        }

        // Load player levels
        var levelsFile = Path.Combine(dataDir, "meta", "player-levels.json");
        var playerLevels = Array.Empty<PlayerLevelDefinition>();
        if (File.Exists(levelsFile))
        {
            playerLevels = DeserializeFile<PlayerLevelDefinition[]>(levelsFile);
        }

        // Load structures from data/structures/
        var structures = new List<StructureDefinition>();
        var structuresDir = Path.Combine(dataDir, "structures");
        if (Directory.Exists(structuresDir))
        {
            foreach (var file in Directory.GetFiles(structuresDir, "*.json"))
            {
                var loaded = DeserializeFile<StructureDefinition[]>(file);
                if (loaded != null)
                    structures.AddRange(loaded);
            }
        }

        // Load towns from data/towns/
        var towns = new List<TownDefinition>();
        var townsDir = Path.Combine(dataDir, "towns");
        if (Directory.Exists(townsDir))
        {
            foreach (var file in Directory.GetFiles(townsDir, "*.json"))
            {
                var loaded = DeserializeFile<TownDefinition[]>(file);
                if (loaded != null)
                    towns.AddRange(loaded);
            }
        }

        return Load(tiles, items, recipes, nodes ?? [], npcs ?? [], biomes ?? [], animals ?? [], classes, playerLevels ?? [], structures, towns);
    }
    /// Loads all game data from embedded resources in the RogueLikeNet.Core assembly.
    /// Used as fallback when the filesystem data directory is unavailable (e.g. browser-wasm).
    /// </summary>
    public static GameData LoadFromEmbeddedResources()
    {
        var assembly = typeof(DataLoader).Assembly;
        var resourceNames = assembly.GetManifestResourceNames();

        // Load tiles from all data/tiles/*.json resources
        var tiles = new List<TileDefinition>();
        foreach (var name in resourceNames)
        {
            if (name.StartsWith("data/tiles/", StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal))
            {
                var loaded = DeserializeResource<TileDefinition[]>(assembly, name);
                if (loaded != null)
                    tiles.AddRange(loaded);
            }
        }

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

        // Load animals
        var animals = DeserializeResource<AnimalDefinition[]>(assembly, "data/entities/animals.json");

        // Load classes from data/classes/*.json resources
        var classes = new List<ClassDataDefinition>();
        foreach (var name in resourceNames)
        {
            if (name.StartsWith("data/classes/", StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal))
            {
                var loaded = DeserializeResource<ClassDataDefinition>(assembly, name);
                if (loaded != null)
                    classes.Add(loaded);
            }
        }

        // Load player levels
        var playerLevels = DeserializeResource<PlayerLevelDefinition[]>(assembly, "data/meta/player-levels.json");

        // Load structures from data/structures/*.json resources
        var structures = new List<StructureDefinition>();
        foreach (var name in resourceNames)
        {
            if (name.StartsWith("data/structures/", StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal))
            {
                var loaded = DeserializeResource<StructureDefinition[]>(assembly, name);
                if (loaded != null)
                    structures.AddRange(loaded);
            }
        }

        // Load towns from data/towns/*.json resources
        var towns = new List<TownDefinition>();
        foreach (var name in resourceNames)
        {
            if (name.StartsWith("data/towns/", StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal))
            {
                var loaded = DeserializeResource<TownDefinition[]>(assembly, name);
                if (loaded != null)
                    towns.AddRange(loaded);
            }
        }

        return Load(tiles, items, recipes, nodes ?? [], npcs ?? [], biomes ?? [], animals ?? [], classes, playerLevels ?? [], structures, towns);
    }

    private static GameData Load(IEnumerable<TileDefinition> tiles, IEnumerable<ItemDefinition> items, IEnumerable<RecipeDefinition> recipes, IEnumerable<ResourceNodeDefinition> nodes, IEnumerable<NpcDefinition> npcs, IEnumerable<BiomeDefinition> biomes, IEnumerable<AnimalDefinition> animals, IEnumerable<ClassDataDefinition> classes, IEnumerable<PlayerLevelDefinition> playerLevels, IEnumerable<StructureDefinition> structures, IEnumerable<TownDefinition> towns)
    {
        var data = new GameData();

        data.Tiles.Register(tiles);
        data.Items.Register(items);
        data.Recipes.Register(recipes);
        data.ResourceNodes.Register(nodes);
        data.Npcs.Register(npcs);
        data.Biomes.Register(biomes);
        data.Animals.Register(animals);
        data.Classes.Register(classes);
        data.PlayerLevels.Load(playerLevels);
        data.Structures.Register(structures);
        data.Towns.Register(towns);

        Validate(data);

        return data;
    }

    /// <summary>
    /// Loads data from in-memory JSON strings (for testing or embedded resources).
    /// </summary>
    public static GameData LoadFromJsonForTests(
        string? tilesJson = null,
        string? itemsJson = null,
        string? recipesJson = null,
        string? resourceNodesJson = null,
        string? monstersJson = null,
        string? biomesJson = null,
        string? animalsJson = null,
        string[]? classesJson = null,
        string? playerLevelsJson = null)
    {
        var data = new GameData();

        if (tilesJson != null)
        {
            var tiles = JsonSerializer.Deserialize<TileDefinition[]>(tilesJson, JsonOptions);
            if (tiles != null)
                data.Tiles.Register(tiles);
        }

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

        if (animalsJson != null)
        {
            var animals = JsonSerializer.Deserialize<AnimalDefinition[]>(animalsJson, JsonOptions);
            if (animals != null)
                data.Animals.Register(animals);
        }

        if (classesJson != null)
        {
            var classes = new List<ClassDataDefinition>();
            foreach (var json in classesJson)
            {
                var cls = JsonSerializer.Deserialize<ClassDataDefinition>(json, JsonOptions);
                if (cls != null)
                    classes.Add(cls);
            }
            data.Classes.Register(classes);
        }

        if (playerLevelsJson != null)
        {
            var levels = JsonSerializer.Deserialize<PlayerLevelDefinition[]>(playerLevelsJson, JsonOptions);
            if (levels != null)
                data.PlayerLevels.Load(levels);
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
        if (data.Npcs.Count == 0)
            errors.Add("No NPC definitions found.");

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
            if (data.Tiles.Get(biome.FloorTileId) == null)
                errors.Add($"Biome '{biome.Id}': floor tile '{biome.FloorTileId}' not found.");
            if (data.Tiles.Get(biome.WallTileId) == null)
                errors.Add($"Biome '{biome.Id}': wall tile '{biome.WallTileId}' not found.");
            if (biome.Decorations != null)
            {
                foreach (var deco in biome.Decorations)
                {
                    if (data.Tiles.Get(deco.TileId) == null)
                        errors.Add($"Biome '{biome.Id}': decoration tile '{deco.TileId}' not found.");
                }
            }
            if (biome.Liquid != null)
            {
                if (data.Tiles.Get(biome.Liquid.TileId) == null)
                    errors.Add($"Biome '{biome.Id}': liquid tile '{biome.Liquid.TileId}' not found.");
            }
        }

        for (var i = 0; i < BiomeRegistry.BiomeCount; i++)
        {
            if (data.Biomes.Get((BiomeType)i) == null)
                errors.Add($"Missing biome definition for biome type {(BiomeType)i}");
        }

        // Validate animals
        foreach (var animal in data.Animals.All)
        {
            if (!string.IsNullOrEmpty(animal.ProduceItemId) && data.Items.Get(animal.ProduceItemId) == null)
                errors.Add($"Animal '{animal.Id}': produce item '{animal.ProduceItemId}' not found.");
            if (!string.IsNullOrEmpty(animal.FeedItemId) && data.Items.Get(animal.FeedItemId) == null)
                errors.Add($"Animal '{animal.Id}': feed item '{animal.FeedItemId}' not found.");
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

        foreach (var s in data.Structures.All)
        {
            if (string.IsNullOrEmpty(s.Category))
                errors.Add($"Structure '{s.Id}': category is empty.");
            if (s.Width <= 0)
                errors.Add($"Structure '{s.Id}': width must be > 0 (got {s.Width}).");
            if (s.Height <= 0)
                errors.Add($"Structure '{s.Id}': height must be > 0 (got {s.Height}).");

            // Grid dimensions
            if (s.Grid.Length != s.Height)
                errors.Add($"Structure '{s.Id}': grid has {s.Grid.Length} rows but height is {s.Height}.");
            for (int row = 0; row < s.Grid.Length; row++)
            {
                if (s.Grid[row].Length != s.Width)
                    errors.Add($"Structure '{s.Id}': grid row {row} has length {s.Grid[row].Length} but width is {s.Width}.");
            }

            // Legend values reference valid items or special keys
            foreach (var (key, value) in s.Legend)
            {
                if (key.Length != 1)
                    errors.Add($"Structure '{s.Id}': legend key '{key}' must be a single character.");

                if (!StructureDefinition.SpecialLegendValues.Contains(value) && data.Items.Get(value) == null && data.Tiles.Get(value) == null)
                    errors.Add($"Structure '{s.Id}': legend value '{value}' is not a known item, tile or special key.");
            }

            // All grid characters must appear in legend
            for (int row = 0; row < s.Grid.Length; row++)
            {
                foreach (char ch in s.Grid[row])
                {
                    if (!s.Legend.ContainsKey(ch.ToString()))
                        errors.Add($"Structure '{s.Id}': grid character '{ch}' at row {row} not found in legend.");
                }
            }

            // NPC coordinates must be within bounds
            foreach (var npc in s.Npcs)
            {
                if (npc.X < 0 || npc.X >= s.Width || npc.Y < 0 || npc.Y >= s.Height)
                    errors.Add($"Structure '{s.Id}': NPC at ({npc.X},{npc.Y}) is outside grid bounds ({s.Width}x{s.Height}).");
            }

            // Ground item references
            foreach (var gi in s.GroundItems)
            {
                if (gi.X < 0 || gi.X >= s.Width || gi.Y < 0 || gi.Y >= s.Height)
                    errors.Add($"Structure '{s.Id}': ground item at ({gi.X},{gi.Y}) is outside grid bounds ({s.Width}x{s.Height}).");

                if (data.Items.Get(gi.ItemId) == null)
                    errors.Add($"Structure '{s.Id}': ground item '{gi.ItemId}' not found.");
            }

            // CropGrid dimensions and legend
            if (s.CropGrid != null)
            {
                if (s.CropLegend == null)
                    errors.Add($"Structure '{s.Id}': cropGrid is set but cropLegend is missing.");

                if (s.CropGrid.Length != s.Height)
                    errors.Add($"Structure '{s.Id}': cropGrid has {s.CropGrid.Length} rows but height is {s.Height}.");

                for (int row = 0; row < s.CropGrid.Length; row++)
                {
                    if (s.CropGrid[row].Length != s.Width)
                        errors.Add($"Structure '{s.Id}': cropGrid row {row} has length {s.CropGrid[row].Length} but width is {s.Width}.");
                }
                if (s.CropLegend != null)
                {
                    foreach (var (key, seedId) in s.CropLegend)
                    {
                        var seedDef = data.Items.Get(seedId);
                        if (seedDef == null)
                            errors.Add($"Structure '{s.Id}': cropLegend value '{seedId}' is not a known item.");
                        else if (seedDef.Seed == null)
                            errors.Add($"Structure '{s.Id}': cropLegend value '{seedId}' is not a seed item.");
                    }
                }
            }
            else if (s.CropLegend != null)
            {
                errors.Add($"Structure '{s.Id}': cropLegend is set but cropGrid is missing.");
            }

            // AnimalGrid dimensions and legend
            if (s.AnimalGrid != null)
            {
                if (s.AnimalLegend == null)
                    errors.Add($"Structure '{s.Id}': animalGrid is set but animalLegend is missing.");

                if (s.AnimalGrid.Length != s.Height)
                    errors.Add($"Structure '{s.Id}': animalGrid has {s.AnimalGrid.Length} rows but height is {s.Height}.");

                for (int row = 0; row < s.AnimalGrid.Length; row++)
                {
                    if (s.AnimalGrid[row].Length != s.Width)
                        errors.Add($"Structure '{s.Id}': animalGrid row {row} has length {s.AnimalGrid[row].Length} but width is {s.Width}.");
                }
                if (s.AnimalLegend != null)
                {
                    foreach (var (key, animalId) in s.AnimalLegend)
                    {
                        if (data.Animals.Get(animalId) == null)
                            errors.Add($"Structure '{s.Id}': animalLegend value '{animalId}' is not a known animal.");
                    }
                }
            }
            else if (s.AnimalLegend != null)
            {
                errors.Add($"Structure '{s.Id}': animalLegend is set but animalGrid is missing.");
            }
        }

        // Validate towns
        foreach (var town in data.Towns.All)
        {
            if (town.MinTownSize <= 0)
                errors.Add($"Town '{town.Id}': minTownSize must be > 0.");
            if (town.MaxTownSize < town.MinTownSize)
                errors.Add($"Town '{town.Id}': maxTownSize ({town.MaxTownSize}) < minTownSize ({town.MinTownSize}).");
            if (town.MaxNpcs < town.MinNpcs)
                errors.Add($"Town '{town.Id}': maxNpcs ({town.MaxNpcs}) < minNpcs ({town.MinNpcs}).");

            if (town.Structures.Length == 0)
                errors.Add($"Town '{town.Id}': no structure rules defined.");

            foreach (var rule in town.Structures)
            {
                if (string.IsNullOrEmpty(rule.Category))
                    errors.Add($"Town '{town.Id}': structure rule has empty category.");
                else if (data.Structures.GetByCategory(rule.Category).Count == 0 && rule.StructureIds is not { Length: > 0 })
                    errors.Add($"Town '{town.Id}': no structures found for category '{rule.Category}'.");

                if (rule.MaxCount < rule.MinCount)
                    errors.Add($"Town '{town.Id}': rule for '{rule.Category}' has maxCount ({rule.MaxCount}) < minCount ({rule.MinCount}).");

                if (rule.StructureIds != null)
                {
                    foreach (var sid in rule.StructureIds)
                    {
                        if (data.Structures.Get(sid) == null)
                            errors.Add($"Town '{town.Id}': structureId '{sid}' not found.");
                    }
                }
            }

            if (town.BiomeOverrides != null)
            {
                foreach (var b in town.BiomeOverrides)
                {
                    if (!Enum.TryParse<BiomeType>(b, true, out _))
                        errors.Add($"Town '{town.Id}': biomeOverride '{b}' is not a valid BiomeType.");
                }
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
