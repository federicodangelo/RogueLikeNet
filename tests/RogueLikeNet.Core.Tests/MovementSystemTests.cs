using Arch.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.Systems;
using RogueLikeNet.Core.World;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Core.Tests;

public class MovementSystemTests
{
    private static readonly BspDungeonGenerator _gen = new(42);

    private GameEngine CreateEngine()
    {
        var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0, Position.DefaultZ);
        return engine;
    }

    [Fact]
    public void Move_SuccessfulMove_UpdatesPosition()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);

        // Find a walkable adjacent tile
        int dx = 1, dy = 0;
        if (!engine.WorldMap.IsWalkable(sx + dx, sy + dy, Position.DefaultZ))
        {
            dx = 0; dy = 1;
        }

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.Move;
        input.TargetX = dx;
        input.TargetY = dy;

        engine.Tick();

        ref var pos = ref engine.EcsWorld.Get<Position>(player);
        Assert.Equal(sx + dx, pos.X);
        Assert.Equal(sy + dy, pos.Y);
    }

    [Fact]
    public void Move_NonMoveAction_DoesNotMove()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.None;
        input.TargetX = 1;
        input.TargetY = 0;

        engine.Tick();

        ref var pos = ref engine.EcsWorld.Get<Position>(player);
        Assert.Equal(sx, pos.X);
        Assert.Equal(sy, pos.Y);
    }

    [Fact]
    public void Move_WaitAction_DoesNotMove()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.Wait;
        input.TargetX = 1;
        input.TargetY = 0;

        engine.Tick();

        ref var pos = ref engine.EcsWorld.Get<Position>(player);
        Assert.Equal(sx, pos.X);
        Assert.Equal(sy, pos.Y);
    }

    [Fact]
    public void Move_WithCooldown_DoesNotMove()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();

        // Spawn a monster with high move delay (slow)
        var monster = engine.SpawnMonster(sx + 3, sy, Position.DefaultZ, new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 2 });

        // Give the monster movement input
        engine.EcsWorld.Add(monster, new PlayerInput { ActionType = ActionTypes.Move, TargetX = 1, TargetY = 0 });

        // Set a high cooldown
        ref var delay = ref engine.EcsWorld.Get<MoveDelay>(monster);
        delay.Current = 5;

        engine.Tick();

        ref var pos = ref engine.EcsWorld.Get<Position>(monster);
        Assert.Equal(sx + 3, pos.X); // Should not have moved
    }

    [Fact]
    public void Move_IntoWall_ClearsAction()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);

        // Find a non-walkable tile
        int wallX = -1, wallY = -1;
        var chunk = engine.EnsureChunkLoaded(0, 0, Position.DefaultZ);
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                if (chunk.Tiles[x, y].Type == TileType.Blocked)
                {
                    wallX = x; wallY = y;
                    goto found;
                }
    found:

        if (wallX >= 0)
        {
            // Teleport player adjacent to wall
            ref var pos0 = ref engine.EcsWorld.Get<Position>(player);
            int adjX = wallX - 1;
            int adjY = wallY;
            if (adjX >= 0 && chunk.Tiles[adjX, adjY].Type == TileType.Floor)
            {
                pos0.X = adjX;
                pos0.Y = adjY;

                ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
                input.ActionType = ActionTypes.Move;
                input.TargetX = 1;
                input.TargetY = 0;

                engine.Tick();

                ref var pos = ref engine.EcsWorld.Get<Position>(player);
                Assert.Equal(adjX, pos.X); // Should not have moved
            }
        }
    }

    [Fact]
    public void Move_IntoActor_ConvertsToAttack()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);
        engine.SpawnMonster(sx + 1, sy, Position.DefaultZ, new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.Move;
        input.TargetX = 1;
        input.TargetY = 0;

        engine.Tick();

        // Player should not have moved to the monster's tile
        ref var pos = ref engine.EcsWorld.Get<Position>(player);
        Assert.Equal(sx, pos.X);
        // The move system converts move into attack, and combat should produce events
        Assert.True(engine.Combat.LastTickEvents.Count > 0);
    }

    [Fact]
    public void Move_ResetsDelay_AfterSuccessfulMove()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.Move;
        input.TargetX = 1;
        input.TargetY = 0;

        // Ensure destination is walkable
        if (engine.WorldMap.IsWalkable(sx + 1, sy, Position.DefaultZ))
        {
            engine.Tick();

            ref var delay = ref engine.EcsWorld.Get<MoveDelay>(player);
            // After tick, delay was set to Interval by MovementSystem then decremented by AISystem
            Assert.True(delay.Current > 0, "Move delay should be > 0 after a successful move");
        }
    }

    [Fact]
    public void GridVelocity_ZeroVelocity_DoesNotMove()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();

        var entity = engine.EcsWorld.Create(
            new Position(sx, sy, Position.DefaultZ),
            new GridVelocity { DX = 0, DY = 0 }
        );

        engine.Tick();

        ref var pos = ref engine.EcsWorld.Get<Position>(entity);
        Assert.Equal(sx, pos.X);
        Assert.Equal(sy, pos.Y);
    }

    [Fact]
    public void GridVelocity_MovesToWalkableTile()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();

        var entity = engine.EcsWorld.Create(
            new Position(sx, sy, Position.DefaultZ),
            new GridVelocity { DX = 1, DY = 0 }
        );

        // Ensure target is walkable
        if (engine.WorldMap.IsWalkable(sx + 1, sy, Position.DefaultZ))
        {
            engine.Tick();

            ref var pos = ref engine.EcsWorld.Get<Position>(entity);
            Assert.Equal(sx + 1, pos.X);
            Assert.Equal(sy, pos.Y);

            // Velocity should be reset
            ref var vel = ref engine.EcsWorld.Get<GridVelocity>(entity);
            Assert.Equal(0, vel.DX);
            Assert.Equal(0, vel.DY);
        }
    }

    [Fact]
    public void GridVelocity_BlockedByWall_ResetsVelocity()
    {
        using var engine = CreateEngine();
        var chunk = engine.EnsureChunkLoaded(0, 0, Position.DefaultZ);

        // Find a tile adjacent to a wall
        for (int x = 1; x < Chunk.Size - 1; x++)
            for (int y = 1; y < Chunk.Size - 1; y++)
            {
                if (chunk.Tiles[x, y].Type == TileType.Floor &&
                    chunk.Tiles[x + 1, y].Type == TileType.Blocked)
                {
                    var entity = engine.EcsWorld.Create(
                        new Position(x, y, Position.DefaultZ),
                        new GridVelocity { DX = 1, DY = 0 }
                    );

                    engine.Tick();

                    ref var pos = ref engine.EcsWorld.Get<Position>(entity);
                    Assert.Equal(x, pos.X); // Shouldn't move
                    ref var vel = ref engine.EcsWorld.Get<GridVelocity>(entity);
                    Assert.Equal(0, vel.DX);
                    Assert.Equal(0, vel.DY);
                    return;
                }
            }
    }

    [Fact]
    public void GridVelocity_BlockedByActor_DoesNotMove()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();

        // Place a living actor at the target
        engine.SpawnMonster(sx + 1, sy, Position.DefaultZ, new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });

        var entity = engine.EcsWorld.Create(
            new Position(sx, sy, Position.DefaultZ),
            new GridVelocity { DX = 1, DY = 0 }
        );

        engine.Tick();

        ref var pos = ref engine.EcsWorld.Get<Position>(entity);
        Assert.Equal(sx, pos.X); // Should not move - actor occupies target
    }

    [Fact]
    public void Player_WithCooldown_PreservesActionForNextTick()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);

        // Set up a move action with a high cooldown
        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.Move;
        input.TargetX = 1;
        input.TargetY = 0;
        ref var delay = ref engine.EcsWorld.Get<MoveDelay>(player);
        delay.Current = 100; // very high cooldown, won't reach 0 even after tick decrement

        engine.Tick();

        // Player should not have moved
        ref var pos = ref engine.EcsWorld.Get<Position>(player);
        Assert.Equal(sx, pos.X);
        Assert.Equal(sy, pos.Y);

        // ActionType should still be Move (preserved for next tick)
        ref var inputAfter = ref engine.EcsWorld.Get<PlayerInput>(player);
        Assert.Equal(ActionTypes.Move, inputAfter.ActionType);
    }
}
