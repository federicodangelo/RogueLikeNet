using Arch.Core;
using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Core.Tests;

public class InventoryComponentTests
{
    [Fact]
    public void Inventory_IsFull_WhenAtCapacity()
    {
        var world = Arch.Core.World.Create();
        try
        {
            var inv = new Inventory(2);
            var e1 = world.Create(new Position(0, 0));
            var e2 = world.Create(new Position(1, 1));
            inv.Items!.Add(e1);
            inv.Items.Add(e2);
            Assert.True(inv.IsFull);
        }
        finally
        {
            Arch.Core.World.Destroy(world);
        }
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
        var inv = new Inventory { Capacity = 5, Items = null };
        Assert.False(inv.IsFull);
    }

    [Fact]
    public void Equipment_HasWeapon_ReturnsTrueWhenEntityNull()
    {
        // In Arch ECS v2, Entity.Null is a specific value (not default(Entity))
        var equip = new Equipment { Weapon = Entity.Null };
        Assert.False(equip.HasWeapon);
    }

    [Fact]
    public void Equipment_HasArmor_ReturnsFalseWhenEntityNull()
    {
        var equip = new Equipment { Armor = Entity.Null };
        Assert.False(equip.HasArmor);
    }

    [Fact]
    public void Equipment_HasWeapon_ReturnsTrueWhenSet()
    {
        var world = Arch.Core.World.Create();
        try
        {
            var weaponEntity = world.Create(new Position(0, 0));
            var equip = new Equipment { Weapon = weaponEntity };
            Assert.True(equip.HasWeapon);
        }
        finally
        {
            Arch.Core.World.Destroy(world);
        }
    }

    [Fact]
    public void Equipment_HasArmor_ReturnsTrueWhenSet()
    {
        var world = Arch.Core.World.Create();
        try
        {
            var armorEntity = world.Create(new Position(0, 0));
            var equip = new Equipment { Armor = armorEntity };
            Assert.True(equip.HasArmor);
        }
        finally
        {
            Arch.Core.World.Destroy(world);
        }
    }
}
