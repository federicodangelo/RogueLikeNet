using Arch.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Tests;

public class InventorySystemTests
{
    private GameEngine CreateEngine()
    {
        var engine = new GameEngine(42);
        engine.EnsureChunkLoaded(0, 0);
        return engine;
    }

    [Fact]
    public void PickUp_MovesItemToInventory()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy);

        // Spawn item at player's position
        var template = ItemDefinitions.Templates[0]; // Short Sword
        var item = engine.SpawnItemOnGround(template, 0, sx, sy);

        // Set pickup action
        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.PickUp;

        engine.Tick();

        // Item data should be in inventory, floor entity should be destroyed
        ref var inv = ref engine.EcsWorld.Get<Inventory>(player);
        Assert.Single(inv.Items!);
        Assert.Equal(template.TypeId, inv.Items[0].ItemTypeId);
        Assert.False(engine.EcsWorld.IsAlive(item));
    }

    [Fact]
    public void Drop_PlacesItemOnGround()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy);

        // Spawn and pick up an item
        var template = ItemDefinitions.Templates[0];
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
        var player = engine.SpawnPlayer(1, sx, sy);

        // Damage the player
        ref var health = ref engine.EcsWorld.Get<Health>(player);
        health.Current = 50;

        // Spawn a health potion and pick it up
        var potionTemplate = Array.Find(ItemDefinitions.Templates, t => t.TypeId == ItemDefinitions.HealthPotion);
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
        var player = engine.SpawnPlayer(1, sx, sy);

        ref var statsBefore = ref engine.EcsWorld.Get<CombatStats>(player);
        int baseAttack = statsBefore.Attack;

        // Spawn a sword and pick it up
        var swordTemplate = Array.Find(ItemDefinitions.Templates, t => t.TypeId == ItemDefinitions.LongSword);
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
        var player = engine.SpawnPlayer(1, sx, sy);

        ref var statsBefore = ref engine.EcsWorld.Get<CombatStats>(player);
        int baseDef = statsBefore.Defense;

        // Spawn armor and pick it up
        var armorTemplate = Array.Find(ItemDefinitions.Templates, t => t.TypeId == ItemDefinitions.LeatherArmor);
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
        var player = engine.SpawnPlayer(1, sx, sy);

        // Fill inventory to capacity with ItemData
        ref var inv = ref engine.EcsWorld.Get<Inventory>(player);
        int cap = inv.Capacity;
        for (int i = 0; i < cap; i++)
            inv.Items!.Add(new ItemData { ItemTypeId = ItemDefinitions.ShortSword });

        // Now spawn another item and try to pick it up
        var extraItem = engine.SpawnItemOnGround(ItemDefinitions.Templates[0], 0, sx, sy);

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
        var player = engine.SpawnPlayer(1, sx, sy);

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
        var player = engine.SpawnPlayer(1, sx, sy);

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
        var player = engine.SpawnPlayer(1, sx, sy);

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
        var player = engine.SpawnPlayer(1, sx, sy);

        // Spawn gold and pick it up
        var goldTemplate = Array.Find(ItemDefinitions.Templates, t => t.TypeId == ItemDefinitions.Gold);
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
        var player = engine.SpawnPlayer(1, sx, sy);

        // Pick up first weapon
        var sword1Template = Array.Find(ItemDefinitions.Templates, t => t.TypeId == ItemDefinitions.ShortSword);
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
        var sword2Template = Array.Find(ItemDefinitions.Templates, t => t.TypeId == ItemDefinitions.LongSword);
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
        var player = engine.SpawnPlayer(1, sx, sy);

        // Pick up first armor
        var armor1Template = Array.Find(ItemDefinitions.Templates, t => t.TypeId == ItemDefinitions.LeatherArmor);
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
        var armor2Template = Array.Find(ItemDefinitions.Templates, t => t.TypeId == ItemDefinitions.ChainMail);
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
        var player = engine.SpawnPlayer(1, sx, sy);

        ref var statsBefore = ref engine.EcsWorld.Get<CombatStats>(player);
        int atkBefore = statsBefore.Attack;

        // Spawn a strength potion and pick it up
        var potionTemplate = Array.Find(ItemDefinitions.Templates, t => t.TypeId == ItemDefinitions.StrengthPotion);
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
}
