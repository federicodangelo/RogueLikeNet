using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using Xunit;

namespace RogueLikeNet.Core.Tests;

/// <summary>
/// Shared fixture that loads real JSON data once for all bridge tests.
/// </summary>
public class GameDataFixture : IDisposable
{
    private static readonly object Lock = new();
    private readonly GameData _previousData;

    public GameData TestData { get; }

    public GameDataFixture()
    {
        Monitor.Enter(Lock);
        _previousData = GameData.Instance;

        var dataDir = DataDirectory.Find()
            ?? throw new DirectoryNotFoundException("data/ directory not found");
        TestData = DataLoader.Load(dataDir);
    }

    public void Dispose()
    {
        GameData.Instance = _previousData;
        CraftingDefinitions.InvalidateCache();
        Monitor.Exit(Lock);
    }
}

[CollectionDefinition("GameData")]
public class GameDataCollection : ICollectionFixture<GameDataFixture>;

/// <summary>
/// Validates that the Strangler Fig bridges correctly delegate from old Definitions
/// to the new JSON-based registries when GameData is loaded.
/// Uses a shared fixture with a lock to prevent parallel test pollution.
/// </summary>
[Collection("GameData")]
public class LegacyBridgeTests
{
    public LegacyBridgeTests(GameDataFixture fixture) { _ = fixture; }

    [Fact]
    public void ItemBridge_ShortSword_ReturnsCorrectData()
    {
        var def = ItemDefinitions.Get(ItemDefinitions.ShortSword);
        Assert.Equal("Short Sword", def.Name);
        Assert.Equal(ItemDefinitions.CategoryWeapon, def.Category);
        Assert.True(def.BaseAttack > 0);
        Assert.False(def.Stackable);
    }

    [Fact]
    public void ItemBridge_LeatherArmor_ReturnsCorrectData()
    {
        var def = ItemDefinitions.Get(ItemDefinitions.LeatherArmor);
        Assert.Equal("Leather Armor", def.Name);
        Assert.Equal(ItemDefinitions.CategoryArmor, def.Category);
        Assert.True(def.BaseDefense > 0);
        Assert.False(def.Stackable);
    }

    [Fact]
    public void ItemBridge_HealthPotion_ReturnsCorrectData()
    {
        var def = ItemDefinitions.Get(ItemDefinitions.HealthPotion);
        Assert.Contains("Health Potion", def.Name);
        Assert.Equal(ItemDefinitions.CategoryPotion, def.Category);
        Assert.True(def.BaseHealth > 0);
        Assert.True(def.Stackable);
    }

    [Fact]
    public void ItemBridge_Gold_ReturnsCorrectData()
    {
        var def = ItemDefinitions.Get(ItemDefinitions.Gold);
        Assert.Equal("Gold Coin", def.Name);
        Assert.Equal(ItemDefinitions.CategoryGold, def.Category);
        Assert.True(def.Stackable);
    }

    [Fact]
    public void ItemBridge_Wood_ReturnsCorrectData()
    {
        var def = ItemDefinitions.Get(ItemDefinitions.Wood);
        Assert.Equal("Wood", def.Name);
        Assert.Equal(ItemDefinitions.CategoryResource, def.Category);
        Assert.True(def.Stackable);
    }

    [Fact]
    public void ItemBridge_WoodenDoor_ReturnsCorrectData()
    {
        var def = ItemDefinitions.Get(ItemDefinitions.WoodenDoor);
        Assert.Equal("Wooden Door", def.Name);
        Assert.Equal(ItemDefinitions.CategoryPlaceable, def.Category);
        Assert.True(def.Stackable);
    }

    [Fact]
    public void ItemBridge_AllLegacyItems_HaveValidMapping()
    {
        // Every old item constant should resolve to a non-default definition
        int[] allOldIds =
        [
            ItemDefinitions.ShortSword, ItemDefinitions.LongSword,
            ItemDefinitions.BattleAxe, ItemDefinitions.Dagger,
            ItemDefinitions.LeatherArmor, ItemDefinitions.ChainMail,
            ItemDefinitions.PlateArmor, ItemDefinitions.Shield,
            ItemDefinitions.HealthPotion, ItemDefinitions.StrengthPotion,
            ItemDefinitions.Gold,
            ItemDefinitions.Wood, ItemDefinitions.CopperOre,
            ItemDefinitions.IronOre, ItemDefinitions.GoldOre,
            ItemDefinitions.WoodenDoor, ItemDefinitions.CopperDoor,
            ItemDefinitions.IronDoor, ItemDefinitions.GoldDoor,
            ItemDefinitions.WoodenWall, ItemDefinitions.CopperWall,
            ItemDefinitions.IronWall, ItemDefinitions.GoldWall,
            ItemDefinitions.WoodenWindow,
            ItemDefinitions.WoodenTable, ItemDefinitions.WoodenChair,
            ItemDefinitions.WoodenBed, ItemDefinitions.WoodenBookshelf,
            ItemDefinitions.WoodenFloorTile, ItemDefinitions.StoneFloorTile,
            ItemDefinitions.CopperFloorTile, ItemDefinitions.IronFloorTile,
            ItemDefinitions.GoldFloorTile,
        ];

        foreach (var id in allOldIds)
        {
            var def = ItemDefinitions.Get(id);
            Assert.NotEqual(0, def.TypeId);
            Assert.False(string.IsNullOrEmpty(def.Name), $"Item ID {id} has no name");
        }
    }

