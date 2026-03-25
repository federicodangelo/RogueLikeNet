using Arch.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Generation;
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
            if (inv.Items == null || inv.IsFull) continue;

            // Copy item data to inventory, then destroy the floor entity
            var itemData = world.Get<ItemData>(item);
            inv.Items.Add(itemData);
            world.Destroy(item);
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

            // Create a new entity on the ground
            var template = Array.Find(ItemDefinitions.Templates, t => t.TypeId == itemData.ItemTypeId);
            world.Create(
                new Position(pos.X, pos.Y),
                new TileAppearance(template.GlyphId, template.Color),
                itemData,
                new GroundItemTag());
        }
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
            var template = Array.Find(ItemDefinitions.Templates, t => t.TypeId == itemData.ItemTypeId);

            switch (template.Category)
            {
                case ItemDefinitions.CategoryPotion:
                    ApplyPotion(world, player, itemData);
                    inv.Items.RemoveAt(slot);
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
}
