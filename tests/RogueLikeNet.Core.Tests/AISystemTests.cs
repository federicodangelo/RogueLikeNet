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
        engine.EnsureChunkLoaded(0, 0, Position.DefaultZ);
        return engine;
    }

    [Fact]
    public void Attack_TargetMovesAway_TransitionsToChase()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();

        // Spawn player and a monster adjacent to the player
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);
        var monster = engine.SpawnMonster(sx + 1, sy, Position.DefaultZ, new MonsterData
        {
            MonsterTypeId = 1,
            Health = 100,
            Attack = 5,
            Defense = 0,
            Speed = 8
        });

        // Set the monster's AI state to Attack
        monster.AI.StateId = AIStates.Attack;

        // Move the player far away so nearestDist > 1
        player.X = sx + 10;

        engine.Tick();

        // Monster should transition from Attack → Chase
        Assert.Equal(AIStates.Chase, monster.AI.StateId);
    }

    [Fact]
    public void Chase_MonsterMoves_ResetsMoveDelay()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();

        // Spawn player and a monster a few tiles away (within detection range)
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);
        var monster = engine.SpawnMonster(sx + 3, sy, Position.DefaultZ, new MonsterData
        {
            MonsterTypeId = 1,
            Health = 100,
            Attack = 5,
            Defense = 0,
            Speed = 8
        });

        // Set the monster's AI state to Chase with no move delay
        monster.AI.StateId = AIStates.Chase;
        monster.MoveDelay.Current = 0;

        int originalX = monster.X;

        engine.Tick();

        // Check that move monster.MoveDelay was reset (monster moved)

        // If monster moved, monster.MoveDelay should have been reset
        if (monster.X != originalX || monster.Y != sy)
        {
            Assert.True(monster.MoveDelay.Current > 0, "Move monster.MoveDelay should be reset after movement");
        }
    }

    [Fact]
    public void Chase_ExceedsChaseRange_TransitionsToIdle()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();

        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);
        var monster = engine.SpawnMonster(sx + 13, sy, Position.DefaultZ, new MonsterData
        {
            MonsterTypeId = 1,
            Health = 100,
            Attack = 5,
            Defense = 0,
            Speed = 8
        });

        // Set to Chase state
        monster.AI.StateId = AIStates.Chase;

        engine.Tick();

        // Distance is 13 > ChaseRange(12), so should go Idle
        Assert.Equal(AIStates.Idle, monster.AI.StateId);
    }

    [Fact]
    public void Chase_AdjacentToPlayer_TransitionsToAttack()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();

        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);
        var monster = engine.SpawnMonster(sx + 1, sy, Position.DefaultZ, new MonsterData
        {
            MonsterTypeId = 1,
            Health = 100,
            Attack = 5,
            Defense = 0,
            Speed = 8
        });

        monster.AI.StateId = AIStates.Chase;

        engine.Tick();

        Assert.Equal(AIStates.Attack, monster.AI.StateId);
    }

    [Fact]
    public void Idle_PlayerInDetectionRange_TransitionsToChase()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();

        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);
        var monster = engine.SpawnMonster(sx + 5, sy, Position.DefaultZ, new MonsterData
        {
            MonsterTypeId = 1,
            Health = 100,
            Attack = 5,
            Defense = 0,
            Speed = 8
        });

        // Start in Idle
        monster.AI.StateId = AIStates.Idle;

        engine.Tick();

        Assert.Equal(AIStates.Chase, monster.AI.StateId);
    }

    [Fact]
    public void Chase_WithCooldown_DoesNotMove()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();

        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);
        var monster = engine.SpawnMonster(sx + 3, sy, Position.DefaultZ, new MonsterData
        {
            MonsterTypeId = 1,
            Health = 100,
            Attack = 5,
            Defense = 0,
            Speed = 2
        });

        monster.AI.StateId = AIStates.Chase;
        // Set very high cooldown
        monster.MoveDelay.Current = 100;

        int origX = monster.X;
        int origY = monster.Y;

        engine.Tick();

        // Tick decrements monster.MoveDelay by 1, but 99 >> 0, so monster stays put
        Assert.Equal(origX, monster.X);
        Assert.Equal(origY, monster.Y);
    }

    [Fact]
    public void Idle_PlayerOnAdjacentZ_DoesntTransitionsToChase()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();

        // Load the Z-1 chunk so both levels exist
        engine.EnsureChunkLoaded(0, 0, Position.DefaultZ - 1);

        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ - 1, ClassDefinitions.Warrior);
        var monster = engine.SpawnMonster(sx + 3, sy, Position.DefaultZ, new MonsterData
        {
            MonsterTypeId = 1,
            Health = 100,
            Attack = 5,
            Defense = 0,
            Speed = 8
        });

        // Start in Idle — player is 3 XY + 1 Z = 4 Manhattan, within DetectionRange(8)
        monster.AI.StateId = AIStates.Idle;

        engine.Tick();

        Assert.Equal(AIStates.Idle, monster.AI.StateId);
    }

    [Fact]
    public void Idle_PlayerTwoZLevelsAway_StaysIdle()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();

        // Load the Z-2 chunk
        engine.EnsureChunkLoaded(0, 0, Position.DefaultZ - 2);

        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ - 2, ClassDefinitions.Warrior);
        var monster = engine.SpawnMonster(sx + 3, sy, Position.DefaultZ, new MonsterData
        {
            MonsterTypeId = 1,
            Health = 100,
            Attack = 5,
            Defense = 0,
            Speed = 8
        });

        // Start in Idle — player is 2 Z levels away, zDiff > 1, should NOT detect
        monster.AI.StateId = AIStates.Idle;

        engine.Tick();

        Assert.Equal(AIStates.Idle, monster.AI.StateId);
    }
}