    [Fact]
    public void NpcBridge_Goblin_ReturnsCorrectData()
    {
        var def = NpcDefinitions.Get(NpcDefinitions.Goblin);
        Assert.Equal("Goblin", def.Name);
        Assert.True(def.Health > 0);
        Assert.True(def.Attack > 0);
    }

    [Fact]
    public void NpcBridge_Dragon_ReturnsCorrectData()
    {
        var def = NpcDefinitions.Get(NpcDefinitions.Dragon);
        Assert.Equal("Dragon", def.Name);
        Assert.True(def.Health > 0);
        Assert.True(def.Attack > 0);
    }

    [Fact]
    public void NpcBridge_AllLegacyNpcs_HaveValidMapping()
    {
        int[] allOldIds =
        [
            NpcDefinitions.Goblin, NpcDefinitions.Orc,
            NpcDefinitions.Skeleton, NpcDefinitions.Dragon,
        ];

        foreach (var id in allOldIds)
        {
            var def = NpcDefinitions.Get(id);
            Assert.False(string.IsNullOrEmpty(def.Name), $"NPC ID {id} has no name");
            Assert.True(def.Health > 0, $"NPC ID {id} has zero health");
        }
    }

    [Fact]
    public void ResourceNodeBridge_Tree_ReturnsCorrectData()
    {
        var def = ResourceNodeDefinitions.Get(ResourceNodeDefinitions.Tree);
        Assert.Equal("Tree", def.Name);
        Assert.True(def.Health > 0);
        Assert.Equal(ItemDefinitions.Wood, def.ResourceItemTypeId);
    }

    [Fact]
    public void ResourceNodeBridge_CopperRock_ReturnsCorrectData()
    {
        var def = ResourceNodeDefinitions.Get(ResourceNodeDefinitions.CopperRock);
        Assert.Equal("Copper Rock", def.Name);
        Assert.True(def.Health > 0);
        Assert.Equal(ItemDefinitions.CopperOre, def.ResourceItemTypeId);
    }

    [Fact]
    public void ResourceNodeBridge_AllLegacyNodes_HaveValidMapping()
    {
        int[] allOldIds =
        [
            ResourceNodeDefinitions.CopperRock, ResourceNodeDefinitions.IronRock,
            ResourceNodeDefinitions.GoldRock, ResourceNodeDefinitions.Tree,
        ];

        foreach (var id in allOldIds)
        {
            var def = ResourceNodeDefinitions.Get(id);
            Assert.False(string.IsNullOrEmpty(def.Name), $"Node ID {id} has no name");
            Assert.True(def.Health > 0, $"Node ID {id} has zero health");
        }
    }

    [Fact]
    public void PlaceableBridge_WoodenDoor_ReturnsCorrectData()
    {
        var def = PlaceableDefinitions.Get(ItemDefinitions.WoodenDoor);
        Assert.Equal(PlaceableDefinitions.CategoryDoor, def.Category);
        Assert.True(def.HasState); // Doors have open/close state
        Assert.False(def.Walkable); // Closed door is not walkable
        Assert.True(def.AlternateWalkable); // Open door is walkable
    }

    [Fact]
    public void PlaceableBridge_WoodenWall_ReturnsCorrectData()
    {
        var def = PlaceableDefinitions.Get(ItemDefinitions.WoodenWall);
        Assert.Equal(PlaceableDefinitions.CategoryWall, def.Category);
        Assert.False(def.HasState);
        Assert.False(def.Walkable);
    }

    [Fact]
    public void PlaceableBridge_WoodenTable_ReturnsCorrectData()
    {
        var def = PlaceableDefinitions.Get(ItemDefinitions.WoodenTable);
        Assert.Equal(PlaceableDefinitions.CategoryDecoration, def.Category);
        Assert.True(def.Walkable);
        Assert.True(def.Transparent);
    }

