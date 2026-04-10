using RogueLikeNet.Core.Data;

namespace RogueLikeNet.Core.Components;

public struct Equipment
{
    public const int SlotCount = 10;

    public ItemData Head;      // EquipSlot.Head = 0
    public ItemData Chest;     // EquipSlot.Chest = 1
    public ItemData Legs;      // EquipSlot.Legs = 2
    public ItemData Boots;     // EquipSlot.Boots = 3
    public ItemData Gloves;    // EquipSlot.Gloves = 4
    public ItemData Hand;      // EquipSlot.Hand = 5
    public ItemData Offhand;   // EquipSlot.Offhand = 6
    public ItemData Ring;      // EquipSlot.Ring = 7
    public ItemData Necklace;  // EquipSlot.Necklace = 8
    public ItemData Belt;      // EquipSlot.Belt = 9

    public readonly bool HasWeapon => !Hand.IsNone;
    public readonly bool HasArmor => !Chest.IsNone;

    public readonly bool HasItem(EquipSlot slot) => !this[(int)slot].IsNone;
    public readonly bool HasItem(int slot) => !this[slot].IsNone;

    public ItemData this[int slot]
    {
        readonly get => slot switch
        {
            0 => Head,
            1 => Chest,
            2 => Legs,
            3 => Boots,
            4 => Gloves,
            5 => Hand,
            6 => Offhand,
            7 => Ring,
            8 => Necklace,
            9 => Belt,
            _ => ItemData.None,
        };
        set
        {
            switch (slot)
            {
                case 0: Head = value; break;
                case 1: Chest = value; break;
                case 2: Legs = value; break;
                case 3: Boots = value; break;
                case 4: Gloves = value; break;
                case 5: Hand = value; break;
                case 6: Offhand = value; break;
                case 7: Ring = value; break;
                case 8: Necklace = value; break;
                case 9: Belt = value; break;
            }
        }
    }

    public ItemData this[EquipSlot slot]
    {
        readonly get => this[(int)slot];
        set => this[(int)slot] = value;
    }
}
