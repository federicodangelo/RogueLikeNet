using Arch.Core;
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
        engine.EnsureChunkLoaded(0, 0);
        return engine;
    }

    private Entity SpawnPlayerWithItem(GameEngine engine, int itemTypeId, int count = 1)
    {
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);
        ref var inv = ref engine.EcsWorld.Get<Inventory>(player);
        inv.Items!.Add(new ItemData
        {
            ItemTypeId = itemTypeId,
            Rarity = ItemDefinitions.RarityCommon,
            StackCount = count,
        });
        return player;
    }

    [Fact]
    public void PlaceItem_PlacesDoorClosed()
    {
        using var engine = CreateEngine();
        var player = SpawnPlayerWithItem(engine, ItemDefinitions.WoodenDoor);
        ref var pos = ref engine.EcsWorld.Get<Position>(player);
        int targetX = pos.X + 1, targetY = pos.Y;

        // Ensure target is floor
        engine.WorldMap.SetTile(targetX, targetY, new TileInfo
        {
            Type = TileType.Floor,
            GlyphId = TileDefinitions.GlyphFloor,
            FgColor = TileDefinitions.ColorFloorFg,
            BgColor = TileDefinitions.ColorBlack,
        });

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.PlaceItem;
        input.ItemSlot = 0;
        input.TargetX = 1;
        input.TargetY = 0;

        engine.Tick();

        var tile = engine.WorldMap.GetTile(targetX, targetY);
        Assert.Equal(TileType.DoorClosed, tile.Type);
        Assert.Equal(TileDefinitions.GlyphDoorClosed, tile.GlyphId);
    }

    [Fact]
    public void PlaceItem_PlacesWall()
    {
        using var engine = CreateEngine();
        var player = SpawnPlayerWithItem(engine, ItemDefinitions.WoodenWall);
        ref var pos = ref engine.EcsWorld.Get<Position>(player);
        int targetX = pos.X + 1, targetY = pos.Y;

        engine.WorldMap.SetTile(targetX, targetY, new TileInfo
        {
            Type = TileType.Floor,
            GlyphId = TileDefinitions.GlyphFloor,
            FgColor = TileDefinitions.ColorFloorFg,
            BgColor = TileDefinitions.ColorBlack,
        });

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.PlaceItem;
        input.ItemSlot = 0;
        input.TargetX = 1;
        input.TargetY = 0;

        engine.Tick();

        var tile = engine.WorldMap.GetTile(targetX, targetY);
        Assert.Equal(TileType.Wall, tile.Type);
    }

    [Fact]
    public void PickUpPlaced_ReturnsItemToInventory()
    {
        using var engine = CreateEngine();
        var player = SpawnPlayerWithItem(engine, ItemDefinitions.WoodenWall);
        ref var pos = ref engine.EcsWorld.Get<Position>(player);
        int targetX = pos.X + 1, targetY = pos.Y;

        // Place a wall
        engine.WorldMap.SetTile(targetX, targetY, new TileInfo
        {
            Type = TileType.Floor,
            GlyphId = TileDefinitions.GlyphFloor,
            FgColor = TileDefinitions.ColorFloorFg,
            BgColor = TileDefinitions.ColorBlack,
        });

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.PlaceItem;
        input.ItemSlot = 0;
        input.TargetX = 1;
        input.TargetY = 0;
        engine.Tick();

        // Verify wall was placed and item removed
        var tile = engine.WorldMap.GetTile(targetX, targetY);
        Assert.Equal(TileType.Wall, tile.Type);
        ref var inv = ref engine.EcsWorld.Get<Inventory>(player);
        Assert.Empty(inv.Items!);

        // Now pick it up
        ref var input2 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input2.ActionType = ActionTypes.PickUpPlaced;
        input2.TargetX = 1;
        input2.TargetY = 0;
        engine.Tick();

        // Tile should be floor again
        tile = engine.WorldMap.GetTile(targetX, targetY);
        Assert.Equal(TileType.Floor, tile.Type);

        // Item should be back in inventory
        ref var inv2 = ref engine.EcsWorld.Get<Inventory>(player);
        Assert.Single(inv2.Items!);
        Assert.Equal(ItemDefinitions.WoodenWall, inv2.Items![0].ItemTypeId);
    }

    [Fact]
    public void PickUpPlaced_DoorClosed_ReturnsItem()
    {
        using var engine = CreateEngine();
        var player = SpawnPlayerWithItem(engine, ItemDefinitions.CopperDoor);
        ref var pos = ref engine.EcsWorld.Get<Position>(player);
        int targetX = pos.X + 1, targetY = pos.Y;

        engine.WorldMap.SetTile(targetX, targetY, new TileInfo
        {
            Type = TileType.Floor,
            GlyphId = TileDefinitions.GlyphFloor,
            FgColor = TileDefinitions.ColorFloorFg,
            BgColor = TileDefinitions.ColorBlack,
        });

        // Place the door
        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.PlaceItem;
        input.ItemSlot = 0;
        input.TargetX = 1;
        input.TargetY = 0;
        engine.Tick();

        var tile = engine.WorldMap.GetTile(targetX, targetY);
        Assert.Equal(TileType.DoorClosed, tile.Type);

        // Pick it up
        ref var input2 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input2.ActionType = ActionTypes.PickUpPlaced;
        input2.TargetX = 1;
        input2.TargetY = 0;
        engine.Tick();

        tile = engine.WorldMap.GetTile(targetX, targetY);
        Assert.Equal(TileType.Floor, tile.Type);
        ref var inv = ref engine.EcsWorld.Get<Inventory>(player);
        Assert.Single(inv.Items!);
        Assert.Equal(ItemDefinitions.CopperDoor, inv.Items![0].ItemTypeId);
    }

    [Fact]
    public void PickUpPlaced_NonBuildableTile_DoesNothing()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassDefinitions.Warrior);
        int targetX = sx + 1, targetY = sy;

        // Place a natural wall (not player-placed — uses default wall color)
        engine.WorldMap.SetTile(targetX, targetY, new TileInfo
        {
            Type = TileType.Wall,
            GlyphId = TileDefinitions.GlyphWall,
            FgColor = TileDefinitions.ColorWallFg,
            BgColor = TileDefinitions.ColorBlack,
        });

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.PickUpPlaced;
        input.TargetX = 1;
        input.TargetY = 0;
        engine.Tick();

        // Should still be a wall
        var tile = engine.WorldMap.GetTile(targetX, targetY);
        Assert.Equal(TileType.Wall, tile.Type);
    }

    [Theory]
    [InlineData(ItemDefinitions.WoodenDoor, TileType.DoorClosed, TileDefinitions.GlyphDoorClosed, TileDefinitions.ColorWoodFg)]
    [InlineData(ItemDefinitions.WoodenDoor, TileType.DoorClosed, TileDefinitions.GlyphDoorVertical, TileDefinitions.ColorWoodFg)]
    [InlineData(ItemDefinitions.WoodenDoor, TileType.DoorClosed, TileDefinitions.GlyphDoorHorizontal, TileDefinitions.ColorWoodFg)]
    [InlineData(ItemDefinitions.WoodenWall, TileType.Wall, TileDefinitions.GlyphWall, TileDefinitions.ColorWoodFg)]
    [InlineData(ItemDefinitions.WoodenWindow, TileType.Window, TileDefinitions.GlyphWindow, TileDefinitions.ColorWindowFg)]
    [InlineData(ItemDefinitions.IronWall, TileType.Wall, TileDefinitions.GlyphWall, TileDefinitions.ColorIronFg)]
    public void GetItemFromTile_MatchesBuildables(int expectedItemId, TileType type, int glyphId, int fgColor)
    {
        var tile = new TileInfo { Type = type, GlyphId = glyphId, FgColor = fgColor };
        int? result = BuildingSystem.GetItemFromTile(tile);
        Assert.NotNull(result);
        Assert.Equal(expectedItemId, result!.Value);
    }

    [Fact]
    public void GetItemFromTile_OpenDoor_MatchesBuildable()
    {
        // Open doors with any door glyph should match
        var tile = new TileInfo { Type = TileType.Door, GlyphId = TileDefinitions.GlyphDoorVertical, FgColor = TileDefinitions.ColorWoodFg };
        int? result = BuildingSystem.GetItemFromTile(tile);
        Assert.NotNull(result);
        Assert.Equal(ItemDefinitions.WoodenDoor, result!.Value);
    }

    [Fact]
    public void GetItemFromTile_NaturalWall_ReturnsNull()
    {
        var tile = new TileInfo { Type = TileType.Wall, GlyphId = TileDefinitions.GlyphWall, FgColor = TileDefinitions.ColorWallFg };
        Assert.Null(BuildingSystem.GetItemFromTile(tile));
    }

    [Fact]
    public void GetItemFromTile_Floor_ReturnsNull()
    {
        var tile = new TileInfo { Type = TileType.Floor, GlyphId = TileDefinitions.GlyphFloor, FgColor = TileDefinitions.ColorFloorFg };
        Assert.Null(BuildingSystem.GetItemFromTile(tile));
    }
}
