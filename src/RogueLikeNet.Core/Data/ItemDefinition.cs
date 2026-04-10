using System.Text.Json.Serialization;

namespace RogueLikeNet.Core.Data;

/// <summary>
/// Defines a single item type. Loaded from JSON data files.
/// Category-specific data is stored in the optional sub-structs.
/// </summary>
public sealed class ItemDefinition : BaseDefinition
{
    public ItemCategory Category { get; set; }
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
    public BlockData? Block { get; set; }
    public SeedData? Seed { get; set; }
    public FurnitureData? Furniture { get; set; }
    public AmmoData? Ammo { get; set; }

    // Computed convenience properties for backward compatibility
    public int BaseAttack => Weapon?.BaseDamage ?? Potion?.AttackBoost ?? 0;
    public int BaseDefense => Armor?.BaseDefense ?? Potion?.DefenseBoost ?? 0;
    public int BaseHealth => Potion?.HealthRestore ?? Food?.HealthRestore ?? 0;
    public int HungerReduction => Food?.HungerRestore ?? 0;
    public int ThirstReduction => Food?.ThirstRestore ?? 0;
    public int HealthRestore => Food?.HealthRestore ?? Potion?.HealthRestore ?? 0;

    /// <summary>True for items that can be placed on tiles (furniture, blocks).</summary>
    public bool IsPlaceable => Category is ItemCategory.Furniture or ItemCategory.Block;

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
        ItemCategory.Furniture or ItemCategory.Block => "Building",
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
        ItemCategory.Armor or ItemCategory.Accessory => "[Arm]",
        ItemCategory.Potion => "[Pot]",
        ItemCategory.Material => "[Res]",
        ItemCategory.Furniture or ItemCategory.Block => "[Bld]",
        ItemCategory.Tool => "[Tol]",
        ItemCategory.Food => "[Fod]",
        ItemCategory.Misc => "[Gld]",
        ItemCategory.Seed => "[Res]",
        ItemCategory.Ammo => "[Amm]",
        ItemCategory.Magic => "[Mag]",
        _ => "     ",
    };
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

public sealed class BlockData
{
    public int Hardness { get; set; }
    public string ToolRequired { get; set; } = "none";
}

public sealed class SeedData
{
    public int GrowthTicks { get; set; }
    public string HarvestItemId { get; set; } = "";
    public int HarvestMin { get; set; } = 1;
    public int HarvestMax { get; set; } = 1;
}

public sealed class FurnitureData
{
    public FurnitureType FurnitureType { get; set; }
    public int PlacedGlyphId { get; set; }
    public int PlacedFgColor { get; set; }
    public bool Walkable { get; set; } = true;
    public bool Transparent { get; set; } = true;
    public PlaceableStateType StateType { get; set; }
    public int AlternateGlyphId { get; set; }
    public bool AlternateWalkable { get; set; }
    public bool AlternateTransparent { get; set; }
}

public sealed class AmmoData
{
    public int Damage { get; set; }
    public DamageType DamageType { get; set; }
}
