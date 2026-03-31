using Arch.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Tests;

public class CombatSystemTests
{
    private static readonly BspDungeonGenerator _gen = new(42);

    private GameEngine CreateEngine()
    {
        var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0, Position.DefaultZ);
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
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);
        engine.SpawnMonster(sx + 1, sy, Position.DefaultZ, new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.Attack;
        input.TargetX = 1;
        input.TargetY = 0;
        engine.Tick();

        Assert.True(engine.Combat.LastTickEvents.Count > 0, "Combat should produce events");
    }

    [Fact]
    public void Attack_DealsDamage()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);
        var monster = engine.SpawnMonster(sx + 1, sy, Position.DefaultZ, new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.Attack;
        input.TargetX = 1;
        input.TargetY = 0;
        engine.Tick();

        ref var mHealth = ref engine.EcsWorld.Get<Health>(monster);
        Assert.True(mHealth.Current < 100, "Monster should take damage");
    }

    [Fact]
    public void Attack_KillTarget_EventShowsDeath()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);
        // Low health monster
        engine.SpawnMonster(sx + 1, sy, Position.DefaultZ, new MonsterData { MonsterTypeId = 0, Health = 1, Attack = 0, Defense = 0, Speed = 8 });

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.Attack;
        input.TargetX = 1;
        input.TargetY = 0;
        engine.Tick();

        Assert.True(engine.Combat.LastTickEvents.Count > 0);
        Assert.True(engine.Combat.LastTickEvents[0].TargetDied);
    }

    [Fact]
    public void Attack_MissesEmpty_NoEvent()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.Attack;
        input.TargetX = 1;
        input.TargetY = 0;
        engine.Tick();

        Assert.Empty(engine.Combat.LastTickEvents);
    }

    [Fact]
    public void AutoTarget_FindsEnemyToRight()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);
        var monster = engine.SpawnMonster(sx + 1, sy, Position.DefaultZ, new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.Attack;
        input.TargetX = 0;
        input.TargetY = 0;
        engine.Tick();

        Assert.Single(engine.Combat.LastTickEvents);
        ref var mHealth = ref engine.EcsWorld.Get<Health>(monster);
        Assert.True(mHealth.Current < 100);
    }

    [Fact]
    public void AutoTarget_FindsEnemyToLeft()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);
        var monster = engine.SpawnMonster(sx - 1, sy, Position.DefaultZ, new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.Attack;
        input.TargetX = 0;
        input.TargetY = 0;
        engine.Tick();

        Assert.Single(engine.Combat.LastTickEvents);
        ref var mHealth = ref engine.EcsWorld.Get<Health>(monster);
        Assert.True(mHealth.Current < 100);
    }

    [Fact]
    public void AutoTarget_FindsEnemyAbove()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);
        var monster = engine.SpawnMonster(sx, sy - 1, Position.DefaultZ, new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.Attack;
        input.TargetX = 0;
        input.TargetY = 0;
        engine.Tick();

        Assert.Single(engine.Combat.LastTickEvents);
        ref var mHealth = ref engine.EcsWorld.Get<Health>(monster);
        Assert.True(mHealth.Current < 100);
    }

    [Fact]
    public void AutoTarget_FindsEnemyBelow()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);
        var monster = engine.SpawnMonster(sx, sy + 1, Position.DefaultZ, new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.Attack;
        input.TargetX = 0;
        input.TargetY = 0;
        engine.Tick();

        Assert.Single(engine.Combat.LastTickEvents);
        ref var mHealth = ref engine.EcsWorld.Get<Health>(monster);
        Assert.True(mHealth.Current < 100);
    }

    [Fact]
    public void AutoTarget_FindsEnemyOnSameTile()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);
        var monster = engine.SpawnMonster(sx, sy, Position.DefaultZ, new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.Attack;
        input.TargetX = 0;
        input.TargetY = 0;
        engine.Tick();

        Assert.Single(engine.Combat.LastTickEvents);
        ref var mHealth = ref engine.EcsWorld.Get<Health>(monster);
        Assert.True(mHealth.Current < 100);
    }

    [Fact]
    public void AutoTarget_NoAdjacentEnemy_NoEvent()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);
        // Monster 3 tiles away - not adjacent
        engine.SpawnMonster(sx + 3, sy, Position.DefaultZ, new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.Attack;
        input.TargetX = 0;
        input.TargetY = 0;
        engine.Tick();

        Assert.Empty(engine.Combat.LastTickEvents);
    }

    [Fact]
    public void AutoTarget_PicksClosest_SameTileOverAdjacent()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);
        var sameTile = engine.SpawnMonster(sx, sy, Position.DefaultZ, new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });
        engine.SpawnMonster(sx + 1, sy, Position.DefaultZ, new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.Attack;
        input.TargetX = 0;
        input.TargetY = 0;
        engine.Tick();

        // Should target the same-tile monster (distance 0)
        Assert.True(engine.Combat.LastTickEvents.Count >= 1);
        var evt = engine.Combat.LastTickEvents[0];
        Assert.Equal(sx, evt.TargetX);
        Assert.Equal(sy, evt.TargetY);
    }

    // === Monster Attack Tests ===

    [Fact]
    public void MonsterAttack_AdjacentToPlayer_DealsDamage()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);
        var monster = engine.SpawnMonster(sx + 1, sy, Position.DefaultZ, new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 15, Defense = 0, Speed = 8 });

        ref var ai = ref engine.EcsWorld.Get<AIState>(monster);
        ref var pHealth = ref engine.EcsWorld.Get<Health>(player);

        var healthBefore = pHealth.Current;

        // Set monster to Attack state
        ai.StateId = AIStates.Attack;
        engine.Tick();

        var healthAfter = pHealth.Current;
        Assert.True(healthAfter < healthBefore, "Player should take damage from monster attack");
    }

    [Fact]
    public void MonsterAttack_ProducesCombatEvent()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);
        var monster = engine.SpawnMonster(sx + 1, sy, Position.DefaultZ, new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 15, Defense = 0, Speed = 8 });

        ref var ai = ref engine.EcsWorld.Get<AIState>(monster);
        ai.StateId = AIStates.Attack;

        engine.Tick();

        // Should have at least one event from the monster attack
        var monsterEvents = engine.Combat.LastTickEvents.Where(
            e => e.AttackerX == sx + 1 && e.AttackerY == sy).ToList();
        Assert.Single(monsterEvents);
        Assert.Equal(sx, monsterEvents[0].TargetX);
        Assert.Equal(sy, monsterEvents[0].TargetY);
    }

    [Fact]
    public void MonsterAttack_RespectsDefense()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);
        // Monster attack = 8, player defense = 5+bonus → damage = max(1, 8 - playerDef)
        var monster = engine.SpawnMonster(sx + 1, sy, Position.DefaultZ, new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 8, Defense = 0, Speed = 8 });

        ref var ai = ref engine.EcsWorld.Get<AIState>(monster);
        ai.StateId = AIStates.Attack;

        ref var pHealthBefore = ref engine.EcsWorld.Get<Health>(player);
        int hpBefore = pHealthBefore.Current;
        ref var pStats = ref engine.EcsWorld.Get<CombatStats>(player);
        int expectedDmg = Math.Max(1, 8 - pStats.Defense);

        engine.Tick();

        ref var pHealthAfter = ref engine.EcsWorld.Get<Health>(player);
        Assert.Equal(hpBefore - expectedDmg, pHealthAfter.Current);
    }

    [Fact]
    public void MonsterAttack_NotAdjacent_NoDamage()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);
        // Monster 3 tiles away
        var monster = engine.SpawnMonster(sx + 3, sy, Position.DefaultZ, new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 15, Defense = 0, Speed = 8 });

        ref var ai = ref engine.EcsWorld.Get<AIState>(monster);
        ai.StateId = AIStates.Attack;

        ref var pHealthBefore = ref engine.EcsWorld.Get<Health>(player);
        int hpBefore = pHealthBefore.Current;

        engine.Tick();

        ref var pHealthAfter = ref engine.EcsWorld.Get<Health>(player);
        Assert.Equal(hpBefore, pHealthAfter.Current);
    }

    [Fact]
    public void MonsterAttack_NotInAttackState_NoDamage()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);
        var monster = engine.SpawnMonster(sx + 1, sy, Position.DefaultZ, new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 15, Defense = 0, Speed = 8 });

        // Monster stays in Idle state (default)
        ref var pHealthBefore = ref engine.EcsWorld.Get<Health>(player);
        int hpBefore = pHealthBefore.Current;

        engine.Tick();

        ref var pHealthAfter = ref engine.EcsWorld.Get<Health>(player);
        Assert.Equal(hpBefore, pHealthAfter.Current);
    }

    [Fact]
    public void MonsterAttack_KillsPlayer_EventShowsDeath()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);
        // Very high attack monster
        var monster = engine.SpawnMonster(sx + 1, sy, Position.DefaultZ, new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 500, Defense = 0, Speed = 8 });

        // Reduce player health to 1
        ref var pHealth = ref engine.EcsWorld.Get<Health>(player);
        pHealth.Current = 1;

        ref var ai = ref engine.EcsWorld.Get<AIState>(monster);
        ai.StateId = AIStates.Attack;

        engine.Tick();

        var monsterEvents = engine.Combat.LastTickEvents.Where(
            e => e.AttackerX == sx + 1 && e.AttackerY == sy).ToList();
        Assert.Single(monsterEvents);
        Assert.True(monsterEvents[0].TargetDied);
    }

    [Fact]
    public void MonsterAttack_DeadMonster_DoesNotAttack()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);
        var monster = engine.SpawnMonster(sx + 1, sy, Position.DefaultZ, new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 15, Defense = 0, Speed = 8 });

        // Kill the monster and add DeadTag
        ref var mHealth = ref engine.EcsWorld.Get<Health>(monster);
        mHealth.Current = 0;
        engine.EcsWorld.Add<DeadTag>(monster);

        ref var ai = ref engine.EcsWorld.Get<AIState>(monster);
        ai.StateId = AIStates.Attack;

        ref var pHealthBefore = ref engine.EcsWorld.Get<Health>(player);
        int hpBefore = pHealthBefore.Current;

        engine.Tick();

        ref var pHealthAfter = ref engine.EcsWorld.Get<Health>(player);
        Assert.Equal(hpBefore, pHealthAfter.Current);
    }

    [Fact]
    public void MonsterAttack_DeadPlayer_NotTargeted()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);
        var monster = engine.SpawnMonster(sx + 1, sy, Position.DefaultZ, new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 15, Defense = 0, Speed = 8 });

        // Kill the player (health=0) but keep them alive in ECS (no DeadTag yet)
        ref var pHealth = ref engine.EcsWorld.Get<Health>(player);
        pHealth.Current = 0;

        ref var ai = ref engine.EcsWorld.Get<AIState>(monster);
        ai.StateId = AIStates.Attack;

        engine.Tick();

        // Monster shouldn't produce a combat event targeting a dead player
        var monsterEvents = engine.Combat.LastTickEvents.Where(
            e => e.AttackerX == sx + 1 && e.AttackerY == sy).ToList();
        Assert.Empty(monsterEvents);
    }
}
