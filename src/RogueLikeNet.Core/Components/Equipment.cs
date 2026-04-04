using RogueLikeNet.Core.Definitions;

namespace RogueLikeNet.Core.Components;

public struct Equipment
{
    public ItemData Weapon;
    public ItemData Armor;

    public readonly bool HasWeapon => !Weapon.IsNone;
    public bool HasArmor => !Armor.IsNone;
}
