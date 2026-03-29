using Arch.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Tests;

public class DoorMechanicsTests
{
    private static readonly BspDungeonGenerator _gen = new(42);

    private GameEngine CreateEngine()
    {
        var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0);
        return engine;
    }

    [Fact]
    public void DoorClosed_IsNotWalkable()
    {
        var tile = new TileInfo { Type = TileType.DoorClosed };
        Assert.False(tile.IsWalkable);
    }

    [Fact]
    public void DoorClosed_IsNotTransparent()
    {
        var tile = new TileInfo { Type = TileType.DoorClosed };
        Assert.False(tile.IsTransparent);
    }

    [Fact]
    public void DoorOpen_IsWalkable()
    {
        var tile = new TileInfo { Type = TileType.Door };
        Assert.True(tile.IsWalkable);
    }

    [Fact]
    public void DoorOpen_IsTransparent()
    {
        var tile = new TileInfo { Type = TileType.Door };
        Assert.True(tile.IsTransparent);
    }

    [Fact]
    public void BumpingClosedDoor_OpensIt()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        int doorX = sx + 1, doorY = sy;

        // Place a closed door
        engine.WorldMap.SetTile(doorX, doorY, new TileInfo
        {
            Type = TileType.DoorClosed,
            GlyphId = TileDefinitions.GlyphDoorClosed,
            FgColor = TileDefinitions.ColorWoodFg,
            BgColor = TileDefinitions.ColorBlack,
        });

        // Move toward the door
        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.Move;
        input.TargetX = 1;
        input.TargetY = 0;
        engine.Tick();

        // Door should now be open
        var tile = engine.WorldMap.GetTile(doorX, doorY);
        Assert.Equal(TileType.Door, tile.Type);
        Assert.Equal(TileDefinitions.GlyphDoor, tile.GlyphId);
        Assert.Equal(TileDefinitions.ColorWoodFg, tile.FgColor);

        // Player should NOT have moved (bumping opens door, doesn't move through)
        ref var pos = ref engine.EcsWorld.Get<Position>(player);
        Assert.Equal(sx, pos.X);
        Assert.Equal(sy, pos.Y);
    }

    [Fact]
    public void OpenDoor_AutoClosesAfterGracePeriod()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        int doorX = sx + 1, doorY = sy;

        // Make sure the tile beyond the door is floor
        engine.WorldMap.SetTile(doorX + 1, doorY, new TileInfo
        {
            Type = TileType.Floor,
            GlyphId = TileDefinitions.GlyphFloor,
            FgColor = TileDefinitions.ColorFloorFg,
            BgColor = TileDefinitions.ColorBlack,
        });

        // Place a closed door
        engine.WorldMap.SetTile(doorX, doorY, new TileInfo
        {
            Type = TileType.DoorClosed,
            GlyphId = TileDefinitions.GlyphDoorClosed,
            FgColor = TileDefinitions.ColorWoodFg,
            BgColor = TileDefinitions.ColorBlack,
        });

        // Tick 1: Bump to open
        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.Move;
        input.TargetX = 1;
        input.TargetY = 0;
        engine.Tick();
        Assert.Equal(TileType.Door, engine.WorldMap.GetTile(doorX, doorY).Type);

        // Tick 2: Walk onto door
        ref var delay = ref engine.EcsWorld.Get<MoveDelay>(player);
        delay.Current = 0;
        ref var input2 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input2.ActionType = ActionTypes.Move;
        input2.TargetX = 1;
        input2.TargetY = 0;
        engine.Tick();
        ref var pos = ref engine.EcsWorld.Get<Position>(player);
        Assert.Equal(doorX, pos.X);

        // Tick 3: Move past door
        ref var delay2 = ref engine.EcsWorld.Get<MoveDelay>(player);
        delay2.Current = 0;
        ref var input3 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input3.ActionType = ActionTypes.Move;
        input3.TargetX = 1;
        input3.TargetY = 0;
        engine.Tick();
        ref var pos2 = ref engine.EcsWorld.Get<Position>(player);
        Assert.Equal(doorX + 1, pos2.X);

        // Door should still be open during grace period
        Assert.Equal(TileType.Door, engine.WorldMap.GetTile(doorX, doorY).Type);

        // Tick enough times for grace period to expire (6 ticks grace from open)
        for (int i = 0; i < 10; i++)
            engine.Tick();

        // Door should have auto-closed
        var doorTile = engine.WorldMap.GetTile(doorX, doorY);
        Assert.Equal(TileType.DoorClosed, doorTile.Type);
    }

    [Fact]
    public void DoorStaysOpenDuringGracePeriod()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);

        int doorX = sx + 1, doorY = sy;

        engine.WorldMap.SetTile(doorX, doorY, new TileInfo
        {
            Type = TileType.DoorClosed,
            GlyphId = TileDefinitions.GlyphDoorClosed,
            FgColor = TileDefinitions.ColorWoodFg,
            BgColor = TileDefinitions.ColorBlack,
        });

        // Bump to open
        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.Move;
        input.TargetX = 1;
        input.TargetY = 0;
        engine.Tick();
        Assert.Equal(TileType.Door, engine.WorldMap.GetTile(doorX, doorY).Type);

        // One more tick — door should stay open (grace period active)
        engine.Tick();
        Assert.Equal(TileType.Door, engine.WorldMap.GetTile(doorX, doorY).Type);
    }

    [Fact]
    public void Window_IsNotWalkable_IsTransparent()
    {
        var tile = new TileInfo { Type = TileType.Window };
        Assert.False(tile.IsWalkable);
        Assert.True(tile.IsTransparent);
    }
}
