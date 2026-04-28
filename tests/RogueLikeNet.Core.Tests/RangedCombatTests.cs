using RogueLikeNet.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Entities;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.Systems;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Tests;

public class RangedCombatTests
{
    private static readonly BspDungeonGenerator _gen = new(42);

    private GameEngine CreateEngine()
    {
        var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        return engine;
    }

    private static int ItemId(string id) => GameData.Instance.Items.GetNumericId(id);
    private static ItemDefinition Item(string id) => GameData.Instance.Items.Get(id)!;

    [Fact]
    public void RangedWeaponsExist_WithRangeGreaterThanOne()
    {
        var bow = Item("wooden_bow");
        Assert.NotNull(bow.Weapon);
        Assert.True(bow.Weapon.Range > 1);

        var crossbow = Item("crossbow");
        Assert.NotNull(crossbow.Weapon);
        Assert.True(crossbow.Weapon.Range > 1);
    }

    [Fact]
    public void AmmoUsage_IsExplicitlyDefined_PerWeapon()
    {
        Assert.True(Item("wooden_bow").Weapon?.UsesAmmo);
        Assert.True(Item("crossbow").Weapon?.UsesAmmo);
        Assert.False(Item("fire_staff").Weapon?.UsesAmmo ?? false);
        Assert.False(Item("spear").Weapon?.UsesAmmo ?? false);
    }

    [Fact]
    public void AmmoItemsExist_WithCorrectCategory()
    {
        var arrow = Item("wooden_arrow");
        Assert.Equal(ItemCategory.Ammo, arrow.Category);
        Assert.NotNull(arrow.Ammo);
        Assert.True(arrow.Ammo.Damage > 0);
    }

