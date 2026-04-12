using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Entities;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Tests;

public class CombatSystemTests
{
    private static readonly BspDungeonGenerator _gen = new(42);

    private GameEngine CreateEngine()
    {
        var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        return engine;
    }

    [Fact]
    public void LastTickEvents_ReturnsEvents()
    {
        using var engine = CreateEngine();
        Assert.NotNull(engine.Combat.LastTickEvents);
        Assert.Empty(engine.Combat.LastTickEvents);
    }

    [Fact]
    public void Attack_ProducesEvent()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ), new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });

        player.Input.ActionType = ActionTypes.Attack;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        Assert.True(engine.Combat.LastTickEvents.Count > 0, "Combat should produce events");
    }

    [Fact]
    public void Attack_DealsDamage()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        var _m = engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ), new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });
        ref var monster = ref engine.WorldMap.GetMonsterRef(_m.Id);

        player.Input.ActionType = ActionTypes.Attack;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        monster = ref engine.WorldMap.GetMonsterRef(_m.Id);

        Assert.True(monster.Health.Current < 100, "Monster should take damage");
    }

    [Fact]
    public void Attack_KillTarget_EventShowsDeath()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        // Low health monster
        engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ), new MonsterData { MonsterTypeId = 0, Health = 1, Attack = 0, Defense = 0, Speed = 8 });

        player.Input.ActionType = ActionTypes.Attack;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        Assert.True(engine.Combat.LastTickEvents.Count > 0);
        Assert.True(engine.Combat.LastTickEvents[0].TargetDied);
    }

    [Fact]
    public void Attack_MissesEmpty_NoEvent()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        player.Input.ActionType = ActionTypes.Attack;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        Assert.Empty(engine.Combat.LastTickEvents);
    }

    [Fact]
    public void AutoTarget_FindsEnemyToRight()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        var _m = engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ), new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });
        ref var monster = ref engine.WorldMap.GetMonsterRef(_m.Id);

        player.Input.ActionType = ActionTypes.Attack;
        player.Input.TargetX = 0;
        player.Input.TargetY = 0;
        engine.Tick();

        Assert.Single(engine.Combat.LastTickEvents);
        Assert.True(monster.Health.Current < 100);
    }

    [Fact]
    public void AutoTarget_FindsEnemyToLeft()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        var _m = engine.SpawnMonster(Position.FromCoords(sx - 1, sy, Position.DefaultZ), new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });
        ref var monster = ref engine.WorldMap.GetMonsterRef(_m.Id);

        player.Input.ActionType = ActionTypes.Attack;
        player.Input.TargetX = 0;
        player.Input.TargetY = 0;
        engine.Tick();

        Assert.Single(engine.Combat.LastTickEvents);
        Assert.True(monster.Health.Current < 100);
    }

    [Fact]
    public void AutoTarget_FindsEnemyAbove()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        var _m = engine.SpawnMonster(Position.FromCoords(sx, sy - 1, Position.DefaultZ), new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });
        ref var monster = ref engine.WorldMap.GetMonsterRef(_m.Id);

        player.Input.ActionType = ActionTypes.Attack;
        player.Input.TargetX = 0;
        player.Input.TargetY = 0;
        engine.Tick();

        Assert.Single(engine.Combat.LastTickEvents);
        Assert.True(monster.Health.Current < 100);
    }

    [Fact]
    public void AutoTarget_FindsEnemyBelow()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        var _m = engine.SpawnMonster(Position.FromCoords(sx, sy + 1, Position.DefaultZ), new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });
        ref var monster = ref engine.WorldMap.GetMonsterRef(_m.Id);

        player.Input.ActionType = ActionTypes.Attack;
        player.Input.TargetX = 0;
        player.Input.TargetY = 0;
        engine.Tick();

        Assert.Single(engine.Combat.LastTickEvents);
        Assert.True(monster.Health.Current < 100);
    }

    [Fact]
    public void AutoTarget_FindsEnemyOnSameTile()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        var _m = engine.SpawnMonster(Position.FromCoords(sx, sy, Position.DefaultZ), new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });
        ref var monster = ref engine.WorldMap.GetMonsterRef(_m.Id);

        player.Input.ActionType = ActionTypes.Attack;
        player.Input.TargetX = 0;
        player.Input.TargetY = 0;
        engine.Tick();

        Assert.Single(engine.Combat.LastTickEvents);
        Assert.True(monster.Health.Current < 100);
    }

    [Fact]
    public void AutoTarget_NoAdjacentEnemy_NoEvent()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        // Monster 3 tiles away - not adjacent
        engine.SpawnMonster(Position.FromCoords(sx + 3, sy, Position.DefaultZ), new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });

        player.Input.ActionType = ActionTypes.Attack;
        player.Input.TargetX = 0;
        player.Input.TargetY = 0;
        engine.Tick();

        Assert.Empty(engine.Combat.LastTickEvents);
    }

    [Fact]
    public void AutoTarget_PicksClosest_SameTileOverAdjacent()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        var _sameTile = engine.SpawnMonster(Position.FromCoords(sx, sy, Position.DefaultZ), new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });
        ref var sameTile = ref engine.WorldMap.GetMonsterRef(_sameTile.Id);
        engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ), new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });

        player.Input.ActionType = ActionTypes.Attack;
        player.Input.TargetX = 0;
        player.Input.TargetY = 0;
        engine.Tick();

        // Should target the same-tile monster (distance 0)
        Assert.True(engine.Combat.LastTickEvents.Count >= 1);
        var evt = engine.Combat.LastTickEvents[0];
        Assert.Equal(sx, evt.Target.X);
        Assert.Equal(sy, evt.Target.Y);
    }

    // === Monster Attack Tests ===

    [Fact]
    public void MonsterAttack_AdjacentToPlayer_DealsDamage()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        var _m = engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ), new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 15, Defense = 0, Speed = 8 });
        ref var monster = ref engine.WorldMap.GetMonsterRef(_m.Id);


        var healthBefore = player.Health.Current;

        // Set monster to Attack state
        monster.AI.StateId = AIStates.Attack;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        var healthAfter = player.Health.Current;
        Assert.True(healthAfter < healthBefore, "Player should take damage from monster attack");
    }

    [Fact]
    public void MonsterAttack_ProducesCombatEvent()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        var _m = engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ), new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 15, Defense = 0, Speed = 8 });
        ref var monster = ref engine.WorldMap.GetMonsterRef(_m.Id);

        monster.AI.StateId = AIStates.Attack;

        engine.Tick();

        // Should have at least one event from the monster attack
        var monsterEvents = engine.Combat.LastTickEvents.Where(
            e => e.Attacker.X == sx + 1 && e.Attacker.Y == sy).ToList();
        Assert.Single(monsterEvents);
        Assert.Equal(sx, monsterEvents[0].Target.X);
        Assert.Equal(sy, monsterEvents[0].Target.Y);
    }

    [Fact]
    public void MonsterAttack_RespectsDefense()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        // Monster attack = 8, player defense = 5+bonus → damage = max(1, 8 - playerDef)
        var _m = engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ), new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 8, Defense = 0, Speed = 8 });
        ref var monster = ref engine.WorldMap.GetMonsterRef(_m.Id);

        monster.AI.StateId = AIStates.Attack;

        int hpBefore = player.Health.Current;
        int expectedDmg = Math.Max(1, 8 - player.CombatStats.Defense);

        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        Assert.Equal(hpBefore - expectedDmg, player.Health.Current);
    }

    [Fact]
    public void MonsterAttack_NotAdjacent_NoDamage()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        // Monster 3 tiles away
        var _m = engine.SpawnMonster(Position.FromCoords(sx + 3, sy, Position.DefaultZ), new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 15, Defense = 0, Speed = 8 });
        ref var monster = ref engine.WorldMap.GetMonsterRef(_m.Id);

        monster.AI.StateId = AIStates.Attack;

        int hpBefore = player.Health.Current;

        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        Assert.Equal(hpBefore, player.Health.Current);
    }

    [Fact]
    public void MonsterAttack_NotInAttackState_NoDamage()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        var _m = engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ), new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 15, Defense = 0, Speed = 8 });
        ref var monster = ref engine.WorldMap.GetMonsterRef(_m.Id);

        // Monster stays in Idle state (default)
        int hpBefore = player.Health.Current;

        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        Assert.Equal(hpBefore, player.Health.Current);
    }

    [Fact]
    public void MonsterAttack_KillsPlayer_EventShowsDeath()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        // Very high attack monster
        var _m = engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ), new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 500, Defense = 0, Speed = 8 });
        ref var monster = ref engine.WorldMap.GetMonsterRef(_m.Id);

        // Reduce player health to 1
        player.Health.Current = 1;

        monster.AI.StateId = AIStates.Attack;

        engine.Tick();

        var monsterEvents = engine.Combat.LastTickEvents.Where(
            e => e.Attacker.X == sx + 1 && e.Attacker.Y == sy).ToList();
        Assert.Single(monsterEvents);
        Assert.True(monsterEvents[0].TargetDied);
    }

    [Fact]
    public void MonsterAttack_DeadMonster_DoesNotAttack()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        var _m = engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ), new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 15, Defense = 0, Speed = 8 });
        ref var monster = ref engine.WorldMap.GetMonsterRef(_m.Id);

        // Kill the monster and add DeadTag
        monster.Health.Current = 0;

        monster.AI.StateId = AIStates.Attack;

        int hpBefore = player.Health.Current;

        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        Assert.Equal(hpBefore, player.Health.Current);
    }

    [Fact]
    public void MonsterAttack_DeadPlayer_NotTargeted()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        var _m = engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ), new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 15, Defense = 0, Speed = 8 });
        ref var monster = ref engine.WorldMap.GetMonsterRef(_m.Id);

        // Kill the player (health=0) but keep them alive in ECS (no DeadTag yet)
        player.Health.Current = 0;

        monster.AI.StateId = AIStates.Attack;

        engine.Tick();

        // Monster shouldn't produce a combat event targeting a dead player
        var monsterEvents = engine.Combat.LastTickEvents.Where(
            e => e.Attacker.X == sx + 1 && e.Attacker.Y == sy).ToList();
        Assert.Empty(monsterEvents);
    }

    // === Shield Block Tests ===

    private static int ItemId(string id) => GameData.Instance.Items.GetNumericId(id);
    private static ItemDefinition Item(string id) => GameData.Instance.Items.Get(id)!;

    private static void EquipItem(ref PlayerEntity player, GameEngine engine, string itemId)
    {
        var pos = player.Position;
        var def = Item(itemId);
        engine.SpawnItemOnGround(def, pos);
        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();
        player.Input.ActionType = ActionTypes.UseItem;
        player.Input.ItemSlot = 0;
        engine.Tick();
    }

    [Fact]
    public void ShieldBlock_CanBlockMonsterAttack()
    {
        // steel_shield: steel tier (250%), baseDefense=7 → effectiveDefense=17, blockChance=34%
        // Run many iterations; statistically some should block and some should not
        int blocked = 0;
        int hit = 0;

        for (int seed = 0; seed < 200; seed++)
        {
            using var engine = CreateEngine();
            var (sx, sy, _) = engine.FindSpawnPosition();
            var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
            ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

            EquipItem(ref player, engine, "steel_shield");
            player = ref engine.WorldMap.GetPlayerRef(_p.Id);

            var _m = engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ),
                new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 50, Defense = 0, Speed = 8 });
            ref var monster = ref engine.WorldMap.GetMonsterRef(_m.Id);
            monster.AI.StateId = AIStates.Attack;

            engine.Tick();

            var events = engine.Combat.LastTickEvents.Where(
                e => e.Attacker.X == sx + 1 && e.Attacker.Y == sy).ToList();
            Assert.Single(events);

            if (events[0].Blocked)
            {
                Assert.Equal(0, events[0].Damage);
                blocked++;
            }
            else
            {
                Assert.True(events[0].Damage > 0);
                hit++;
            }
        }

        // With 34% block chance over 200 trials, expect roughly 68 blocks
        // Extremely unlikely to get 0 blocks or 200 blocks
        Assert.True(blocked > 0, $"Expected some blocks, got {blocked} out of 200");
        Assert.True(hit > 0, $"Expected some hits, got {hit} out of 200");
    }

    [Fact]
    public void ShieldBlock_NoDamageOnBlock()
    {
        // Use large number of tries to find a block event
        for (int seed = 0; seed < 500; seed++)
        {
            using var engine = CreateEngine();
            var (sx, sy, _) = engine.FindSpawnPosition();
            var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
            ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

            int hpBefore = player.Health.Current;

            EquipItem(ref player, engine, "steel_shield");
            player = ref engine.WorldMap.GetPlayerRef(_p.Id);

            var _m = engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ),
                new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 50, Defense = 0, Speed = 8 });
            ref var monster = ref engine.WorldMap.GetMonsterRef(_m.Id);
            monster.AI.StateId = AIStates.Attack;

            engine.Tick();
            player = ref engine.WorldMap.GetPlayerRef(_p.Id);

            var events = engine.Combat.LastTickEvents.Where(
                e => e.Attacker.X == sx + 1 && e.Attacker.Y == sy).ToList();

            if (events.Count > 0 && events[0].Blocked)
            {
                // Player should take zero damage on block
                Assert.Equal(hpBefore, player.Health.Current);
                return; // Test passed
            }
        }

        Assert.Fail("No block event observed in 500 iterations");
    }

    [Fact]
    public void NoShield_NeverBlocks()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // No shield equipped
        var _m = engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 50, Defense = 0, Speed = 8 });
        ref var monster = ref engine.WorldMap.GetMonsterRef(_m.Id);
        monster.AI.StateId = AIStates.Attack;

        engine.Tick();

        var events = engine.Combat.LastTickEvents.Where(
            e => e.Attacker.X == sx + 1 && e.Attacker.Y == sy).ToList();
        Assert.Single(events);
        Assert.False(events[0].Blocked);
        Assert.True(events[0].Damage > 0);
    }

    [Fact]
    public void EquippedWeapon_AppliesTieredDamageToMonster()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        int baseAtk = player.CombatStats.Attack;

        // Equip long_sword (iron, baseDamage=5, effective=10)
        EquipItem(ref player, engine, "long_sword");
        player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        int weaponAtk = player.CombatStats.Attack;
        Assert.Equal(baseAtk + Item("long_sword").EffectiveAttack, weaponAtk);

        // Spawn monster with known defense=0, health=100
        var _m = engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });
        ref var monster = ref engine.WorldMap.GetMonsterRef(_m.Id);

        player.Input.ActionType = ActionTypes.Attack;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        monster = ref engine.WorldMap.GetMonsterRef(_m.Id);

        // Damage should be max(1, weaponAtk - 0) = weaponAtk
        int expectedDamage = Math.Max(1, weaponAtk);
        Assert.Equal(100 - expectedDamage, monster.Health.Current);
    }

    [Fact]
    public void EquippedArmor_ReducesDamageFromMonster()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        int baseDef = player.CombatStats.Defense;

        // Equip chain_mail (iron, baseDefense=4, effective=8)
        EquipItem(ref player, engine, "chain_mail");
        player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        int totalDef = player.CombatStats.Defense;
        Assert.Equal(baseDef + Item("chain_mail").EffectiveDefense, totalDef);

        // Monster with attack = totalDef + 5 → expect 5 damage
        int monsterAtk = totalDef + 5;
        var _m = engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 100, Attack = monsterAtk, Defense = 0, Speed = 8 });
        ref var monster = ref engine.WorldMap.GetMonsterRef(_m.Id);
        monster.AI.StateId = AIStates.Attack;

        int hpBefore = player.Health.Current;
        engine.Tick();
        player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        Assert.Equal(hpBefore - 5, player.Health.Current);
    }

    // ── NPC Dialogue Tests ──

    [Fact]
    public void Attack_TownNpc_TriggersDialogue()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        engine.SpawnTownNpc(Position.FromCoords(sx + 1, sy, Position.DefaultZ), "TestNpc", sx, sy, 10);

        player.Input.ActionType = ActionTypes.Attack;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        Assert.True(engine.Combat.LastTickDialogueEvents.Count > 0);
        Assert.Equal("TestNpc", engine.Combat.LastTickDialogueEvents[0].NpcName);
    }

    [Fact]
    public void Attack_TownNpc_DialogueCooldown()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        engine.SpawnTownNpc(Position.FromCoords(sx + 1, sy, Position.DefaultZ), "TestNpc", sx, sy, 10);

        // First talk
        player.Input.ActionType = ActionTypes.Attack;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();
        Assert.True(engine.Combat.LastTickDialogueEvents.Count > 0);

        // Second immediate talk - should be on cooldown
        player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        player.Input.ActionType = ActionTypes.Attack;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();
        Assert.Empty(engine.Combat.LastTickDialogueEvents);
    }

    // ── Resource Node Tool Bonus Tests ──

    [Fact]
    public void Attack_ResourceNode_WithMatchingTool_BonusDamage()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Spawn a tree (requires axe)
        var treeDef = GameData.Instance.ResourceNodes.Get("tree");
        if (treeDef == null) return;
        engine.SpawnResourceNode(Position.FromCoords(sx + 1, sy, Position.DefaultZ), treeDef);

        // Attack without tool
        player.Input.ActionType = ActionTypes.Attack;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        var eventsNoTool = engine.Combat.LastTickEvents.ToList();
        int damageWithoutTool = eventsNoTool.Count > 0 ? eventsNoTool[0].Damage : 0;

        // Now equip matching tool and attack again
        player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        var axeDef = GameData.Instance.Items.All.FirstOrDefault(
            i => i.Tool != null && i.Tool.ToolType == treeDef.RequiredToolType);
        if (axeDef == null) return;

        EquipItem(ref player, engine, axeDef.Id);
        player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Respawn tree for a fresh attack
        engine.SpawnResourceNode(Position.FromCoords(sx + 1, sy, Position.DefaultZ), treeDef);

        player.Input.ActionType = ActionTypes.Attack;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        var eventsWithTool = engine.Combat.LastTickEvents.ToList();
        if (eventsWithTool.Count > 0)
        {
            Assert.True(eventsWithTool[0].Damage >= damageWithoutTool);
        }
    }

    // ── Auto-target resource node ──

    [Fact]
    public void AutoTarget_FindsAdjacentResourceNode()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        var treeDef = GameData.Instance.ResourceNodes.Get("tree");
        if (treeDef == null) return;
        engine.SpawnResourceNode(Position.FromCoords(sx + 1, sy, Position.DefaultZ), treeDef);

        player.Input.ActionType = ActionTypes.Attack;
        player.Input.TargetX = 0;
        player.Input.TargetY = 0;
        engine.Tick();

        Assert.True(engine.Combat.LastTickEvents.Count > 0);
    }

    // ── DebugInvulnerable ──

    [Fact]
    public void DebugInvulnerable_MonsterCannotDamagePlayer()
    {
        using var engine = CreateEngine();
        engine.DebugInvulnerable = true;
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        int hpBefore = player.Health.Current;
        var _m = engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 500, Defense = 0, Speed = 8 });
        ref var monster = ref engine.WorldMap.GetMonsterRef(_m.Id);
        monster.AI.StateId = AIStates.Attack;

        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        Assert.Equal(hpBefore, player.Health.Current);
    }

    [Fact]
    public void Attack_AttackDelayActive_DoesNotAttack()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        var _m = engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });

        // Set attack delay active
        player.AttackDelay.Current = 10;
        player.Input.ActionType = ActionTypes.Attack;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        // No combat events since attack delay still active
        Assert.Empty(engine.Combat.LastTickEvents);
    }

    [Fact]
    public void Attack_SetsAttackDelayAfterAttack()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });

        player.Input.ActionType = ActionTypes.Attack;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        Assert.True(player.AttackDelay.Current > 0, "Attack delay should be set after attacking");
    }

    [Fact]
    public void ResourceNode_NoToolEquipped_MinimumDamage()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Clear hand slot (no tool)
        player.Equipment.Hand = ItemData.None;

        var treeDef = GameData.Instance.ResourceNodes.Get("tree");
        Assert.NotNull(treeDef);

        var _n = engine.SpawnResourceNode(Position.FromCoords(sx + 1, sy, Position.DefaultZ), treeDef!);

        player.Input.ActionType = ActionTypes.Attack;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        // Should still deal damage (at least 1)
        Assert.NotEmpty(engine.Combat.LastTickEvents);
        Assert.True(engine.Combat.LastTickEvents[0].Damage >= 1);
    }

    [Fact]
    public void MonsterAttack_SetsAttackDelayOnMonster()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        var _m = engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 15, Defense = 0, Speed = 8 });
        ref var monster = ref engine.WorldMap.GetMonsterRef(_m.Id);
        monster.AI.StateId = AIStates.Attack;
        monster.AttackDelay.Current = 0;
        monster.AttackDelay.Interval = 5;

        engine.Tick();

        monster = ref engine.WorldMap.GetMonsterRef(_m.Id);
        Assert.True(monster.AttackDelay.Current > 0, "Monster attack delay should be set after attacking");
    }

    [Fact]
    public void MonsterAttack_AttackDelayActive_DoesNotAttack()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        int hpBefore = player.Health.Current;

        var _m = engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 999, Defense = 0, Speed = 8 });
        ref var monster = ref engine.WorldMap.GetMonsterRef(_m.Id);
        monster.AI.StateId = AIStates.Attack;
        monster.AttackDelay.Current = 10; // Attack delay active

        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        Assert.Equal(hpBefore, player.Health.Current);
    }

    [Fact]
    public void Attack_DirectTarget_AttacksSpecificDirection()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Place monsters in two directions
        engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });
        engine.SpawnMonster(Position.FromCoords(sx - 1, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });

        // Attack specifically to the left
        player.Input.ActionType = ActionTypes.Attack;
        player.Input.TargetX = -1;
        player.Input.TargetY = 0;
        engine.Tick();

        Assert.NotEmpty(engine.Combat.LastTickEvents);
        // The target should be at sx-1, sy
        Assert.Equal(sx - 1, engine.Combat.LastTickEvents[0].Target.X);
    }

    [Fact]
    public void Attack_MinimumDamage_IsAlwaysOne()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Spawn monster with very high defense
        engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 999, Speed = 8 });

        player.Input.ActionType = ActionTypes.Attack;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        Assert.NotEmpty(engine.Combat.LastTickEvents);
        Assert.Equal(1, engine.Combat.LastTickEvents[0].Damage); // Min damage = 1
    }
}
