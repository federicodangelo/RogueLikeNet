namespace RogueLikeNet.Core.Data;

public enum BiomeType
{
    Stone,
    Lava,
    Ice,
    Forest,
    Arcane,
    Crypt,
    Sewer,
    Fungal,
    Ruined,
    Infernal,
}

public enum ItemCategory
{
    Weapon = 0,
    Armor = 1,
    Tool = 2,
    Food = 3,
    Potion = 4,
    Material = 5,
    Seed = 6,
    Block = 7,
    Furniture = 8,
    Accessory = 9,
    Ammo = 10,
    Magic = 11,
    Misc = 12,
}

public enum EquipSlot
{
    Head = 0,
    Chest = 1,
    Legs = 2,
    Boots = 3,
    Gloves = 4,
    Weapon = 5,
    Offhand = 6,
    Ring = 7,
    Necklace = 8,
    Belt = 9,
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

public enum DamageType
{
    Physical = 0,
    Fire = 1,
    Ice = 2,
    Lightning = 3,
    Poison = 4,
    Magic = 5,
}

public enum CraftingStationType
{
    Hand = 0,
    Workbench = 1,
    Forge = 2,
    Anvil = 3,
    Furnace = 4,
    CookingPot = 5,
    Alchemy = 6,
    Loom = 7,
    TanningRack = 8,
    StoneCutter = 9,
    Sawmill = 10,
}

public enum FurnitureType
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
