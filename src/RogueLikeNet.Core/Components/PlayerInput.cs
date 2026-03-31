using Arch.AOT.SourceGenerator;

namespace RogueLikeNet.Core.Components;

[Component]
public struct PlayerInput
{
    public int ActionType;
    public int TargetX;
    public int TargetY;
    public int ItemSlot;
    public int TargetSlot;
}

public static class ActionTypes
{
    public const int None = 0;
    public const int Move = 1;
    public const int Attack = 2;
    public const int UseItem = 3;
    public const int UseSkill = 4;
    public const int PickUp = 5;
    public const int Drop = 6;
    public const int Wait = 7;
    public const int SwapItems = 8;
    public const int Unequip = 9;
    public const int Equip = 10;
    public const int SetQuickSlot = 11;
    public const int UseQuickSlot = 12;
    public const int Craft = 13;
    public const int PlaceItem = 14;
    public const int PickUpPlaced = 15;
    public const int UseStairs = 16;
}
