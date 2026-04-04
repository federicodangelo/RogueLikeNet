using RogueLikeNet.Core.Definitions;

namespace RogueLikeNet.Core.Components;

public struct ItemData
{
    public static readonly ItemData None = new() { ItemTypeId = ItemDefinitions.None };

    public int ItemTypeId;
    public int Rarity; // 0=Common, 1=Uncommon, 2=Rare, 3=Epic, 4=Legendary
    public int BonusAttack;
    public int BonusDefense;
    public int BonusHealth;
    public int StackCount;

    public readonly bool IsNone => ItemTypeId == ItemDefinitions.None;
}
