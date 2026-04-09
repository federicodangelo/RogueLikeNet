using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Definitions;

/// <summary>
/// Item type definitions and loot generation. All values are integers.
/// TypeId 0 is reserved for "None" (no item).
/// </summary>
public static class ItemDefinitions
{
    // Item type categories
    public const int CategoryWeapon = 0;
    public const int CategoryArmor = 1;
    public const int CategoryPotion = 2;
    public const int CategoryGold = 3;
    public const int CategoryResource = 4;
    public const int CategoryPlaceable = 5;

    // Item rarities
    public const int RarityCommon = 0;
    public const int RarityUncommon = 1;
    public const int RarityRare = 2;
    public const int RarityEpic = 3;
    public const int RarityLegendary = 4;

    /// <summary>Sentinel value: no item.</summary>
    public const int None = 0;

    // Specific item types (IDs start at 1; 0 is reserved for None)
    public const int ShortSword = 1;
    public const int LongSword = 2;
    public const int BattleAxe = 3;
    public const int Dagger = 4;
    public const int LeatherArmor = 11;
    public const int ChainMail = 12;
    public const int PlateArmor = 13;
    public const int Shield = 14;
    public const int HealthPotion = 21;
    public const int StrengthPotion = 22;
    public const int Gold = 31;

    // Resource items
    public const int Wood = 41;
    public const int CopperOre = 42;
    public const int IronOre = 43;
    public const int GoldOre = 44;

    // Placeable items
    public const int WoodenDoor = 51;
    public const int WoodenWall = 52;
    public const int WoodenWindow = 53;
    public const int CopperDoor = 54;
    public const int CopperWall = 55;
    public const int IronDoor = 56;
    public const int IronWall = 57;
    public const int GoldDoor = 58;
    public const int GoldWall = 59;

