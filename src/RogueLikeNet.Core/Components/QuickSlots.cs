namespace RogueLikeNet.Core.Components;

/// <summary>
/// Stores quick-use slot assignments. Each slot holds an inventory index (-1 = empty).
/// Decoupled from inventory position — the player explicitly assigns items.
/// </summary>
public struct QuickSlots
{
    public const int SlotCount = 8;

    public int Slot0;
    public int Slot1;
    public int Slot2;
    public int Slot3;
    public int Slot4;
    public int Slot5;
    public int Slot6;
    public int Slot7;

    public QuickSlots()
    {
        Slot0 = -1;
        Slot1 = -1;
        Slot2 = -1;
        Slot3 = -1;
        Slot4 = -1;
        Slot5 = -1;
        Slot6 = -1;
        Slot7 = -1;
    }

    public int Count => SlotCount;

    public int this[int index]
    {
        readonly get => index switch { 0 => Slot0, 1 => Slot1, 2 => Slot2, 3 => Slot3, 4 => Slot4, 5 => Slot5, 6 => Slot6, 7 => Slot7, _ => -1 };
        set { switch (index) { case 0: Slot0 = value; break; case 1: Slot1 = value; break; case 2: Slot2 = value; break; case 3: Slot3 = value; break; case 4: Slot4 = value; break; case 5: Slot5 = value; break; case 6: Slot6 = value; break; case 7: Slot7 = value; break; } }
    }

    /// <summary>Returns the first empty slot index, or -1 if all full.</summary>
    public int FirstEmptySlot()
    {
        if (Slot0 == -1) return 0;
        if (Slot1 == -1) return 1;
        if (Slot2 == -1) return 2;
        if (Slot3 == -1) return 3;
        if (Slot4 == -1) return 4;
        if (Slot5 == -1) return 5;
        if (Slot6 == -1) return 6;
        if (Slot7 == -1) return 7;
        return -1;
    }

    /// <summary>Clears any slot that references the given inventory index.</summary>
    public void ClearIndex(int inventoryIndex)
    {
        if (Slot0 == inventoryIndex) Slot0 = -1;
        if (Slot1 == inventoryIndex) Slot1 = -1;
        if (Slot2 == inventoryIndex) Slot2 = -1;
        if (Slot3 == inventoryIndex) Slot3 = -1;
        if (Slot4 == inventoryIndex) Slot4 = -1;
        if (Slot5 == inventoryIndex) Slot5 = -1;
        if (Slot6 == inventoryIndex) Slot6 = -1;
        if (Slot7 == inventoryIndex) Slot7 = -1;
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
        AdjustSlot(ref Slot4, removedIndex);
        AdjustSlot(ref Slot5, removedIndex);
        AdjustSlot(ref Slot6, removedIndex);
        AdjustSlot(ref Slot7, removedIndex);
    }

    private static void AdjustSlot(ref int slot, int removedIndex)
    {
        if (slot == removedIndex) slot = -1;
        else if (slot > removedIndex) slot--;
    }
}
