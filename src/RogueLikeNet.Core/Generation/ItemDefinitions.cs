namespace RogueLikeNet.Core.Generation;

/// <summary>
/// Item templates for procedural loot generation. All values are integers.
/// </summary>
public static class ItemDefinitions
{
    // Item type categories
    public const int CategoryWeapon = 0;
    public const int CategoryArmor = 1;
    public const int CategoryPotion = 2;
    public const int CategoryGold = 3;

    // Specific item types
    public const int ShortSword = 0;
    public const int LongSword = 1;
    public const int BattleAxe = 2;
    public const int Dagger = 3;
    public const int LeatherArmor = 10;
    public const int ChainMail = 11;
    public const int PlateArmor = 12;
    public const int Shield = 13;
    public const int HealthPotion = 20;
    public const int StrengthPotion = 21;
    public const int Gold = 30;

    public static readonly ItemTemplate[] Templates =
    [
        // Weapons
        new(ShortSword,   CategoryWeapon, "Short Sword",   TileDefinitions.GlyphSword,  TileDefinitions.ColorWhite,  3, 0, 0),
        new(LongSword,    CategoryWeapon, "Long Sword",    TileDefinitions.GlyphSword,  TileDefinitions.ColorCyan,   5, 0, 0),
        new(BattleAxe,    CategoryWeapon, "Battle Axe",    TileDefinitions.GlyphSword,  TileDefinitions.ColorRed,    8, 0, 0),
        new(Dagger,       CategoryWeapon, "Dagger",        TileDefinitions.GlyphSword,  TileDefinitions.ColorGray,   2, 0, 0),
        // Armor
        new(LeatherArmor, CategoryArmor,  "Leather Armor", TileDefinitions.GlyphShield, TileDefinitions.ColorBrown,  0, 2, 0),
        new(ChainMail,    CategoryArmor,  "Chain Mail",    TileDefinitions.GlyphShield, TileDefinitions.ColorGray,   0, 4, 0),
        new(PlateArmor,   CategoryArmor,  "Plate Armor",   TileDefinitions.GlyphShield, TileDefinitions.ColorWhite,  0, 7, 0),
        new(Shield,       CategoryArmor,  "Shield",        TileDefinitions.GlyphShield, TileDefinitions.ColorYellow, 0, 3, 0),
        // Consumables
        new(HealthPotion,   CategoryPotion, "Health Potion",   TileDefinitions.GlyphPotion, TileDefinitions.ColorRed,   0, 0, 30),
        new(StrengthPotion, CategoryPotion, "Strength Potion", TileDefinitions.GlyphPotion, TileDefinitions.ColorOrange, 3, 0, 0),
        // Currency
        new(Gold, CategoryGold, "Gold", TileDefinitions.GlyphGold, TileDefinitions.ColorYellow, 0, 0, 0),
    ];

    /// <summary>
    /// Generates a random item with optional rarity bonus.
    /// </summary>
    public static (ItemTemplate Template, int Rarity) GenerateLoot(SeededRandom rng, int difficulty)
    {
        // Pick category weighted: 30% weapon, 25% armor, 25% potion, 20% gold
        int roll = rng.Next(100);
        int category;
        if (roll < 30) category = CategoryWeapon;
        else if (roll < 55) category = CategoryArmor;
        else if (roll < 80) category = CategoryPotion;
        else category = CategoryGold;

        // Filter templates by category
        var candidates = Templates.Where(t => t.Category == category).ToArray();
        var template = candidates[rng.Next(candidates.Length)];

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

        return (template, rarity);
    }
}

public readonly record struct ItemTemplate(
    int TypeId, int Category, string Name, int GlyphId, int Color,
    int BaseAttack, int BaseDefense, int BaseHealth);
