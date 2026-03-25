using Arch.Core;

namespace RogueLikeNet.Core.Components;

public struct Inventory
{
    public List<Entity>? Items;
    public int Capacity;

    public Inventory(int capacity)
    {
        Capacity = capacity;
        Items = new List<Entity>(capacity);
    }

    public bool IsFull => Items != null && Items.Count >= Capacity;
}

public struct Equipment
{
    public Entity Weapon;
    public Entity Armor;
    public Entity Helmet;
    public Entity Boots;
    public Entity Ring;

    public bool HasWeapon => Weapon != Entity.Null;
    public bool HasArmor => Armor != Entity.Null;
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
