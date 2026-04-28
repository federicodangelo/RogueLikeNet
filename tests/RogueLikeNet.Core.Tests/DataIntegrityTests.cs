using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;

namespace RogueLikeNet.Core.Tests;

public class DataIntegrityTests
{
    [Fact]
    public void LoadedGameData_HasValidCrossReferences()
    {
        var data = GameData.Instance;
        var errors = new List<string>();

        ValidateItems(data, errors);
        ValidateRecipes(data, errors);
        ValidateResourceNodes(data, errors);
        ValidateNpcs(data, errors);
        ValidateBiomes(data, errors);
        ValidateAnimals(data, errors);
        ValidateQuests(data, errors);
        ValidateShops(data, errors);
        ValidateStructures(data, errors);
        ValidateTowns(data, errors);
        ValidateClasses(data, errors);

        Assert.True(errors.Count == 0, "Game data integrity failures:\n" + string.Join("\n", errors));
    }

    private static void ValidateItems(GameData data, List<string> errors)
    {
        foreach (var item in data.Items.All)
        {
            if (item.Seed != null && data.Items.Get(item.Seed.HarvestItemId) == null)
                errors.Add($"Item '{item.Id}' seed harvest item '{item.Seed.HarvestItemId}' does not exist.");

            if (item.Magic != null && !string.IsNullOrWhiteSpace(item.Magic.SpellId) && data.Spells.Get(item.Magic.SpellId) == null)
                errors.Add($"Item '{item.Id}' magic spell '{item.Magic.SpellId}' does not exist.");
        }
    }

    private static void ValidateRecipes(GameData data, List<string> errors)
    {
        foreach (var recipe in data.Recipes.All)
        {
            foreach (var ingredient in recipe.Ingredients)
            {
                if (data.Items.Get(ingredient.ItemId) == null)
                    errors.Add($"Recipe '{recipe.Id}' ingredient '{ingredient.ItemId}' does not exist.");
                if (ingredient.Count <= 0)
                    errors.Add($"Recipe '{recipe.Id}' ingredient '{ingredient.ItemId}' has non-positive count {ingredient.Count}.");
            }

            if (data.Items.Get(recipe.Result.ItemId) == null)
                errors.Add($"Recipe '{recipe.Id}' result '{recipe.Result.ItemId}' does not exist.");
            if (recipe.Result.Count <= 0)
                errors.Add($"Recipe '{recipe.Id}' result has non-positive count {recipe.Result.Count}.");
        }
    }

    private static void ValidateResourceNodes(GameData data, List<string> errors)
    {
        foreach (var node in data.ResourceNodes.All)
        {
            if (data.Items.Get(node.DropItemId) == null)
                errors.Add($"Resource node '{node.Id}' drop item '{node.DropItemId}' does not exist.");
            if (node.MinDrop <= 0 || node.MaxDrop < node.MinDrop)
                errors.Add($"Resource node '{node.Id}' has invalid drop range {node.MinDrop}-{node.MaxDrop}.");
        }
    }

    private static void ValidateNpcs(GameData data, List<string> errors)
    {
        foreach (var npc in data.Npcs.All)
        {
            foreach (var loot in npc.LootTable)
            {
                if (data.Items.Get(loot.ItemId) == null)
                    errors.Add($"NPC '{npc.Id}' loot item '{loot.ItemId}' does not exist.");
                if (loot.MinCount <= 0 || loot.MaxCount < loot.MinCount)
                    errors.Add($"NPC '{npc.Id}' loot item '{loot.ItemId}' has invalid count range {loot.MinCount}-{loot.MaxCount}.");
                if (loot.Chance < 0 || loot.Chance > 1)
                    errors.Add($"NPC '{npc.Id}' loot item '{loot.ItemId}' has invalid chance {loot.Chance}.");
            }

            foreach (var modifier in npc.DamageModifiers)
            {
                if (modifier.MultiplierBase100 < 0 || modifier.MultiplierBase100 > 500)
                    errors.Add($"NPC '{npc.Id}' damage modifier '{modifier.DamageType}' has suspicious multiplier {modifier.MultiplierBase100}.");
            }
        }
    }

