using RogueLikeNet.Core.Definitions;

namespace RogueLikeNet.Core.Data;

/// <summary>
/// Maps legacy numeric item IDs (from ItemDefinitions constants) to new string-based
/// ItemDefinition objects from the ItemRegistry. Enables gradual migration from
/// hardcoded definitions to JSON-driven data.
/// </summary>
public static class LegacyItemBridge
{
    private static readonly Dictionary<int, string> OldToNewId = new()
    {
        // Weapons
        [ItemDefinitions.ShortSword] = "short_sword",
        [ItemDefinitions.LongSword] = "long_sword",
        [ItemDefinitions.BattleAxe] = "battle_axe",
        [ItemDefinitions.Dagger] = "dagger",
        // Armor
        [ItemDefinitions.LeatherArmor] = "leather_armor",
        [ItemDefinitions.ChainMail] = "chain_mail",
        [ItemDefinitions.PlateArmor] = "plate_armor",
        [ItemDefinitions.Shield] = "shield",
        // Potions
        [ItemDefinitions.HealthPotion] = "health_potion_small",
        [ItemDefinitions.StrengthPotion] = "strength_potion",
        // Currency
        [ItemDefinitions.Gold] = "gold_coin",
        // Resources
        [ItemDefinitions.Wood] = "wood",
        [ItemDefinitions.CopperOre] = "copper_ore",
        [ItemDefinitions.IronOre] = "iron_ore",
        [ItemDefinitions.GoldOre] = "gold_ore",
        // Placeable - doors
        [ItemDefinitions.WoodenDoor] = "wooden_door",
        [ItemDefinitions.CopperDoor] = "copper_door",
        [ItemDefinitions.IronDoor] = "iron_door",
        [ItemDefinitions.GoldDoor] = "gold_door",
        // Placeable - walls
        [ItemDefinitions.WoodenWall] = "wooden_wall",
        [ItemDefinitions.CopperWall] = "copper_wall",
        [ItemDefinitions.IronWall] = "iron_wall",
        [ItemDefinitions.GoldWall] = "gold_wall",
        // Placeable - windows
        [ItemDefinitions.WoodenWindow] = "wooden_window",
        // Furniture
        [ItemDefinitions.WoodenTable] = "wooden_table",
        [ItemDefinitions.WoodenChair] = "wooden_chair",
        [ItemDefinitions.WoodenBed] = "wooden_bed",
        [ItemDefinitions.WoodenBookshelf] = "wooden_bookshelf",
        // Floor tiles
        [ItemDefinitions.WoodenFloorTile] = "wooden_floor_tile",
        [ItemDefinitions.StoneFloorTile] = "stone_floor_tile",
        [ItemDefinitions.CopperFloorTile] = "copper_floor_tile",
        [ItemDefinitions.IronFloorTile] = "iron_floor_tile",
        [ItemDefinitions.GoldFloorTile] = "gold_floor_tile",
    };

    private static readonly Dictionary<string, int> NewToOldId;

    static LegacyItemBridge()
    {
        NewToOldId = new Dictionary<string, int>(OldToNewId.Count);
        foreach (var (oldId, newId) in OldToNewId)
            NewToOldId[newId] = oldId;
    }

    /// <summary>
    /// Gets the new string ID for a legacy numeric item ID.
    /// Returns null if the legacy ID has no mapping.
    /// </summary>
    public static string? GetNewId(int legacyId) =>
        OldToNewId.GetValueOrDefault(legacyId);

    /// <summary>
    /// Gets the legacy numeric ID for a new string item ID.
    /// Returns 0 if the string ID has no legacy mapping.
    /// </summary>
    public static int GetLegacyId(string newId) =>
        NewToOldId.GetValueOrDefault(newId);

    /// <summary>
    /// Looks up the new ItemDefinition for a legacy numeric item ID.
    /// Returns null if the registry is not loaded or the mapping doesn't exist.
    /// </summary>
    public static ItemDefinition? GetNewDefinition(int legacyId)
    {
        var newId = GetNewId(legacyId);
        return newId != null ? GameData.Instance.Items.Get(newId) : null;
    }
}
