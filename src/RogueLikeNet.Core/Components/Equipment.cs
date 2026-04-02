namespace RogueLikeNet.Core.Components;

public struct Equipment
{
    public ItemData? Weapon;
    public ItemData? Armor;

    public bool HasWeapon => Weapon.HasValue;
    public bool HasArmor => Armor.HasValue;
}
