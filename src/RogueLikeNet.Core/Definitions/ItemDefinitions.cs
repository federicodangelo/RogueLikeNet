using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Definitions;

/// <summary>
/// Item type definitions and loot generation. All values are integers.
/// </summary>
public static class ItemDefinitions
{
    // Item type categories
    public const int CategoryWeapon = 0;
    public const int CategoryArmor = 1;
    public const int CategoryPotion = 2;
    public const int CategoryGold = 3;
    public const int CategoryResource = 4;
    public const int CategoryBuildable = 5;

    // Item rarities
    public const int RarityCommon = 0;
    public const int RarityUncommon = 1;
    public const int RarityRare = 2;
    public const int RarityEpic = 3;
    public const int RarityLegendary = 4;

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

    // Resource items
    public const int Wood = 40;
    public const int CopperOre = 41;
    public const int IronOre = 42;
    public const int GoldOre = 43;

    // Buildable items
    public const int WoodenDoor = 50;
    public const int WoodenWall = 51;
    public const int WoodenWindow = 52;
    public const int CopperDoor = 53;
    public const int CopperWall = 54;
    public const int IronDoor = 55;
    public const int IronWall = 56;
    public const int GoldDoor = 57;
    public const int GoldWall = 58;

    // Furniture / decoration buildable items
    public const int WoodenTable = 59;
    public const int WoodenChair = 60;
    public const int WoodenBed = 61;
    public const int WoodenBookshelf = 62;
    public const int WoodenFloorTile = 63;
    public const int StoneFloorTile = 64;
    public const int CopperFloorTile = 65;
    public const int IronFloorTile = 66;
    public const int GoldFloorTile = 67;

    public static readonly ItemDefinition[] All =
    [
        // Weapons (not stackable)
        new(ShortSword,   CategoryWeapon, "Short Sword",   TileDefinitions.GlyphSword,  TileDefinitions.ColorWhite,  3, 0, 0, false, 1),
        new(LongSword,    CategoryWeapon, "Long Sword",    TileDefinitions.GlyphSword,  TileDefinitions.ColorCyan,   5, 0, 0, false, 1),
        new(BattleAxe,    CategoryWeapon, "Battle Axe",    TileDefinitions.GlyphSword,  TileDefinitions.ColorRed,    8, 0, 0, false, 1),
        new(Dagger,       CategoryWeapon, "Dagger",        TileDefinitions.GlyphSword,  TileDefinitions.ColorGray,   2, 0, 0, false, 1),
        // Armor (not stackable)
        new(LeatherArmor, CategoryArmor,  "Leather Armor", TileDefinitions.GlyphShield, TileDefinitions.ColorBrown,  0, 2, 0, false, 1),
        new(ChainMail,    CategoryArmor,  "Chain Mail",    TileDefinitions.GlyphShield, TileDefinitions.ColorGray,   0, 4, 0, false, 1),
        new(PlateArmor,   CategoryArmor,  "Plate Armor",   TileDefinitions.GlyphShield, TileDefinitions.ColorWhite,  0, 7, 0, false, 1),
        new(Shield,       CategoryArmor,  "Shield",        TileDefinitions.GlyphShield, TileDefinitions.ColorYellow, 0, 3, 0, false, 1),
        // Consumables (stackable)
        new(HealthPotion,   CategoryPotion, "Health Potion",   TileDefinitions.GlyphPotion, TileDefinitions.ColorRed,   0, 0, 30, true, 10),
        new(StrengthPotion, CategoryPotion, "Strength Potion", TileDefinitions.GlyphPotion, TileDefinitions.ColorOrange, 3, 0, 0,  true, 10),
        // Currency (stackable)
        new(Gold, CategoryGold, "Gold", TileDefinitions.GlyphGold, TileDefinitions.ColorYellow, 0, 0, 0, true, 999),
        // Resources (stackable)
        new(Wood,      CategoryResource, "Wood",       TileDefinitions.GlyphLog,        TileDefinitions.ColorWoodFg,   0, 0, 0, true, 99),
        new(CopperOre, CategoryResource, "Copper Ore", TileDefinitions.GlyphOreNugget,  TileDefinitions.ColorCopperFg, 0, 0, 0, true, 99),
        new(IronOre,   CategoryResource, "Iron Ore",   TileDefinitions.GlyphOreNugget,  TileDefinitions.ColorIronFg,   0, 0, 0, true, 99),
        new(GoldOre,   CategoryResource, "Gold Ore",   TileDefinitions.GlyphOreNugget,  TileDefinitions.ColorGoldFg,   0, 0, 0, true, 99),
        // Buildable items (stackable)
        new(WoodenDoor,   CategoryBuildable, "Wooden Door",   TileDefinitions.GlyphDoor, TileDefinitions.ColorWoodFg,   0, 0, 0, true, 99),
        new(WoodenWall,   CategoryBuildable, "Wooden Wall",   TileDefinitions.GlyphWall, TileDefinitions.ColorWoodFg,   0, 0, 0, true, 99),
        new(WoodenWindow, CategoryBuildable, "Wooden Window", TileDefinitions.GlyphWindow, TileDefinitions.ColorWindowFg, 0, 0, 0, true, 99),
        new(CopperDoor,   CategoryBuildable, "Copper Door",   TileDefinitions.GlyphDoor, TileDefinitions.ColorCopperFg, 0, 0, 0, true, 99),
        new(CopperWall,   CategoryBuildable, "Copper Wall",   TileDefinitions.GlyphWall, TileDefinitions.ColorCopperFg, 0, 0, 0, true, 99),
        new(IronDoor,     CategoryBuildable, "Iron Door",     TileDefinitions.GlyphDoor, TileDefinitions.ColorIronFg,   0, 0, 0, true, 99),
        new(IronWall,     CategoryBuildable, "Iron Wall",     TileDefinitions.GlyphWall, TileDefinitions.ColorIronFg,   0, 0, 0, true, 99),
        new(GoldDoor,     CategoryBuildable, "Gold Door",     TileDefinitions.GlyphDoor, TileDefinitions.ColorGoldFg,   0, 0, 0, true, 99),
        new(GoldWall,     CategoryBuildable, "Gold Wall",     TileDefinitions.GlyphWall, TileDefinitions.ColorGoldFg,   0, 0, 0, true, 99),
        // Furniture (buildable)
        new(WoodenTable,     CategoryBuildable, "Wooden Table",     TileDefinitions.GlyphTable,     TileDefinitions.ColorTableFg,     0, 0, 0, true, 99),
        new(WoodenChair,     CategoryBuildable, "Wooden Chair",     TileDefinitions.GlyphChair,     TileDefinitions.ColorChairFg,     0, 0, 0, true, 99),
        new(WoodenBed,       CategoryBuildable, "Wooden Bed",       TileDefinitions.GlyphBed,       TileDefinitions.ColorBedFg,       0, 0, 0, true, 99),
        new(WoodenBookshelf, CategoryBuildable, "Wooden Bookshelf", TileDefinitions.GlyphBookshelf, TileDefinitions.ColorBookshelfFg, 0, 0, 0, true, 99),
        // Floor tiles (buildable)
        new(WoodenFloorTile, CategoryBuildable, "Wooden Floor",  TileDefinitions.GlyphFloorTile, TileDefinitions.ColorWoodFg,      0, 0, 0, true, 99),
        new(StoneFloorTile,  CategoryBuildable, "Stone Floor",   TileDefinitions.GlyphFloorTile, TileDefinitions.ColorStoneTileFg, 0, 0, 0, true, 99),
        new(CopperFloorTile, CategoryBuildable, "Copper Floor",  TileDefinitions.GlyphFloorTile, TileDefinitions.ColorCopperFg,    0, 0, 0, true, 99),
        new(IronFloorTile,   CategoryBuildable, "Iron Floor",    TileDefinitions.GlyphFloorTile, TileDefinitions.ColorIronFg,      0, 0, 0, true, 99),
        new(GoldFloorTile,   CategoryBuildable, "Gold Floor",    TileDefinitions.GlyphFloorTile, TileDefinitions.ColorGoldFg,      0, 0, 0, true, 99),
    ];

