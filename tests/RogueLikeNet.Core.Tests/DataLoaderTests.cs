using RogueLikeNet.Core.Data;
using Xunit;

namespace RogueLikeNet.Core.Tests;

public class DataLoaderTests
{
    [Fact]
    public void LoadFromJson_RegistersItems()
    {
        var json = """
        [
          {
            "id": "test_sword",
            "name": "Test Sword",
            "category": "weapon",
            "glyphId": 47,
            "fgColor": 16777215,
            "stackable": false,
            "maxStackSize": 1,
            "materialTier": "iron",
            "equipSlot": "weapon",
            "weapon": { "baseDamage": 10, "attackSpeed": 3, "damageType": "physical", "range": 1, "twoHanded": false }
          },
          {
            "id": "test_potion",
            "name": "Test Potion",
            "category": "potion",
            "glyphId": 173,
            "fgColor": 16711680,
            "stackable": true,
            "maxStackSize": 10,
            "materialTier": "none",
            "potion": { "healthRestore": 25, "attackBoost": 0, "defenseBoost": 0, "speedBoost": 0, "duration": 0 }
          }
        ]
        """;

        var data = DataLoader.LoadFromJson(itemsJson: json);

        Assert.Equal(2, data.Items.Count);
        var sword = data.Items.Get("test_sword");
        Assert.NotNull(sword);
        Assert.Equal("Test Sword", sword.Name);
        Assert.Equal(ItemCategory.Weapon, sword.Category);
        Assert.Equal(MaterialTier.Iron, sword.MaterialTier);
        Assert.Equal(EquipSlot.Weapon, sword.EquipSlot);
        Assert.NotNull(sword.Weapon);
        Assert.Equal(10, sword.Weapon.BaseDamage);
        Assert.Equal(DamageType.Physical, sword.Weapon.DamageType);

        var potion = data.Items.Get("test_potion");
        Assert.NotNull(potion);
        Assert.Equal(25, potion.Potion!.HealthRestore);
        Assert.True(potion.Stackable);
    }

    [Fact]
    public void ItemRegistry_AssignsDeterministicNumericIds()
    {
        var json = """
        [
          { "id": "z_item", "name": "Z", "category": "misc", "glyphId": 0, "fgColor": 0, "stackable": false, "maxStackSize": 1, "materialTier": "none" },
          { "id": "a_item", "name": "A", "category": "misc", "glyphId": 0, "fgColor": 0, "stackable": false, "maxStackSize": 1, "materialTier": "none" }
        ]
        """;

        var data = DataLoader.LoadFromJson(itemsJson: json);

        // "a_item" sorts first, so NumericId = 1
        var a = data.Items.Get("a_item");
        var z = data.Items.Get("z_item");
        Assert.Equal(1, a!.NumericId);
        Assert.Equal(2, z!.NumericId);

        // Lookup by numeric ID works
        Assert.Same(a, data.Items.Get(1));
        Assert.Same(z, data.Items.Get(2));
    }

    [Fact]
    public void LoadFromJson_RegistersRecipesWithStation()
    {
        var itemsJson = """
        [
          { "id": "wood", "name": "Wood", "category": "material", "glyphId": 0, "fgColor": 0, "stackable": true, "maxStackSize": 99, "materialTier": "wood" },
          { "id": "wooden_door", "name": "Door", "category": "furniture", "glyphId": 0, "fgColor": 0, "stackable": true, "maxStackSize": 10, "materialTier": "wood" }
        ]
        """;
        var recipesJson = """
        [
          {
            "id": "craft_door",
            "name": "Wooden Door",
            "station": "workbench",
            "ingredients": [ { "itemId": "wood", "count": 5 } ],
            "result": { "itemId": "wooden_door", "count": 1 }
          }
        ]
        """;

        var data = DataLoader.LoadFromJson(itemsJson: itemsJson, recipesJson: recipesJson);

        Assert.Equal(1, data.Recipes.Count);
        var recipe = data.Recipes.Get("craft_door");
        Assert.NotNull(recipe);
        Assert.Equal(CraftingStationType.Workbench, recipe.Station);
        Assert.Single(recipe.Ingredients);
        Assert.Equal("wood", recipe.Ingredients[0].ItemId);
        Assert.Equal(5, recipe.Ingredients[0].Count);
        Assert.Equal("wooden_door", recipe.Result.ItemId);

        var byStation = data.Recipes.GetByStation(CraftingStationType.Workbench);
        Assert.Single(byStation);
    }

