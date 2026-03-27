using Arch.AOT.SourceGenerator;

namespace RogueLikeNet.Core.Components;

[Component]
public struct Inventory
{
    public List<ItemData>? Items;
    public int Capacity;

    public Inventory(int capacity)
    {
        Capacity = capacity;
        Items = new List<ItemData>(capacity);
    }

    public bool IsFull => Items != null && Items.Count >= Capacity;
}
