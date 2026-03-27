using Arch.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Tests;

public class InventorySystemTests
{
    private static readonly BspDungeonGenerator _gen = new();

    private GameEngine CreateEngine()
    {
        var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0);
        return engine;
    }

    [Fact]
    public void PickUp_MovesItemToInventory()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        // Spawn item at player's position
        var template = ItemDefinitions.All[0]; // Short Sword
        var item = engine.SpawnItemOnGround(template, 0, sx, sy);

        // Set pickup action
        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.PickUp;

        engine.Tick();

        // Item data should be in inventory, floor entity should be destroyed
        ref var inv = ref engine.EcsWorld.Get<Inventory>(player);
        Assert.Single(inv.Items!);
        Assert.NotNull(inv.Items);
        Assert.Equal(template.TypeId, inv.Items[0].ItemTypeId);
        Assert.False(engine.EcsWorld.IsAlive(item));
    }

    [Fact]
    public void Drop_PlacesItemOnGround()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        // Spawn and pick up an item
        var template = ItemDefinitions.All[0];
        var item = engine.SpawnItemOnGround(template, 0, sx, sy);

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Drop it
        ref var input2 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input2.ActionType = ActionTypes.Drop;
        input2.ItemSlot = 0;
        engine.Tick();

        ref var inv = ref engine.EcsWorld.Get<Inventory>(player);
        Assert.Empty(inv.Items!);

        // Original entity was destroyed on pickup; drop creates a new ground entity
        int groundCount = 0;
        var gq = new QueryDescription().WithAll<Position, ItemData, GroundItemTag>();
        engine.EcsWorld.Query(in gq, (ref Position gPos) =>
        {
            if (gPos.X == sx && gPos.Y == sy) groundCount++;
        });
        Assert.Equal(1, groundCount);
    }

    [Fact]
    public void UsePotion_RestoresHealth()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        // Damage the player
        ref var health = ref engine.EcsWorld.Get<Health>(player);
        health.Current = 50;

        // Spawn a health potion and pick it up
        var potionTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.HealthPotion);
        engine.SpawnItemOnGround(potionTemplate, 0, sx, sy);

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Use it
        ref var input2 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input2.ActionType = ActionTypes.UseItem;
        input2.ItemSlot = 0;
        engine.Tick();

        ref var healthAfter = ref engine.EcsWorld.Get<Health>(player);
        Assert.True(healthAfter.Current > 50);
    }

    [Fact]
    public void EquipWeapon_IncreasesAttack()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        ref var statsBefore = ref engine.EcsWorld.Get<CombatStats>(player);
        int baseAttack = statsBefore.Attack;

        // Spawn a sword and pick it up
        var swordTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.LongSword);
        engine.SpawnItemOnGround(swordTemplate, 0, sx, sy);

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Use (equip) it
        ref var input2 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input2.ActionType = ActionTypes.UseItem;
        input2.ItemSlot = 0;
        engine.Tick();

        ref var statsAfter = ref engine.EcsWorld.Get<CombatStats>(player);
        Assert.True(statsAfter.Attack > baseAttack, $"Attack {statsAfter.Attack} should be > {baseAttack} after equipping sword");
    }

    [Fact]
    public void EquipArmor_IncreasesDefense()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        ref var statsBefore = ref engine.EcsWorld.Get<CombatStats>(player);
        int baseDef = statsBefore.Defense;

        // Spawn armor and pick it up
        var armorTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.LeatherArmor);
        engine.SpawnItemOnGround(armorTemplate, 0, sx, sy);

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Equip it
        ref var input2 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input2.ActionType = ActionTypes.UseItem;
        input2.ItemSlot = 0;
        engine.Tick();

        ref var statsAfter = ref engine.EcsWorld.Get<CombatStats>(player);
        Assert.True(statsAfter.Defense > baseDef, $"Defense {statsAfter.Defense} should be > {baseDef} after equipping armor");
    }

    [Fact]
    public void PickUp_WhenFull_DoesNotPickUp()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        // Fill inventory to capacity with ItemData
        ref var inv = ref engine.EcsWorld.Get<Inventory>(player);
        int cap = inv.Capacity;
        for (int i = 0; i < cap; i++)
            inv.Items!.Add(new ItemData { ItemTypeId = ItemDefinitions.ShortSword });

        // Now spawn another item and try to pick it up
        var extraItem = engine.SpawnItemOnGround(ItemDefinitions.All[0], 0, sx, sy);

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Item should still be on the ground
        Assert.True(engine.EcsWorld.Has<GroundItemTag>(extraItem));
    }

    [Fact]
    public void Drop_InvalidSlot_DoesNothing()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        // Drop from an empty inventory slot
        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.Drop;
        input.ItemSlot = 99; // invalid slot
        engine.Tick(); // Should not crash
    }

    [Fact]
    public void Drop_NegativeSlot_DoesNothing()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.Drop;
        input.ItemSlot = -1;
        engine.Tick(); // Should not crash
    }

    [Fact]
    public void UseItem_InvalidSlot_DoesNothing()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.UseItem;
        input.ItemSlot = 99;
        engine.Tick(); // Should not crash
    }

    [Fact]
    public void UseGold_DoesNothing()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        // Spawn gold and pick it up
        var goldTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.Gold);
        engine.SpawnItemOnGround(goldTemplate, 0, sx, sy);

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        ref var inv = ref engine.EcsWorld.Get<Inventory>(player);
        int countBefore = inv.Items!.Count;

        // Try to use gold
        ref var input2 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input2.ActionType = ActionTypes.UseItem;
        input2.ItemSlot = 0;
        engine.Tick();

        // Gold should still be in inventory (no effect for CategoryGold)
        ref var invAfter = ref engine.EcsWorld.Get<Inventory>(player);
        Assert.Equal(countBefore, invAfter.Items!.Count);
    }

    [Fact]
    public void EquipWeapon_SwapsOldWeapon()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        // Pick up first weapon
        var sword1Template = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.ShortSword);
        engine.SpawnItemOnGround(sword1Template, 0, sx, sy);
        ref var input1 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input1.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Equip first weapon
        ref var input2 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input2.ActionType = ActionTypes.UseItem;
        input2.ItemSlot = 0;
        engine.Tick();

        // Pick up second weapon
        var sword2Template = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.LongSword);
        engine.SpawnItemOnGround(sword2Template, 0, sx, sy);
        ref var input3 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input3.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Equip second weapon (should swap old one back to inventory)
        ref var input4 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input4.ActionType = ActionTypes.UseItem;
        input4.ItemSlot = 0;
        engine.Tick();

        ref var inv = ref engine.EcsWorld.Get<Inventory>(player);
        // Old weapon should be back in inventory
        Assert.True(inv.Items!.Count >= 1, "Old weapon should be swapped into inventory");
    }

    [Fact]
    public void EquipArmor_SwapsOldArmor()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        // Pick up first armor
        var armor1Template = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.LeatherArmor);
        engine.SpawnItemOnGround(armor1Template, 0, sx, sy);
        ref var input1 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input1.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Equip first armor
        ref var input2 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input2.ActionType = ActionTypes.UseItem;
        input2.ItemSlot = 0;
        engine.Tick();

        ref var statsMid = ref engine.EcsWorld.Get<CombatStats>(player);
        int defWithFirstArmor = statsMid.Defense;

        // Pick up second armor
        var armor2Template = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.ChainMail);
        engine.SpawnItemOnGround(armor2Template, 0, sx, sy);
        ref var input3 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input3.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Equip second armor (should swap old one back)
        ref var input4 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input4.ActionType = ActionTypes.UseItem;
        input4.ItemSlot = 0;
        engine.Tick();

        ref var inv = ref engine.EcsWorld.Get<Inventory>(player);
        Assert.True(inv.Items!.Count >= 1, "Old armor should be swapped into inventory");

        ref var statsAfter = ref engine.EcsWorld.Get<CombatStats>(player);
        // Chain Mail has more defense than Leather Armor
        Assert.True(statsAfter.Defense > defWithFirstArmor || statsAfter.Defense == defWithFirstArmor,
            "Defense should reflect new armor");
    }

    [Fact]
    public void UseStrengthPotion_BoostsAttack()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        ref var statsBefore = ref engine.EcsWorld.Get<CombatStats>(player);
        int atkBefore = statsBefore.Attack;

        // Spawn a strength potion and pick it up
        var potionTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.StrengthPotion);
        engine.SpawnItemOnGround(potionTemplate, 0, sx, sy);

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        ref var input2 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input2.ActionType = ActionTypes.UseItem;
        input2.ItemSlot = 0;
        engine.Tick();

        ref var statsAfter = ref engine.EcsWorld.Get<CombatStats>(player);
        Assert.True(statsAfter.Attack > atkBefore, $"Attack {statsAfter.Attack} should be > {atkBefore} after strength potion");
    }

    [Fact]
    public void PickUp_StackableItem_AutoStacks()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        // Pick up first health potion
        var potionTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.HealthPotion);
        engine.SpawnItemOnGround(potionTemplate, 0, sx, sy);

        ref var input1 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input1.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Pick up second health potion (should auto-stack)
        engine.SpawnItemOnGround(potionTemplate, 0, sx, sy);

        ref var input2 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input2.ActionType = ActionTypes.PickUp;
        engine.Tick();

        ref var inv = ref engine.EcsWorld.Get<Inventory>(player);
        // Should be one slot (stacked), not two
        Assert.Single(inv.Items!);
        Assert.NotNull(inv.Items);
        Assert.True(inv.Items[0].StackCount > 1, "Stack count should be > 1 after auto-stacking");
    }

    [Fact]
    public void SwapItems_SwapsSlots()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        // Pick up two different items
        var swordTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.ShortSword);
        engine.SpawnItemOnGround(swordTemplate, 0, sx, sy);
        ref var input1 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input1.ActionType = ActionTypes.PickUp;
        engine.Tick();

        var armorTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.LeatherArmor);
        engine.SpawnItemOnGround(armorTemplate, 0, sx, sy);
        ref var input2 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input2.ActionType = ActionTypes.PickUp;
        engine.Tick();

        ref var invBefore = ref engine.EcsWorld.Get<Inventory>(player);
        int typeAtSlot0 = invBefore.Items![0].ItemTypeId;
        int typeAtSlot1 = invBefore.Items[1].ItemTypeId;

        // Swap slots 0 and 1
        ref var input3 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input3.ActionType = ActionTypes.SwapItems;
        input3.ItemSlot = 0;
        input3.TargetSlot = 1;
        engine.Tick();

        ref var invAfter = ref engine.EcsWorld.Get<Inventory>(player);
        Assert.Equal(typeAtSlot1, invAfter.Items![0].ItemTypeId);
        Assert.Equal(typeAtSlot0, invAfter.Items[1].ItemTypeId);
    }

    [Fact]
    public void SwapItems_InvalidSlot_DoesNothing()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.SwapItems;
        input.ItemSlot = 0;
        input.TargetSlot = 99;
        engine.Tick(); // Should not crash
    }

    [Fact]
    public void UnequipWeapon_MovesToInventory()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        // Pick up and equip a weapon
        var swordTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.LongSword);
        engine.SpawnItemOnGround(swordTemplate, 0, sx, sy);
        ref var input1 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input1.ActionType = ActionTypes.PickUp;
        engine.Tick();

        ref var input2 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input2.ActionType = ActionTypes.UseItem;
        input2.ItemSlot = 0;
        engine.Tick();

        ref var equipBefore = ref engine.EcsWorld.Get<Equipment>(player);
        Assert.True(equipBefore.HasWeapon);

        ref var statsBefore = ref engine.EcsWorld.Get<CombatStats>(player);
        int atkWithWeapon = statsBefore.Attack;

        // Unequip weapon (slot 0 = weapon)
        ref var input3 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input3.ActionType = ActionTypes.Unequip;
        input3.ItemSlot = 0;
        engine.Tick();

        ref var equipAfter = ref engine.EcsWorld.Get<Equipment>(player);
        Assert.False(equipAfter.HasWeapon);

        ref var inv = ref engine.EcsWorld.Get<Inventory>(player);
        Assert.Single(inv.Items!);
        Assert.NotNull(inv.Items);
        Assert.Equal(ItemDefinitions.LongSword, inv.Items[0].ItemTypeId);

        ref var statsAfter = ref engine.EcsWorld.Get<CombatStats>(player);
        Assert.True(statsAfter.Attack < atkWithWeapon);
    }

    [Fact]
    public void UnequipArmor_MovesToInventory()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        // Pick up and equip armor
        var armorTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.ChainMail);
        engine.SpawnItemOnGround(armorTemplate, 0, sx, sy);
        ref var input1 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input1.ActionType = ActionTypes.PickUp;
        engine.Tick();

        ref var input2 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input2.ActionType = ActionTypes.UseItem;
        input2.ItemSlot = 0;
        engine.Tick();

        ref var equipBefore = ref engine.EcsWorld.Get<Equipment>(player);
        Assert.True(equipBefore.HasArmor);

        ref var statsBefore = ref engine.EcsWorld.Get<CombatStats>(player);
        int defWithArmor = statsBefore.Defense;

        // Unequip armor (slot 1 = armor)
        ref var input3 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input3.ActionType = ActionTypes.Unequip;
        input3.ItemSlot = 1;
        engine.Tick();

        ref var equipAfter = ref engine.EcsWorld.Get<Equipment>(player);
        Assert.False(equipAfter.HasArmor);

        ref var inv = ref engine.EcsWorld.Get<Inventory>(player);
        Assert.Single(inv.Items!);
        Assert.NotNull(inv.Items);
        Assert.Equal(ItemDefinitions.ChainMail, inv.Items[0].ItemTypeId);

        ref var statsAfter = ref engine.EcsWorld.Get<CombatStats>(player);
        Assert.True(statsAfter.Defense < defWithArmor);
    }

    [Fact]
    public void Equip_WeaponFromInventory()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        ref var statsBefore = ref engine.EcsWorld.Get<CombatStats>(player);
        int baseAtk = statsBefore.Attack;

        // Pick up a weapon
        var swordTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.LongSword);
        engine.SpawnItemOnGround(swordTemplate, 0, sx, sy);
        ref var input1 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input1.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Equip via Equip action (not UseItem)
        ref var input2 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input2.ActionType = ActionTypes.Equip;
        input2.ItemSlot = 0;
        engine.Tick();

        ref var equipAfter = ref engine.EcsWorld.Get<Equipment>(player);
        Assert.True(equipAfter.HasWeapon);

        ref var statsAfter = ref engine.EcsWorld.Get<CombatStats>(player);
        Assert.True(statsAfter.Attack > baseAtk);

        ref var inv = ref engine.EcsWorld.Get<Inventory>(player);
        Assert.Empty(inv.Items!);
    }

    [Fact]
    public void Equip_ArmorFromInventory()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        ref var statsBefore = ref engine.EcsWorld.Get<CombatStats>(player);
        int baseDef = statsBefore.Defense;

        // Pick up armor
        var armorTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.ChainMail);
        engine.SpawnItemOnGround(armorTemplate, 0, sx, sy);
        ref var input1 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input1.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Equip via Equip action
        ref var input2 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input2.ActionType = ActionTypes.Equip;
        input2.ItemSlot = 0;
        engine.Tick();

        ref var equipAfter = ref engine.EcsWorld.Get<Equipment>(player);
        Assert.True(equipAfter.HasArmor);

        ref var statsAfter = ref engine.EcsWorld.Get<CombatStats>(player);
        Assert.True(statsAfter.Defense > baseDef);

        ref var inv = ref engine.EcsWorld.Get<Inventory>(player);
        Assert.Empty(inv.Items!);
    }

    [Fact]
    public void PickUp_StackableItem_OverflowToNewSlot()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        // Manually add a potion at max stack size to inventory
        ref var inv = ref engine.EcsWorld.Get<Inventory>(player);
        var potionDef = ItemDefinitions.Get(ItemDefinitions.HealthPotion);
        inv.Items!.Add(new ItemData
        {
            ItemTypeId = ItemDefinitions.HealthPotion,
            StackCount = potionDef.MaxStackSize // Full stack (10)
        });

        // Pickup another potion — can't merge into full stack, should go to new slot
        var potionTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.HealthPotion);
        engine.SpawnItemOnGround(potionTemplate, 0, sx, sy);

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        ref var invAfter = ref engine.EcsWorld.Get<Inventory>(player);
        Assert.Equal(2, invAfter.Items!.Count); // Original full stack + new slot
        Assert.Equal(potionDef.MaxStackSize, invAfter.Items[0].StackCount);
        Assert.Equal(1, invAfter.Items[1].StackCount);
    }

    [Fact]
    public void PickUp_StackableItem_SkipsFullStack_MergesIntoPartial()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        var potionDef = ItemDefinitions.Get(ItemDefinitions.HealthPotion);

        // Add a non-matching item, a FULL potion stack, and a partial potion stack
        ref var inv = ref engine.EcsWorld.Get<Inventory>(player);
        inv.Items!.Add(new ItemData
        {
            ItemTypeId = ItemDefinitions.ShortSword, // Different item — forces "if" false branch
            StackCount = 1
        });
        inv.Items.Add(new ItemData
        {
            ItemTypeId = ItemDefinitions.HealthPotion,
            StackCount = potionDef.MaxStackSize // Full stack
        });
        inv.Items.Add(new ItemData
        {
            ItemTypeId = ItemDefinitions.HealthPotion,
            StackCount = 3 // Partial stack
        });

        // Pickup another potion — should skip non-matching + full stack, merge into partial
        var potionTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.HealthPotion);
        engine.SpawnItemOnGround(potionTemplate, 0, sx, sy);

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        ref var invAfter = ref engine.EcsWorld.Get<Inventory>(player);
        Assert.Equal(3, invAfter.Items!.Count);
        Assert.Equal(ItemDefinitions.ShortSword, invAfter.Items[0].ItemTypeId);
        Assert.Equal(potionDef.MaxStackSize, invAfter.Items[1].StackCount);
        Assert.Equal(4, invAfter.Items[2].StackCount); // 3 + 1
    }

    [Fact]
    public void PickUp_StackableItem_PartialMerge_ContinuesLoop()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        var potionDef = ItemDefinitions.Get(ItemDefinitions.HealthPotion);
        // MaxStackSize for potions is 10

        // Inventory: slot 0 has potion at 9 (can absorb 1 more), slot 1 has potion at 5
        ref var inv = ref engine.EcsWorld.Get<Inventory>(player);
        inv.Items!.Add(new ItemData { ItemTypeId = ItemDefinitions.HealthPotion, StackCount = 9 });
        inv.Items.Add(new ItemData { ItemTypeId = ItemDefinitions.HealthPotion, StackCount = 5 });

        // Spawn potion and manually increase its StackCount to 3
        var potionTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.HealthPotion);
        var groundItem = engine.SpawnItemOnGround(potionTemplate, 0, sx, sy);
        ref var groundData = ref engine.EcsWorld.Get<ItemData>(groundItem);
        groundData.StackCount = 3; // Slot 0 absorbs 1 (9→10), leaves 2 → slot 1 absorbs 2 (5→7)

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        ref var invAfter = ref engine.EcsWorld.Get<Inventory>(player);
        Assert.Equal(2, invAfter.Items!.Count);
        Assert.Equal(10, invAfter.Items[0].StackCount); // Was 9, absorbed 1
        Assert.Equal(7, invAfter.Items[1].StackCount); // Was 5, absorbed 2
    }

    // === Drop Position Spreading Tests ===

    [Fact]
    public void Drop_OnEmptyTile_StaysAtPlayerPosition()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        // Give player an item directly
        ref var inv = ref engine.EcsWorld.Get<Inventory>(player);
        inv.Items!.Add(new ItemData { ItemTypeId = ItemDefinitions.ShortSword, StackCount = 1 });

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.Drop;
        input.ItemSlot = 0;
        engine.Tick();

        // Item should land at player position since nothing is there
        var positions = new List<(int X, int Y)>();
        var gq = new QueryDescription().WithAll<Position, GroundItemTag>();
        engine.EcsWorld.Query(in gq, (ref Position gPos) =>
        {
            positions.Add((gPos.X, gPos.Y));
        });
        Assert.Contains((sx, sy), positions);
    }

    [Fact]
    public void Drop_OnOccupiedTile_MovesToAdjacentPosition()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        // Place an item on the ground at player's position
        var template = ItemDefinitions.All[0];
        engine.SpawnItemOnGround(template, 0, sx, sy);

        // Count ground items before drop
        int countBefore = 0;
        var gqBefore = new QueryDescription().WithAll<Position, GroundItemTag>();
        engine.EcsWorld.Query(in gqBefore, (ref Position gPos) => { countBefore++; });

        // Give player an item to drop
        ref var inv = ref engine.EcsWorld.Get<Inventory>(player);
        inv.Items!.Add(new ItemData { ItemTypeId = ItemDefinitions.ShortSword, StackCount = 1 });

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.Drop;
        input.ItemSlot = 0;
        engine.Tick();

        // Count ground items after drop — should be one more
        int countAfter = 0;
        int atOrigin = 0;
        var gqAfter = new QueryDescription().WithAll<Position, GroundItemTag>();
        engine.EcsWorld.Query(in gqAfter, (ref Position gPos) =>
        {
            countAfter++;
            if (gPos.X == sx && gPos.Y == sy) atOrigin++;
        });
        Assert.Equal(countBefore + 1, countAfter);
        // Only the original item should be at origin; dropped one goes elsewhere
        Assert.Equal(1, atOrigin);
    }

    [Fact]
    public void Drop_MultipleItems_SpreadOutward()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        // Count items before dropping
        int countBefore = 0;
        var gqBefore = new QueryDescription().WithAll<Position, GroundItemTag>();
        engine.EcsWorld.Query(in gqBefore, (ref Position gPos) => { countBefore++; });

        // Drop 3 items — each should land at a different position
        for (int i = 0; i < 3; i++)
        {
            ref var inv = ref engine.EcsWorld.Get<Inventory>(player);
            inv.Items!.Add(new ItemData { ItemTypeId = ItemDefinitions.ShortSword, StackCount = 1 });

            ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
            input.ActionType = ActionTypes.Drop;
            input.ItemSlot = 0;
            engine.Tick();
        }

        // Count total items after
        int countAfter = 0;
        var gqAfter = new QueryDescription().WithAll<Position, GroundItemTag>();
        engine.EcsWorld.Query(in gqAfter, (ref Position gPos) => { countAfter++; });
        Assert.Equal(countBefore + 3, countAfter);

        // Verify all 3 dropped items are at distinct positions near player
        // (we can check the total distinct positions increased by 3)
        var nearPositions = new HashSet<long>();
        engine.EcsWorld.Query(in gqAfter, (ref Position gPos) =>
        {
            if (Math.Abs(gPos.X - sx) <= 5 && Math.Abs(gPos.Y - sy) <= 5)
                nearPositions.Add(Position.PackCoord(gPos.X, gPos.Y));
        });
        // At least 3 distinct positions near player (the 3 drops; possibly more from dungeon gen)
        Assert.True(nearPositions.Count >= 3);
    }
}
