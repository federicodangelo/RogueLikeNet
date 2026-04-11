using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Tests;

public class TileInfoTests
{
    [Fact]
    public void Floor_IsWalkable()
    {
        var tile = new TileInfo { Type = TileType.Floor };
        Assert.True(tile.IsWalkable);
    }

    [Fact]
    public void Blocked_IsNotWalkable()
    {
        var tile = new TileInfo { Type = TileType.Blocked };
        Assert.False(tile.IsWalkable);
    }

    [Fact]
    public void StairsDown_IsWalkable()
    {
        var tile = new TileInfo { Type = TileType.StairsDown };
        Assert.True(tile.IsWalkable);
    }

    [Fact]
    public void StairsUp_IsWalkable()
    {
        var tile = new TileInfo { Type = TileType.StairsUp };
        Assert.True(tile.IsWalkable);
    }

    [Fact]
    public void Void_IsNotWalkable()
    {
        var tile = new TileInfo { Type = TileType.Void };
        Assert.False(tile.IsWalkable);
    }

    [Fact]
    public void Water_IsNotWalkable()
    {
        var tile = new TileInfo { Type = TileType.Water };
        Assert.False(tile.IsWalkable);
    }

    [Fact]
    public void Lava_IsNotWalkable()
    {
        var tile = new TileInfo { Type = TileType.Lava };
        Assert.False(tile.IsWalkable);
    }

    [Fact]
    public void Floor_IsTransparent()
    {
        var tile = new TileInfo { Type = TileType.Floor };
        Assert.True(tile.IsTransparent);
    }

    [Fact]
    public void Blocked_IsNotTransparent()
    {
        var tile = new TileInfo { Type = TileType.Blocked };
        Assert.False(tile.IsTransparent);
    }

    [Fact]
    public void Void_IsTransparent()
    {
        var tile = new TileInfo { Type = TileType.Void };
        Assert.True(tile.IsTransparent);
    }

    [Fact]
    public void HasPlaceable_FalseWhenNoItem()
    {
        var tile = new TileInfo { Type = TileType.Floor };
        Assert.False(tile.HasPlaceable);
    }

    [Fact]
    public void HasPlaceable_TrueWhenItemPlaced()
    {
        var tile = new TileInfo { Type = TileType.Floor, PlaceableItemId = 1 };
        Assert.True(tile.HasPlaceable);
    }

    [Fact]
    public void Floor_WithClosedDoor_IsNotWalkable()
    {
        var doorDef = GameData.Instance.Items.Get("wooden_door");
        if (doorDef == null) return;
        var tile = new TileInfo
        {
            Type = TileType.Floor,
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
            Type = TileType.Floor,
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
            Type = TileType.Floor,
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
            Type = TileType.Floor,
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
            Type = TileType.Floor,
            PlaceableItemId = wallDef.NumericId,
        };
        Assert.False(tile.IsWalkable);
    }
}
