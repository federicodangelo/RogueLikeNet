using Arch.Core;
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
    public void Update(Arch.Core.World world, WorldMap worldMap)
    {
        ProcessPickups(world);
        ProcessDrops(world);
        ProcessUseItem(world);
        ProcessUseQuickSlot(world);
        ProcessSetQuickSlot(world);
        ProcessSwapItems(world);
        ProcessUnequip(world);
        ProcessEquip(world);
    }

    private void ProcessPickups(Arch.Core.World world)
    {
        var pickups = new List<(Entity Player, Entity Item)>();

        var playerQuery = new QueryDescription().WithAll<Position, PlayerInput, Inventory>();
        world.Query(in playerQuery, (Entity player, ref Position pPos, ref PlayerInput input, ref Inventory inv) =>
        {
            if (input.ActionType != ActionTypes.PickUp) return;
            input.ActionType = ActionTypes.None;

            if (inv.Items == null || inv.IsFull) return;

            int px = pPos.X, py = pPos.Y;

            var itemQuery = new QueryDescription().WithAll<Position, ItemData, GroundItemTag>();
            world.Query(in itemQuery, (Entity item, ref Position iPos) =>
            {
                if (iPos.X == px && iPos.Y == py)
                    pickups.Add((player, item));
            });
        });

        foreach (var (player, item) in pickups)
        {
            if (!world.IsAlive(item) || !world.IsAlive(player)) continue;

            ref var inv = ref world.Get<Inventory>(player);
            if (inv.Items == null) continue;

            // Copy item data to inventory, then destroy the floor entity
            var itemData = world.Get<ItemData>(item);
            var def = ItemDefinitions.Get(itemData.ItemTypeId);

            // Try auto-stack if stackable
            if (def.Stackable)
            {
                bool stacked = false;
                for (int i = 0; i < inv.Items.Count; i++)
                {
                    if (inv.Items[i].ItemTypeId == itemData.ItemTypeId &&
                        inv.Items[i].StackCount < def.MaxStackSize)
                    {
                        var existing = inv.Items[i];
                        int canAdd = def.MaxStackSize - existing.StackCount;
                        int toAdd = Math.Min(canAdd, itemData.StackCount);
                        existing.StackCount += toAdd;
                        inv.Items[i] = existing;
                        itemData.StackCount -= toAdd;
                        if (itemData.StackCount <= 0) { stacked = true; break; }
                    }
                }
                if (stacked) { world.Destroy(item); continue; }
                // Remaining stack goes into new slot
                if (itemData.StackCount > 0 && !inv.IsFull)
                {
                    int newIndex = inv.Items.Count;
                    inv.Items.Add(itemData);
                    AutoAssignQuickSlot(world, player, newIndex);
                    world.Destroy(item);
                }
            }
            else
            {
                if (inv.IsFull) continue;
                int newIndex = inv.Items.Count;
                inv.Items.Add(itemData);
                AutoAssignQuickSlot(world, player, newIndex);
                world.Destroy(item);
            }
        }
    }

    private void ProcessDrops(Arch.Core.World world)
    {
        var drops = new List<(Entity Player, int Slot)>();

        var playerQuery = new QueryDescription().WithAll<Position, PlayerInput, Inventory>();
        world.Query(in playerQuery, (Entity player, ref PlayerInput input) =>
        {
            if (input.ActionType != ActionTypes.Drop) return;
            drops.Add((player, input.ItemSlot));
            input.ActionType = ActionTypes.None;
        });

        foreach (var (player, slot) in drops)
        {
            if (!world.IsAlive(player)) continue;
            ref var inv = ref world.Get<Inventory>(player);
            ref var pos = ref world.Get<Position>(player);

            if (inv.Items == null || slot < 0 || slot >= inv.Items.Count) continue;

            var itemData = inv.Items[slot];
            inv.Items.RemoveAt(slot);

            // Adjust quick-slot references after removal
            if (world.Has<QuickSlots>(player))
            {
                ref var qs = ref world.Get<QuickSlots>(player);
                qs.OnItemRemoved(slot);
            }

            // Find a position without an existing ground item (spiral outward)
            var (dropX, dropY) = FindDropPosition(world, pos.X, pos.Y);

            // Create a new entity on the ground
            var template = Array.Find(ItemDefinitions.All, t => t.TypeId == itemData.ItemTypeId);
            world.Create(
                new Position(dropX, dropY),
                new TileAppearance(template.GlyphId, template.Color),
                itemData,
                new GroundItemTag());
        }
    }

    private static (int X, int Y) FindDropPosition(Arch.Core.World world, int originX, int originY)
    {
        // Collect positions of all ground items
        var occupied = new HashSet<long>();
        var groundQuery = new QueryDescription().WithAll<Position, GroundItemTag>();
        world.Query(in groundQuery, (ref Position gPos) =>
        {
            occupied.Add(FOVData.PackCoord(gPos.X, gPos.Y));
        });

        // Try origin first, then spiral outward up to radius 5
        if (!occupied.Contains(FOVData.PackCoord(originX, originY)))
            return (originX, originY);

        for (int r = 1; r <= 5; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
            {
                if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue; // only check ring
                int x = originX + dx;
                int y = originY + dy;
                if (!occupied.Contains(FOVData.PackCoord(x, y)))
                    return (x, y);
            }
        }

        return (originX, originY); // fallback
    }

    private void ProcessUseItem(Arch.Core.World world)
    {
        var uses = new List<(Entity Player, int Slot)>();

        var playerQuery = new QueryDescription().WithAll<PlayerInput, Inventory, Health, CombatStats>();
        world.Query(in playerQuery, (Entity player, ref PlayerInput input) =>
        {
            if (input.ActionType != ActionTypes.UseItem) return;
            uses.Add((player, input.ItemSlot));
            input.ActionType = ActionTypes.None;
        });

        foreach (var (player, slot) in uses)
        {
            if (!world.IsAlive(player)) continue;
            ref var inv = ref world.Get<Inventory>(player);
            if (inv.Items == null || slot < 0 || slot >= inv.Items.Count) continue;

            var itemData = inv.Items[slot];
            var template = Array.Find(ItemDefinitions.All, t => t.TypeId == itemData.ItemTypeId);

            switch (template.Category)
            {
                case ItemDefinitions.CategoryPotion:
                    ApplyPotion(world, player, itemData);
                    inv.Items.RemoveAt(slot);
                    if (world.Has<QuickSlots>(player))
                    {
                        ref var qs = ref world.Get<QuickSlots>(player);
                        qs.OnItemRemoved(slot);
                    }
                    break;

                case ItemDefinitions.CategoryWeapon:
                    EquipWeapon(world, player, slot);
                    break;

                case ItemDefinitions.CategoryArmor:
                    EquipArmor(world, player, slot);
                    break;
            }
        }
    }

    private static void ApplyPotion(Arch.Core.World world, Entity player, ItemData itemData)
    {
        ref var health = ref world.Get<Health>(player);
        ref var stats = ref world.Get<CombatStats>(player);

        if (itemData.BonusHealth > 0)
            health.Current = Math.Min(health.Max, health.Current + itemData.BonusHealth);
        if (itemData.BonusAttack > 0)
            stats.Attack += itemData.BonusAttack;
    }

    private static void EquipWeapon(Arch.Core.World world, Entity player, int slot)
    {
        if (!world.Has<Equipment>(player)) return;
        ref var equip = ref world.Get<Equipment>(player);
        ref var inv = ref world.Get<Inventory>(player);
        ref var stats = ref world.Get<CombatStats>(player);

        var newWeapon = inv.Items![slot];
        inv.Items.RemoveAt(slot);

        // Adjust quick-slot references after removal
        if (world.Has<QuickSlots>(player))
        {
            ref var qs = ref world.Get<QuickSlots>(player);
            qs.OnItemRemoved(slot);
        }

        // Unequip current weapon if any
        if (equip.HasWeapon)
        {
            var oldData = equip.Weapon!.Value;
            stats.Attack -= oldData.BonusAttack;
            inv.Items.Add(oldData);
        }

        // Equip new weapon
        equip.Weapon = newWeapon;
        stats.Attack += newWeapon.BonusAttack;
    }

    private static void EquipArmor(Arch.Core.World world, Entity player, int slot)
    {
        if (!world.Has<Equipment>(player)) return;
        ref var equip = ref world.Get<Equipment>(player);
        ref var inv = ref world.Get<Inventory>(player);
        ref var stats = ref world.Get<CombatStats>(player);

        var newArmor = inv.Items![slot];
        inv.Items.RemoveAt(slot);

        // Adjust quick-slot references after removal
        if (world.Has<QuickSlots>(player))
        {
            ref var qs = ref world.Get<QuickSlots>(player);
            qs.OnItemRemoved(slot);
        }

        // Unequip current armor if any
        if (equip.HasArmor)
        {
            var oldData = equip.Armor!.Value;
            stats.Defense -= oldData.BonusDefense;
            inv.Items.Add(oldData);
        }

        // Equip new armor
        equip.Armor = newArmor;
        stats.Defense += newArmor.BonusDefense;
    }

    private void ProcessSwapItems(Arch.Core.World world)
    {
        var swaps = new List<(Entity Player, int SlotA, int SlotB)>();

        var playerQuery = new QueryDescription().WithAll<PlayerInput, Inventory>();
        world.Query(in playerQuery, (Entity player, ref PlayerInput input) =>
        {
            if (input.ActionType != ActionTypes.SwapItems) return;
            swaps.Add((player, input.ItemSlot, input.TargetSlot));
            input.ActionType = ActionTypes.None;
        });

        foreach (var (player, slotA, slotB) in swaps)
        {
            if (!world.IsAlive(player)) continue;
            ref var inv = ref world.Get<Inventory>(player);
            if (inv.Items == null) continue;
            if (slotA < 0 || slotA >= inv.Items.Count) continue;
            if (slotB < 0 || slotB >= inv.Items.Count) continue;
            if (slotA == slotB) continue;

            (inv.Items[slotA], inv.Items[slotB]) = (inv.Items[slotB], inv.Items[slotA]);

            // Update quick-slot references to track the swapped positions
            if (world.Has<QuickSlots>(player))
            {
                ref var qs = ref world.Get<QuickSlots>(player);
                for (int i = 0; i < QuickSlots.SlotCount; i++)
                {
                    if (qs[i] == slotA) qs[i] = slotB;
                    else if (qs[i] == slotB) qs[i] = slotA;
                }
            }
        }
    }

    private void ProcessUnequip(Arch.Core.World world)
    {
        var actions = new List<(Entity Player, int EquipSlot)>();

        var playerQuery = new QueryDescription().WithAll<PlayerInput, Inventory, Equipment, CombatStats>();
        world.Query(in playerQuery, (Entity player, ref PlayerInput input) =>
        {
            if (input.ActionType != ActionTypes.Unequip) return;
            actions.Add((player, input.ItemSlot)); // 0 = weapon, 1 = armor
            input.ActionType = ActionTypes.None;
        });

        foreach (var (player, equipSlot) in actions)
        {
            if (!world.IsAlive(player)) continue;
            ref var equip = ref world.Get<Equipment>(player);
            ref var inv = ref world.Get<Inventory>(player);
            ref var stats = ref world.Get<CombatStats>(player);
            if (inv.Items == null || inv.IsFull) continue;

            if (equipSlot == 0 && equip.HasWeapon)
            {
                var old = equip.Weapon!.Value;
                stats.Attack -= old.BonusAttack;
                inv.Items.Add(old);
                equip.Weapon = null;
            }
            else if (equipSlot == 1 && equip.HasArmor)
            {
                var old = equip.Armor!.Value;
                stats.Defense -= old.BonusDefense;
                inv.Items.Add(old);
                equip.Armor = null;
            }
        }
    }

    private void ProcessEquip(Arch.Core.World world)
    {
        var actions = new List<(Entity Player, int Slot)>();

        var playerQuery = new QueryDescription().WithAll<PlayerInput, Inventory, Equipment, Health, CombatStats>();
        world.Query(in playerQuery, (Entity player, ref PlayerInput input) =>
        {
            if (input.ActionType != ActionTypes.Equip) return;
            actions.Add((player, input.ItemSlot));
            input.ActionType = ActionTypes.None;
        });

        foreach (var (player, slot) in actions)
        {
            if (!world.IsAlive(player)) continue;
            ref var inv = ref world.Get<Inventory>(player);
            if (inv.Items == null || slot < 0 || slot >= inv.Items.Count) continue;

            var itemData = inv.Items[slot];
            var def = ItemDefinitions.Get(itemData.ItemTypeId);

            switch (def.Category)
            {
                case ItemDefinitions.CategoryWeapon:
                    EquipWeapon(world, player, slot);
                    break;
                case ItemDefinitions.CategoryArmor:
                    EquipArmor(world, player, slot);
                    break;
            }
        }
    }

    /// <summary>
    /// Handles SetQuickSlot action: toggle-assign an inventory item to a quick slot.
    /// ItemSlot = quick slot number (0-3), TargetSlot = inventory index to assign.
    /// If the quick slot already holds that inventory index, clear it (toggle off).
    /// </summary>
    private void ProcessSetQuickSlot(Arch.Core.World world)
    {
        var actions = new List<(Entity Player, int QuickSlotNum, int InvIndex)>();

        var playerQuery = new QueryDescription().WithAll<PlayerInput, Inventory, QuickSlots>();
        world.Query(in playerQuery, (Entity player, ref PlayerInput input) =>
        {
            if (input.ActionType != ActionTypes.SetQuickSlot) return;
            actions.Add((player, input.ItemSlot, input.TargetSlot));
            input.ActionType = ActionTypes.None;
        });

        foreach (var (player, qsNum, invIndex) in actions)
        {
            if (!world.IsAlive(player)) continue;
            if (qsNum < 0 || qsNum >= QuickSlots.SlotCount) continue;

            ref var qs = ref world.Get<QuickSlots>(player);
            ref var inv = ref world.Get<Inventory>(player);

            // Toggle: if this quick slot already points to this item, clear it
            if (qs[qsNum] == invIndex)
            {
                qs[qsNum] = -1;
            }
            else
            {
                // Validate the inventory index
                if (inv.Items == null || invIndex < 0 || invIndex >= inv.Items.Count) continue;

                // Clear any other quick slot that had this inventory index
                qs.ClearIndex(invIndex);
                qs[qsNum] = invIndex;
            }
        }
    }

    /// <summary>
    /// Handles UseQuickSlot action: resolves quick slot to inventory index, then uses the item.
    /// ItemSlot = quick slot number (0-3).
    /// </summary>
    private void ProcessUseQuickSlot(Arch.Core.World world)
    {
        var uses = new List<(Entity Player, int QuickSlotNum)>();

        var playerQuery = new QueryDescription().WithAll<PlayerInput, Inventory, QuickSlots, Health, CombatStats>();
        world.Query(in playerQuery, (Entity player, ref PlayerInput input) =>
        {
            if (input.ActionType != ActionTypes.UseQuickSlot) return;
            uses.Add((player, input.ItemSlot));
            input.ActionType = ActionTypes.None;
        });

        foreach (var (player, qsNum) in uses)
        {
            if (!world.IsAlive(player)) continue;
            if (qsNum < 0 || qsNum >= QuickSlots.SlotCount) continue;

            ref var qs = ref world.Get<QuickSlots>(player);
            int invIndex = qs[qsNum];
            if (invIndex < 0) continue;

            ref var inv = ref world.Get<Inventory>(player);
            if (inv.Items == null || invIndex >= inv.Items.Count) continue;

            var itemData = inv.Items[invIndex];
            var template = Array.Find(ItemDefinitions.All, t => t.TypeId == itemData.ItemTypeId);

            switch (template.Category)
            {
                case ItemDefinitions.CategoryPotion:
                    ApplyPotion(world, player, itemData);
                    inv.Items.RemoveAt(invIndex);
                    qs.OnItemRemoved(invIndex);
                    break;

                case ItemDefinitions.CategoryWeapon:
                    EquipWeapon(world, player, invIndex);
                    break;

                case ItemDefinitions.CategoryArmor:
                    EquipArmor(world, player, invIndex);
                    break;
            }
        }
    }

    /// <summary>
    /// Auto-assigns a newly added inventory item to the first empty quick slot.
    /// </summary>
    private static void AutoAssignQuickSlot(Arch.Core.World world, Entity player, int newIndex)
    {
        if (!world.Has<QuickSlots>(player)) return;
        ref var qs = ref world.Get<QuickSlots>(player);
        int emptySlot = qs.FirstEmptySlot();
        if (emptySlot >= 0)
            qs[emptySlot] = newIndex;
    }
}
