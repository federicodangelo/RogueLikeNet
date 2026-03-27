using Arch.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.Systems;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Core.Tests;

public class AISystemTests
{
    private static readonly BspDungeonGenerator _gen = new(42);

    private GameEngine CreateEngine()
    {
        var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0);
        return engine;
    }

    [Fact]
    public void Attack_TargetMovesAway_TransitionsToChase()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();

        // Spawn player and a monster adjacent to the player
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);
        var monster = engine.SpawnMonster(sx + 1, sy, new MonsterData
        {
            MonsterTypeId = 1,
            Health = 100,
            Attack = 5,
            Defense = 0,
            Speed = 8
        });

        // Set the monster's AI state to Attack
        ref var ai = ref engine.EcsWorld.Get<AIState>(monster);
        ai.StateId = AIStates.Attack;

        // Move the player far away so nearestDist > 1
        ref var playerPos = ref engine.EcsWorld.Get<Position>(player);
        playerPos.X = sx + 10;

        engine.Tick();

        // Monster should transition from Attack → Chase
        ref var aiAfter = ref engine.EcsWorld.Get<AIState>(monster);
        Assert.Equal(AIStates.Chase, aiAfter.StateId);
    }

    [Fact]
    public void Chase_MonsterMoves_ResetsMoveDelay()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();

        // Spawn player and a monster a few tiles away (within detection range)
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);
        var monster = engine.SpawnMonster(sx + 3, sy, new MonsterData
        {
            MonsterTypeId = 1,
            Health = 100,
            Attack = 5,
            Defense = 0,
            Speed = 8
        });

        // Set the monster's AI state to Chase with no move delay
        ref var ai = ref engine.EcsWorld.Get<AIState>(monster);
        ai.StateId = AIStates.Chase;
        ref var delay = ref engine.EcsWorld.Get<MoveDelay>(monster);
        delay.Current = 0;

        ref var posBefore = ref engine.EcsWorld.Get<Position>(monster);
        int originalX = posBefore.X;

        engine.Tick();

        // Check that move delay was reset (monster moved)
        ref var delayAfter = ref engine.EcsWorld.Get<MoveDelay>(monster);
        ref var posAfter = ref engine.EcsWorld.Get<Position>(monster);

        // If monster moved, delay should have been reset
        if (posAfter.X != originalX || posAfter.Y != sy)
        {
            Assert.True(delayAfter.Current > 0, "Move delay should be reset after movement");
        }
    }

    [Fact]
    public void Chase_ExceedsChaseRange_TransitionsToIdle()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();

        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);
        var monster = engine.SpawnMonster(sx + 13, sy, new MonsterData
        {
            MonsterTypeId = 1,
            Health = 100,
            Attack = 5,
            Defense = 0,
            Speed = 8
        });

        // Set to Chase state
        ref var ai = ref engine.EcsWorld.Get<AIState>(monster);
        ai.StateId = AIStates.Chase;

        engine.Tick();

        // Distance is 13 > ChaseRange(12), so should go Idle
        ref var aiAfter = ref engine.EcsWorld.Get<AIState>(monster);
        Assert.Equal(AIStates.Idle, aiAfter.StateId);
    }

    [Fact]
    public void Chase_AdjacentToPlayer_TransitionsToAttack()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();

        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);
        var monster = engine.SpawnMonster(sx + 1, sy, new MonsterData
        {
            MonsterTypeId = 1,
            Health = 100,
            Attack = 5,
            Defense = 0,
            Speed = 8
        });

        ref var ai = ref engine.EcsWorld.Get<AIState>(monster);
        ai.StateId = AIStates.Chase;

        engine.Tick();

        ref var aiAfter = ref engine.EcsWorld.Get<AIState>(monster);
        Assert.Equal(AIStates.Attack, aiAfter.StateId);
    }

    [Fact]
    public void Idle_PlayerInDetectionRange_TransitionsToChase()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();

        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);
        var monster = engine.SpawnMonster(sx + 5, sy, new MonsterData
        {
            MonsterTypeId = 1,
            Health = 100,
            Attack = 5,
            Defense = 0,
            Speed = 8
        });

        // Start in Idle
        ref var ai = ref engine.EcsWorld.Get<AIState>(monster);
        ai.StateId = AIStates.Idle;

        engine.Tick();

        ref var aiAfter = ref engine.EcsWorld.Get<AIState>(monster);
        Assert.Equal(AIStates.Chase, aiAfter.StateId);
    }

    [Fact]
    public void Chase_WithCooldown_DoesNotMove()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();

        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);
        var monster = engine.SpawnMonster(sx + 3, sy, new MonsterData
        {
            MonsterTypeId = 1,
            Health = 100,
            Attack = 5,
            Defense = 0,
            Speed = 2
        });

        ref var ai = ref engine.EcsWorld.Get<AIState>(monster);
        ai.StateId = AIStates.Chase;
        // Set very high cooldown
        ref var delay = ref engine.EcsWorld.Get<MoveDelay>(monster);
        delay.Current = 100;

        ref var posBefore = ref engine.EcsWorld.Get<Position>(monster);
        int origX = posBefore.X;
        int origY = posBefore.Y;

        engine.Tick();

        ref var posAfter = ref engine.EcsWorld.Get<Position>(monster);
        // Tick decrements delay by 1, but 99 >> 0, so monster stays put
        Assert.Equal(origX, posAfter.X);
        Assert.Equal(origY, posAfter.Y);
    }
}
