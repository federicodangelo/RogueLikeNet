using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Core.Tests;

public class InventoryComponentTests
{
    [Fact]
    public void Inventory_IsFull_WhenAtCapacity()
    {
        var inv = new Inventory(2);
        inv.Items!.Add(new ItemData { ItemTypeId = 1 });
        inv.Items.Add(new ItemData { ItemTypeId = 2 });
        Assert.True(inv.IsFull);
    }

    [Fact]
    public void Inventory_IsNotFull_WhenBelowCapacity()
    {
        var inv = new Inventory(5);
        Assert.False(inv.IsFull);
    }

    [Fact]
    public void Inventory_NullItems_IsNotFull()
    {
        var inv = new Inventory { Capacity = 5 };
        Assert.False(inv.IsFull);
    }

    [Fact]
    public void Equipment_HasWeapon_ReturnsFalseWhenNull()
    {
        var equip = new Equipment { Weapon = ItemData.None };
        Assert.False(equip.HasWeapon);
    }

    [Fact]
    public void Equipment_HasArmor_ReturnsFalseWhenNull()
    {
        var equip = new Equipment { Chest = ItemData.None };
        Assert.False(equip.HasArmor);
    }

    [Fact]
    public void Equipment_HasWeapon_ReturnsTrueWhenSet()
    {
        var equip = new Equipment { Weapon = new ItemData { ItemTypeId = 1, StackCount = 1 } };
        Assert.True(equip.HasWeapon);
    }

    [Fact]
    public void Equipment_HasArmor_ReturnsTrueWhenSet()
    {
        var equip = new Equipment { Chest = new ItemData { ItemTypeId = 2, StackCount = 1 } };
        Assert.True(equip.HasArmor);
    }
}
