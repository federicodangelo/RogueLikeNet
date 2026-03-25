using Arch.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// Handles item pickup, drop, use, and equipment.
/// Processes PlayerInput actions: PickUp, Drop, UseItem.
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

            // Find a ground item at the player's position
            var itemQuery = new QueryDescription().WithAll<Position, ItemData, GroundItemTag>();
            world.Query(in itemQuery, (Entity item, ref Position iPos) =>
            {
                if (iPos.X == px && iPos.Y == py)
                {
                    pickups.Add((player, item));
                }
            });
        });

        foreach (var (player, item) in pickups)
        {
            if (!world.IsAlive(item) || !world.IsAlive(player)) continue;

            ref var inv = ref world.Get<Inventory>(player);
            if (inv.Items == null || inv.IsFull) continue;

            // Move item from ground to inventory
            inv.Items.Add(item);
            world.Remove<GroundItemTag>(item);
            world.Remove<Position>(item);
            if (world.Has<TileAppearance>(item))
                world.Remove<TileAppearance>(item);
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

            var itemEntity = inv.Items[slot];
            inv.Items.RemoveAt(slot);

            if (!world.IsAlive(itemEntity)) continue;

            // Place item on the ground at the player's position
            world.Add(itemEntity, new Position(pos.X, pos.Y));
            world.Add(itemEntity, new GroundItemTag());
            if (world.Has<ItemData>(itemEntity) && !world.Has<TileAppearance>(itemEntity))
            {
                var itemData = world.Get<ItemData>(itemEntity);
                var template = Array.Find(ItemDefinitions.Templates, t => t.TypeId == itemData.ItemTypeId);
                world.Add(itemEntity, new TileAppearance(template.GlyphId, template.Color));
            }
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

            var itemEntity = inv.Items[slot];
            if (!world.IsAlive(itemEntity) || !world.Has<ItemData>(itemEntity)) continue;

            var itemData = world.Get<ItemData>(itemEntity);
            var template = Array.Find(ItemDefinitions.Templates, t => t.TypeId == itemData.ItemTypeId);

            switch (template.Category)
            {
                case ItemDefinitions.CategoryPotion:
                    ApplyPotion(world, player, itemData);
                    inv.Items.RemoveAt(slot);
                    world.Destroy(itemEntity);
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
        if (equip.HasWeapon && world.IsAlive(equip.Weapon) && world.Has<ItemData>(equip.Weapon))
        {
            var oldData = world.Get<ItemData>(equip.Weapon);
            stats.Attack -= oldData.BonusAttack;
            inv.Items.Add(equip.Weapon);
        }

        // Equip new weapon
        equip.Weapon = newWeapon;
        if (world.Has<ItemData>(newWeapon))
        {
            var weaponData = world.Get<ItemData>(newWeapon);
            stats.Attack += weaponData.BonusAttack;
        }
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
        if (equip.HasArmor && world.IsAlive(equip.Armor) && world.Has<ItemData>(equip.Armor))
        {
            var oldData = world.Get<ItemData>(equip.Armor);
            stats.Defense -= oldData.BonusDefense;
            inv.Items.Add(equip.Armor);
        }

        // Equip new armor
        equip.Armor = newArmor;
        if (world.Has<ItemData>(newArmor))
        {
            var armorData = world.Get<ItemData>(newArmor);
            stats.Defense += armorData.BonusDefense;
        }
    }
}
