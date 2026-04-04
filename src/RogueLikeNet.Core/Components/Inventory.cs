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
}
