using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;

namespace RogueLikeNet.Core.Generation;

public readonly record struct Loot(ItemDefinition Definition);

/// <summary>
/// Generates random loot from the item registry.
/// </summary>
public static class LootGenerator
{
    /// <summary>
    /// Generates a random item.
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

        return new Loot(def);
    }

    private static ItemDefinition[] GetItemsByCategory(ItemCategory category)
    {
        return GameData.Instance.Items.All
            .Where(d => d.Category == category)
            .ToArray();
    }

    public static ItemData GenerateItemData(ItemDefinition def, SeededRandom rng)
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
