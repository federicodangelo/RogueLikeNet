namespace RogueLikeNet.Core.Components;

public struct ItemData
{
    public static readonly ItemData None = new() { ItemTypeId = 0 };

    public int ItemTypeId;
    public int StackCount;
    public int Durability;

    public readonly bool IsNone => ItemTypeId == 0;
}
