using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Entities;
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
        foreach (ref var player in worldMap.Players)
        {
            if (player.IsDead) continue;

            switch (player.Input.ActionType)
            {
                case ActionTypes.PickUp:
                    ProcessPickup(ref player, worldMap);
                    break;
                case ActionTypes.Drop:
                    ProcessDrop(ref player, engine);
                    break;
                case ActionTypes.UseItem:
                    ProcessUseItem(ref player);
                    break;
                case ActionTypes.UseQuickSlot:
                    ProcessUseQuickSlot(ref player);
                    break;
                case ActionTypes.SetQuickSlot:
                    ProcessSetQuickSlot(ref player);
                    break;
                case ActionTypes.SwapItems:
                    ProcessSwapItems(ref player);
                    break;
                case ActionTypes.Unequip:
                    ProcessUnequip(ref player);
                    break;
                case ActionTypes.DropEquipped:
                    ProcessDropEquipped(ref player, engine);
                    break;
            }
        }
    }

    private static void ProcessPickup(ref PlayerEntity player, WorldMap map)
    {
        player.Input.ActionType = ActionTypes.None;

        var chunk = map.GetChunkForWorldPos(player.Position);
        if (chunk == null) return;

        bool pickedAny = false;
        bool failedDueToInventoryFull = false;
        foreach (ref var gi in chunk.GroundItems)
        {
            if (gi.IsDestroyed || gi.Position != player.Position) continue;

            if (AddItemToInventory(ref player, gi.Item))
            {
                player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.PickUp, ItemTypeId = gi.Item.ItemTypeId, StackCount = gi.Item.StackCount });
                gi.IsDestroyed = true;
                pickedAny = true;
            }
            else
            {
                failedDueToInventoryFull = true;
            }
        }

        if (!pickedAny)
        {
            if (failedDueToInventoryFull)
                player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.PickUp, Failed = true, FailReason = ActionFailReason.InventoryFull });
            else
                player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.PickUp, Failed = true, FailReason = ActionFailReason.NoItemsOnGround });
        }
    }

    private static void ProcessDrop(ref PlayerEntity player, GameEngine engine)
    {
        int slot = player.Input.ItemSlot;
        player.Input.ActionType = ActionTypes.None;

        if (slot < 0 || slot >= player.Inventory.Items.Count)
        {
            player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.Drop, Failed = true });
            return;
        }

        var itemData = player.Inventory.Items[slot];
        player.Inventory.Items.RemoveAt(slot);
        player.QuickSlots.OnItemRemoved(slot);
        player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.Drop, ItemTypeId = itemData.ItemTypeId, StackCount = itemData.StackCount });

        var drop = engine.FindDropPosition(player.Position);

        var def = GameData.Instance.Items.Get(itemData.ItemTypeId);
        int dropGlyph = def != null && def.IsPlaceable
            ? RenderConstants.GlyphDroppedPlaceable
            : def?.GlyphId ?? 0;
        engine.SpawnItemOnGround(itemData, drop);
    }

    private static void ProcessUseItem(ref PlayerEntity player)
    {
        int slot = player.Input.ItemSlot;
        player.Input.ActionType = ActionTypes.None;

        if (slot < 0 || slot >= player.Inventory.Items.Count) return;

        var itemData = player.Inventory.Items[slot];
        var template = GameData.Instance.Items.Get(itemData.ItemTypeId);
        if (template == null) return;

        switch (template.Category)
        {
            case ItemCategory.Potion:
                ApplyPotion(ref player, template);
                player.Inventory.Items.RemoveAt(slot);
                player.QuickSlots.OnItemRemoved(slot);
                player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.UsePotion, ItemTypeId = itemData.ItemTypeId });
                break;

            case ItemCategory.Food:
                ApplyFood(ref player, template);
                player.Inventory.Items.RemoveAt(slot);
                player.QuickSlots.OnItemRemoved(slot);
                player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.EatFood, ItemTypeId = itemData.ItemTypeId });
                break;

            case ItemCategory.Weapon:
            case ItemCategory.Armor:
            case ItemCategory.Tool:
                EquipItem(ref player, slot);
                break;

            default:
                player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.UsePotion, ItemTypeId = itemData.ItemTypeId, Failed = true });
                break;
        }
    }

    private static void ApplyFood(ref PlayerEntity player, ItemDefinition newDef)
    {
        int healthRestore = newDef.Food?.HealthRestore ?? 0;
        if (healthRestore > 0)
            player.Health.Current = Math.Min(player.Health.Max, player.Health.Current + healthRestore);
        int hungerRestore = newDef.Food?.HungerRestore ?? 0;
        if (hungerRestore > 0)
            player.Survival.Hunger = Math.Min(player.Survival.MaxHunger, player.Survival.Hunger + hungerRestore);
        int thirstRestore = newDef.Food?.ThirstRestore ?? 0;
        if (thirstRestore > 0)
            player.Survival.Thirst = Math.Min(player.Survival.MaxThirst, player.Survival.Thirst + thirstRestore);
    }

    private static void ApplyPotion(ref PlayerEntity player, ItemDefinition def)
    {
        var potion = def.Potion;
        if (potion == null) return;

        // Instant heal
        if (potion.HealthRestore > 0)
            player.Health.Current = Math.Min(player.Health.Max, player.Health.Current + potion.HealthRestore);

        // Create temporary effect if there are stat boosts
        bool hasBuff = potion.AttackBoost != 0 || potion.DefenseBoost != 0 || potion.SpeedBoost != 0;
        if (hasBuff && potion.DurationTicks > 0)
        {
            var effectType = EffectType.StatsBoost;
            int speedMult = 100 + potion.SpeedBoost * 10;

            player.ActiveEffects.Add(new ActiveEffect(
                effectType,
                speedMult,
                potion.AttackBoost,
                potion.DefenseBoost,
                potion.DurationTicks));

            // Recalculate stats with new potion effect
            ActiveEffectsSystem.RecalculatePlayerStats(ref player);
        }
    }



    private static int ResolveEquipSlot(ItemDefinition def)
    {
        if (def.EquipSlot is { } slot)
            return (int)slot;

        // Fallback: weapons/tools → Weapon slot, armor → Chest
        return def.Category is ItemCategory.Weapon or ItemCategory.Tool
            ? (int)Data.EquipSlot.Hand
            : (int)Data.EquipSlot.Chest;
    }

    private static void EquipItem(ref PlayerEntity player, int slot)
    {
        var newItem = player.Inventory.Items[slot];
        var def = GameData.Instance.Items.Get(newItem.ItemTypeId);
        if (def == null) return;
        player.Inventory.Items.RemoveAt(slot);
        player.QuickSlots.OnItemRemoved(slot);

        int equipSlot = ResolveEquipSlot(def);

        if (player.Equipment.HasItem(equipSlot))
        {
            player.Inventory.Items.Add(player.Equipment[equipSlot]);
        }
        player.Equipment[equipSlot] = newItem;

        ActiveEffectsSystem.RecalculatePlayerStats(ref player);
        player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.Equip, ItemTypeId = newItem.ItemTypeId });
    }

    private static void ProcessSwapItems(ref PlayerEntity player)
    {
        int slotA = player.Input.ItemSlot;
        int slotB = player.Input.TargetSlot;
        player.Input.ActionType = ActionTypes.None;

        if (slotA < 0 || slotA >= player.Inventory.Items.Count) return;
        if (slotB < 0 || slotB >= player.Inventory.Items.Count) return;
        if (slotA == slotB) return;

        (player.Inventory.Items[slotA], player.Inventory.Items[slotB]) = (player.Inventory.Items[slotB], player.Inventory.Items[slotA]);

        for (int i = 0; i < QuickSlots.SlotCount; i++)
        {
            if (player.QuickSlots[i] == slotA) player.QuickSlots[i] = slotB;
            else if (player.QuickSlots[i] == slotB) player.QuickSlots[i] = slotA;
        }
    }

    private static void ProcessUnequip(ref PlayerEntity player)
    {
        int equipSlot = player.Input.ItemSlot;
        player.Input.ActionType = ActionTypes.None;

        if (player.Inventory.IsFull)
        {
            player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.Unequip, Failed = true });
            return;
        }
        if (equipSlot < 0 || equipSlot >= Equipment.SlotCount) return;

        if (player.Equipment.HasItem(equipSlot))
        {
            var old = player.Equipment[equipSlot];
            player.Inventory.Items.Add(old);
            player.Equipment[equipSlot] = ItemData.None;
            ActiveEffectsSystem.RecalculatePlayerStats(ref player);
            player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.Unequip, ItemTypeId = old.ItemTypeId });
        }
    }

    private static void ProcessDropEquipped(ref PlayerEntity player, GameEngine engine)
    {
        int equipSlot = player.Input.ItemSlot;
        player.Input.ActionType = ActionTypes.None;

        if (equipSlot < 0 || equipSlot >= Equipment.SlotCount) return;

        if (player.Equipment.HasItem(equipSlot))
        {
            var old = player.Equipment[equipSlot];
            player.Equipment[equipSlot] = ItemData.None;
            ActiveEffectsSystem.RecalculatePlayerStats(ref player);
            player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.Drop, ItemTypeId = old.ItemTypeId, StackCount = old.StackCount });

            engine.SpawnItemOnGround(old, engine.FindDropPosition(player.Position));
        }
    }

    private static void ProcessSetQuickSlot(ref PlayerEntity player)
    {
        int qsNum = player.Input.ItemSlot;
        int invIndex = player.Input.TargetSlot;
        player.Input.ActionType = ActionTypes.None;

        if (qsNum < 0 || qsNum >= QuickSlots.SlotCount) return;

        if (player.QuickSlots[qsNum] == invIndex)
        {
            player.QuickSlots[qsNum] = -1;
        }
        else
        {
            if (invIndex < 0 || invIndex >= player.Inventory.Items.Count) return;
            player.QuickSlots.ClearIndex(invIndex);
            player.QuickSlots[qsNum] = invIndex;
        }
    }

    private static void ProcessUseQuickSlot(ref PlayerEntity player)
    {
        int qsNum = player.Input.ItemSlot;
        player.Input.ActionType = ActionTypes.None;

        if (qsNum < 0 || qsNum >= QuickSlots.SlotCount) return;

        int invIndex = player.QuickSlots[qsNum];
        if (invIndex < 0) return;

        if (invIndex >= player.Inventory.Items.Count) return;

        var itemData = player.Inventory.Items[invIndex];
        var template = GameData.Instance.Items.Get(itemData.ItemTypeId);
        if (template == null) return;

        switch (template.Category)
        {
            case ItemCategory.Potion:
                ApplyPotion(ref player, template);
                player.Inventory.Items.RemoveAt(invIndex);
                player.QuickSlots.OnItemRemoved(invIndex);
                player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.UsePotion, ItemTypeId = itemData.ItemTypeId });
                break;

            case ItemCategory.Food:
                ApplyFood(ref player, template);
                player.Inventory.Items.RemoveAt(invIndex);
                player.QuickSlots.OnItemRemoved(invIndex);
                player.ActionEvents.Add(new PlayerActionEvent { EventType = PlayerActionEventType.EatFood, ItemTypeId = itemData.ItemTypeId });
                break;

            case ItemCategory.Weapon:
            case ItemCategory.Armor:
            case ItemCategory.Tool:
                EquipItem(ref player, invIndex);
                break;
        }
    }

    /// <summary>
    /// Adds an item to the player's inventory, handling auto-stacking and quick-slot assignment.
    /// Returns true if the item was fully added.
    /// </summary>
    public static bool AddItemToInventory(ref PlayerEntity player, ItemData itemData)
    {
        var def = GameData.Instance.Items.Get(itemData.ItemTypeId);
        if (def == null) return false;

        if (def.Stackable)
        {
            for (int i = 0; i < player.Inventory.Items.Count; i++)
            {
                if (player.Inventory.Items[i].ItemTypeId == itemData.ItemTypeId &&
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
        if (newIndex < 0 || newIndex >= player.Inventory.Items.Count) return;
        var itemData = player.Inventory.Items[newIndex];
        var def = GameData.Instance.Items.Get(itemData.ItemTypeId);
        if (def == null) return;
        if (def.Category is not (ItemCategory.Weapon or ItemCategory.Armor
            or ItemCategory.Potion or ItemCategory.Placeable
            or ItemCategory.Tool or ItemCategory.Food))
            return;
        int emptySlot = player.QuickSlots.FirstEmptySlot();
        if (emptySlot >= 0)
            player.QuickSlots[emptySlot] = newIndex;
    }
}
