using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Tests;

public class DoorMechanicsTests
{
    private static readonly BspDungeonGenerator _gen = new(42);

    private static int ItemId(string id) => GameData.Instance.Items.GetNumericId(id);

    private GameEngine CreateEngine()
    {
        var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        return engine;
    }

    private static TileInfo MakeClosedDoor() => new()
    {
        Type = TileType.Floor,
        GlyphId = TileDefinitions.GlyphFloor,
        FgColor = TileDefinitions.ColorFloorFg,
        BgColor = TileDefinitions.ColorBlack,
        PlaceableItemId = ItemId("wooden_door"),
        PlaceableItemExtra = 0,
    };

    [Fact]
    public void DoorClosed_IsNotWalkable()
    {
        var tile = MakeClosedDoor();
        Assert.False(tile.IsWalkable);
    }

    [Fact]
    public void DoorClosed_IsNotTransparent()
    {
        var tile = MakeClosedDoor();
        Assert.False(tile.IsTransparent);
    }

    [Fact]
    public void DoorOpen_IsWalkable()
    {
        var tile = MakeClosedDoor();
        tile.PlaceableItemExtra = 1; // open
        Assert.True(tile.IsWalkable);
    }

    [Fact]
    public void DoorOpen_IsTransparent()
    {
        var tile = MakeClosedDoor();
        tile.PlaceableItemExtra = 1; // open
        Assert.True(tile.IsTransparent);
    }

    [Fact]
    public void BumpingClosedDoor_OpensIt()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        int doorX = sx + 1, doorY = sy;

        // Place a closed door (placeable on a floor tile)
        engine.WorldMap.SetTile(Position.FromCoords(doorX, doorY, Position.DefaultZ), MakeClosedDoor());

        // Move toward the door
        player.Input.ActionType = ActionTypes.Move;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        // Door should now be open (PlaceableItemExtra > 0 = ticks remaining)
        var tile = engine.WorldMap.GetTile(Position.FromCoords(doorX, doorY, Position.DefaultZ));
        Assert.Equal(ItemId("wooden_door"), tile.PlaceableItemId);
        Assert.True(GameData.Instance.Items.IsPlaceableDoorOpen(tile.PlaceableItemId, tile.PlaceableItemExtra));

        // Player should NOT have moved (bumping opens door, doesn't move through)
        Assert.Equal(sx, player.Position.X);
        Assert.Equal(sy, player.Position.Y);
    }

    [Fact]
    public void OpenDoor_AutoClosesAfterGracePeriod()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        int doorX = sx + 1, doorY = sy;

        // Make sure the tile beyond the door is floor
        engine.WorldMap.SetTile(Position.FromCoords(doorX + 1, doorY, Position.DefaultZ), new TileInfo
        {
            Type = TileType.Floor,
            GlyphId = TileDefinitions.GlyphFloor,
            FgColor = TileDefinitions.ColorFloorFg,
            BgColor = TileDefinitions.ColorBlack,
        });

        // Place a closed door
        engine.WorldMap.SetTile(Position.FromCoords(doorX, doorY, Position.DefaultZ), MakeClosedDoor());

        // Tick 1: Bump to open
        player.Input.ActionType = ActionTypes.Move;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();
        Assert.True(GameData.Instance.Items.IsPlaceableDoorOpen(
            engine.WorldMap.GetTile(Position.FromCoords(doorX, doorY, Position.DefaultZ)).PlaceableItemId,
            engine.WorldMap.GetTile(Position.FromCoords(doorX, doorY, Position.DefaultZ)).PlaceableItemExtra)); // open

        // Tick 2: Walk onto door
        player.MoveDelay.Current = 0;
        player.Input.ActionType = ActionTypes.Move;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();
        Assert.Equal(doorX, player.Position.X);

        // Tick 3: Move past door
        player.MoveDelay.Current = 0;
        player.Input.ActionType = ActionTypes.Move;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();
        Assert.Equal(doorX + 1, player.Position.X);

        // Door should still be open during grace period
        Assert.True(GameData.Instance.Items.IsPlaceableDoorOpen(
            engine.WorldMap.GetTile(Position.FromCoords(doorX, doorY, Position.DefaultZ)).PlaceableItemId,
            engine.WorldMap.GetTile(Position.FromCoords(doorX, doorY, Position.DefaultZ)).PlaceableItemExtra));

        // Tick enough times for grace period to expire (6 ticks grace from open)
        for (int i = 0; i < 30; i++)
            engine.Tick();

        // Door should have auto-closed (PlaceableItemExtra = 0)
        var doorTile = engine.WorldMap.GetTile(Position.FromCoords(doorX, doorY, Position.DefaultZ));
        Assert.Equal(0, doorTile.PlaceableItemExtra);
    }

    [Fact]
    public void DoorStaysOpenDuringGracePeriod()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        int doorX = sx + 1, doorY = sy;

        engine.WorldMap.SetTile(Position.FromCoords(doorX, doorY, Position.DefaultZ), MakeClosedDoor());

        // Bump to open
        player.Input.ActionType = ActionTypes.Move;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();
        Assert.True(GameData.Instance.Items.IsPlaceableDoorOpen(
            engine.WorldMap.GetTile(Position.FromCoords(doorX, doorY, Position.DefaultZ)).PlaceableItemId,
            engine.WorldMap.GetTile(Position.FromCoords(doorX, doorY, Position.DefaultZ)).PlaceableItemExtra));

        // One more tick — door should stay open (grace period active)
        engine.Tick();
        Assert.True(GameData.Instance.Items.IsPlaceableDoorOpen(
            engine.WorldMap.GetTile(Position.FromCoords(doorX, doorY, Position.DefaultZ)).PlaceableItemId,
            engine.WorldMap.GetTile(Position.FromCoords(doorX, doorY, Position.DefaultZ)).PlaceableItemExtra));
    }

    [Fact]
    public void Window_IsNotWalkable_IsTransparent()
    {
        var tile = new TileInfo
        {
            Type = TileType.Floor,
            PlaceableItemId = ItemId("wooden_window"),
        };
        Assert.False(tile.IsWalkable);
        Assert.True(tile.IsTransparent);
    }
}
