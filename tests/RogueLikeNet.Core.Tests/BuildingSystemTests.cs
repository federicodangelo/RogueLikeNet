using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.Systems;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Tests;

public class BuildingSystemTests
{
    private static readonly BspDungeonGenerator _gen = new(42);

    private static int ItemId(string id) => GameData.Instance.Items.GetNumericId(id);

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
        var _pid = SpawnPlayerWithItem(engine, ItemId("wooden_door"));
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
        Assert.Equal(ItemId("wooden_door"), tile.PlaceableItemId);
        Assert.Equal(0, tile.PlaceableItemExtra); // closed
    }

    [Fact]
    public void PlaceItem_PlacesWall()
    {
        using var engine = CreateEngine();
        var _pid = SpawnPlayerWithItem(engine, ItemId("wooden_wall"));
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
        Assert.Equal(ItemId("wooden_wall"), tile.PlaceableItemId);
    }

    [Fact]
    public void PickUpPlaced_ReturnsItemToInventory()
    {
        using var engine = CreateEngine();
        var _pid = SpawnPlayerWithItem(engine, ItemId("wooden_wall"));
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
        Assert.Equal(ItemId("wooden_wall"), tile.PlaceableItemId);
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
        Assert.Equal(0, tile.PlaceableItemId);

        // Item should be back in inventory
        Assert.Single(player.Inventory.Items);
        Assert.Equal(ItemId("wooden_wall"), player.Inventory.Items[0].ItemTypeId);
    }

    [Fact]
    public void PickUpPlaced_DoorClosed_ReturnsItem()
    {
        using var engine = CreateEngine();
        var _pid = SpawnPlayerWithItem(engine, ItemId("copper_door"));
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
        Assert.Equal(ItemId("copper_door"), tile.PlaceableItemId);
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
        Assert.Equal(ItemId("copper_door"), player.Inventory.Items[0].ItemTypeId);
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
        var tile = new TileInfo { Type = TileType.Floor, PlaceableItemId = ItemId("wooden_wall") };
        Assert.Equal(ItemId("wooden_wall"), tile.PlaceableItemId);
    }

    [Fact]
    public void PlaceableItemId_NoneForNaturalTile()
    {
        var tile = new TileInfo { Type = TileType.Blocked, GlyphId = TileDefinitions.GlyphWall, FgColor = TileDefinitions.ColorWallFg };
        Assert.Equal(0, tile.PlaceableItemId);
    }

    [Fact]
    public void PlaceableItemId_NoneForFloor()
    {
        var tile = new TileInfo { Type = TileType.Floor, GlyphId = TileDefinitions.GlyphFloor, FgColor = TileDefinitions.ColorFloorFg };
        Assert.Equal(0, tile.PlaceableItemId);
    }

    // ── Invalid slot placement ──

    [Fact]
    public void PlaceItem_InvalidSlot_DoesNothing()
    {
        using var engine = CreateEngine();
        var _pid = SpawnPlayerWithItem(engine, ItemId("wooden_door"));
        ref var player = ref engine.WorldMap.GetPlayerRef(_pid);

        player.Input.ActionType = ActionTypes.PlaceItem;
        player.Input.ItemSlot = -1; // invalid slot
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_pid);
        Assert.Single(player.Inventory.Items); // Item still in inventory
    }

    [Fact]
    public void PlaceItem_OutOfRangeSlot_DoesNothing()
    {
        using var engine = CreateEngine();
        var _pid = SpawnPlayerWithItem(engine, ItemId("wooden_door"));
        ref var player = ref engine.WorldMap.GetPlayerRef(_pid);

        player.Input.ActionType = ActionTypes.PlaceItem;
        player.Input.ItemSlot = 99; // out of range
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_pid);
        Assert.Single(player.Inventory.Items);
    }

    // ── Non-placeable item ──

    [Fact]
    public void PlaceItem_NonPlaceable_DoesNothing()
    {
        using var engine = CreateEngine();
        var _pid = SpawnPlayerWithItem(engine, ItemId("short_sword")); // sword is not placeable
        ref var player = ref engine.WorldMap.GetPlayerRef(_pid);

        int targetX = player.Position.X + 1;
        int targetY = player.Position.Y;
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
        Assert.Single(player.Inventory.Items); // Still there
    }

    // ── Non-adjacent placement ──

    [Fact]
    public void PlaceItem_NonAdjacent_DoesNothing()
    {
        using var engine = CreateEngine();
        var _pid = SpawnPlayerWithItem(engine, ItemId("wooden_door"));
        ref var player = ref engine.WorldMap.GetPlayerRef(_pid);

        // Try placing 2 tiles away (diagonal = non-adjacent for 4-directional placement)
        player.Input.ActionType = ActionTypes.PlaceItem;
        player.Input.ItemSlot = 0;
        player.Input.TargetX = 1;
        player.Input.TargetY = 1; // diagonal - not in AdjacentOffsets
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_pid);
        Assert.Single(player.Inventory.Items);
    }

    // ── Entity occupation blocking ──

    [Fact]
    public void PlaceItem_EntityOccupied_DoesNothing()
    {
        using var engine = CreateEngine();
        var _pid = SpawnPlayerWithItem(engine, ItemId("wooden_wall"));
        ref var player = ref engine.WorldMap.GetPlayerRef(_pid);

        int targetX = player.Position.X + 1;
        int targetY = player.Position.Y;
        var targetPos = Position.FromCoords(targetX, targetY, Position.DefaultZ);

        // Ensure target is floor
        engine.WorldMap.SetTile(targetPos, new TileInfo
        {
            Type = TileType.Floor,
            GlyphId = TileDefinitions.GlyphFloor,
            FgColor = TileDefinitions.ColorFloorFg,
            BgColor = TileDefinitions.ColorBlack,
        });

        // Place a monster at the target
        engine.SpawnMonster(targetPos, new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 1, Defense = 0, Speed = 1 });

        player.Input.ActionType = ActionTypes.PlaceItem;
        player.Input.ItemSlot = 0;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_pid);
        Assert.Single(player.Inventory.Items); // Could not place
    }

    // ── Place on wall ──

    [Fact]
    public void PlaceItem_OnWall_DoesNothing()
    {
        using var engine = CreateEngine();
        var _pid = SpawnPlayerWithItem(engine, ItemId("wooden_wall"));
        ref var player = ref engine.WorldMap.GetPlayerRef(_pid);

        int targetX = player.Position.X + 1;
        int targetY = player.Position.Y;

        // Set target to wall
        engine.WorldMap.SetTile(Position.FromCoords(targetX, targetY, Position.DefaultZ), new TileInfo
        {
            Type = TileType.Blocked,
            GlyphId = TileDefinitions.GlyphWall,
            FgColor = TileDefinitions.ColorWallFg,
            BgColor = TileDefinitions.ColorBlack,
        });

        player.Input.ActionType = ActionTypes.PlaceItem;
        player.Input.ItemSlot = 0;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_pid);
        Assert.Single(player.Inventory.Items);
    }

    // ── PickUpPlaced ground items fallback ──

    [Fact]
    public void PickUpPlaced_GroundItem_WhenNoPlaceable()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Place a ground item at adjacent tile
        var targetPos = Position.FromCoords(sx + 1, sy, Position.DefaultZ);
        engine.WorldMap.SetTile(targetPos, new TileInfo
        {
            Type = TileType.Floor,
            GlyphId = TileDefinitions.GlyphFloor,
            FgColor = TileDefinitions.ColorFloorFg,
            BgColor = TileDefinitions.ColorBlack,
        });

        var template = GameData.Instance.Items.Get("short_sword")!;
        engine.SpawnItemOnGround(template, targetPos);

        player.Input.ActionType = ActionTypes.PickUpPlaced;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        // Ground item should be picked up
        Assert.True(player.Inventory.Items.Count > 0);
    }

    // ── PickUpPlaced with full inventory ──

    [Fact]
    public void PickUpPlaced_FullInventory_DoesNothing()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Fill inventory
        for (int i = 0; i < player.Inventory.Capacity; i++)
            player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("short_sword"), StackCount = 1 });

        player.Input.ActionType = ActionTypes.PickUpPlaced;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick(); // no crash, does nothing
    }

    // ── Place item with stack > 1 decrements stack ──

    [Fact]
    public void PlaceItem_StackGreaterThanOne_Decrements()
    {
        using var engine = CreateEngine();
        var _pid = SpawnPlayerWithItem(engine, ItemId("wooden_wall"), count: 3);
        ref var player = ref engine.WorldMap.GetPlayerRef(_pid);

        int targetX = player.Position.X + 1;
        int targetY = player.Position.Y;
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
        Assert.Single(player.Inventory.Items);
        Assert.Equal(2, player.Inventory.Items[0].StackCount); // 3 - 1 = 2
    }
}
