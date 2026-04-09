using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;

namespace RogueLikeNet.Core.Generation;

public readonly record struct Loot(ItemDefinition Definition, int Rarity);

/// <summary>
/// Generates random loot from the item registry.
/// </summary>
public static class LootGenerator
{
    /// <summary>
    /// Generates a random item with optional rarity bonus.
    /// Uses items from the GameData registry.
    /// </summary>
    public static Loot GenerateLoot(SeededRandom rng, int difficulty)
    {
        // Pick category weighted: 70% Misc(gold), 10% resource, 10% potion, 5% armor, 5% weapon
        int roll = rng.Next(100);
        ItemCategory category;
        if (roll < 70) category = ItemCategory.Misc;
        else if (roll < 80) category = ItemCategory.Material;
        else if (roll < 90) category = ItemCategory.Potion;
        else if (roll < 95) category = ItemCategory.Armor;
        else category = ItemCategory.Weapon;

        // Filter by category from registry
        var candidates = GetItemsByCategory(category);
        var def = candidates[rng.Next(candidates.Length)];

        // Rarity roll: 0=Common(60%), 1=Uncommon(25%), 2=Rare(10%), 3=Epic(4%), 4=Legendary(1%)
        int rarityRoll = rng.Next(100);
        int rarity;
        if (rarityRoll < 60) rarity = 0;
        else if (rarityRoll < 85) rarity = 1;
        else if (rarityRoll < 95) rarity = 2;
        else if (rarityRoll < 99) rarity = 3;
        else rarity = 4;

        // Higher difficulty slightly boosts rarity
        rarity = Math.Min(4, rarity + difficulty / 3);
        rarity = CapRarity(def.Category, rarity);

        return new Loot(def, rarity);
    }

    private static ItemDefinition[] GetItemsByCategory(ItemCategory category)
    {
        return GameData.Instance.Items.All
            .Where(d => d.Category == category)
            .ToArray();
    }

    public static int CapRarity(ItemCategory itemCategory, int rarity)
    {
        // Gold, resources, and placeables are always Common rarity
        if (itemCategory is ItemCategory.Misc or ItemCategory.Material
            or ItemCategory.Block or ItemCategory.Furniture)
            return ItemDefinition.RarityCommon;
        return rarity;
    }

    public static ItemData GenerateItemData(ItemDefinition def, int rarity, SeededRandom rng)
    {
        return new ItemData
        {
            ItemTypeId = def.NumericId,
            StackCount = def.Stackable
                    ? def.Category switch
                    {
                        ItemCategory.Misc => 10 + rng.Next(50),
                        ItemCategory.Material or ItemCategory.Block or ItemCategory.Furniture => 1,
                        _ => 1,
                    }
                    : 1,
        };
    }
}
