using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.Systems;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Tests;

public class BuildingSystemTests
{
    private static readonly BspDungeonGenerator _gen = new(42);

    private GameEngine CreateEngine()
    {
        var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        return engine;
    }

    private int SpawnPlayerWithItem(GameEngine engine, int itemTypeId, int count = 1)
    {
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        player.Inventory.Items.Add(new ItemData
        {
            ItemTypeId = itemTypeId,
            StackCount = count,
        });
        return _p.Id;
    }

    [Fact]
    public void PlaceItem_PlacesDoorClosed()
    {
        using var engine = CreateEngine();
        var _pid = SpawnPlayerWithItem(engine, ItemDefinitions.WoodenDoor);
        ref var player = ref engine.WorldMap.GetPlayerRef(_pid);
        int targetX = player.Position.X + 1, targetY = player.Position.Y;

        // Ensure target is floor
        engine.WorldMap.SetTile(Position.FromCoords(targetX, targetY, Position.DefaultZ), new TileInfo
        {
            Type = TileType.Floor,
            GlyphId = TileDefinitions.GlyphFloor,
            FgColor = TileDefinitions.ColorFloorFg,
            BgColor = TileDefinitions.ColorBlack,
        });

        player.Input.ActionType = ActionTypes.PlaceItem;
        player.Input.ItemSlot = 0;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;

        engine.Tick();

        var tile = engine.WorldMap.GetTile(Position.FromCoords(targetX, targetY, Position.DefaultZ));
        Assert.Equal(TileType.Floor, tile.Type); // base tile unchanged
        Assert.Equal(ItemDefinitions.WoodenDoor, tile.PlaceableItemId);
        Assert.Equal(0, tile.PlaceableItemExtra); // closed
    }

    [Fact]
    public void PlaceItem_PlacesWall()
    {
        using var engine = CreateEngine();
        var _pid = SpawnPlayerWithItem(engine, ItemDefinitions.WoodenWall);
        ref var player = ref engine.WorldMap.GetPlayerRef(_pid);
        int targetX = player.Position.X + 1, targetY = player.Position.Y;

        engine.WorldMap.SetTile(Position.FromCoords(targetX, targetY, Position.DefaultZ), new TileInfo
        {
            Type = TileType.Floor,
            GlyphId = TileDefinitions.GlyphFloor,
            FgColor = TileDefinitions.ColorFloorFg,
            BgColor = TileDefinitions.ColorBlack,
        });

        player.Input.ActionType = ActionTypes.PlaceItem;
        player.Input.ItemSlot = 0;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;

        engine.Tick();

        var tile = engine.WorldMap.GetTile(Position.FromCoords(targetX, targetY, Position.DefaultZ));
        Assert.Equal(TileType.Floor, tile.Type);
        Assert.Equal(ItemDefinitions.WoodenWall, tile.PlaceableItemId);
    }

    [Fact]
    public void PickUpPlaced_ReturnsItemToInventory()
    {
        using var engine = CreateEngine();
        var _pid = SpawnPlayerWithItem(engine, ItemDefinitions.WoodenWall);
        ref var player = ref engine.WorldMap.GetPlayerRef(_pid);
        int targetX = player.Position.X + 1, targetY = player.Position.Y;

        // Place a wall
        engine.WorldMap.SetTile(Position.FromCoords(targetX, targetY, Position.DefaultZ), new TileInfo
        {
            Type = TileType.Floor,
            GlyphId = TileDefinitions.GlyphFloor,
            FgColor = TileDefinitions.ColorFloorFg,
            BgColor = TileDefinitions.ColorBlack,
        });

        player.Input.ActionType = ActionTypes.PlaceItem;
        player.Input.ItemSlot = 0;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_pid);
        // Verify wall was placed and item removed
        var tile = engine.WorldMap.GetTile(Position.FromCoords(targetX, targetY, Position.DefaultZ));
        Assert.Equal(ItemDefinitions.WoodenWall, tile.PlaceableItemId);
        Assert.Empty(player.Inventory.Items);

