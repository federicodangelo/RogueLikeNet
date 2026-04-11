using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Tests;

public class TileInfoTests
{
    private static int TId(string id) => GameData.Instance.Tiles.GetNumericId(id);

    [Fact]
    public void Floor_IsWalkable()
    {
        var tile = new TileInfo { TileId = TId("floor") };
        Assert.True(tile.IsWalkable);
    }

    [Fact]
    public void Blocked_IsNotWalkable()
    {
        var tile = new TileInfo { TileId = TId("wall") };
        Assert.False(tile.IsWalkable);
    }

    [Fact]
    public void StairsDown_IsWalkable()
    {
        var tile = new TileInfo { TileId = TId("stairs_down") };
        Assert.True(tile.IsWalkable);
    }

    [Fact]
    public void StairsUp_IsWalkable()
    {
        var tile = new TileInfo { TileId = TId("stairs_up") };
        Assert.True(tile.IsWalkable);
    }

    [Fact]
    public void Void_IsNotWalkable()
    {
        var tile = new TileInfo { TileId = TId("void") };
        Assert.False(tile.IsWalkable);
    }

    [Fact]
    public void Water_IsNotWalkable()
    {
        var tile = new TileInfo { TileId = TId("ice_water") };
        Assert.False(tile.IsWalkable);
    }

    [Fact]
    public void Lava_IsNotWalkable()
    {
        var tile = new TileInfo { TileId = TId("lava_lava") };
        Assert.False(tile.IsWalkable);
    }

    [Fact]
    public void Floor_IsTransparent()
    {
        var tile = new TileInfo { TileId = TId("floor") };
        Assert.True(tile.IsTransparent);
    }

    [Fact]
    public void Blocked_IsNotTransparent()
    {
        var tile = new TileInfo { TileId = TId("wall") };
        Assert.False(tile.IsTransparent);
    }

    [Fact]
    public void Void_IsTransparent()
    {
        var tile = new TileInfo { TileId = TId("void") };
        Assert.True(tile.IsTransparent);
    }

    [Fact]
    public void HasPlaceable_FalseWhenNoItem()
    {
        var tile = new TileInfo { TileId = TId("floor") };
        Assert.False(tile.HasPlaceable);
    }

    [Fact]
    public void HasPlaceable_TrueWhenItemPlaced()
    {
        var tile = new TileInfo { TileId = TId("floor"), PlaceableItemId = 1 };
        Assert.True(tile.HasPlaceable);
    }

    [Fact]
    public void Floor_WithClosedDoor_IsNotWalkable()
    {
        var doorDef = GameData.Instance.Items.Get("wooden_door");
        if (doorDef == null) return;
        var tile = new TileInfo
        {
            TileId = TId("floor"),
            PlaceableItemId = doorDef.NumericId,
            PlaceableItemExtra = 0 // closed
        };
        Assert.False(tile.IsWalkable);
    }

    [Fact]
    public void Floor_WithOpenDoor_IsWalkable()
    {
        var doorDef = GameData.Instance.Items.Get("wooden_door");
        if (doorDef == null) return;
        var tile = new TileInfo
        {
            TileId = TId("floor"),
            PlaceableItemId = doorDef.NumericId,
            PlaceableItemExtra = 1 // open
        };
        Assert.True(tile.IsWalkable);
    }

    [Fact]
    public void Floor_WithClosedDoor_IsNotTransparent()
    {
        var doorDef = GameData.Instance.Items.Get("wooden_door");
        if (doorDef == null) return;
        var tile = new TileInfo
        {
            TileId = TId("floor"),
            PlaceableItemId = doorDef.NumericId,
            PlaceableItemExtra = 0 // closed
        };
        Assert.False(tile.IsTransparent);
    }

    [Fact]
    public void Floor_WithOpenDoor_IsTransparent()
    {
        var doorDef = GameData.Instance.Items.Get("wooden_door");
        if (doorDef == null) return;
        var tile = new TileInfo
        {
            TileId = TId("floor"),
            PlaceableItemId = doorDef.NumericId,
            PlaceableItemExtra = 1 // open
        };
        Assert.True(tile.IsTransparent);
    }

    [Fact]
    public void Floor_WithWall_IsNotWalkable()
    {
        var wallDef = GameData.Instance.Items.Get("stone_wall");
        if (wallDef == null) return;
        var tile = new TileInfo
        {
            TileId = TId("floor"),
            PlaceableItemId = wallDef.NumericId,
        };
        Assert.False(tile.IsWalkable);
    }

    [Fact]
    public void TileId_ResolvesCorrectType()
    {
        Assert.Equal(TileType.Floor, new TileInfo { TileId = TId("floor") }.Type);
        Assert.Equal(TileType.Blocked, new TileInfo { TileId = TId("wall") }.Type);
        Assert.Equal(TileType.StairsDown, new TileInfo { TileId = TId("stairs_down") }.Type);
        Assert.Equal(TileType.StairsUp, new TileInfo { TileId = TId("stairs_up") }.Type);
        Assert.Equal(TileType.Void, new TileInfo { TileId = TId("void") }.Type);
    }
}