    private static void ValidateBiomes(GameData data, List<string> errors)
    {
        foreach (var biome in data.Biomes.All)
        {
            if (data.Tiles.Get(biome.FloorTileId) == null)
                errors.Add($"Biome '{biome.Id}' floor tile '{biome.FloorTileId}' does not exist.");
            if (data.Tiles.Get(biome.WallTileId) == null)
                errors.Add($"Biome '{biome.Id}' wall tile '{biome.WallTileId}' does not exist.");

            foreach (var deco in biome.Decorations)
            {
                if (data.Tiles.Get(deco.TileId) == null)
                    errors.Add($"Biome '{biome.Id}' decoration tile '{deco.TileId}' does not exist.");
                if (deco.Chance1000 < 0 || deco.Chance1000 > 1000)
                    errors.Add($"Biome '{biome.Id}' decoration '{deco.TileId}' has invalid chance {deco.Chance1000}.");
            }

            if (biome.Liquid != null && data.Tiles.Get(biome.Liquid.TileId) == null)
                errors.Add($"Biome '{biome.Id}' liquid tile '{biome.Liquid.TileId}' does not exist.");

            foreach (var spawn in biome.EnemySpawns)
            {
                if (data.Npcs.Get(spawn.NpcId) == null)
                    errors.Add($"Biome '{biome.Id}' enemy '{spawn.NpcId}' does not exist.");
                if (spawn.Weight <= 0)
                    errors.Add($"Biome '{biome.Id}' enemy '{spawn.NpcId}' has non-positive weight {spawn.Weight}.");
            }

            foreach (var resource in biome.ResourceWeights)
            {
                if (data.ResourceNodes.Get(resource.NodeId) == null)
                    errors.Add($"Biome '{biome.Id}' resource node '{resource.NodeId}' does not exist.");
                if (resource.Weight <= 0)
                    errors.Add($"Biome '{biome.Id}' resource node '{resource.NodeId}' has non-positive weight {resource.Weight}.");
            }

            ValidateOptionalItem(data, errors, $"Biome '{biome.Id}' town wall", biome.TownWallItemId);
            ValidateOptionalItem(data, errors, $"Biome '{biome.Id}' town door", biome.TownDoorItemId);
            ValidateOptionalItem(data, errors, $"Biome '{biome.Id}' town window", biome.TownWindowItemId);
            ValidateOptionalItem(data, errors, $"Biome '{biome.Id}' town floor", biome.TownFloorItemId);
        }
    }

    private static void ValidateAnimals(GameData data, List<string> errors)
    {
        foreach (var animal in data.Animals.All)
        {
            ValidateOptionalItem(data, errors, $"Animal '{animal.Id}' produce", animal.ProduceItemId);
            ValidateOptionalItem(data, errors, $"Animal '{animal.Id}' feed", animal.FeedItemId);
        }
    }

    private static void ValidateQuests(GameData data, List<string> errors)
    {
        var questIds = data.Quests.All.Select(q => q.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var quest in data.Quests.All)
        {
            foreach (var prerequisite in quest.PrerequisiteQuestIds)
            {
                if (!questIds.Contains(prerequisite))
                    errors.Add($"Quest '{quest.Id}' prerequisite '{prerequisite}' does not exist.");
            }

            foreach (var objective in quest.Objectives)
                ValidateQuestObjective(data, errors, quest.Id, objective);

            foreach (var rewardItem in quest.Rewards.Items)
            {
                if (data.Items.Get(rewardItem.ItemId) == null)
                    errors.Add($"Quest '{quest.Id}' reward item '{rewardItem.ItemId}' does not exist.");
                if (rewardItem.Count <= 0)
                    errors.Add($"Quest '{quest.Id}' reward item '{rewardItem.ItemId}' has non-positive count {rewardItem.Count}.");
            }
        }
    }

