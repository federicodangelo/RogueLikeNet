namespace RogueLikeNet.Core.Components;

public struct Inventory
{
    public List<ItemData>? Items;
    public int Capacity;

    public Inventory(int capacity)
    {
        Capacity = capacity;
        Items = new List<ItemData>(capacity);
    }

    public bool IsFull => Items != null && Items.Count >= Capacity;
}

public struct Equipment
{
    public ItemData? Weapon;
    public ItemData? Armor;

    public bool HasWeapon => Weapon.HasValue;
    public bool HasArmor => Armor.HasValue;
}

public struct ItemData
{
    public int ItemTypeId;
    public int Rarity; // 0=Common, 1=Uncommon, 2=Rare, 3=Epic, 4=Legendary
    public int BonusAttack;
    public int BonusDefense;
    public int BonusHealth;
    public int StackCount;
}