    [Fact]
    public void PlaceableBridge_WoodenFloorTile_ReturnsCorrectData()
    {
        var def = PlaceableDefinitions.Get(ItemDefinitions.WoodenFloorTile);
        Assert.Equal(PlaceableDefinitions.CategoryFloorTile, def.Category);
        Assert.True(def.Walkable);
        Assert.True(def.Transparent);
    }

    [Fact]
    public void ItemRegistry_LegacyIds_MatchOldConstants()
    {
        // Verify items with legacy mappings get their old NumericIds
        var reg = GameData.Instance.Items;
        Assert.Equal(ItemDefinitions.ShortSword, reg.Get("short_sword")!.NumericId);
        Assert.Equal(ItemDefinitions.Gold, reg.Get("gold_coin")!.NumericId);
        Assert.Equal(ItemDefinitions.Wood, reg.Get("wood")!.NumericId);
        Assert.Equal(ItemDefinitions.WoodenDoor, reg.Get("wooden_door")!.NumericId);
        Assert.Equal(ItemDefinitions.GoldFloorTile, reg.Get("gold_floor_tile")!.NumericId);
    }

    [Fact]
    public void ItemRegistry_NewItems_GetIdsAboveLegacyRange()
    {
        // Verify new items (tools, food, etc.) get NumericIds above old range
        var reg = GameData.Instance.Items;
        var ironPickaxe = reg.Get("iron_pickaxe");
        Assert.NotNull(ironPickaxe);
        Assert.True(ironPickaxe.NumericId > ItemDefinitions.GoldFloorTile,
            $"New item iron_pickaxe should have NumericId > {ItemDefinitions.GoldFloorTile}, got {ironPickaxe.NumericId}");
    }

    [Fact]
    public void ItemRegistry_NewItems_AccessibleByNumericId()
    {
        // Verify new items can be looked up by their assigned NumericId
        var reg = GameData.Instance.Items;
        var ironPickaxe = reg.Get("iron_pickaxe");
        Assert.NotNull(ironPickaxe);

        var byNumericId = reg.Get(ironPickaxe.NumericId);
        Assert.Same(ironPickaxe, byNumericId);
    }

    [Fact]
    public void ItemDefinitions_Get_WorksForNewItems()
    {
        // Verify ItemDefinitions.Get() returns correct data for new items (via registry)
        var reg = GameData.Instance.Items;
        var ironPickaxe = reg.Get("iron_pickaxe");
        Assert.NotNull(ironPickaxe);

        var def = ItemDefinitions.Get(ironPickaxe.NumericId);
        Assert.Equal("Iron Pickaxe", def.Name);
        Assert.Equal(ItemDefinitions.CategoryTool, def.Category);
    }

    [Fact]
    public void NpcRegistry_LegacyIds_MatchOldConstants()
    {
        var reg = GameData.Instance.Npcs;
        Assert.Equal(NpcDefinitions.Goblin, reg.Get("goblin")!.NumericId);
        Assert.Equal(NpcDefinitions.Dragon, reg.Get("dragon")!.NumericId);
    }

    [Fact]
    public void ResourceNodeRegistry_LegacyIds_MatchOldConstants()
    {
        var reg = GameData.Instance.ResourceNodes;
        Assert.Equal(ResourceNodeDefinitions.Tree, reg.Get("tree")!.NumericId);
        Assert.Equal(ResourceNodeDefinitions.CopperRock, reg.Get("copper_rock")!.NumericId);
    }

    // --- New resource node tests ---

    [Fact]
    public void ResourceNode_NewNodes_AccessibleByNumericId()
    {
        var reg = GameData.Instance.ResourceNodes;
        var coal = reg.Get("coal_deposit");
        Assert.NotNull(coal);
        Assert.True(coal.NumericId > ResourceNodeDefinitions.Tree,
            $"coal_deposit should have NumericId > {ResourceNodeDefinitions.Tree}");

        var def = ResourceNodeDefinitions.Get(coal.NumericId);
        Assert.Equal("Coal Deposit", def.Name);
        Assert.True(def.Health > 0);
        Assert.True(def.ResourceItemTypeId > 0, "Drop item should have valid NumericId");
    }

    [Fact]
    public void ResourceNode_NewNodes_DropItemsResolve()
    {
        var reg = GameData.Instance.ResourceNodes;
        string[] newNodes = ["coal_deposit", "sand_deposit", "clay_deposit", "mithril_rock", "adamantite_rock"];
        foreach (var nodeId in newNodes)
        {
            var nodeDef = reg.Get(nodeId);
            Assert.NotNull(nodeDef);
            var def = ResourceNodeDefinitions.Get(nodeDef.NumericId);
            Assert.True(def.ResourceItemTypeId > 0,
                $"{nodeId} drop item should have valid NumericId, got {def.ResourceItemTypeId}");
            // Verify the drop item actually exists
            var dropItem = ItemDefinitions.Get(def.ResourceItemTypeId);
            Assert.False(string.IsNullOrEmpty(dropItem.Name),
                $"{nodeId} drop item {def.ResourceItemTypeId} has no definition");
        }
    }