    private static void ValidateQuestObjective(GameData data, List<string> errors, string questId, QuestObjective objective)
    {
        bool exists = objective.Type switch
        {
            QuestObjectiveType.Kill => data.Npcs.Get(objective.TargetId) != null,
            QuestObjectiveType.Collect or QuestObjectiveType.Deliver or QuestObjectiveType.Craft or QuestObjectiveType.Harvest => data.Items.Get(objective.TargetId) != null,
            QuestObjectiveType.Reach => data.Biomes.Get(objective.TargetId) != null,
            QuestObjectiveType.Gather => data.ResourceNodes.Get(objective.TargetId) != null,
            _ => false,
        };

        if (!exists)
            errors.Add($"Quest '{questId}' objective {objective.Type} target '{objective.TargetId}' does not exist.");
        if (objective.Count <= 0)
            errors.Add($"Quest '{questId}' objective {objective.Type} target '{objective.TargetId}' has non-positive count {objective.Count}.");
    }

    private static void ValidateShops(GameData data, List<string> errors)
    {
        foreach (var shop in data.Shops.All)
        {
            foreach (var item in shop.Items)
            {
                if (data.Items.Get(item.ItemId) == null)
                    errors.Add($"Shop '{shop.Id}' item '{item.ItemId}' does not exist.");
                if (item.Price < 0)
                    errors.Add($"Shop '{shop.Id}' item '{item.ItemId}' has negative price {item.Price}.");
                if (item.Stock < -1)
                    errors.Add($"Shop '{shop.Id}' item '{item.ItemId}' has invalid stock {item.Stock}.");
            }
        }
    }

    private static void ValidateStructures(GameData data, List<string> errors)
    {
        foreach (var structure in data.Structures.All)
        {
            if (structure.Width <= 0 || structure.Height <= 0)
                errors.Add($"Structure '{structure.Id}' has invalid size {structure.Width}x{structure.Height}.");
            if (structure.Grid.Length != structure.Height)
                errors.Add($"Structure '{structure.Id}' grid height {structure.Grid.Length} does not match {structure.Height}.");

            for (int y = 0; y < structure.Grid.Length; y++)
            {
                string row = structure.Grid[y];
                if (row.Length != structure.Width)
                    errors.Add($"Structure '{structure.Id}' grid row {y} width {row.Length} does not match {structure.Width}.");
                ValidateLegendCharacters(errors, structure.Id, "grid", row, structure.Legend, requireEverySymbol: true);
            }

            foreach (var entry in structure.Legend)
                ValidateStructureLegendValue(data, errors, structure.Id, entry.Value);

            ValidateOverlayGrid(data, errors, structure.Id, "cropGrid", structure.CropGrid, structure.CropLegend, id => data.Items.Get(id) != null);
            ValidateOverlayGrid(data, errors, structure.Id, "animalGrid", structure.AnimalGrid, structure.AnimalLegend, id => data.Animals.Get(id) != null);

            foreach (var item in structure.GroundItems)
            {
                if (data.Items.Get(item.ItemId) == null)
                    errors.Add($"Structure '{structure.Id}' ground item '{item.ItemId}' does not exist.");
                if (item.X < 0 || item.X >= structure.Width || item.Y < 0 || item.Y >= structure.Height)
                    errors.Add($"Structure '{structure.Id}' ground item '{item.ItemId}' is out of bounds at {item.X},{item.Y}.");
            }
        }
    }

