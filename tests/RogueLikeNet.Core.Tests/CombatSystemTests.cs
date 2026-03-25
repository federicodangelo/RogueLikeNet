using Arch.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Tests;

public class CombatSystemTests
{
    private static readonly BspDungeonGenerator _gen = new();

    private GameEngine CreateEngine()
    {
        var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0);
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
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);
        engine.SpawnMonster(0, sx + 1, sy, 103, 0x00FF00, 100, 5, 0, 8);

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
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);
        var monster = engine.SpawnMonster(0, sx + 1, sy, 103, 0x00FF00, 100, 5, 0, 8);

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
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);
        // Low health monster
        engine.SpawnMonster(0, sx + 1, sy, 103, 0x00FF00, 1, 0, 0, 8);

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
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

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
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);
        var monster = engine.SpawnMonster(0, sx + 1, sy, 103, 0x00FF00, 100, 5, 0, 8);

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
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);
        var monster = engine.SpawnMonster(0, sx - 1, sy, 103, 0x00FF00, 100, 5, 0, 8);

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
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);
        var monster = engine.SpawnMonster(0, sx, sy - 1, 103, 0x00FF00, 100, 5, 0, 8);

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
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);
        var monster = engine.SpawnMonster(0, sx, sy + 1, 103, 0x00FF00, 100, 5, 0, 8);

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
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);
        var monster = engine.SpawnMonster(0, sx, sy, 103, 0x00FF00, 100, 5, 0, 8);

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
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);
        // Monster 3 tiles away - not adjacent
        engine.SpawnMonster(0, sx + 3, sy, 103, 0x00FF00, 100, 5, 0, 8);

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
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);
        var sameTile = engine.SpawnMonster(0, sx, sy, 103, 0x00FF00, 100, 5, 0, 8);
        engine.SpawnMonster(0, sx + 1, sy, 103, 0x00FF00, 100, 5, 0, 8);

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
}