        // Now pick it up
        player.Input.ActionType = ActionTypes.PickUpPlaced;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_pid);
        // Tile should be floor again (placeable removed)
        tile = engine.WorldMap.GetTile(Position.FromCoords(targetX, targetY, Position.DefaultZ));
        Assert.Equal(TileType.Floor, tile.Type);
        Assert.Equal(ItemDefinitions.None, tile.PlaceableItemId);

        // Item should be back in inventory
        Assert.Single(player.Inventory.Items);
        Assert.Equal(ItemDefinitions.WoodenWall, player.Inventory.Items[0].ItemTypeId);
    }

    [Fact]
    public void PickUpPlaced_DoorClosed_ReturnsItem()
    {
        using var engine = CreateEngine();
        var _pid = SpawnPlayerWithItem(engine, ItemDefinitions.CopperDoor);
        ref var player = ref engine.WorldMap.GetPlayerRef(_pid);
        int targetX = player.Position.X + 1, targetY = player.Position.Y;

        engine.WorldMap.SetTile(Position.FromCoords(targetX, targetY, Position.DefaultZ), new TileInfo
        {
            Type = TileType.Floor,
            GlyphId = TileDefinitions.GlyphFloor,
            FgColor = TileDefinitions.ColorFloorFg,
            BgColor = TileDefinitions.ColorBlack,
        });

        // Place the door
        player.Input.ActionType = ActionTypes.PlaceItem;
        player.Input.ItemSlot = 0;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();
        player = ref engine.WorldMap.GetPlayerRef(_pid);

        var tile = engine.WorldMap.GetTile(Position.FromCoords(targetX, targetY, Position.DefaultZ));
        Assert.Equal(ItemDefinitions.CopperDoor, tile.PlaceableItemId);
        Assert.Equal(0, tile.PlaceableItemExtra);

        // Pick it up
        player.Input.ActionType = ActionTypes.PickUpPlaced;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();
        player = ref engine.WorldMap.GetPlayerRef(_pid);

        tile = engine.WorldMap.GetTile(Position.FromCoords(targetX, targetY, Position.DefaultZ));
        Assert.Equal(TileType.Floor, tile.Type);
        Assert.Single(player.Inventory.Items);
        Assert.Equal(ItemDefinitions.CopperDoor, player.Inventory.Items[0].ItemTypeId);
    }

    [Fact]
    public void PickUpPlaced_NonBuildableTile_DoesNothing()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        int targetX = sx + 1, targetY = sy;

        // Place a natural wall (not player-placed — Blocked terrain type)
        engine.WorldMap.SetTile(Position.FromCoords(targetX, targetY, Position.DefaultZ), new TileInfo
        {
            Type = TileType.Blocked,
            GlyphId = TileDefinitions.GlyphWall,
            FgColor = TileDefinitions.ColorWallFg,
            BgColor = TileDefinitions.ColorBlack,
        });

        player.Input.ActionType = ActionTypes.PickUpPlaced;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        // Should still be a wall
        var tile = engine.WorldMap.GetTile(Position.FromCoords(targetX, targetY, Position.DefaultZ));
        Assert.Equal(TileType.Blocked, tile.Type);
    }

    [Fact]
    public void PlaceableItemId_SetOnPlacedTile()
    {
        var tile = new TileInfo { Type = TileType.Floor, PlaceableItemId = ItemDefinitions.WoodenWall };
        Assert.Equal(ItemDefinitions.WoodenWall, tile.PlaceableItemId);
    }

    [Fact]
    public void PlaceableItemId_NoneForNaturalTile()
    {
        var tile = new TileInfo { Type = TileType.Blocked, GlyphId = TileDefinitions.GlyphWall, FgColor = TileDefinitions.ColorWallFg };
        Assert.Equal(ItemDefinitions.None, tile.PlaceableItemId);
    }

    [Fact]
    public void PlaceableItemId_NoneForFloor()
    {
        var tile = new TileInfo { Type = TileType.Floor, GlyphId = TileDefinitions.GlyphFloor, FgColor = TileDefinitions.ColorFloorFg };
        Assert.Equal(ItemDefinitions.None, tile.PlaceableItemId);
    }
}
