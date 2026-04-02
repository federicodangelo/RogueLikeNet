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

        player.Input.ActionType = ActionTypes.Move;
        player.Input.TargetX = dx;
        player.Input.TargetY = dy;

        engine.Tick();

        Assert.Equal(sx + dx, player.X);
        Assert.Equal(sy + dy, player.Y);
    }

    [Fact]
    public void Move_NonMoveAction_DoesNotMove()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);

        player.Input.ActionType = ActionTypes.None;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;

        engine.Tick();

        Assert.Equal(sx, player.X);
        Assert.Equal(sy, player.Y);
    }

    [Fact]
    public void Move_WaitAction_DoesNotMove()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);

        player.Input.ActionType = ActionTypes.Wait;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;

        engine.Tick();

        Assert.Equal(sx, player.X);
        Assert.Equal(sy, player.Y);
    }

    [Fact]
    public void Move_WithCooldown_DoesNotMove()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();

        // Spawn a monster with high move delay (slow)
        var monster = engine.SpawnMonster(sx + 3, sy, Position.DefaultZ, new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 2 });

        // Set a high cooldown — AI should skip movement
        monster.MoveDelay.Current = 5;

        engine.Tick();

        Assert.Equal(sx + 3, monster.X); // Should not have moved
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
            int adjX = wallX - 1;
            int adjY = wallY;
            if (adjX >= 0 && chunk.Tiles[adjX, adjY].Type == TileType.Floor)
            {
                player.X = adjX;
                player.Y = adjY;

                player.Input.ActionType = ActionTypes.Move;
                player.Input.TargetX = 1;
                player.Input.TargetY = 0;

                engine.Tick();

                Assert.Equal(adjX, player.X); // Should not have moved
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

        player.Input.ActionType = ActionTypes.Move;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;

        engine.Tick();

        // Player should not have moved to the monster's tile
        Assert.Equal(sx, player.X);
        // The move system converts move into attack, and combat should produce events
        Assert.True(engine.Combat.LastTickEvents.Count > 0);
    }

    [Fact]
    public void Move_ResetsDelay_AfterSuccessfulMove()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);

        player.Input.ActionType = ActionTypes.Move;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;

        // Ensure destination is walkable
        if (engine.WorldMap.IsWalkable(sx + 1, sy, Position.DefaultZ))
        {
            engine.Tick();

            // After tick, player.MoveDelay was set to Interval by MovementSystem then decremented by AISystem
            Assert.True(player.MoveDelay.Current > 0, "Move player.MoveDelay should be > 0 after a successful move");
        }
    }

    [Fact]
    public void Player_WithCooldown_PreservesActionForNextTick()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);

        // Set up a move action with a high cooldown
        player.Input.ActionType = ActionTypes.Move;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        player.MoveDelay.Current = 100; // very high cooldown, won't reach 0 even after tick decrement

        engine.Tick();

        // Player should not have moved
        Assert.Equal(sx, player.X);
        Assert.Equal(sy, player.Y);

        // ActionType should still be Move (preserved for next tick)
        Assert.Equal(ActionTypes.Move, player.Input.ActionType);
    }
}
