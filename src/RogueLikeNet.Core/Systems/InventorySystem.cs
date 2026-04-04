using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// Handles item pickup, drop, use, and equipment.
/// Inventory stores ItemData values — picked-up floor entities are destroyed.
/// </summary>
public class InventorySystem
{
    public void Update(WorldMap worldMap, GameEngine engine)
    {
        ProcessPickups(worldMap);
        ProcessDrops(worldMap, engine);
        ProcessUseItem(worldMap);
        ProcessUseQuickSlot(worldMap);
        ProcessSetQuickSlot(worldMap);
        ProcessSwapItems(worldMap);
        ProcessUnequip(worldMap);
        ProcessEquip(worldMap);
    }

    private void ProcessPickups(WorldMap map)
    {
        foreach (ref var player in map.Players)
        {
            if (player.IsDead) continue;
            if (player.Input.ActionType != ActionTypes.PickUp) continue;
            player.Input.ActionType = ActionTypes.None;

            if (player.Inventory.Items == null || player.Inventory.IsFull) continue;

            var chunk = map.GetChunkForWorldPos(player.Position);
            if (chunk == null) continue;

            foreach (ref var gi in chunk.GroundItems)
            {
                if (gi.IsDestroyed || gi.Position != player.Position) continue;

                if (AddItemToInventory(ref player, gi.Item))
                    gi.IsDestroyed = true;
            }
        }
    }

    private void ProcessDrops(WorldMap map, GameEngine engine)
    {
        foreach (ref var player in map.Players)
        {
            if (player.IsDead) continue;
            if (player.Input.ActionType != ActionTypes.Drop) continue;

            int slot = player.Input.ItemSlot;
            player.Input.ActionType = ActionTypes.None;

            if (player.Inventory.Items == null || slot < 0 || slot >= player.Inventory.Items.Count) continue;

            var itemData = player.Inventory.Items[slot];
            player.Inventory.Items.RemoveAt(slot);
            player.QuickSlots.OnItemRemoved(slot);

            var drop = engine.FindDropPosition(player.Position);

            var def = ItemDefinitions.Get(itemData.ItemTypeId);
            int dropGlyph = def.Category == ItemDefinitions.CategoryPlaceable
                ? TileDefinitions.GlyphDroppedPlaceable
                : def.GlyphId;
            engine.SpawnItemOnGround(itemData, drop);
        }
    }

    private void ProcessUseItem(WorldMap map)
    {
        foreach (ref var player in map.Players)
        {
            if (player.IsDead) continue;
            if (player.Input.ActionType != ActionTypes.UseItem) continue;

            int slot = player.Input.ItemSlot;
            player.Input.ActionType = ActionTypes.None;

            if (player.Inventory.Items == null || slot < 0 || slot >= player.Inventory.Items.Count) continue;

            var itemData = player.Inventory.Items[slot];
            var template = ItemDefinitions.Get(itemData.ItemTypeId);

            switch (template.Category)
            {
                case ItemDefinitions.CategoryPotion:
                    ApplyPotion(ref player, itemData);
                    player.Inventory.Items!.RemoveAt(slot);
                    player.QuickSlots.OnItemRemoved(slot);
                    break;

                case ItemDefinitions.CategoryWeapon:
                case ItemDefinitions.CategoryArmor:
                    EquipItem(ref player, slot);
                    break;
            }
        }
    }

    private static void ApplyPotion(ref PlayerEntity player, ItemData itemData)
    {
        if (itemData.BonusHealth > 0)
            player.Health.Current = Math.Min(player.Health.Max, player.Health.Current + itemData.BonusHealth);
        if (itemData.BonusAttack > 0)
            player.CombatStats.Attack += itemData.BonusAttack;
        if (itemData.BonusDefense > 0)
            player.CombatStats.Defense += itemData.BonusDefense;
    }

    private static void ApplyItemStats(ref PlayerEntity player, ItemData item)
    {
        player.CombatStats.Attack += item.BonusAttack;
        player.CombatStats.Defense += item.BonusDefense;
        player.Health.Max += item.BonusHealth;
        player.Health.Current = Math.Min(player.Health.Current, player.Health.Max);
    }