    // Furniture / decoration placeable items
    public const int WoodenTable = 60;
    public const int WoodenChair = 61;
    public const int WoodenBed = 62;
    public const int WoodenBookshelf = 63;
    public const int WoodenFloorTile = 64;
    public const int StoneFloorTile = 65;
    public const int CopperFloorTile = 66;
    public const int IronFloorTile = 67;
    public const int GoldFloorTile = 68;

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
        // Placeable items (stackable)
        new(WoodenDoor,   CategoryPlaceable, "Wooden Door",   TileDefinitions.GlyphDoor, TileDefinitions.ColorWoodFg,   0, 0, 0, true, 99),
        new(WoodenWall,   CategoryPlaceable, "Wooden Wall",   TileDefinitions.GlyphWall, TileDefinitions.ColorWoodFg,   0, 0, 0, true, 99),
        new(WoodenWindow, CategoryPlaceable, "Wooden Window", TileDefinitions.GlyphWindow, TileDefinitions.ColorWindowFg, 0, 0, 0, true, 99),
        new(CopperDoor,   CategoryPlaceable, "Copper Door",   TileDefinitions.GlyphDoor, TileDefinitions.ColorCopperFg, 0, 0, 0, true, 99),
        new(CopperWall,   CategoryPlaceable, "Copper Wall",   TileDefinitions.GlyphWall, TileDefinitions.ColorCopperFg, 0, 0, 0, true, 99),
        new(IronDoor,     CategoryPlaceable, "Iron Door",     TileDefinitions.GlyphDoor, TileDefinitions.ColorIronFg,   0, 0, 0, true, 99),
        new(IronWall,     CategoryPlaceable, "Iron Wall",     TileDefinitions.GlyphWall, TileDefinitions.ColorIronFg,   0, 0, 0, true, 99),
        new(GoldDoor,     CategoryPlaceable, "Gold Door",     TileDefinitions.GlyphDoor, TileDefinitions.ColorGoldFg,   0, 0, 0, true, 99),
        new(GoldWall,     CategoryPlaceable, "Gold Wall",     TileDefinitions.GlyphWall, TileDefinitions.ColorGoldFg,   0, 0, 0, true, 99),
        // Furniture (placeable)
        new(WoodenTable,     CategoryPlaceable, "Wooden Table",     TileDefinitions.GlyphTable,     TileDefinitions.ColorTableFg,     0, 0, 0, true, 99),
        new(WoodenChair,     CategoryPlaceable, "Wooden Chair",     TileDefinitions.GlyphChair,     TileDefinitions.ColorChairFg,     0, 0, 0, true, 99),
        new(WoodenBed,       CategoryPlaceable, "Wooden Bed",       TileDefinitions.GlyphBed,       TileDefinitions.ColorBedFg,       0, 0, 0, true, 99),
        new(WoodenBookshelf, CategoryPlaceable, "Wooden Bookshelf", TileDefinitions.GlyphBookshelf, TileDefinitions.ColorBookshelfFg, 0, 0, 0, true, 99),
        // Floor tiles (placeable)
        new(WoodenFloorTile, CategoryPlaceable, "Wooden Floor",  TileDefinitions.GlyphFloorTile, TileDefinitions.ColorWoodFg,      0, 0, 0, true, 99),
        new(StoneFloorTile,  CategoryPlaceable, "Stone Floor",   TileDefinitions.GlyphFloorTile, TileDefinitions.ColorStoneTileFg, 0, 0, 0, true, 99),
        new(CopperFloorTile, CategoryPlaceable, "Copper Floor",  TileDefinitions.GlyphFloorTile, TileDefinitions.ColorCopperFg,    0, 0, 0, true, 99),
        new(IronFloorTile,   CategoryPlaceable, "Iron Floor",    TileDefinitions.GlyphFloorTile, TileDefinitions.ColorIronFg,      0, 0, 0, true, 99),
        new(GoldFloorTile,   CategoryPlaceable, "Gold Floor",    TileDefinitions.GlyphFloorTile, TileDefinitions.ColorGoldFg,      0, 0, 0, true, 99),
    ];

    /// <summary>
    /// Lookup by TypeId. When GameData is loaded, returns data from the JSON registry
    /// via LegacyItemBridge. Otherwise falls back to the hardcoded array.
    /// </summary>
    public static ItemDefinition Get(int typeId)
    {
        var newDef = LegacyItemBridge.GetNewDefinition(typeId);
        if (newDef != null)
            return ConvertFromData(typeId, newDef);

        return typeId > 0 && typeId < _byId.Length ? _byId[typeId] : default;
    }

    private static ItemDefinition ConvertFromData(int typeId, Data.ItemDefinition d) => new(
        typeId,
        CategoryFromData(d),
        d.Name,
        d.GlyphId,
        d.FgColor,
        d.Weapon?.BaseDamage ?? d.Potion?.AttackBoost ?? 0,
        d.Armor?.BaseDefense ?? d.Potion?.DefenseBoost ?? 0,
        d.Potion?.HealthRestore ?? 0,
        d.Stackable,
        d.MaxStackSize
    );

    private static int CategoryFromData(Data.ItemDefinition d) => d.Category switch
    {
        ItemCategory.Weapon => CategoryWeapon,
        ItemCategory.Armor or ItemCategory.Accessory => CategoryArmor,
        ItemCategory.Potion => CategoryPotion,
        ItemCategory.Material => CategoryResource,
        ItemCategory.Furniture or ItemCategory.Block => CategoryPlaceable,
        // gold_coin is Misc in JSON but CategoryGold in old system
        ItemCategory.Misc when d.Id == "gold_coin" => CategoryGold,
        _ => CategoryResource, // Tools, Food, Seeds, etc. default to resource (stackable)
    };

    private static readonly ItemDefinition[] _byId;

    static ItemDefinitions()
    {
        // Pre-build a flat array indexed by TypeId for O(1) lookup.
        int maxId = All.Max(t => t.TypeId);
        _byId = new ItemDefinition[maxId + 1];
        foreach (var d in All)
            _byId[d.TypeId] = d;
    }

    /// <summary>
    /// Generates a random item with optional rarity bonus.
    /// </summary>
    public static Loot GenerateLoot(SeededRandom rng, int difficulty)
    {
        // Pick category weighted: 80% gold, 10% potion, 5% weapon, 5% armor
        int roll = rng.Next(100);
        int category;
        if (roll < 80) category = CategoryGold;
        else if (roll < 90) category = CategoryPotion;
        else if (roll < 95) category = CategoryArmor;
        else category = CategoryWeapon;

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
        if (itemCategory is CategoryGold or CategoryResource or CategoryPlaceable)
            return RarityCommon;
        return rarity;
    }

    public static ItemData GenerateItemData(ItemDefinition def, int rarity, SeededRandom rnd)
    {
        return new ItemData
        {
            ItemTypeId = def.TypeId,
            StackCount = def.Stackable
                    ? def.Category switch
                    {
                        CategoryGold => 10 + rnd.Next(50),
                        CategoryResource or CategoryPlaceable => 1,
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
