namespace RogueLikeNet.Core.Components;

/// <summary>
/// Stores quick-use slot assignments. Each slot holds an inventory index (-1 = empty).
/// Decoupled from inventory position — the player explicitly assigns items.
/// </summary>
public struct QuickSlots
{
    public const int SlotCount = 4;

    public int Slot0;
    public int Slot1;
    public int Slot2;
    public int Slot3;

    public QuickSlots()
    {
        Slot0 = -1;
        Slot1 = -1;
        Slot2 = -1;
        Slot3 = -1;
    }

    public int this[int index]
    {
        readonly get => index switch { 0 => Slot0, 1 => Slot1, 2 => Slot2, 3 => Slot3, _ => -1 };
        set { switch (index) { case 0: Slot0 = value; break; case 1: Slot1 = value; break; case 2: Slot2 = value; break; case 3: Slot3 = value; break; } }
    }

    /// <summary>Returns the first empty slot index, or -1 if all full.</summary>
    public int FirstEmptySlot()
    {
        if (Slot0 == -1) return 0;
        if (Slot1 == -1) return 1;
        if (Slot2 == -1) return 2;
        if (Slot3 == -1) return 3;
        return -1;
    }

    /// <summary>Clears any slot that references the given inventory index.</summary>
    public void ClearIndex(int inventoryIndex)
    {
        if (Slot0 == inventoryIndex) Slot0 = -1;
        if (Slot1 == inventoryIndex) Slot1 = -1;
        if (Slot2 == inventoryIndex) Slot2 = -1;
        if (Slot3 == inventoryIndex) Slot3 = -1;
    }

    /// <summary>
    /// Adjusts all slot references after an item is removed from inventory at the given index.
    /// Slots pointing to removed index become -1; slots above it shift down by 1.
    /// </summary>
    public void OnItemRemoved(int removedIndex)
    {
        AdjustSlot(ref Slot0, removedIndex);
        AdjustSlot(ref Slot1, removedIndex);
        AdjustSlot(ref Slot2, removedIndex);
        AdjustSlot(ref Slot3, removedIndex);
    }

    private static void AdjustSlot(ref int slot, int removedIndex)
    {
        if (slot == removedIndex) slot = -1;
        else if (slot > removedIndex) slot--;
    }
}