    private static void RemoveItemStats(ref PlayerEntity player, ItemData item)
    {
        player.CombatStats.Attack -= item.BonusAttack;
        player.CombatStats.Defense -= item.BonusDefense;
        player.Health.Max -= item.BonusHealth;
        player.Health.Current = Math.Min(player.Health.Current, player.Health.Max);
    }

    private static void EquipItem(ref PlayerEntity player, int slot)
    {
        var newItem = player.Inventory.Items![slot];
        var def = ItemDefinitions.Get(newItem.ItemTypeId);
        player.Inventory.Items.RemoveAt(slot);
        player.QuickSlots.OnItemRemoved(slot);

        if (def.Category == ItemDefinitions.CategoryWeapon)
        {
            if (player.Equipment.HasWeapon)
            {
                RemoveItemStats(ref player, player.Equipment.Weapon!.Value);
                player.Inventory.Items!.Add(player.Equipment.Weapon!.Value);
            }
            player.Equipment.Weapon = newItem;
        }
        else
        {
            if (player.Equipment.HasArmor)
            {
                RemoveItemStats(ref player, player.Equipment.Armor!.Value);
                player.Inventory.Items!.Add(player.Equipment.Armor!.Value);
            }
            player.Equipment.Armor = newItem;
        }

        ApplyItemStats(ref player, newItem);
    }

    private void ProcessSwapItems(WorldMap map)
    {
        foreach (ref var player in map.Players)
        {
            if (player.IsDead) continue;
            if (player.Input.ActionType != ActionTypes.SwapItems) continue;

            int slotA = player.Input.ItemSlot;
            int slotB = player.Input.TargetSlot;
            player.Input.ActionType = ActionTypes.None;

            if (player.Inventory.Items == null) continue;
            if (slotA < 0 || slotA >= player.Inventory.Items.Count) continue;
            if (slotB < 0 || slotB >= player.Inventory.Items.Count) continue;
            if (slotA == slotB) continue;

            (player.Inventory.Items[slotA], player.Inventory.Items[slotB]) = (player.Inventory.Items[slotB], player.Inventory.Items[slotA]);

            for (int i = 0; i < QuickSlots.SlotCount; i++)
            {
                if (player.QuickSlots[i] == slotA) player.QuickSlots[i] = slotB;
                else if (player.QuickSlots[i] == slotB) player.QuickSlots[i] = slotA;
            }
        }
    }

    private void ProcessUnequip(WorldMap map)
    {
        foreach (ref var player in map.Players)
        {
            if (player.IsDead) continue;
            if (player.Input.ActionType != ActionTypes.Unequip) continue;

            int equipSlot = player.Input.ItemSlot;
            player.Input.ActionType = ActionTypes.None;

            if (player.Inventory.Items == null || player.Inventory.IsFull) continue;

            if (equipSlot == 0 && player.Equipment.HasWeapon)
            {
                var old = player.Equipment.Weapon!.Value;
                RemoveItemStats(ref player, old);
                player.Inventory.Items!.Add(old);
                player.Equipment.Weapon = null;
            }
            else if (equipSlot == 1 && player.Equipment.HasArmor)
            {
                var old = player.Equipment.Armor!.Value;
                RemoveItemStats(ref player, old);
                player.Inventory.Items!.Add(old);
                player.Equipment.Armor = null;
            }
        }
    }

    private void ProcessEquip(WorldMap map)
    {
        foreach (ref var player in map.Players)
        {
            if (player.IsDead) continue;
            if (player.Input.ActionType != ActionTypes.Equip) continue;

            int slot = player.Input.ItemSlot;
            player.Input.ActionType = ActionTypes.None;

            if (player.Inventory.Items == null || slot < 0 || slot >= player.Inventory.Items.Count) continue;

            var itemData = player.Inventory.Items[slot];
            var def = ItemDefinitions.Get(itemData.ItemTypeId);

            switch (def.Category)
            {
                case ItemDefinitions.CategoryWeapon:
                case ItemDefinitions.CategoryArmor:
                    EquipItem(ref player, slot);
                    break;
            }
        }
    }

