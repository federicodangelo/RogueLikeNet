using Arch.AOT.SourceGenerator;

namespace RogueLikeNet.Core.Components;

[Component]
public struct ItemData
{
    public int ItemTypeId;
    public int Rarity; // 0=Common, 1=Uncommon, 2=Rare, 3=Epic, 4=Legendary
    public int BonusAttack;
    public int BonusDefense;
    public int BonusHealth;
    public int StackCount;
}
