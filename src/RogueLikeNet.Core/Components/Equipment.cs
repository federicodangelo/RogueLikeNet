using Arch.AOT.SourceGenerator;

namespace RogueLikeNet.Core.Components;

[Component]
public struct Equipment
{
    public ItemData? Weapon;
    public ItemData? Armor;

    public bool HasWeapon => Weapon.HasValue;
    public bool HasArmor => Armor.HasValue;
}
