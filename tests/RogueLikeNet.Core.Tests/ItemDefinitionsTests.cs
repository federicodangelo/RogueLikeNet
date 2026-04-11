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
        Assert.NotNull(loot.Definition.Name);
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

        var itemData = LootGenerator.GenerateItemData(goldDef, rng);
        Assert.Equal(goldDef.NumericId, itemData.ItemTypeId);
        Assert.True(itemData.StackCount >= 10);
    }

    // ── Computed properties ──

    [Fact]
    public void BaseAttack_FromWeapon()
    {
        var def = new ItemDefinition { Weapon = new WeaponData { BaseDamage = 10 } };
        Assert.Equal(10, def.BaseAttack);
    }

    [Fact]
    public void BaseAttack_FromPotion()
    {
        var def = new ItemDefinition { Potion = new PotionData { AttackBoost = 5 } };
        Assert.Equal(5, def.BaseAttack);
    }

    [Fact]
    public void BaseAttack_NoData_ReturnsZero()
    {
        var def = new ItemDefinition();
        Assert.Equal(0, def.BaseAttack);
    }

    [Fact]
    public void BaseDefense_FromArmor()
    {
        var def = new ItemDefinition { Armor = new ArmorData { BaseDefense = 8 } };
        Assert.Equal(8, def.BaseDefense);
    }

    [Fact]
    public void BaseDefense_FromPotion()
    {
        var def = new ItemDefinition { Potion = new PotionData { DefenseBoost = 3 } };
        Assert.Equal(3, def.BaseDefense);
    }

    [Fact]
    public void BaseHealth_FromPotion()
    {
        var def = new ItemDefinition { Potion = new PotionData { HealthRestore = 25 } };
        Assert.Equal(25, def.BaseHealth);
    }

    [Fact]
    public void BaseHealth_FromFood()
    {
        var def = new ItemDefinition { Food = new FoodData { HealthRestore = 10 } };
        Assert.Equal(10, def.BaseHealth);
    }

    [Fact]
    public void EffectiveAttack_AppliesMaterialTier()
    {
        var def = new ItemDefinition { Weapon = new WeaponData { BaseDamage = 10 }, MaterialTier = MaterialTier.Iron };
        Assert.Equal(20, def.EffectiveAttack); // 10 * 200 / 100
    }

    [Fact]
    public void EffectiveDefense_AppliesMaterialTier()
    {
        var def = new ItemDefinition { Armor = new ArmorData { BaseDefense = 10 }, MaterialTier = MaterialTier.Stone };
        Assert.Equal(13, def.EffectiveDefense); // 10 * 130 / 100
    }

    [Fact]
    public void HungerReduction_FromFood()
    {
        var def = new ItemDefinition { Food = new FoodData { HungerRestore = 30 } };
        Assert.Equal(30, def.HungerReduction);
    }

    [Fact]
    public void ThirstReduction_FromFood()
    {
        var def = new ItemDefinition { Food = new FoodData { ThirstRestore = 20 } };
        Assert.Equal(20, def.ThirstReduction);
    }

    [Fact]
    public void HealthRestore_FoodAndPotion()
    {
        // Potion takes precedence (Food?.HealthRestore ?? Potion is wrong, it's Food?.HealthRestore)
        var foodDef = new ItemDefinition { Food = new FoodData { HealthRestore = 15 } };
        Assert.Equal(15, foodDef.HealthRestore);

        var potionDef = new ItemDefinition { Potion = new PotionData { HealthRestore = 25 } };
        Assert.Equal(25, potionDef.HealthRestore);
    }

    [Fact]
    public void IsPlaceable_TrueOnlyForPlaceableCategory()
    {
        Assert.True(new ItemDefinition { Category = ItemCategory.Placeable }.IsPlaceable);
        Assert.False(new ItemDefinition { Category = ItemCategory.Weapon }.IsPlaceable);
    }

    [Fact]
    public void IsEquippable_ForCorrectCategories()
    {
        Assert.True(new ItemDefinition { Category = ItemCategory.Weapon }.IsEquippable);
        Assert.True(new ItemDefinition { Category = ItemCategory.Armor }.IsEquippable);
        Assert.True(new ItemDefinition { Category = ItemCategory.Tool }.IsEquippable);
        Assert.True(new ItemDefinition { Category = ItemCategory.Accessory }.IsEquippable);
        Assert.False(new ItemDefinition { Category = ItemCategory.Food }.IsEquippable);
        Assert.False(new ItemDefinition { Category = ItemCategory.Potion }.IsEquippable);
    }

    [Fact]
    public void IsConsumable_ForFoodAndPotion()
    {
        Assert.True(new ItemDefinition { Category = ItemCategory.Food }.IsConsumable);
        Assert.True(new ItemDefinition { Category = ItemCategory.Potion }.IsConsumable);
        Assert.False(new ItemDefinition { Category = ItemCategory.Weapon }.IsConsumable);
    }

    [Fact]
    public void CategoryInt_ReturnsCorrectValue()
    {
        Assert.Equal(0, new ItemDefinition { Category = ItemCategory.Weapon }.CategoryInt);
        Assert.Equal(3, new ItemDefinition { Category = ItemCategory.Food }.CategoryInt);
    }

    [Theory]
    [InlineData(ItemCategory.Weapon, "Weapons")]
    [InlineData(ItemCategory.Armor, "Armor")]
    [InlineData(ItemCategory.Potion, "Potions")]
    [InlineData(ItemCategory.Material, "Resources")]
    [InlineData(ItemCategory.Placeable, "Placeables")]
    [InlineData(ItemCategory.Tool, "Tools")]
    [InlineData(ItemCategory.Food, "Food")]
    [InlineData(ItemCategory.Misc, "Other")]
    [InlineData(ItemCategory.Accessory, "Accessories")]
    [InlineData(ItemCategory.Seed, "Seeds")]
    [InlineData(ItemCategory.Ammo, "Ammo")]
    [InlineData(ItemCategory.Magic, "Magic")]
    public void CategoryName_ReturnsExpected(ItemCategory category, string expected)
    {
        Assert.Equal(expected, ItemDefinition.CategoryName(category));
    }

    [Fact]
    public void CategoryName_UnknownCategory_ReturnsOther()
    {
        Assert.Equal("Other", ItemDefinition.CategoryName((ItemCategory)999));
    }

    [Theory]
    [InlineData(ItemCategory.Weapon, "[Wpn]")]
    [InlineData(ItemCategory.Armor, "[Arm]")]
    [InlineData(ItemCategory.Accessory, "[Acc]")]
    [InlineData(ItemCategory.Potion, "[Pot]")]
    [InlineData(ItemCategory.Material, "[Res]")]
    [InlineData(ItemCategory.Placeable, "[Plc]")]
    [InlineData(ItemCategory.Tool, "[Tol]")]
    [InlineData(ItemCategory.Food, "[Fod]")]
    [InlineData(ItemCategory.Misc, "[Gld]")]
    [InlineData(ItemCategory.Seed, "[Res]")]
    [InlineData(ItemCategory.Ammo, "[Amm]")]
    [InlineData(ItemCategory.Magic, "[Mag]")]
    public void CategoryTag_ReturnsExpected(ItemCategory category, string expected)
    {
        Assert.Equal(expected, ItemDefinition.CategoryTag(category));
    }

    [Fact]
    public void CategoryTag_UnknownCategory_ReturnsSpaces()
    {
        Assert.Equal("     ", ItemDefinition.CategoryTag((ItemCategory)999));
    }
}
