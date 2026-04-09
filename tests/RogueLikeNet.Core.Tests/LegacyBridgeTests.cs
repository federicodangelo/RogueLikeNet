using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using Xunit;

namespace RogueLikeNet.Core.Tests;

/// <summary>
/// Validates that the Strangler Fig bridges correctly delegate from old Definitions
/// to the new JSON-based registries when GameData is loaded.
/// </summary>
public class LegacyBridgeTests : IDisposable
{
    private readonly GameData _previousData;

    public LegacyBridgeTests()
    {
        // Save previous GameData state (tests may run in parallel)
        _previousData = GameData.Instance;

        // Load real JSON data from the data/ directory
        var dataDir = DataDirectory.Find();
        Assert.NotNull(dataDir);
        DataLoader.Load(dataDir);
    }

    public void Dispose()
    {
        // Restore previous GameData state
        GameData.Instance = _previousData;
    }

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
}
