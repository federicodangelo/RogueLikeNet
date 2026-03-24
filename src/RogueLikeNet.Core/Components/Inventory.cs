namespace RogueLikeNet.Core.Components;

public struct Inventory
{
    public List<long>? ItemEntityIds;

    public Inventory(int capacity)
    {
        ItemEntityIds = new List<long>(capacity);
    }
}

public struct Equipment
{
    public long WeaponEntityId;
    public long ArmorEntityId;
    public long HelmetEntityId;
    public long BootsEntityId;
    public long RingEntityId;
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