    /// <summary>Lookup by TypeId. Returns definition or default if not found.</summary>
    public static ItemDefinition Get(int typeId) =>
        Array.Find(All, d => d.TypeId == typeId);

    /// <summary>
    /// Generates a random item with optional rarity bonus.
    /// </summary>
    public static Loot GenerateLoot(SeededRandom rng, int difficulty)
    {
        // Pick category weighted: 30% weapon, 25% armor, 25% potion, 20% gold
        int roll = rng.Next(100);
        int category;
        if (roll < 30) category = CategoryWeapon;
        else if (roll < 55) category = CategoryArmor;
        else if (roll < 80) category = CategoryPotion;
        else category = CategoryGold;

        // Filter by category
        var candidates = All.Where(t => t.Category == category).ToArray();
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

    public static int CapRarity(int itemCategory, int rarity)
    {
        // Gold and resources are always Common rarity
        if (itemCategory is CategoryGold or CategoryResource or CategoryBuildable)
            return RarityCommon;
        return rarity;
    }

    public static ItemData GenerateItemData(ItemDefinition def, int rarity, SeededRandom rnd)
    {
        rarity = CapRarity(def.Category, rarity);

        int rarityMult = 100 + rarity * 50;
        return new ItemData
        {
            ItemTypeId = def.TypeId,
            Rarity = rarity,
            BonusAttack = def.BaseAttack * rarityMult / 100,
            BonusDefense = def.BaseDefense * rarityMult / 100,
            BonusHealth = def.BaseHealth * rarityMult / 100,
            StackCount = def.Stackable
                    ? def.Category switch
                    {
                        CategoryGold => 10 + rnd.Next(50),
                        CategoryResource or CategoryBuildable => 1,
                        _ => 1,
                    }
                    : 1,
        };
    }
}

/// <summary>
/// Immutable definition for an item type. Describes everything about an item kind:
/// appearance, base stats, stackability, etc. Instances live in ItemDefinitions.All.
/// </summary>
public readonly record struct ItemDefinition(
    int TypeId, int Category, string Name, int GlyphId, int Color,
    int BaseAttack, int BaseDefense, int BaseHealth,
    bool Stackable, int MaxStackSize
);

public readonly record struct Loot(ItemDefinition Definition, int Rarity);
