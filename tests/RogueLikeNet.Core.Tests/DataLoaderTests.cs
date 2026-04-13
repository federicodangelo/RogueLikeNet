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
            "equipSlot": "hand",
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
            "potion": { "healthRestore": 25, "attackBoost": 0, "defenseBoost": 0, "speedBoost": 0, "durationTicks": 1 }
          }
        ]
        """;

        var data = DataLoader.LoadFromJsonForTests(itemsJson: json);

        Assert.Equal(2, data.Items.Count);
        var sword = data.Items.Get("test_sword");
        Assert.NotNull(sword);
        Assert.Equal("Test Sword", sword.Name);
        Assert.Equal(ItemCategory.Weapon, sword.Category);
        Assert.Equal(MaterialTier.Iron, sword.MaterialTier);
        Assert.Equal(EquipSlot.Hand, sword.EquipSlot);
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

        var data = DataLoader.LoadFromJsonForTests(itemsJson: json);

        var a = data.Items.Get("a_item");
        var z = data.Items.Get("z_item");
        // NumericId is computed via MurmurHash3, must be non-zero and unique
        Assert.NotEqual(0, a!.NumericId);
        Assert.NotEqual(0, z!.NumericId);
        Assert.NotEqual(a.NumericId, z.NumericId);

        // Lookup by numeric ID works
        Assert.Same(a, data.Items.Get(a.NumericId));
        Assert.Same(z, data.Items.Get(z.NumericId));
    }

    [Fact]
    public void LoadFromJson_RegistersRecipesWithStation()
    {
        var itemsJson = """
        [
          { "id": "wood", "name": "Wood", "category": "material", "glyphId": 0, "fgColor": 0, "stackable": true, "maxStackSize": 99, "materialTier": "wood" },
          { "id": "wooden_door", "name": "Door", "category": "placeable", "glyphId": 0, "fgColor": 0, "stackable": true, "maxStackSize": 10, "materialTier": "wood" }
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

        var data = DataLoader.LoadFromJsonForTests(itemsJson: itemsJson, recipesJson: recipesJson);

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

        var data = DataLoader.LoadFromJsonForTests(resourceNodesJson: json);

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

        var data = DataLoader.LoadFromJsonForTests(monstersJson: json);

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
        var tilesJson = """
        [
          { "id": "forest_floor", "name": "Forest Floor", "type": "floor", "glyphId": 250, "fgColor": "#558844", "bgColor": "#000000", "walkable": true, "transparent": true },
          { "id": "forest_wall",  "name": "Forest Wall",  "type": "blocked", "glyphId": 219, "fgColor": "#90BB88", "bgColor": "#000000", "walkable": false, "transparent": false },
          { "id": "forest_deco",  "name": "Forest Deco",  "type": "floor", "glyphId": 44, "fgColor": "#558844", "bgColor": "#000000", "walkable": true, "transparent": true },
          { "id": "forest_water", "name": "Forest Water", "type": "water", "glyphId": 247, "fgColor": "#4484FF", "bgColor": "#001133", "walkable": false, "transparent": true }
        ]
        """;
        var json = """
        [
          {
            "id": "forest",
            "name": "Forest",
            "floorTileId": "forest_floor",
            "wallTileId": "forest_wall",
            "decorations": [
              { "tileId": "forest_deco", "chance1000": 12 }
            ],
            "enemySpawns": [
              { "npcId": "goblin", "weight": 75 }
            ],
            "liquid": {
              "tileId": "forest_water",
              "chance100": 20
            },
            "resourceWeights": [
              { "nodeId": "tree", "weight": 60 }
            ]
          }
        ]
        """;

        var data = DataLoader.LoadFromJsonForTests(tilesJson: tilesJson, biomesJson: json);

        Assert.Equal(1, data.Biomes.Count);
        var forest = data.Biomes.Get("forest");
        Assert.NotNull(forest);
        Assert.NotEqual(0, forest.NumericId);
        Assert.Equal("Forest", forest.Name);
        Assert.Equal("forest_floor", forest.FloorTileId);
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

        var data = DataLoader.LoadFromJsonForTests(itemsJson: json);
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
            "category": "placeable",
            "glyphId": 197,
            "fgColor": 9136404,
            "stackable": true,
            "maxStackSize": 10,
            "materialTier": "wood",
            "placeable": {
              "placeableType": "door",
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

        var data = DataLoader.LoadFromJsonForTests(itemsJson: json);
        var door = data.Items.Get("wooden_door");
        Assert.NotNull(door);
        Assert.NotNull(door.Placeable);
        Assert.Equal(PlaceableType.Door, door.Placeable.PlaceableType);
        Assert.Equal(PlaceableStateType.OpenClose, door.Placeable.StateType);
        Assert.False(door.Placeable.Walkable);
        Assert.True(door.Placeable.AlternateWalkable);
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

        var data = DataLoader.LoadFromJsonForTests(itemsJson: json);
        var seeds = data.Items.Get("wheat_seeds");
        Assert.NotNull(seeds);
        Assert.NotNull(seeds.Seed);
        Assert.Equal(600, seeds.Seed.GrowthTicks);
        Assert.Equal("wheat", seeds.Seed.HarvestItemId);
        Assert.Equal(3, seeds.Seed.HarvestMax);
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

        var data = DataLoader.LoadFromJsonForTests(itemsJson: json);
        var arrow = data.Items.Get("fire_arrow");
        Assert.NotNull(arrow);
        Assert.NotNull(arrow.Ammo);
        Assert.Equal(6, arrow.Ammo.Damage);
        Assert.Equal(DamageType.Fire, arrow.Ammo.DamageType);
    }
}
