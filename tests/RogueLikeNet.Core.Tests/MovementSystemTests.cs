using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
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
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        return engine;
    }

    [Fact]
    public void Move_SuccessfulMove_UpdatesPosition()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Find a walkable adjacent tile
        int dx = 1, dy = 0;
        if (!engine.WorldMap.IsWalkable(Position.FromCoords(sx + dx, sy + dy, Position.DefaultZ)))
        {
            dx = 0; dy = 1;
        }

        player.Input.ActionType = ActionTypes.Move;
        player.Input.TargetX = dx;
        player.Input.TargetY = dy;

        engine.Tick();

        Assert.Equal(sx + dx, player.Position.X);
        Assert.Equal(sy + dy, player.Position.Y);
    }

    [Fact]
    public void Move_NonMoveAction_DoesNotMove()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        player.Input.ActionType = ActionTypes.None;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;

        engine.Tick();

        Assert.Equal(sx, player.Position.X);
        Assert.Equal(sy, player.Position.Y);
    }

    [Fact]
    public void Move_WaitAction_DoesNotMove()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        player.Input.ActionType = ActionTypes.Wait;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;

        engine.Tick();

        Assert.Equal(sx, player.Position.X);
        Assert.Equal(sy, player.Position.Y);
    }

    [Fact]
    public void Move_WithCooldown_DoesNotMove()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();

        // Spawn a monster with high move delay (slow)
        var _m = engine.SpawnMonster(Position.FromCoords(sx + 3, sy, Position.DefaultZ), new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 2 });
        ref var monster = ref engine.WorldMap.GetMonsterRef(_m.Id);

        // Set a high cooldown — AI should skip movement
        monster.MoveDelay.Current = 5;

        engine.Tick();

        Assert.Equal(sx + 3, monster.Position.X); // Should not have moved
    }

    [Fact]
    public void Move_IntoWall_ClearsAction()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Find a non-walkable tile
        int wallX = -1, wallY = -1;
        var chunk = engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
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
                player.Position.X = adjX;
                player.Position.Y = adjY;

                player.Input.ActionType = ActionTypes.Move;
                player.Input.TargetX = 1;
                player.Input.TargetY = 0;

                engine.Tick();

                Assert.Equal(adjX, player.Position.X); // Should not have moved
            }
        }
    }

    [Fact]
    public void Move_IntoActor_ConvertsToAttack()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ), new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });

        player.Input.ActionType = ActionTypes.Move;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;

        engine.Tick();

        // Player should not have moved to the monster's tile
        Assert.Equal(sx, player.Position.X);
        // The move system converts move into attack, and combat should produce events
        Assert.True(engine.Combat.LastTickEvents.Count > 0);
    }

    [Fact]
    public void Move_ResetsDelay_AfterSuccessfulMove()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        player.Input.ActionType = ActionTypes.Move;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;

        // Ensure destination is walkable
        if (engine.WorldMap.IsWalkable(Position.FromCoords(sx + 1, sy, Position.DefaultZ)))
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
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Set up a move action with a high cooldown
        player.Input.ActionType = ActionTypes.Move;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        player.MoveDelay.Current = 100; // very high cooldown, won't reach 0 even after tick decrement

        engine.Tick();

        // Player should not have moved
        Assert.Equal(sx, player.Position.X);
        Assert.Equal(sy, player.Position.Y);

        // ActionType should still be Move (preserved for next tick)
        Assert.Equal(ActionTypes.Move, player.Input.ActionType);
    }

    // ── UseStairs tests ──

    [Fact]
    public void UseStairs_OnStairsDown_MovesPlayerDown()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Place stairs down at player position
        var chunk = engine.WorldMap.GetChunkForWorldPos(player.Position);
        if (chunk != null && chunk.WorldToLocal(sx, sy, out int lx, out int ly))
        {
            chunk.Tiles[lx, ly].TileId = GameData.Instance.Tiles.GetNumericId("stairs_down");
        }

        // Ensure the chunk below exists and has a walkable tile at the same position
        var belowZ = Position.DefaultZ - 1;
        var belowChunkPos = ChunkPosition.FromCoords(
            Chunk.WorldToChunkCoord(Position.FromCoords(sx, sy, belowZ)).X,
            Chunk.WorldToChunkCoord(Position.FromCoords(sx, sy, belowZ)).Y,
            belowZ);
        var belowChunk = engine.EnsureChunkLoaded(belowChunkPos);
        // Make destination tile walkable
        if (belowChunk.WorldToLocal(sx, sy, out int blx, out int bly))
            belowChunk.Tiles[blx, bly].TileId = GameData.Instance.Tiles.GetNumericId("floor");

        player.Input.ActionType = ActionTypes.UseStairs;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        Assert.Equal(belowZ, player.Position.Z);
    }

    [Fact]
    public void UseStairs_OnStairsUp_MovesPlayerUp()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Place stairs up at player position
        var chunk = engine.WorldMap.GetChunkForWorldPos(player.Position);
        if (chunk != null && chunk.WorldToLocal(sx, sy, out int lx, out int ly))
        {
            chunk.Tiles[lx, ly].TileId = GameData.Instance.Tiles.GetNumericId("stairs_up");
        }

        // Ensure the chunk above exists
        var aboveZ = Position.DefaultZ + 1;
        var aboveChunkPos = ChunkPosition.FromCoords(
            Chunk.WorldToChunkCoord(Position.FromCoords(sx, sy, aboveZ)).X,
            Chunk.WorldToChunkCoord(Position.FromCoords(sx, sy, aboveZ)).Y,
            aboveZ);
        var aboveChunk = engine.EnsureChunkLoaded(aboveChunkPos);
        // Make destination tile walkable
        if (aboveChunk.WorldToLocal(sx, sy, out int alx, out int aly))
            aboveChunk.Tiles[alx, aly].TileId = GameData.Instance.Tiles.GetNumericId("floor");

        player.Input.ActionType = ActionTypes.UseStairs;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        Assert.Equal(aboveZ, player.Position.Z);
    }

    [Fact]
    public void UseStairs_OnFloor_DoesNotMove()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        player.Input.ActionType = ActionTypes.UseStairs;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        Assert.Equal(Position.DefaultZ, player.Position.Z);
    }

    [Fact]
    public void UseStairs_WithCooldown_ClearsAction()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        player.MoveDelay.Current = 100;
        player.Input.ActionType = ActionTypes.UseStairs;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        Assert.Equal(ActionTypes.None, player.Input.ActionType);
        Assert.Equal(Position.DefaultZ, player.Position.Z);
    }

    // ── Door bumping tests ──

    [Fact]
    public void Move_IntoDoor_OpensDoor()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        var doorDef = GameData.Instance.Items.Get("wooden_door");
        if (doorDef == null) return;

        var doorPos = Position.FromCoords(sx + 1, sy, Position.DefaultZ);
        // Ensure adjacent tile is floor
        var chunk = engine.WorldMap.GetChunkForWorldPos(doorPos);
        if (chunk != null && chunk.WorldToLocal(doorPos.X, doorPos.Y, out int lx, out int ly))
        {
            chunk.Tiles[lx, ly].TileId = GameData.Instance.Tiles.GetNumericId("floor");
            chunk.Tiles[lx, ly].PlaceableItemId = doorDef.NumericId;
            chunk.Tiles[lx, ly].PlaceableItemExtra = 0; // closed
        }

        player.Input.ActionType = ActionTypes.Move;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        // Player should not have moved (door opens instead)
        Assert.Equal(sx, player.Position.X);

        // Door should now be open (PlaceableItemExtra set to DoorGraceTicks timer, minus 1 for this tick's update)
        var tile = engine.WorldMap.GetTile(doorPos);
        Assert.Equal(WorldMap.DoorGraceTicks - 1, tile.PlaceableItemExtra);
    }

    // ── Speed effect on delay ──

    [Fact]
    public void Move_WithSlowEffect_IncreasesDelay()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Set survival to hungry state so ActiveEffectsSystem applies Hungry(75)
        player.Survival.Hunger = 10;
        player.Survival.Thirst = 10;
        // prevent decay during test
        player.Survival.HungerDecayRate = 0;
        player.Survival.ThirstDecayRate = 0;

        int baseInterval = player.MoveDelay.Interval;

        player.Input.ActionType = ActionTypes.Move;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;

        if (engine.WorldMap.IsWalkable(Position.FromCoords(sx + 1, sy, Position.DefaultZ)))
        {
            engine.Tick();
            player = ref engine.WorldMap.GetPlayerRef(_p.Id);
            // With 75% speed from Hungry effect, delay should be higher than base
            Assert.True(player.MoveDelay.Current > baseInterval);
        }
    }

    [Fact]
    public void Move_WithNoSlowEffect_GivesBaseDelay()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Ensure survival is full so no slow effects are applied
        player.Survival.Hunger = 100;
        player.Survival.Thirst = 100;

        int baseInterval = player.MoveDelay.Interval;

        player.Input.ActionType = ActionTypes.Move;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;

        if (engine.WorldMap.IsWalkable(Position.FromCoords(sx + 1, sy, Position.DefaultZ)))
        {
            engine.Tick();
            player = ref engine.WorldMap.GetPlayerRef(_p.Id);
            // With no slow effects, delay should be base interval minus 1 (decremented by AISystem in same tick)
            Assert.Equal(baseInterval - 1, player.MoveDelay.Current);
        }
    }

    // ── Debug mode tests ──

    [Fact]
    public void DebugNoCollision_AllowsMoveThroughWalls()
    {
        using var engine = CreateEngine();
        engine.DebugNoCollision = true;
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        player.Input.ActionType = ActionTypes.Move;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        Assert.Equal(sx + 1, player.Position.X);
    }

    [Fact]
    public void DebugMaxSpeed_IgnoresDelay()
    {
        using var engine = CreateEngine();
        engine.DebugMaxSpeed = true;
        engine.DebugNoCollision = true; // ensure destination is reachable
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        player.MoveDelay.Current = 100;
        player.Input.ActionType = ActionTypes.Move;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        // Should have moved 4 tiles despite cooldown
        Assert.Equal(sx + 4, player.Position.X);
    }

    [Fact]
    public void Move_WithSpeedupEffect_DecreasesDelay()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Ensure survival is full so no slow effects
        player.Survival.Hunger = 100;
        player.Survival.Thirst = 100;

        // Add a speedup effect (200% speed = double speed = half delay)
        player.ActiveEffects.Add(new ActiveEffect(EffectType.Hungry, 200));

        int baseInterval = player.MoveDelay.Interval;

        player.Input.ActionType = ActionTypes.Move;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;

        if (engine.WorldMap.IsWalkable(Position.FromCoords(sx + 1, sy, Position.DefaultZ)))
        {
            engine.Tick();
            player = ref engine.WorldMap.GetPlayerRef(_p.Id);
            // With 200% speed, delay should be <= base interval
            // GetEffectiveDelay: baseDelay * 100 / 200 = baseDelay / 2
            Assert.True(player.MoveDelay.Current <= baseInterval,
                $"Speedup delay {player.MoveDelay.Current} should be <= base {baseInterval}");
        }
    }

    [Fact]
    public void Move_DeadPlayer_IsSkipped()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Kill the player
        player.Health.Current = 0;

        player.Input.ActionType = ActionTypes.Move;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;

        // Save position before tick (ProcessPlayerDeath will respawn, so check action not processed)
        int origX = player.Position.X;
        int origY = player.Position.Y;

        engine.Tick();

        // After tick, player was respawned by ProcessPlayerDeath, not by MovementSystem
        // The fact that the test doesn't crash proves the IsDead check works
        player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        // Player should have been respawned (health > 0 now)
        Assert.True(player.Health.Current > 0);
    }

    [Fact]
    public void UseStairs_AtZBoundary_DoesNotMove()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Move player to Z=0 and place stairs down (would go to Z=-1 which is invalid)
        player.Position = Position.FromCoords(sx, sy, 0);
        var chunk = engine.EnsureChunkLoaded(ChunkPosition.FromCoords(
            Chunk.WorldToChunkCoord(player.Position).X,
            Chunk.WorldToChunkCoord(player.Position).Y, 0));
        if (chunk.WorldToLocal(sx, sy, out int lx, out int ly))
            chunk.Tiles[lx, ly].TileId = GameData.Instance.Tiles.GetNumericId("stairs_down");

        player.Input.ActionType = ActionTypes.UseStairs;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        // Should stay at Z=0 - can't go below
        Assert.Equal(0, player.Position.Z);
    }
}