    [Fact]
    public void LoadFromJson_RegistersResourceNodes()
    {
        var json = """
        [
          {
            "id": "tree",
            "numericId": 4,
            "name": "Tree",
            "glyphId": 5,
            "fgColor": 2263842,
            "health": 5,
            "defense": 0,
            "dropItemId": "wood",
            "minDrop": 2,
            "maxDrop": 4
          }
        ]
        """;

        var data = DataLoader.LoadFromJson(resourceNodesJson: json);

        Assert.Equal(1, data.ResourceNodes.Count);
        var tree = data.ResourceNodes.Get("tree");
        Assert.NotNull(tree);
        Assert.Equal("Tree", tree.Name);
        Assert.Equal(5, tree.Health);
        Assert.Equal("wood", tree.DropItemId);
    }

    [Fact]
    public void LoadFromJson_RegistersMonsters()
    {
        var json = """
        [
          {
            "id": "goblin",
            "numericId": 0,
            "name": "Goblin",
            "glyphId": 103,
            "fgColor": 65280,
            "health": 15,
            "attack": 4,
            "defense": 1,
            "speed": 3,
            "xpReward": 10,
            "lootTable": [
              { "itemId": "gold_coin", "minCount": 1, "maxCount": 5, "chance": 0.5 }
            ]
          }
        ]
        """;

        var data = DataLoader.LoadFromJson(monstersJson: json);

        Assert.Equal(1, data.Npcs.Count);
        var goblin = data.Npcs.Get("goblin");
        Assert.NotNull(goblin);
        Assert.Equal(15, goblin.Health);
        Assert.Equal(10, goblin.XpReward);
        Assert.Single(goblin.LootTable);
        Assert.Equal("gold_coin", goblin.LootTable[0].ItemId);
        Assert.Equal(0.5, goblin.LootTable[0].Chance);
    }

    [Fact]
    public void LoadFromJson_RegistersBiomes()
    {
        var json = """
        [
          {
            "id": "forest",
            "numericId": 3,
            "name": "Forest",
            "floorColor": 6710835,
            "tintR": 85,
            "tintG": 110,
            "tintB": 80,
            "decorations": [
              { "glyphId": 44, "fgColor": 5605444, "chance1000": 12 }
            ],
            "enemySpawns": [
              { "npcId": "goblin", "weight": 75 }
            ],
            "liquid": {
              "tileType": "water",
              "glyphId": 247,
              "fgColor": 4490495,
              "bgColor": 4403,
              "chance100": 20
            },
            "resourceWeights": [
              { "nodeId": "tree", "weight": 60 }
            ]
          }
        ]
        """;

        var data = DataLoader.LoadFromJson(biomesJson: json);

        Assert.Equal(1, data.Biomes.Count);
        var forest = data.Biomes.Get("forest");
        Assert.NotNull(forest);
        Assert.Equal(3, forest.NumericId);
        Assert.Equal("Forest", forest.Name);
        Assert.Equal(85, forest.TintR);
        Assert.Single(forest.Decorations);
        Assert.Single(forest.EnemySpawns);
        Assert.NotNull(forest.Liquid);
        Assert.Equal(20, forest.Liquid.Chance100);
        Assert.Single(forest.ResourceWeights);
        Assert.Equal("tree", forest.ResourceWeights[0].NodeId);
    }

    [Fact]
    public void LoadFromJson_ToolData_ParsesCorrectly()
    {
        var json = """
        [
          {
            "id": "iron_pickaxe",
            "name": "Iron Pickaxe",
            "category": "tool",
            "glyphId": 47,
            "fgColor": 11053176,
            "stackable": false,
            "maxStackSize": 1,
            "materialTier": "iron",
            "tool": { "toolType": "pickaxe", "miningPower": 4, "durability": 250 }
          }
        ]
        """;

        var data = DataLoader.LoadFromJson(itemsJson: json);
        var pick = data.Items.Get("iron_pickaxe");
        Assert.NotNull(pick);
        Assert.NotNull(pick.Tool);
        Assert.Equal(ToolType.Pickaxe, pick.Tool.ToolType);
        Assert.Equal(4, pick.Tool.MiningPower);
        Assert.Equal(250, pick.Tool.Durability);
    }