    private void ProcessSetQuickSlot(WorldMap map)
    {
        foreach (ref var player in map.Players)
        {
            if (player.IsDead) continue;
            if (player.Input.ActionType != ActionTypes.SetQuickSlot) continue;

            int qsNum = player.Input.ItemSlot;
            int invIndex = player.Input.TargetSlot;
            player.Input.ActionType = ActionTypes.None;

            if (qsNum < 0 || qsNum >= QuickSlots.SlotCount) continue;

            if (player.QuickSlots[qsNum] == invIndex)
            {
                player.QuickSlots[qsNum] = -1;
            }
            else
            {
                if (player.Inventory.Items == null || invIndex < 0 || invIndex >= player.Inventory.Items.Count) continue;
                player.QuickSlots.ClearIndex(invIndex);
                player.QuickSlots[qsNum] = invIndex;
            }
        }
    }

    private void ProcessUseQuickSlot(WorldMap map)
    {
        foreach (ref var player in map.Players)
        {
            if (player.IsDead) continue;
            if (player.Input.ActionType != ActionTypes.UseQuickSlot) continue;

            int qsNum = player.Input.ItemSlot;
            player.Input.ActionType = ActionTypes.None;

            if (qsNum < 0 || qsNum >= QuickSlots.SlotCount) continue;

            int invIndex = player.QuickSlots[qsNum];
            if (invIndex < 0) continue;

            if (player.Inventory.Items == null || invIndex >= player.Inventory.Items.Count) continue;

            var itemData = player.Inventory.Items[invIndex];
            var template = ItemDefinitions.Get(itemData.ItemTypeId);

            switch (template.Category)
            {
                case ItemDefinitions.CategoryPotion:
                    ApplyPotion(ref player, itemData);
                    player.Inventory.Items!.RemoveAt(invIndex);
                    player.QuickSlots.OnItemRemoved(invIndex);
                    break;

                case ItemDefinitions.CategoryWeapon:
                case ItemDefinitions.CategoryArmor:
                    EquipItem(ref player, invIndex);
                    break;
            }
        }
    }

    /// <summary>
    /// Adds an item to the player's inventory, handling auto-stacking and quick-slot assignment.
    /// Returns true if the item was fully added.
    /// </summary>
    public static bool AddItemToInventory(ref PlayerEntity player, ItemData itemData)
    {
        if (player.Inventory.Items == null) return false;

        var def = ItemDefinitions.Get(itemData.ItemTypeId);

        if (def.Stackable)
        {
            for (int i = 0; i < player.Inventory.Items.Count; i++)
            {
                if (player.Inventory.Items[i].ItemTypeId == itemData.ItemTypeId &&
                    player.Inventory.Items[i].Rarity == itemData.Rarity &&
                    player.Inventory.Items[i].StackCount < def.MaxStackSize)
                {
                    var existing = player.Inventory.Items[i];
                    int canAdd = def.MaxStackSize - existing.StackCount;
                    int toAdd = Math.Min(canAdd, itemData.StackCount);
                    existing.StackCount += toAdd;
                    player.Inventory.Items[i] = existing;
                    itemData.StackCount -= toAdd;
                    if (itemData.StackCount <= 0) return true;
                }
            }
            if (itemData.StackCount > 0 && !player.Inventory.IsFull)
            {
                int newIndex = player.Inventory.Items.Count;
                player.Inventory.Items.Add(itemData);
                AutoAssignQuickSlot(ref player, newIndex);
                return true;
            }
            return false;
        }
        else
        {
            if (player.Inventory.IsFull) return false;
            int newIndex = player.Inventory.Items.Count;
            player.Inventory.Items.Add(itemData);
            AutoAssignQuickSlot(ref player, newIndex);
            return true;
        }
    }

    private static void AutoAssignQuickSlot(ref PlayerEntity player, int newIndex)
    {
        if (player.Inventory.Items == null || newIndex < 0 || newIndex >= player.Inventory.Items.Count) return;
        var itemData = player.Inventory.Items[newIndex];
        var def = ItemDefinitions.Get(itemData.ItemTypeId);
        if (def.Category != ItemDefinitions.CategoryWeapon &&
            def.Category != ItemDefinitions.CategoryArmor &&
            def.Category != ItemDefinitions.CategoryPotion &&
            def.Category != ItemDefinitions.CategoryPlaceable)
            return;
        int emptySlot = player.QuickSlots.FirstEmptySlot();
        if (emptySlot >= 0)
            player.QuickSlots[emptySlot] = newIndex;
    }
}
