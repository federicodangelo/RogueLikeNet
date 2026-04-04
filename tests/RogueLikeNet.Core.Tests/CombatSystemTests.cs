using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
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
}
