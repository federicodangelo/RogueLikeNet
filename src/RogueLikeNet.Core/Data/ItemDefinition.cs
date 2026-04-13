using System.Text.Json.Serialization;

namespace RogueLikeNet.Core.Data;

public enum ItemCategory
{
    Weapon = 0,
    Armor = 1,
    Tool = 2,
    Food = 3,
    Potion = 4,
    Material = 5,
    Seed = 6,
    Placeable = 8,
    Accessory = 9,
    Ammo = 10,
    Magic = 11,
    Misc = 12,
}

public enum MaterialTier
{
    None = 0,
    Wood = 1,
    Stone = 2,
    Copper = 3,
    Iron = 4,
    Steel = 5,
    Gold = 6,
    Mithril = 7,
    Adamantite = 8,
}

public enum EquipSlot
{
    Head = 0,
    Chest = 1,
    Legs = 2,
    Boots = 3,
    Gloves = 4,
    Hand = 5,
    Offhand = 6,
    Ring = 7,
    Necklace = 8,
    Belt = 9,
}

/// <summary>
/// Defines a single item type. Loaded from JSON data files.
/// Category-specific data is stored in the optional sub-structs.
/// </summary>
public sealed class ItemDefinition : BaseDefinition
{
    public ItemCategory Category { get; set; }
    [JsonConverter(typeof(GlyphConverter))]
    public int GlyphId { get; set; }
    public int FgColor { get; set; }
    public bool Stackable { get; set; }
    public int MaxStackSize { get; set; } = 1;
    public MaterialTier MaterialTier { get; set; }
    public EquipSlot? EquipSlot { get; set; }

    // Category-specific data (only one should be populated per item)
    public WeaponData? Weapon { get; set; }
    public ArmorData? Armor { get; set; }
    public ToolData? Tool { get; set; }
    public FoodData? Food { get; set; }
    public PotionData? Potion { get; set; }
    public SeedData? Seed { get; set; }
    public PlaceableData? Placeable { get; set; }
    public AmmoData? Ammo { get; set; }

    // Computed convenience properties for backward compatibility
    public int BaseAttack => Weapon?.BaseDamage ?? Potion?.AttackBoost ?? 0;
    public int BaseDefense => Armor?.BaseDefense ?? Potion?.DefenseBoost ?? 0;
    public int BaseHealth => 0;

    // Material-tier-adjusted effective stats (damage = BaseDamage × tier, defense = BaseDefense × tier)
    public int EffectiveAttack => MaterialTiers.Apply(BaseAttack, MaterialTier);
    public int EffectiveDefense => MaterialTiers.Apply(BaseDefense, MaterialTier);
    public int HungerReduction => Food?.HungerRestore ?? 0;
    public int ThirstReduction => Food?.ThirstRestore ?? 0;
    public int HealthRestore => Food?.HealthRestore ?? Potion?.HealthRestore ?? 0;

    /// <summary>True for items that can be placed on tiles </summary>
    public bool IsPlaceable => Category is ItemCategory.Placeable;

    /// <summary>True for items that can be equipped.</summary>
    public bool IsEquippable => Category is ItemCategory.Weapon or ItemCategory.Armor or ItemCategory.Tool or ItemCategory.Accessory;

    public bool IsConsumable => Category is ItemCategory.Food or ItemCategory.Potion;

    /// <summary>Returns the category as an int suitable for protocol messages.</summary>
    public int CategoryInt => (int)Category;

    public static string CategoryName(ItemCategory category) => category switch
    {
        ItemCategory.Weapon => "Weapons",
        ItemCategory.Armor => "Armor",
        ItemCategory.Potion => "Potions",
        ItemCategory.Material => "Resources",
        ItemCategory.Placeable => "Placeables",
        ItemCategory.Tool => "Tools",
        ItemCategory.Food => "Food",
        ItemCategory.Misc => "Other",
        ItemCategory.Accessory => "Accessories",
        ItemCategory.Seed => "Seeds",
        ItemCategory.Ammo => "Ammo",
        ItemCategory.Magic => "Magic",
        _ => "Other",
    };

    public static string CategoryTag(ItemCategory category) => category switch
    {
        ItemCategory.Weapon => "[Wpn]",
        ItemCategory.Armor => "[Arm]",
        ItemCategory.Accessory => "[Acc]",
        ItemCategory.Potion => "[Pot]",
        ItemCategory.Material => "[Res]",
        ItemCategory.Placeable => "[Plc]",
        ItemCategory.Tool => "[Tol]",
        ItemCategory.Food => "[Fod]",
        ItemCategory.Misc => "[Gld]",
        ItemCategory.Seed => "[Res]",
        ItemCategory.Ammo => "[Amm]",
        ItemCategory.Magic => "[Mag]",
        _ => "     ",
    };
}

public enum DamageType
{
    Physical = 0,
    Fire = 1,
    Ice = 2,
    Lightning = 3,
    Poison = 4,
    Magic = 5,
}

public sealed class WeaponData
{
    public int BaseDamage { get; set; }
    public int AttackSpeed { get; set; }
    public DamageType DamageType { get; set; }
    public int Range { get; set; } = 1;
    public bool TwoHanded { get; set; }
}

public sealed class ArmorData
{
    public int BaseDefense { get; set; }
}

public enum ToolType
{
    None = 0,
    Pickaxe = 1,
    Axe = 2,
    Shovel = 3,
    Hoe = 4,
    Hammer = 5,
    Knife = 6,
    FishingRod = 7,
    WateringCan = 8,
}

public sealed class ToolData
{
    public ToolType ToolType { get; set; }
    public int MiningPower { get; set; }
    public int Durability { get; set; } = 100;
}

public sealed class FoodData
{
    public int HungerRestore { get; set; }
    public int ThirstRestore { get; set; }
    public int HealthRestore { get; set; }
    public string[]? Buffs { get; set; }
    public int BuffDuration { get; set; }
}

public sealed class PotionData
{
    public int HealthRestore { get; set; }
    public int AttackBoost { get; set; }
    public int DefenseBoost { get; set; }
    public int SpeedBoost { get; set; }
    public int Duration { get; set; }
}

public sealed class SeedData
{
    public int GrowthTicks { get; set; }
    public string HarvestItemId { get; set; } = "";
    public int HarvestMin { get; set; } = 1;
    public int HarvestMax { get; set; } = 1;
    public double SeedReturnChance { get; set; } = 0.5;
    public int WateredGrowthMultiplierBase100 { get; set; } = 150;
}

public enum PlaceableType
{
    Decoration = 0,
    CraftingStation = 1,
    Storage = 2,
    Lighting = 3,
    Door = 4,
    Wall = 5,
    FloorTile = 6,
    Window = 7,
    Table = 8,
    Chair = 9,
    Bed = 10,
}

public enum PlaceableStateType
{
    None = 0,
    OpenClose = 1,
}

public sealed class PlaceableData
{
    public PlaceableType PlaceableType { get; set; }
    public CraftingStationType? CraftingStationType { get; set; }
    public bool Walkable { get; set; } = true;
    public bool Transparent { get; set; } = true;
    public PlaceableStateType StateType { get; set; }
    public int AlternateGlyphId { get; set; }
    public bool AlternateWalkable { get; set; }
    public bool AlternateTransparent { get; set; }
    public int LightRadius { get; set; }
    public int LightColor { get; set; } = 0xFFFFFF;
}

public sealed class AmmoData
{
    public int Damage { get; set; }
    public DamageType DamageType { get; set; }
}