    private static void ValidateTowns(GameData data, List<string> errors)
    {
        var structureCategories = data.Structures.All.Select(s => s.Category).ToHashSet(StringComparer.Ordinal);
        foreach (var town in data.Towns.All)
        {
            if (town.MinTownSize <= 0 || town.MaxTownSize < town.MinTownSize)
                errors.Add($"Town '{town.Id}' has invalid size range {town.MinTownSize}-{town.MaxTownSize}.");
            if (town.MinNpcs < 0 || town.MaxNpcs < town.MinNpcs)
                errors.Add($"Town '{town.Id}' has invalid NPC range {town.MinNpcs}-{town.MaxNpcs}.");

            foreach (var biomeId in town.BiomeOverrides ?? [])
            {
                if (data.Biomes.Get(biomeId) == null)
                    errors.Add($"Town '{town.Id}' biome override '{biomeId}' does not exist.");
            }

            foreach (var rule in town.Structures)
            {
                if (!structureCategories.Contains(rule.Category))
                    errors.Add($"Town '{town.Id}' structure category '{rule.Category}' does not exist.");
                if (rule.MinCount < 0 || rule.MaxCount < rule.MinCount)
                    errors.Add($"Town '{town.Id}' structure rule '{rule.Category}' has invalid count range {rule.MinCount}-{rule.MaxCount}.");
                if (rule.Weight <= 0)
                    errors.Add($"Town '{town.Id}' structure rule '{rule.Category}' has non-positive weight {rule.Weight}.");

                foreach (var structureId in rule.StructureIds ?? [])
                {
                    if (data.Structures.Get(structureId) == null)
                        errors.Add($"Town '{town.Id}' structure '{structureId}' does not exist.");
                }
            }
        }
    }

    private static void ValidateClasses(GameData data, List<string> errors)
    {
        var orders = new HashSet<int>();
        foreach (var classDef in data.Classes.All)
        {
            if (!orders.Add(classDef.Order))
                errors.Add($"Class '{classDef.Id}' duplicates order {classDef.Order}.");
            var finalStats = ClassDefinitions.BaseStats + new ClassStats(
                classDef.StartingStats.Attack,
                classDef.StartingStats.Defense,
                classDef.StartingStats.Health,
                classDef.StartingStats.Speed);
            if (finalStats.Health <= 0)
                errors.Add($"Class '{classDef.Id}' final starting health is non-positive {finalStats.Health}.");
        }
    }

    private static void ValidateOptionalItem(GameData data, List<string> errors, string owner, string itemId)
    {
        if (!string.IsNullOrWhiteSpace(itemId) && data.Items.Get(itemId) == null)
            errors.Add($"{owner} item '{itemId}' does not exist.");
    }

    private static void ValidateStructureLegendValue(GameData data, List<string> errors, string structureId, string value)
    {
        if (StructureDefinition.SpecialLegendValues.Contains(value)) return;
        if (data.Items.Get(value) == null && data.Tiles.Get(value) == null)
            errors.Add($"Structure '{structureId}' legend value '{value}' does not exist as an item or tile.");
    }

    private static void ValidateOverlayGrid(
        GameData data,
        List<string> errors,
        string structureId,
        string gridName,
        string[]? grid,
        Dictionary<string, string>? legend,
        Func<string, bool> idExists)
    {
        if (grid == null) return;
        if (legend == null)
        {
            errors.Add($"Structure '{structureId}' has {gridName} without a legend.");
            return;
        }

        foreach (var row in grid)
            ValidateLegendCharacters(errors, structureId, gridName, row, legend, requireEverySymbol: false);

        foreach (var entry in legend)
        {
            if (!idExists(entry.Value))
                errors.Add($"Structure '{structureId}' {gridName} legend value '{entry.Value}' does not exist.");
        }
    }

    private static void ValidateLegendCharacters(
        List<string> errors,
        string structureId,
        string gridName,
        string row,
        Dictionary<string, string> legend,
        bool requireEverySymbol)
    {
        foreach (char symbol in row)
        {
            if (!requireEverySymbol && (symbol == '.' || symbol == ' ')) continue;
            if (!legend.ContainsKey(symbol.ToString()))
                errors.Add($"Structure '{structureId}' {gridName} uses symbol '{symbol}' with no legend entry.");
        }
    }
}