    [Fact]
    public void LoadFromJson_FurnitureData_DoorWithState()
    {
        var json = """
        [
          {
            "id": "wooden_door",
            "name": "Wooden Door",
            "category": "furniture",
            "glyphId": 197,
            "fgColor": 9136404,
            "stackable": true,
            "maxStackSize": 10,
            "materialTier": "wood",
            "furniture": {
              "furnitureType": "door",
              "placedGlyphId": 197,
              "placedFgColor": 9136404,
              "walkable": false,
              "transparent": false,
              "stateType": "openClose",
              "alternateGlyphId": 0,
              "alternateWalkable": true,
              "alternateTransparent": true
            }
          }
        ]
        """;

        var data = DataLoader.LoadFromJson(itemsJson: json);
        var door = data.Items.Get("wooden_door");
        Assert.NotNull(door);
        Assert.NotNull(door.Furniture);
        Assert.Equal(FurnitureType.Door, door.Furniture.FurnitureType);
        Assert.Equal(PlaceableStateType.OpenClose, door.Furniture.StateType);
        Assert.False(door.Furniture.Walkable);
        Assert.True(door.Furniture.AlternateWalkable);
    }

    [Fact]
    public void LoadFromJson_SeedData_ParsesCorrectly()
    {
        var json = """
        [
          {
            "id": "wheat_seeds",
            "name": "Wheat Seeds",
            "category": "seed",
            "glyphId": 44,
            "fgColor": 14329120,
            "stackable": true,
            "maxStackSize": 99,
            "materialTier": "none",
            "seed": { "growthTicks": 600, "harvestItemId": "wheat", "harvestMin": 1, "harvestMax": 3 }
          }
        ]
        """;

        var data = DataLoader.LoadFromJson(itemsJson: json);
        var seeds = data.Items.Get("wheat_seeds");
        Assert.NotNull(seeds);
        Assert.NotNull(seeds.Seed);
        Assert.Equal(600, seeds.Seed.GrowthTicks);
        Assert.Equal("wheat", seeds.Seed.HarvestItemId);
        Assert.Equal(3, seeds.Seed.HarvestMax);
    }

    [Fact]
    public void LoadFromJson_BlockData_ParsesCorrectly()
    {
        var json = """
        [
          {
            "id": "stone_block",
            "name": "Stone Block",
            "category": "block",
            "glyphId": 219,
            "fgColor": 8947848,
            "stackable": true,
            "maxStackSize": 99,
            "materialTier": "stone",
            "block": { "hardness": 3, "toolRequired": "pickaxe" }
          }
        ]
        """;

        var data = DataLoader.LoadFromJson(itemsJson: json);
        var block = data.Items.Get("stone_block");
        Assert.NotNull(block);
        Assert.NotNull(block.Block);
        Assert.Equal(3, block.Block.Hardness);
        Assert.Equal("pickaxe", block.Block.ToolRequired);
    }

    [Fact]
    public void MaterialTiers_GetMultiplier_ReturnsExpectedValues()
    {
        Assert.Equal(100, MaterialTiers.GetMultiplier(MaterialTier.Wood));
        Assert.Equal(200, MaterialTiers.GetMultiplier(MaterialTier.Iron));
        Assert.Equal(400, MaterialTiers.GetMultiplier(MaterialTier.Adamantite));
        Assert.Equal(100, MaterialTiers.GetMultiplier(MaterialTier.None));
    }

    [Fact]
    public void LoadFromJson_AmmoData_ParsesCorrectly()
    {
        var json = """
        [
          {
            "id": "fire_arrow",
            "name": "Fire Arrow",
            "category": "ammo",
            "glyphId": 47,
            "fgColor": 16744448,
            "stackable": true,
            "maxStackSize": 50,
            "materialTier": "iron",
            "ammo": { "damage": 6, "damageType": "fire" }
          }
        ]
        """;

        var data = DataLoader.LoadFromJson(itemsJson: json);
        var arrow = data.Items.Get("fire_arrow");
        Assert.NotNull(arrow);
        Assert.NotNull(arrow.Ammo);
        Assert.Equal(6, arrow.Ammo.Damage);
        Assert.Equal(DamageType.Fire, arrow.Ammo.DamageType);
    }
}