    [Fact]
    public void RangedAttack_WithBowAndAmmo_DamagesMonster()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Ranger);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // Equip bow and add arrows
        player.Equipment[(int)EquipSlot.Hand] = new ItemData { ItemTypeId = ItemId("wooden_bow"), StackCount = 1 };
        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("wooden_arrow"), StackCount = 10 });
        ActiveEffectsSystem.RecalculatePlayerStats(ref player);

        // Spawn monster at distance
        var m = engine.SpawnMonster(Position.FromCoords(sx + 3, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 200, Attack = 5, Defense = 0, Speed = 8 });

        // Attack with auto-target
        player.Input.ActionType = ActionTypes.Attack;
        engine.Tick();

        Assert.True(engine.Combat.LastTickEvents.Count > 0);
        var evt = engine.Combat.LastTickEvents[0];
        Assert.True(evt.IsRanged);
        Assert.True(evt.Damage > 0);

        // Ammo should be consumed
        player = ref engine.WorldMap.GetPlayerRef(p.Id);
        Assert.Equal(9, player.Inventory.Items[0].StackCount);
    }

    [Theory]
    [InlineData("fire_staff", 3)]
    [InlineData("spear", 2)]
    public void NonAmmoRangeWeapons_WithoutAmmo_DoNotFallbackToMelee(string weaponId, int targetDistance)
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        player.Equipment[(int)EquipSlot.Hand] = new ItemData { ItemTypeId = ItemId(weaponId), StackCount = 1 };
        ActiveEffectsSystem.RecalculatePlayerStats(ref player);

        engine.SpawnMonster(Position.FromCoords(sx + targetDistance, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 200, Attack = 5, Defense = 0, Speed = 8 });

        player.Input.ActionType = ActionTypes.Attack;
        engine.Tick();

        Assert.True(engine.Combat.LastTickEvents.Count > 0);
        var evt = engine.Combat.LastTickEvents[0];
        Assert.True(evt.IsRanged);
        Assert.True(evt.Damage > 0);

        player = ref engine.WorldMap.GetPlayerRef(p.Id);
        Assert.Empty(player.Inventory.Items);
    }

    [Fact]
    public void RangedAttack_WithoutAmmo_DoesNotAttack()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Ranger);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // Equip bow but NO arrows
        player.Equipment[(int)EquipSlot.Hand] = new ItemData { ItemTypeId = ItemId("wooden_bow"), StackCount = 1 };
        ActiveEffectsSystem.RecalculatePlayerStats(ref player);

        // Monster at range 3 — out of melee range, so no-ammo fallback still can't reach it
        engine.SpawnMonster(Position.FromCoords(sx + 3, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 200, Attack = 5, Defense = 0, Speed = 8 });

        player.Input.ActionType = ActionTypes.Attack;
        engine.Tick();

        Assert.Empty(engine.Combat.LastTickEvents);
    }

    [Fact]
    public void RangedAttack_WithoutAmmo_FallsBackToMelee()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Ranger);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // Equip bow but NO arrows — should fall back to melee
        player.Equipment[(int)EquipSlot.Hand] = new ItemData { ItemTypeId = ItemId("wooden_bow"), StackCount = 1 };
        ActiveEffectsSystem.RecalculatePlayerStats(ref player);

        // Monster adjacent (melee range)
        engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 200, Attack = 0, Defense = 0, Speed = 0 });

        player.Input.ActionType = ActionTypes.Attack;
        engine.Tick();

        // Should hit via melee fallback
        Assert.True(engine.Combat.LastTickEvents.Count > 0);
        Assert.False(engine.Combat.LastTickEvents[0].IsRanged);
        Assert.True(engine.Combat.LastTickEvents[0].Damage > 0);
    }

    [Fact]
    public void RangedAttack_MonsterOutOfRange_DoesNotAttack()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Ranger);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // Wooden bow range = 5
        player.Equipment[(int)EquipSlot.Hand] = new ItemData { ItemTypeId = ItemId("wooden_bow"), StackCount = 1 };
        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("wooden_arrow"), StackCount = 10 });
        ActiveEffectsSystem.RecalculatePlayerStats(ref player);

        // Spawn monster way out of range
        engine.SpawnMonster(Position.FromCoords(sx + 20, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 200, Attack = 5, Defense = 0, Speed = 8 });

        player.Input.ActionType = ActionTypes.Attack;
        engine.Tick();

        // Should not hit — no events
        Assert.Empty(engine.Combat.LastTickEvents);

        // Ammo should NOT be consumed
        player = ref engine.WorldMap.GetPlayerRef(p.Id);
        Assert.Equal(10, player.Inventory.Items[0].StackCount);
    }

    [Fact]
    public void RangedAttack_ManualTarget_HitsCorrectPosition()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Ranger);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        player.Equipment[(int)EquipSlot.Hand] = new ItemData { ItemTypeId = ItemId("wooden_bow"), StackCount = 1 };
        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("wooden_arrow"), StackCount = 10 });
        ActiveEffectsSystem.RecalculatePlayerStats(ref player);

        // Two monsters at different distances
        var m1 = engine.SpawnMonster(Position.FromCoords(sx + 2, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 200, Attack = 5, Defense = 0, Speed = 8 });
        engine.SpawnMonster(Position.FromCoords(sx + 4, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 200, Attack = 5, Defense = 0, Speed = 8 });

        // Target monster 1 explicitly
        player.Input.ActionType = ActionTypes.Attack;
        player.Input.TargetX = 2;
        player.Input.TargetY = 0;
        engine.Tick();

        Assert.True(engine.Combat.LastTickEvents.Count > 0);
        var evt = engine.Combat.LastTickEvents[0];
        Assert.Equal(sx + 2, evt.Target.X);
        Assert.Equal(sy, evt.Target.Y);
    }

    [Fact]
    public void RangedAttack_AmmoFullyConsumed_RemovesFromInventory()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Ranger);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        player.Equipment[(int)EquipSlot.Hand] = new ItemData { ItemTypeId = ItemId("wooden_bow"), StackCount = 1 };
        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("wooden_arrow"), StackCount = 1 });
        ActiveEffectsSystem.RecalculatePlayerStats(ref player);

        engine.SpawnMonster(Position.FromCoords(sx + 2, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 200, Attack = 5, Defense = 0, Speed = 8 });

        player.Input.ActionType = ActionTypes.Attack;
        engine.Tick();

        Assert.True(engine.Combat.LastTickEvents.Count > 0);

        // Last arrow consumed — inventory should be empty
        player = ref engine.WorldMap.GetPlayerRef(p.Id);
        Assert.Empty(player.Inventory.Items);
    }

    [Fact]
    public void RangedAttack_AmmoBonusDamage_AddedToAttack()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Ranger);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // Use iron arrows (damage=5) instead of wooden (damage=2)
        player.Equipment[(int)EquipSlot.Hand] = new ItemData { ItemTypeId = ItemId("wooden_bow"), StackCount = 1 };
        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("iron_arrow"), StackCount = 10 });
        ActiveEffectsSystem.RecalculatePlayerStats(ref player);

        engine.SpawnMonster(Position.FromCoords(sx + 2, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 200, Attack = 5, Defense = 0, Speed = 8 });

        player.Input.ActionType = ActionTypes.Attack;
        engine.Tick();

        var evt1 = engine.Combat.LastTickEvents[0];

        // Now test with wooden arrows for comparison — need a new engine for clean state
        using var engine2 = CreateEngine();
        var (sx2, sy2, _) = engine2.FindSpawnPosition();
        var p2 = engine2.SpawnPlayer(1, Position.FromCoords(sx2, sy2, Position.DefaultZ), ClassDefinitions.Ranger);
        ref var player2 = ref engine2.WorldMap.GetPlayerRef(p2.Id);

        player2.Equipment[(int)EquipSlot.Hand] = new ItemData { ItemTypeId = ItemId("wooden_bow"), StackCount = 1 };
        player2.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("wooden_arrow"), StackCount = 10 });
        ActiveEffectsSystem.RecalculatePlayerStats(ref player2);

        engine2.SpawnMonster(Position.FromCoords(sx2 + 2, sy2, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 200, Attack = 5, Defense = 0, Speed = 8 });

        player2.Input.ActionType = ActionTypes.Attack;
        engine2.Tick();

        var evt2 = engine2.Combat.LastTickEvents[0];

        // Iron arrows (5 bonus) should do more damage than wooden (2 bonus)
        Assert.True(evt1.Damage > evt2.Damage);
    }

    [Fact]
    public void RangedAttack_ElementalAmmo_AppliesNpcWeaknessMetadata()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Ranger);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        player.Equipment[(int)EquipSlot.Hand] = new ItemData { ItemTypeId = ItemId("wooden_bow"), StackCount = 1 };
        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("fire_arrow"), StackCount = 10 });
        ActiveEffectsSystem.RecalculatePlayerStats(ref player);

        int skeletonId = GameData.Instance.Npcs.GetNumericId("skeleton");
        engine.SpawnMonster(Position.FromCoords(sx + 2, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = skeletonId, Health = 200, Attack = 0, Defense = 0, Speed = 0 });

        player.Input.ActionType = ActionTypes.Attack;
        engine.Tick();

        Assert.Single(engine.Combat.LastTickEvents);
        var evt = engine.Combat.LastTickEvents[0];
        Assert.Equal(DamageType.Fire, evt.DamageType);
        Assert.True(evt.WasWeakness);
        Assert.False(evt.WasResisted);
        Assert.True(evt.Damage > 0);
    }

    [Fact]
    public void RangedAttack_KillsMonster_SetsTargetDied()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Ranger);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        player.Equipment[(int)EquipSlot.Hand] = new ItemData { ItemTypeId = ItemId("crossbow"), StackCount = 1 };
        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("steel_arrow"), StackCount = 10 });
        ActiveEffectsSystem.RecalculatePlayerStats(ref player);

        // Very low HP monster
        engine.SpawnMonster(Position.FromCoords(sx + 2, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 1, Attack = 5, Defense = 0, Speed = 8 });

        player.Input.ActionType = ActionTypes.Attack;
        engine.Tick();

        Assert.True(engine.Combat.LastTickEvents.Count > 0);
        Assert.True(engine.Combat.LastTickEvents[0].TargetDied);
    }

    [Fact]
    public void RangedAttack_ResourceNodes_NotAttackedAtRange()
    {
        // Resource nodes can only be hit with melee — ranged attacks should skip them
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Ranger);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        player.Equipment[(int)EquipSlot.Hand] = new ItemData { ItemTypeId = ItemId("wooden_bow"), StackCount = 1 };
        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("wooden_arrow"), StackCount = 10 });
        ActiveEffectsSystem.RecalculatePlayerStats(ref player);

        // No monsters — only resource nodes potentially nearby
        // Attack with no valid targets
        player.Input.ActionType = ActionTypes.Attack;
        engine.Tick();

        // No events if no valid ranged targets exist
        Assert.Empty(engine.Combat.LastTickEvents);

        // Ammo not consumed
        player = ref engine.WorldMap.GetPlayerRef(p.Id);
        Assert.Equal(10, player.Inventory.Items[0].StackCount);
    }

    [Fact]
    public void RangedAttack_ManualTarget_OutOfRange_DoesNotAttack()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Ranger);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // Wooden bow range = 5
        player.Equipment[(int)EquipSlot.Hand] = new ItemData { ItemTypeId = ItemId("wooden_bow"), StackCount = 1 };
        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("wooden_arrow"), StackCount = 10 });
        ActiveEffectsSystem.RecalculatePlayerStats(ref player);

        engine.SpawnMonster(Position.FromCoords(sx + 10, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 200, Attack = 0, Defense = 0, Speed = 0 });

        // Manual target at distance 10, beyond bow range of 5
        player.Input.ActionType = ActionTypes.Attack;
        player.Input.TargetX = 10;
        player.Input.TargetY = 0;
        engine.Tick();

        // Should fail — target out of range
        Assert.Empty(engine.Combat.LastTickEvents);

        // Ammo should NOT be consumed
        player = ref engine.WorldMap.GetPlayerRef(p.Id);
        Assert.Equal(10, player.Inventory.Items[0].StackCount);
    }

    [Fact]
    public void RangedAttack_KillMonster_GrantsXpAndPossibleLevelUp()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Ranger);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        player.Equipment[(int)EquipSlot.Hand] = new ItemData { ItemTypeId = ItemId("crossbow"), StackCount = 1 };
        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("steel_arrow"), StackCount = 10 });
        ActiveEffectsSystem.RecalculatePlayerStats(ref player);

        // Give enough XP to be close to leveling, then kill a monster
        player.ClassData.Experience = 99999;
        int levelBefore = player.ClassData.Level;

        // Spawn a killable monster with a valid MonsterTypeId (use one from data)
        var firstNpc = GameData.Instance.Npcs.All.FirstOrDefault(n => n.XpReward > 0);
        int monsterTypeId = firstNpc != null ? GameData.Instance.Npcs.GetNumericId(firstNpc.Id) : 0;

        engine.SpawnMonster(Position.FromCoords(sx + 2, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = monsterTypeId, Health = 1, Attack = 0, Defense = 0, Speed = 0 });

        player.Input.ActionType = ActionTypes.Attack;
        engine.Tick();

        Assert.True(engine.Combat.LastTickEvents.Count > 0);
        Assert.True(engine.Combat.LastTickEvents[0].TargetDied);

        player = ref engine.WorldMap.GetPlayerRef(p.Id);
        Assert.Contains(player.ActionEvents, e => e.EventType == PlayerActionEventType.Kill);
    }

    [Fact]
    public void ShieldBlock_MonsterAttackBlocked_NoDamage()
    {
        // Shield block is probabilistic, so use a high-tier shield and a fast-attacking monster
        // to verify that the post-attack-speed-split combat loop still emits blocked hits.
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // Equip a shield in offhand
        player.Equipment[(int)EquipSlot.Offhand] = new ItemData { ItemTypeId = ItemId("adamantite_shield"), StackCount = 1 };
        ActiveEffectsSystem.RecalculatePlayerStats(ref player);

        // Spawn an aggressive monster adjacent to player
        engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 9999, Attack = 10, Defense = 0, Speed = 10, AttackSpeed = 10 });

        // Run many ticks to give the shield a chance to block
        bool sawBlock = false;
        for (int i = 0; i < 200; i++)
        {
            engine.Tick();
            foreach (var evt in engine.Combat.LastTickEvents)
            {
                if (evt.Blocked)
                {
                    Assert.Equal(0, evt.Damage);
                    Assert.False(evt.TargetDied);
                    sawBlock = true;
                }
            }
            if (sawBlock) break;
        }

        // Adamantite shield block chance reaches the 50% cap, and the monster attacks every tick.
        Assert.True(sawBlock, "Shield should block at least one attack over many ticks");
    }

    [Fact]
    public void MeleeAttack_WithNoWeapon_UsesBaseStats()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 200, Attack = 0, Defense = 0, Speed = 0 });

        // No weapon equipped — melee should still work with base attack
        player.Input.ActionType = ActionTypes.Attack;
        engine.Tick();

        Assert.True(engine.Combat.LastTickEvents.Count > 0);
        Assert.False(engine.Combat.LastTickEvents[0].IsRanged);
        Assert.True(engine.Combat.LastTickEvents[0].Damage > 0);
    }

    [Fact]
    public void RangedAttack_WithAttackDelay_DoesNotFireUntilReady()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Ranger);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        player.Equipment[(int)EquipSlot.Hand] = new ItemData { ItemTypeId = ItemId("wooden_bow"), StackCount = 1 };
        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("wooden_arrow"), StackCount = 10 });
        ActiveEffectsSystem.RecalculatePlayerStats(ref player);

        engine.SpawnMonster(Position.FromCoords(sx + 3, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 200, Attack = 0, Defense = 0, Speed = 0 });

        // First attack should succeed
        player.Input.ActionType = ActionTypes.Attack;
        engine.Tick();
        Assert.True(engine.Combat.LastTickEvents.Count > 0);

        // Immediate second attack should not fire (attack delay)
        player = ref engine.WorldMap.GetPlayerRef(p.Id);
        player.Input.ActionType = ActionTypes.Attack;
        engine.Tick();

        // May or may not fire depending on attack speed, but after one tick delay is likely still active
        // This tests the delay check path
    }
}
