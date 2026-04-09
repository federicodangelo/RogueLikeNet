using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Tests;

public class ItemDefinitionsTests
{
    [Fact]
    public void GenerateLoot_ReturnsValidItem()
    {
        var rng = new SeededRandom(42);
        var loot = LootGenerator.GenerateLoot(rng, 0);
        Assert.True(loot.Rarity >= 0 && loot.Rarity <= 4);
        Assert.NotNull(loot.Definition.Name);
    }

    [Fact]
    public void HighDifficulty_BoostsRarity()
    {
        var rng = new SeededRandom(42);
        int totalRarity = 0;
        for (int i = 0; i < 110; i++)
        {
            var loot = LootGenerator.GenerateLoot(rng, 10);
            totalRarity += loot.Rarity;
        }
        Assert.True(totalRarity > 100, $"Total rarity {totalRarity} should be > 100 at high difficulty");
    }

    [Fact]
    public void GenerateLoot_AllCategories()
    {
        var rng = new SeededRandom(1);
        bool foundWeapon = false, foundArmor = false, foundPotion = false, foundMisc = false;
        // Run many iterations to cover all category branches
        for (int i = 0; i < 500; i++)
        {
            var loot = LootGenerator.GenerateLoot(rng, 0);
            switch (loot.Definition.Category)
            {
                case ItemCategory.Weapon: foundWeapon = true; break;
                case ItemCategory.Armor: foundArmor = true; break;
                case ItemCategory.Potion: foundPotion = true; break;
                case ItemCategory.Misc: foundMisc = true; break;
            }
        }
        Assert.True(foundWeapon, "Should generate weapons");
        Assert.True(foundArmor, "Should generate armor");
        Assert.True(foundPotion, "Should generate potions");
        Assert.True(foundMisc, "Should generate gold/misc");
    }

    [Fact]
    public void GenerateLoot_AllRarities()
    {
        var rng = new SeededRandom(1);
        var rarities = new HashSet<int>();
        for (int i = 0; i < 1000; i++)
        {
            var loot = LootGenerator.GenerateLoot(rng, 0);
            rarities.Add(loot.Rarity);
        }
        // At difficulty 0, should see at least Common and Uncommon
        Assert.Contains(0, rarities);
        Assert.Contains(1, rarities);
    }

    [Fact]
    public void Registry_ContainsItems()
    {
        Assert.True(GameData.Instance.Items.Count > 0, "Item registry should have items");
    }

    [Fact]
    public void Registry_Get_ByStringId()
    {
        var def = GameData.Instance.Items.Get("short_sword");
        Assert.NotNull(def);
        Assert.Equal("Short Sword", def.Name);
        Assert.Equal(ItemCategory.Weapon, def.Category);
    }

    [Fact]
    public void Registry_Get_ByNumericId_RoundTrips()
    {
        var def = GameData.Instance.Items.Get("short_sword");
        Assert.NotNull(def);
        var def2 = GameData.Instance.Items.Get(def.NumericId);
        Assert.NotNull(def2);
        Assert.Equal("short_sword", def2.Id);
    }

    [Fact]
    public void Registry_Get_InvalidTypeId_ReturnsNull()
    {
        var def = GameData.Instance.Items.Get(9999);
        Assert.Null(def);
    }

    [Fact]
    public void Registry_Stackable_And_MaxStackSize()
    {
        var potion = GameData.Instance.Items.Get("health_potion_small");
        Assert.NotNull(potion);
        Assert.True(potion.Stackable);
        Assert.True(potion.MaxStackSize > 1);

        var sword = GameData.Instance.Items.Get("short_sword");
        Assert.NotNull(sword);
        Assert.False(sword.Stackable);
    }

    [Fact]
    public void GenerateItemData_Gold_CorrectStackCount()
    {
        var goldDef = GameData.Instance.Items.Get("gold_coin");
        Assert.NotNull(goldDef);
        var rng = new SeededRandom(123);

        var itemData = LootGenerator.GenerateItemData(goldDef, 0, rng);
        Assert.Equal(goldDef.NumericId, itemData.ItemTypeId);
        Assert.True(itemData.StackCount >= 10);
    }
}
