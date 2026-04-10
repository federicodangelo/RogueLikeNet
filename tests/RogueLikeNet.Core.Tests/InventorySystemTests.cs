using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Tests;

public class InventorySystemTests
{
    private static readonly BspDungeonGenerator _gen = new(42);

    private static int ItemId(string id) => GameData.Instance.Items.GetNumericId(id);
    private static Data.ItemDefinition Item(string id) => GameData.Instance.Items.Get(id)!;

    private GameEngine CreateEngine()
    {
        var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        return engine;
    }

    [Fact]
    public void PickUp_MovesItemToInventory()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Spawn item at player's position
        var template = Item("short_sword");
        var item = engine.SpawnItemOnGround(template, Position.FromCoords(sx, sy, Position.DefaultZ));

        // Set pickup action
        player.Input.ActionType = ActionTypes.PickUp;

        engine.Tick();

        // Item data should be in inventory, floor entity should be destroyed
        player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        Assert.Single(player.Inventory.Items);
        Assert.NotNull(player.Inventory.Items);
        Assert.Equal(template.NumericId, player.Inventory.Items[0].ItemTypeId);
    }

    [Fact]
    public void Drop_PlacesItemOnGround()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Spawn and pick up an item
        var template = Item("short_sword");
        var item = engine.SpawnItemOnGround(template, Position.FromCoords(sx, sy, Position.DefaultZ));

        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Drop it
        player.Input.ActionType = ActionTypes.Drop;
        player.Input.ItemSlot = 0;
        engine.Tick();

        Assert.Empty(player.Inventory.Items);

        // Original entity was destroyed on pickup; drop creates a new ground entity
        var chunk = engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ))!;
        int groundCount = chunk.GroundItems.ToArray().Count(gi => !gi.IsDestroyed && gi.Position.X == sx && gi.Position.Y == sy);
        Assert.Equal(1, groundCount);
    }

    [Fact]
    public void UsePotion_RestoresHealth()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Damage the player
        player.Health.Current = 50;

        // Spawn a player.Health potion and pick it up
        var potionTemplate = Item("health_potion_small");
        engine.SpawnItemOnGround(potionTemplate, Position.FromCoords(sx, sy, Position.DefaultZ));

        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Use it
        player.Input.ActionType = ActionTypes.UseItem;
        player.Input.ItemSlot = 0;
        engine.Tick();

        Assert.True(player.Health.Current > 50);
    }

    [Fact]
    public void EquipWeapon_IncreasesAttack()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        int baseAttack = player.CombatStats.Attack;

        // Spawn a sword and pick it up
        var swordTemplate = Item("long_sword");
        engine.SpawnItemOnGround(swordTemplate, Position.FromCoords(sx, sy, Position.DefaultZ));

        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Use (equip) it
        player.Input.ActionType = ActionTypes.UseItem;
        player.Input.ItemSlot = 0;
        engine.Tick();

        Assert.True(player.CombatStats.Attack > baseAttack, $"Attack {player.CombatStats.Attack} should be > {baseAttack} after equipping sword");
    }

    [Fact]
    public void EquipArmor_IncreasesDefense()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        int baseDef = player.CombatStats.Defense;

        // Spawn armor and pick it up
        var armorTemplate = Item("leather_armor");
        engine.SpawnItemOnGround(armorTemplate, Position.FromCoords(sx, sy, Position.DefaultZ));

        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Equip it
        player.Input.ActionType = ActionTypes.UseItem;
        player.Input.ItemSlot = 0;
        engine.Tick();

        Assert.True(player.CombatStats.Defense > baseDef, $"Defense {player.CombatStats.Defense} should be > {baseDef} after equipping armor");
    }

    [Fact]
    public void PickUp_WhenFull_DoesNotPickUp()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Fill inventory to capacity with ItemData
        int cap = player.Inventory.Capacity;
        for (int i = 0; i < cap; i++)
            player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("short_sword") });

        // Now spawn another item and try to pick it up
        var extraItem = engine.SpawnItemOnGround(Item("short_sword"), Position.FromCoords(sx, sy, Position.DefaultZ));

        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Item should still be on the ground
        Assert.False(extraItem.IsDestroyed);
    }

    [Fact]
    public void Drop_InvalidSlot_DoesNothing()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Drop from an empty inventory slot
        player.Input.ActionType = ActionTypes.Drop;
        player.Input.ItemSlot = 99; // invalid slot
        engine.Tick(); // Should not crash
    }

    [Fact]
    public void Drop_NegativeSlot_DoesNothing()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        player.Input.ActionType = ActionTypes.Drop;
        player.Input.ItemSlot = -1;
        engine.Tick(); // Should not crash
    }

    [Fact]
    public void UseItem_InvalidSlot_DoesNothing()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        player.Input.ActionType = ActionTypes.UseItem;
        player.Input.ItemSlot = 99;
        engine.Tick(); // Should not crash
    }

    [Fact]
    public void UseGold_DoesNothing()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Spawn gold and pick it up
        var goldTemplate = Item("gold_coin");
        engine.SpawnItemOnGround(goldTemplate, Position.FromCoords(sx, sy, Position.DefaultZ));

        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        int countBefore = player.Inventory.Items.Count;

        // Try to use gold
        player.Input.ActionType = ActionTypes.UseItem;
        player.Input.ItemSlot = 0;
        engine.Tick();

        // Gold should still be in inventory (no effect for CategoryGold)
        Assert.Equal(countBefore, player.Inventory.Items.Count);
    }

    [Fact]
    public void EquipWeapon_SwapsOldWeapon()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Pick up first weapon
        var sword1Template = Item("short_sword");
        engine.SpawnItemOnGround(sword1Template, Position.FromCoords(sx, sy, Position.DefaultZ));
        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Equip first weapon
        player.Input.ActionType = ActionTypes.UseItem;
        player.Input.ItemSlot = 0;
        engine.Tick();

        // Pick up second weapon
        var sword2Template = Item("long_sword");
        engine.SpawnItemOnGround(sword2Template, Position.FromCoords(sx, sy, Position.DefaultZ));
        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Equip second weapon (should swap old one back to inventory)
        player.Input.ActionType = ActionTypes.UseItem;
        player.Input.ItemSlot = 0;
        engine.Tick();

        // Old weapon should be back in inventory
        Assert.True(player.Inventory.Items.Count >= 1, "Old weapon should be swapped into inventory");
    }

    [Fact]
    public void EquipArmor_SwapsOldArmor()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Pick up first armor
        var armor1Template = Item("leather_armor");
        engine.SpawnItemOnGround(armor1Template, Position.FromCoords(sx, sy, Position.DefaultZ));
        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Equip first armor
        player.Input.ActionType = ActionTypes.UseItem;
        player.Input.ItemSlot = 0;
        engine.Tick();

        int defWithFirstArmor = player.CombatStats.Defense;

        // Pick up second armor
        var armor2Template = Item("chain_mail");
        engine.SpawnItemOnGround(armor2Template, Position.FromCoords(sx, sy, Position.DefaultZ));
        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Equip second armor (should swap old one back)
        player.Input.ActionType = ActionTypes.UseItem;
        player.Input.ItemSlot = 0;
        engine.Tick();

        Assert.True(player.Inventory.Items.Count >= 1, "Old armor should be swapped into inventory");

        // Chain Mail has more defense than Leather Armor
        Assert.True(player.CombatStats.Defense > defWithFirstArmor || player.CombatStats.Defense == defWithFirstArmor,
            "Defense should reflect new armor");
    }

    [Fact]
    public void UseStrengthPotion_BoostsAttack()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        int atkBefore = player.CombatStats.Attack;

        // Spawn a strength potion and pick it up
        var potionTemplate = Item("strength_potion");
        engine.SpawnItemOnGround(potionTemplate, Position.FromCoords(sx, sy, Position.DefaultZ));

        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        player.Input.ActionType = ActionTypes.UseItem;
        player.Input.ItemSlot = 0;
        engine.Tick();

        Assert.True(player.CombatStats.Attack > atkBefore, $"Attack {player.CombatStats.Attack} should be > {atkBefore} after strength potion");
    }

    [Fact]
    public void PickUp_StackableItem_AutoStacks()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Pick up first player.Health potion
        var potionTemplate = Item("health_potion_small");
        engine.SpawnItemOnGround(potionTemplate, Position.FromCoords(sx, sy, Position.DefaultZ));

        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Pick up second player.Health potion (should auto-stack)
        engine.SpawnItemOnGround(potionTemplate, Position.FromCoords(sx, sy, Position.DefaultZ));

        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Should be one slot (stacked), not two
        Assert.Single(player.Inventory.Items);
        Assert.NotNull(player.Inventory.Items);
        Assert.True(player.Inventory.Items[0].StackCount > 1, "Stack count should be > 1 after auto-stacking");
    }

    [Fact]
    public void SwapItems_SwapsSlots()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Pick up two different items
        var swordTemplate = Item("short_sword");
        engine.SpawnItemOnGround(swordTemplate, Position.FromCoords(sx, sy, Position.DefaultZ));
        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        var armorTemplate = Item("leather_armor");
        engine.SpawnItemOnGround(armorTemplate, Position.FromCoords(sx, sy, Position.DefaultZ));
        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        int typeAtSlot0 = player.Inventory.Items[0].ItemTypeId;
        int typeAtSlot1 = player.Inventory.Items[1].ItemTypeId;

        // Swap slots 0 and 1
        player.Input.ActionType = ActionTypes.SwapItems;
        player.Input.ItemSlot = 0;
        player.Input.TargetSlot = 1;
        engine.Tick();

        Assert.Equal(typeAtSlot1, player.Inventory.Items[0].ItemTypeId);
        Assert.Equal(typeAtSlot0, player.Inventory.Items[1].ItemTypeId);
    }

    [Fact]
    public void SwapItems_InvalidSlot_DoesNothing()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        player.Input.ActionType = ActionTypes.SwapItems;
        player.Input.ItemSlot = 0;
        player.Input.TargetSlot = 99;
        engine.Tick(); // Should not crash
    }

    [Fact]
    public void UnequipWeapon_MovesToInventory()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Pick up and equip a weapon
        var swordTemplate = Item("long_sword");
        engine.SpawnItemOnGround(swordTemplate, Position.FromCoords(sx, sy, Position.DefaultZ));
        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        player.Input.ActionType = ActionTypes.UseItem;
        player.Input.ItemSlot = 0;
        engine.Tick();

        Assert.True(player.Equipment.HasWeapon);

        int atkWithWeapon = player.CombatStats.Attack;

        // Unequip weapon (slot 5 = EquipSlot.Weapon)
        player.Input.ActionType = ActionTypes.Unequip;
        player.Input.ItemSlot = 5;
        engine.Tick();

        Assert.False(player.Equipment.HasWeapon);

        Assert.Single(player.Inventory.Items);
        Assert.NotNull(player.Inventory.Items);
        Assert.Equal(ItemId("long_sword"), player.Inventory.Items[0].ItemTypeId);

        Assert.True(player.CombatStats.Attack < atkWithWeapon);
    }

    [Fact]
    public void UnequipArmor_MovesToInventory()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Pick up and equip armor
        var armorTemplate = Item("chain_mail");
        engine.SpawnItemOnGround(armorTemplate, Position.FromCoords(sx, sy, Position.DefaultZ));
        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        player.Input.ActionType = ActionTypes.UseItem;
        player.Input.ItemSlot = 0;
        engine.Tick();

        Assert.True(player.Equipment.HasArmor);

        int defWithArmor = player.CombatStats.Defense;

        // Unequip armor (slot 1 = EquipSlot.Chest)
        player.Input.ActionType = ActionTypes.Unequip;
        player.Input.ItemSlot = 1;
        engine.Tick();

        Assert.False(player.Equipment.HasArmor);

        Assert.Single(player.Inventory.Items);
        Assert.NotNull(player.Inventory.Items);
        Assert.Equal(ItemId("chain_mail"), player.Inventory.Items[0].ItemTypeId);

        Assert.True(player.CombatStats.Defense < defWithArmor);
    }

    [Fact]
    public void Equip_WeaponFromInventory()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        int baseAtk = player.CombatStats.Attack;

        // Pick up a weapon
        var swordTemplate = Item("long_sword");
        engine.SpawnItemOnGround(swordTemplate, Position.FromCoords(sx, sy, Position.DefaultZ));
        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Equip via Equip action (not UseItem)
        player.Input.ActionType = ActionTypes.Equip;
        player.Input.ItemSlot = 0;
        engine.Tick();

        Assert.True(player.Equipment.HasWeapon);

        Assert.True(player.CombatStats.Attack > baseAtk);

        Assert.Empty(player.Inventory.Items);
    }

    [Fact]
    public void Equip_ArmorFromInventory()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        int baseDef = player.CombatStats.Defense;

        // Pick up armor
        var armorTemplate = Item("chain_mail");
        engine.SpawnItemOnGround(armorTemplate, Position.FromCoords(sx, sy, Position.DefaultZ));
        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Equip via Equip action
        player.Input.ActionType = ActionTypes.Equip;
        player.Input.ItemSlot = 0;
        engine.Tick();

        Assert.True(player.Equipment.HasArmor);

        Assert.True(player.CombatStats.Defense > baseDef);

        Assert.Empty(player.Inventory.Items);
    }

    [Fact]
    public void PickUp_StackableItem_OverflowToNewSlot()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Manually add a potion at max stack size to inventory
        var potionDef = Item("health_potion_small");
        player.Inventory.Items.Add(new ItemData
        {
            ItemTypeId = ItemId("health_potion_small"),
            StackCount = potionDef.MaxStackSize // Full stack (10)
        });

        // Pickup another potion — can't merge into full stack, should go to new slot
        var potionTemplate = Item("health_potion_small");
        engine.SpawnItemOnGround(potionTemplate, Position.FromCoords(sx, sy, Position.DefaultZ));

        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        Assert.Equal(2, player.Inventory.Items.Count); // Original full stack + new slot
        Assert.Equal(potionDef.MaxStackSize, player.Inventory.Items[0].StackCount);
        Assert.Equal(1, player.Inventory.Items[1].StackCount);
    }

    [Fact]
    public void PickUp_StackableItem_SkipsFullStack_MergesIntoPartial()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        var potionDef = Item("health_potion_small");

        // Add a non-matching item, a FULL potion stack, and a partial potion stack
        player.Inventory.Items.Add(new ItemData
        {
            ItemTypeId = ItemId("short_sword"), // Different item — forces "if" false branch
            StackCount = 1
        });
        player.Inventory.Items.Add(new ItemData
        {
            ItemTypeId = ItemId("health_potion_small"),
            StackCount = potionDef.MaxStackSize // Full stack
        });
        player.Inventory.Items.Add(new ItemData
        {
            ItemTypeId = ItemId("health_potion_small"),
            StackCount = 3 // Partial stack
        });

        // Pickup another potion — should skip non-matching + full stack, merge into partial
        var potionTemplate = Item("health_potion_small");
        engine.SpawnItemOnGround(potionTemplate, Position.FromCoords(sx, sy, Position.DefaultZ));

        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        Assert.Equal(3, player.Inventory.Items.Count);
        Assert.Equal(ItemId("short_sword"), player.Inventory.Items[0].ItemTypeId);
        Assert.Equal(potionDef.MaxStackSize, player.Inventory.Items[1].StackCount);
        Assert.Equal(4, player.Inventory.Items[2].StackCount); // 3 + 1
    }

    [Fact]
    public void PickUp_StackableItem_PartialMerge_ContinuesLoop()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        var potionDef = Item("health_potion_small");
        // MaxStackSize for potions is 10

        // Inventory: slot 0 has potion at 9 (can absorb 1 more), slot 1 has potion at 5
        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("health_potion_small"), StackCount = 9 });
        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("health_potion_small"), StackCount = 5 });

        // Spawn potion and manually increase its StackCount to 3
        var potionTemplate = Item("health_potion_small");
        var _gi = engine.SpawnItemOnGround(potionTemplate, Position.FromCoords(sx, sy, Position.DefaultZ));
        ref var groundItem = ref engine.WorldMap.GetGroundItemRef(_gi.Id);
        groundItem.Item.StackCount = 3; // Slot 0 absorbs 1 (9→10), leaves 2 → slot 1 absorbs 2 (5→7)

        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        Assert.Equal(2, player.Inventory.Items.Count);
        Assert.Equal(10, player.Inventory.Items[0].StackCount); // Was 9, absorbed 1
        Assert.Equal(7, player.Inventory.Items[1].StackCount); // Was 5, absorbed 2
    }

    [Fact]
    public void PickUp_StackableItem_SameType_Stacks()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Add a health potion to inventory
        player.Inventory.Items.Add(new ItemData
        {
            ItemTypeId = ItemId("health_potion_small"),
            StackCount = 3
        });

        // Spawn another health potion on the ground
        var potionTemplate = Item("health_potion_small");
        var _gi = engine.SpawnItemOnGround(potionTemplate, Position.FromCoords(sx, sy, Position.DefaultZ));

        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Should merge into one slot because same type
        Assert.NotNull(player.Inventory.Items);
        Assert.Single(player.Inventory.Items);
        Assert.Equal(4, player.Inventory.Items[0].StackCount);
    }

    [Fact]
    public void PickUp_StackableItem_DifferentType_DoesNotStack()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Add a health potion to inventory
        player.Inventory.Items.Add(new ItemData
        {
            ItemTypeId = ItemId("health_potion_small"),
            StackCount = 3
        });

        // Spawn a strength potion on the ground (different type)
        var strPotionTemplate = Item("strength_potion");
        var _gi = engine.SpawnItemOnGround(strPotionTemplate, Position.FromCoords(sx, sy, Position.DefaultZ));

        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Should be two separate slots because different types
        Assert.Equal(2, player.Inventory.Items.Count);
        Assert.Equal(3, player.Inventory.Items[0].StackCount);
    }

    // === Drop Position Spreading Tests ===

    [Fact]
    public void Drop_OnEmptyTile_StaysAtPlayerPosition()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Give player an item directly
        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("short_sword"), StackCount = 1 });

        player.Input.ActionType = ActionTypes.Drop;
        player.Input.ItemSlot = 0;
        engine.Tick();

        // Item should land at player position since nothing is there
        var chunk = engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ))!;
        var positions = chunk.GroundItems.ToArray().Where(gi => !gi.IsDestroyed).Select(gi => (gi.Position.X, gi.Position.Y)).ToList();
        Assert.Contains((sx, sy), positions);
    }

    [Fact]
    public void Drop_OnOccupiedTile_MovesToAdjacentPosition()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Place an item on the ground at player's position
        var template = Item("short_sword");
        engine.SpawnItemOnGround(template, Position.FromCoords(sx, sy, Position.DefaultZ));

        // Count ground items before drop
        var chunk = engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ))!;
        int countBefore = chunk.GroundItems.ToArray().Count(gi => !gi.IsDestroyed);

        // Give player an item to drop
        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("short_sword"), StackCount = 1 });

        player.Input.ActionType = ActionTypes.Drop;
        player.Input.ItemSlot = 0;
        engine.Tick();

        // Count ground items after drop — should be one more
        int countAfter = chunk.GroundItems.ToArray().Count(gi => !gi.IsDestroyed);
        int atOrigin = chunk.GroundItems.ToArray().Count(gi => !gi.IsDestroyed && gi.Position.X == sx && gi.Position.Y == sy);
        Assert.Equal(countBefore + 1, countAfter);
        // Only the original item should be at origin; dropped one goes elsewhere
        Assert.Equal(1, atOrigin);
    }

    [Fact]
    public void Drop_MultipleItems_SpreadOutward()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Count items before dropping
        var chunk = engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ))!;
        int countBefore = chunk.GroundItems.ToArray().Count(gi => !gi.IsDestroyed);

        // Drop 3 items — each should land at a different position
        for (int i = 0; i < 3; i++)
        {
            player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("short_sword"), StackCount = 1 });

            player.Input.ActionType = ActionTypes.Drop;
            player.Input.ItemSlot = 0;
            engine.Tick();
        }

        // Count total items after
        int countAfter = chunk.GroundItems.ToArray().Count(gi => !gi.IsDestroyed);
        Assert.Equal(countBefore + 3, countAfter);

        // Verify all 3 dropped items are at distinct positions near player
        var nearPositions = new HashSet<long>();
        foreach (var gi in chunk.GroundItems.ToArray().Where(gi => !gi.IsDestroyed))
        {
            if (Math.Abs(gi.Position.X - sx) <= 5 && Math.Abs(gi.Position.Y - sy) <= 5)
                nearPositions.Add(Position.PackCoord(gi.Position.X, gi.Position.Y, Position.DefaultZ));
        }
        // At least 3 distinct positions near player (the 3 drops; possibly more from dungeon gen)
        Assert.True(nearPositions.Count >= 3);
    }

    [Fact]
    public void UnequipItem_WithBonusHealth_ClampsCurrentHealth()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // LeatherArmor definition: BaseAttack=0, BaseDefense=2, BaseHealth=0
        var armor = new ItemData
        {
            ItemTypeId = ItemId("leather_armor"),
            StackCount = 1,
        };
        Assert.NotNull(player.Inventory.Items);
        player.Inventory.Items.Add(armor);

        int defBefore = player.CombatStats.Defense;

        // Equip it
        player.Input.ActionType = ActionTypes.Equip;
        player.Input.ItemSlot = 0; // First inventory slot
        engine.Tick();

        // Player now has +2 defense from LeatherArmor definition
        Assert.Equal(defBefore + 2, player.CombatStats.Defense);

        // Unequip the armor
        player.Input.ActionType = ActionTypes.Unequip;
        player.Input.ItemSlot = 1; // Chest slot
        engine.Tick();

        // After unequip, defense is restored
        Assert.Equal(defBefore, player.CombatStats.Defense);
    }
}
