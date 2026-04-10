namespace RogueLikeNet.Core.Components;

public struct Inventory
{
    public List<ItemData> Items;
    public int Capacity;

    public Inventory()
    {
        Items = [];
    }

    public Inventory(int capacity)
    {
        Capacity = capacity;
        Items = new List<ItemData>(capacity);
    }

    public readonly bool IsFull => Items.Count >= Capacity;

    public bool FindSlotWithItem(int itemTypeId, out int slotIndex)
    {
        for (int i = 0; i < Items.Count; i++)
        {
            if (Items[i].ItemTypeId == itemTypeId)
            {
                slotIndex = i;
                return true;
            }
        }
        slotIndex = -1;
        return false;
    }
}
