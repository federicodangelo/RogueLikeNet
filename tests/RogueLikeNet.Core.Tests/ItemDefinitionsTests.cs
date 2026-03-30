using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Tests;

public class ItemDefinitionsTests
{
    [Fact]
    public void GenerateLoot_ReturnsValidItem()
    {
        var rng = new SeededRandom(42);
        var (template, rarity) = ItemDefinitions.GenerateLoot(rng, 0);
        Assert.True(rarity >= 0 && rarity <= 4);
        Assert.NotNull(template.Name);
    }

    [Fact]
    public void HighDifficulty_BoostsRarity()
    {
        var rng = new SeededRandom(42);
        int totalRarity = 0;
        for (int i = 0; i < 110; i++)
        {
            var (_, rarity) = ItemDefinitions.GenerateLoot(rng, 10);
            totalRarity += rarity;
        }
        Assert.True(totalRarity > 100, $"Total rarity {totalRarity} should be > 100 at high difficulty");
    }

    [Fact]
    public void GenerateLoot_AllCategories()
    {
        var rng = new SeededRandom(1);
        bool foundWeapon = false, foundArmor = false, foundPotion = false, foundGold = false;
        // Run many iterations to cover all category branches
        for (int i = 0; i < 500; i++)
        {
            var (template, _) = ItemDefinitions.GenerateLoot(rng, 0);
            switch (template.Category)
            {
                case ItemDefinitions.CategoryWeapon: foundWeapon = true; break;
                case ItemDefinitions.CategoryArmor: foundArmor = true; break;
                case ItemDefinitions.CategoryPotion: foundPotion = true; break;
                case ItemDefinitions.CategoryGold: foundGold = true; break;
            }
        }
        Assert.True(foundWeapon, "Should generate weapons");
        Assert.True(foundArmor, "Should generate armor");
        Assert.True(foundPotion, "Should generate potions");
        Assert.True(foundGold, "Should generate gold");
    }

    [Fact]
    public void GenerateLoot_AllRarities()
    {
        var rng = new SeededRandom(1);
        var rarities = new HashSet<int>();
        for (int i = 0; i < 1000; i++)
        {
            var (_, rarity) = ItemDefinitions.GenerateLoot(rng, 0);
            rarities.Add(rarity);
        }
        // At difficulty 0, should see at least Common and Uncommon
        Assert.Contains(0, rarities);
        Assert.Contains(1, rarities);
    }

    [Fact]
    public void All_ContainsAllDefinedItems()
    {
        Assert.Equal(33, ItemDefinitions.All.Length);
    }

    [Theory]
    [InlineData(ItemDefinitions.ShortSword, "Short Sword", ItemDefinitions.CategoryWeapon)]
    [InlineData(ItemDefinitions.LongSword, "Long Sword", ItemDefinitions.CategoryWeapon)]
    [InlineData(ItemDefinitions.LeatherArmor, "Leather Armor", ItemDefinitions.CategoryArmor)]
    [InlineData(ItemDefinitions.HealthPotion, "Health Potion", ItemDefinitions.CategoryPotion)]
    [InlineData(ItemDefinitions.Gold, "Gold", ItemDefinitions.CategoryGold)]
    public void Get_ReturnsCorrectDefinition(int typeId, string expectedName, int expectedCategory)
    {
        var def = ItemDefinitions.Get(typeId);
        Assert.Equal(typeId, def.TypeId);
        Assert.Equal(expectedName, def.Name);
        Assert.Equal(expectedCategory, def.Category);
    }

    [Fact]
    public void Get_InvalidTypeId_ReturnsDefault()
    {
        var def = ItemDefinitions.Get(9999);
        Assert.Equal(0, def.TypeId);
        Assert.Null(def.Name);
    }

    [Theory]
    [InlineData(ItemDefinitions.HealthPotion, true, 10)]
    [InlineData(ItemDefinitions.StrengthPotion, true, 10)]
    [InlineData(ItemDefinitions.Gold, true, 999)]
    [InlineData(ItemDefinitions.ShortSword, false, 1)]
    [InlineData(ItemDefinitions.LeatherArmor, false, 1)]
    public void Stackable_And_MaxStackSize(int typeId, bool expectedStackable, int expectedMaxStack)
    {
        var def = ItemDefinitions.Get(typeId);
        Assert.Equal(expectedStackable, def.Stackable);
        Assert.Equal(expectedMaxStack, def.MaxStackSize);
    }

    [Fact]
    public void GenerateItemData_Gold_AlwaysCommonRarity()
    {
        var goldDef = ItemDefinitions.Get(ItemDefinitions.Gold);
        var rng = new SeededRandom(123);

        // Even when requesting Legendary rarity, gold should always be Common
        var itemData = ItemDefinitions.GenerateItemData(goldDef, ItemDefinitions.RarityLegendary, rng);
        Assert.Equal(ItemDefinitions.RarityCommon, itemData.Rarity);
    }
}