    [Fact]
    public void ResourceNode_GetForBiome_IncludesNewNodes()
    {
        var stoneNodes = ResourceNodeDefinitions.GetForBiome(BiomeType.Stone);
        // With GameData loaded, should include coal, sand, clay, and mithril
        Assert.True(stoneNodes.Length > 4,
            $"Stone biome should have >4 node types with new data, got {stoneNodes.Length}");

        var names = stoneNodes.Select(n => n.Def.Name).ToArray();
        Assert.Contains("Coal Deposit", names);
        Assert.Contains("Mithril Rock", names);
    }

    // --- Crafting recipe bridge tests ---

    [Fact]
    public void CraftingBridge_ReturnsJsonRecipes_WhenDataLoaded()
    {
        var recipes = CraftingDefinitions.All;
        Assert.True(recipes.Length > 18, $"Expected more than 18 recipes (old count), got {recipes.Length}");
    }

    [Fact]
    public void CraftingBridge_LegacyRecipe_WoodenDoor_Exists()
    {
        var recipe = Array.Find(CraftingDefinitions.All, r => r.Name == "Wooden Door");
        Assert.NotEqual(default, recipe);
        Assert.Equal(ItemDefinitions.WoodenDoor, recipe.ResultItemTypeId);
        Assert.Equal(1, recipe.ResultCount);
    }

    [Fact]
    public void CraftingBridge_Ingredients_ResolveToNumericIds()
    {
        // Find a recipe with known ingredients
        var recipe = Array.Find(CraftingDefinitions.All, r => r.Name == "Wooden Door");
        Assert.NotEqual(default, recipe);

        // Wooden Door requires wood
        var woodIngredient = Array.Find(recipe.Ingredients, i => i.ItemTypeId == ItemDefinitions.Wood);
        Assert.NotEqual(default, woodIngredient);
        Assert.True(woodIngredient.Count > 0);
    }

    [Fact]
    public void CraftingBridge_NewRecipe_WoodenPickaxe_Exists()
    {
        var recipe = Array.Find(CraftingDefinitions.All, r => r.Name == "Wooden Pickaxe");
        Assert.NotEqual(default, recipe);

        // Result should be a valid numeric ID above legacy range
        Assert.True(recipe.ResultItemTypeId > ItemDefinitions.GoldFloorTile,
            $"Wooden Pickaxe should have a new NumericId, got {recipe.ResultItemTypeId}");
        Assert.Equal(1, recipe.ResultCount);
    }

    [Fact]
    public void CraftingBridge_NewRecipe_IngredientsResolve()
    {
        var recipe = Array.Find(CraftingDefinitions.All, r => r.Name == "Wooden Pickaxe");
        Assert.NotEqual(default, recipe);

        // Wooden Pickaxe requires wood (legacy ID) and fiber (new ID)
        var woodIngredient = Array.Find(recipe.Ingredients, i => i.ItemTypeId == ItemDefinitions.Wood);
        Assert.NotEqual(default, woodIngredient);

        // All ingredients should have non-zero ItemTypeIds
        foreach (var ingredient in recipe.Ingredients)
        {
            Assert.True(ingredient.ItemTypeId > 0, $"Ingredient should have NumericId > 0");
            Assert.True(ingredient.Count > 0, $"Ingredient count should be > 0");
        }
    }

    [Fact]
    public void CraftingBridge_RecipeIds_AreContiguous()
    {
        var recipes = CraftingDefinitions.All;
        for (int i = 0; i < recipes.Length; i++)
        {
            Assert.Equal(i, recipes[i].RecipeId);
        }
    }

    [Fact]
    public void CraftingBridge_AllRecipes_HaveValidResults()
    {
        foreach (var recipe in CraftingDefinitions.All)
        {
            Assert.True(recipe.ResultItemTypeId > 0, $"Recipe '{recipe.Name}' has invalid result NumericId");
            Assert.True(recipe.ResultCount > 0, $"Recipe '{recipe.Name}' has zero result count");
            Assert.False(string.IsNullOrEmpty(recipe.Name), $"Recipe ID {recipe.RecipeId} has no name");

            // Every result should be resolvable by ItemDefinitions.Get()
            var resultDef = ItemDefinitions.Get(recipe.ResultItemTypeId);
            Assert.False(string.IsNullOrEmpty(resultDef.Name),
                $"Recipe '{recipe.Name}' result item {recipe.ResultItemTypeId} has no definition");
        }
    }
}
