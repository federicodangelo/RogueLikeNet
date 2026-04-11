using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Entities;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Tests;

public class ComponentTests
{
    // ── Position: Unpack ──

    [Fact]
    public void Position_Unpack_SetsFieldsFromPacked()
    {
        var original = Position.FromCoords(42, -17, 200);
        long packed = original.Pack();

        var p = new Position();
        p.Unpack(packed);

        Assert.Equal(42, p.X);
        Assert.Equal(-17, p.Y);
        Assert.Equal(200, p.Z);
    }

    [Fact]
    public void Position_Unpack_RoundTrips()
    {
        var original = Position.FromCoords(-100, 255, 0);
        long packed = original.Pack();

        var p = new Position();
        p.Unpack(packed);

        Assert.Equal(original.X, p.X);
        Assert.Equal(original.Y, p.Y);
        Assert.Equal(original.Z, p.Z);
    }

    // ── Position: ChebyshevDistance 2D ──

    [Fact]
    public void Position_ChebyshevDistance2D_ReturnsCorrectValue()
    {
        Assert.Equal(4, Position.ChebyshevDistance(0, 0, 3, 4));
        Assert.Equal(5, Position.ChebyshevDistance(0, 0, 5, 3));
        Assert.Equal(0, Position.ChebyshevDistance(7, 7, 7, 7));
    }

    // ── Position: PackCoord(Position overload) ──

    [Fact]
    public void Position_PackCoordPosition_MatchesXYZOverload()
    {
        var pos = Position.FromCoords(10, -20, 127);
        Assert.Equal(Position.PackCoord(10, -20, 127), Position.PackCoord(pos));
    }

    // ── Position: ManhattanDistance2D ──

    [Fact]
    public void Position_ManhattanDistance2D_ReturnsCorrectValue()
    {
        Assert.Equal(7, Position.ManhattanDistance2D(0, 0, 3, 4));
        Assert.Equal(0, Position.ManhattanDistance2D(5, 5, 5, 5));
    }

    // ── PlayerEntity: default constructor ──

    [Fact]
    public void PlayerEntity_DefaultConstructor_InitializesInventoryAndSurvival()
    {
        var player = new PlayerEntity();
        Assert.NotNull(player.Inventory.Items);
        Assert.Equal(Survival.DefaultMaxHunger, player.Survival.Hunger);
        Assert.Equal(Survival.DefaultMaxThirst, player.Survival.Thirst);
    }

    // ── Equipment: all slot getters/setters ──

    [Fact]
    public void Equipment_AllSlotGettersAndSetters()
    {
        var equip = new Equipment();

        for (int i = 0; i < Equipment.SlotCount; i++)
        {
            Assert.True(equip[i].IsNone);
            var item = new ItemData { ItemTypeId = 100 + i, StackCount = 1 };
            equip[i] = item;
            Assert.Equal(100 + i, equip[i].ItemTypeId);
        }

        // Out-of-range slot returns None
        Assert.True(equip[-1].IsNone);
        Assert.True(equip[99].IsNone);
    }

    [Fact]
    public void Equipment_EquipSlotIndexer()
    {
        var equip = new Equipment();
        var item = new ItemData { ItemTypeId = 42, StackCount = 1 };
        equip[EquipSlot.Belt] = item;
        Assert.Equal(42, equip[EquipSlot.Belt].ItemTypeId);
    }

    [Fact]
    public void Equipment_HasItem_BySlotAndEnum()
    {
        var equip = new Equipment();
        Assert.False(equip.HasItem(EquipSlot.Hand));
        equip[EquipSlot.Hand] = new ItemData { ItemTypeId = 1, StackCount = 1 };
        Assert.True(equip.HasItem(EquipSlot.Hand));
        Assert.True(equip.HasItem(5));
    }

    [Fact]
    public void Equipment_Setter_OutOfRange_DoesNotThrow()
    {
        var equip = new Equipment();
        equip[99] = new ItemData { ItemTypeId = 1, StackCount = 1 };
        Assert.True(equip[99].IsNone);
    }

    // ── ActiveEffects ──

    [Fact]
    public void ActiveEffects_AddAndGet_AllSlots()
    {
        var effects = new ActiveEffects();
        for (int i = 0; i < ActiveEffects.MaxEffects; i++)
        {
            effects.Add(new ActiveEffect(EffectType.Hungry, 50 + i));
        }
        Assert.Equal(ActiveEffects.MaxEffects, effects.Count);

        // Adding beyond max is silently ignored
        effects.Add(new ActiveEffect(EffectType.Thirsty, 30));
        Assert.Equal(ActiveEffects.MaxEffects, effects.Count);
    }

    [Fact]
    public void ActiveEffects_CombinedSpeedMultiplier_AllSlotsRead()
    {
        // Fill all 8 slots to exercise every Get(index) switch branch
        var effects = new ActiveEffects();
        for (int i = 0; i < ActiveEffects.MaxEffects; i++)
            effects.Add(new ActiveEffect(EffectType.Hungry, 100)); // 100 = no change

        // 100^8 / 100^7 = 100, all slots accessed via CombinedSpeedMultiplierBase100
        Assert.Equal(100, effects.CombinedSpeedMultiplierBase100);
    }

    [Fact]
    public void ActiveEffects_HasEffect_AllSlots()
    {
        // Add Hungry in slot 7 to exercise Get(7) path in HasEffect
        var effects = new ActiveEffects();
        for (int i = 0; i < 7; i++)
            effects.Add(new ActiveEffect(EffectType.Thirsty, 100));
        effects.Add(new ActiveEffect(EffectType.Hungry, 100));

        Assert.True(effects.HasEffect(EffectType.Hungry));
    }

    [Fact]
    public void ActiveEffects_Clear_ResetsCount()
    {
        var effects = new ActiveEffects();
        effects.Add(new ActiveEffect(EffectType.Hungry, 50));
        effects.Clear();
        Assert.Equal(0, effects.Count);
    }

    [Fact]
    public void ActiveEffects_CombinedSpeedMultiplier_NoEffects_Returns100()
    {
        var effects = new ActiveEffects();
        Assert.Equal(100, effects.CombinedSpeedMultiplierBase100);
    }

    [Fact]
    public void ActiveEffects_CombinedSpeedMultiplier_SingleEffect()
    {
        var effects = new ActiveEffects();
        effects.Add(new ActiveEffect(EffectType.Hungry, 70));
        Assert.Equal(70, effects.CombinedSpeedMultiplierBase100);
    }

    [Fact]
    public void ActiveEffects_CombinedSpeedMultiplier_MultipleEffects_Stacks()
    {
        var effects = new ActiveEffects();
        effects.Add(new ActiveEffect(EffectType.Hungry, 50));
        effects.Add(new ActiveEffect(EffectType.Thirsty, 50));
        // 100 * 50/100 = 50; 50 * 50/100 = 25
        Assert.Equal(25, effects.CombinedSpeedMultiplierBase100);
    }

    [Fact]
    public void ActiveEffects_HasEffect_ReturnsFalseWhenNotPresent()
    {
        var effects = new ActiveEffects();
        effects.Add(new ActiveEffect(EffectType.Hungry, 50));
        Assert.True(effects.HasEffect(EffectType.Hungry));
        Assert.False(effects.HasEffect(EffectType.Thirsty));
    }

    // ── Survival thresholds ──

    [Fact]
    public void Survival_IsWellFed_TrueAbove80()
    {
        var s = Survival.Default();
        s.Hunger = 81;
        Assert.True(s.IsWellFed);
        s.Hunger = 80;
        Assert.False(s.IsWellFed);
    }

    [Fact]
    public void Survival_IsWellHydrated_TrueAbove80()
    {
        var s = Survival.Default();
        s.Thirst = 81;
        Assert.True(s.IsWellHydrated);
        s.Thirst = 80;
        Assert.False(s.IsWellHydrated);
    }

    // ── ChunkPosition tests ──

    [Fact]
    public void ChunkPosition_PackUnpack_Roundtrip()
    {
        var cp = ChunkPosition.FromCoords(5, -3, 127);
        long packed = cp.Pack();
        var cp2 = new ChunkPosition();
        cp2.Unpack(packed);
        Assert.Equal(cp.X, cp2.X);
        Assert.Equal(cp.Y, cp2.Y);
        Assert.Equal(cp.Z, cp2.Z);
    }

    [Fact]
    public void ChunkPosition_PackCoord_StaticOverload()
    {
        var cp = ChunkPosition.FromCoords(10, 20, 30);
        long packed1 = ChunkPosition.PackCoord(10, 20, 30);
        long packed2 = ChunkPosition.PackCoord(cp);
        Assert.Equal(packed1, packed2);
    }

    [Fact]
    public void ChunkPosition_UnpackCoord_ReturnsCorrect()
    {
        long packed = ChunkPosition.PackCoord(7, -5, 200);
        var cp = ChunkPosition.UnpackCoord(packed);
        Assert.Equal(7, cp.X);
        Assert.Equal(-5, cp.Y);
        Assert.Equal(200, cp.Z);
    }

    [Fact]
    public void ChunkPosition_ToString_Format()
    {
        var cp = ChunkPosition.FromCoords(1, 2, 3);
        Assert.Equal("(1, 2, 3)", cp.ToString());
    }

    [Fact]
    public void ChunkPosition_DefaultZ_MatchesPositionDefaultZ()
    {
        Assert.Equal(Position.DefaultZ, ChunkPosition.DefaultZ);
    }

    // ── Equipment: HasWeapon / HasArmor ──

    [Fact]
    public void Equipment_HasWeapon_ReturnsTrueWhenHandEquipped()
    {
        var equip = new Equipment();
        Assert.False(equip.HasWeapon);
        equip[EquipSlot.Hand] = new ItemData { ItemTypeId = 1, StackCount = 1 };
        Assert.True(equip.HasWeapon);
    }

    [Fact]
    public void Equipment_HasArmor_ReturnsTrueWhenChestEquipped()
    {
        var equip = new Equipment();
        Assert.False(equip.HasArmor);
        equip[EquipSlot.Chest] = new ItemData { ItemTypeId = 2, StackCount = 1 };
        Assert.True(equip.HasArmor);
    }

    // ── QuickSlots ──

    [Fact]
    public void QuickSlots_DefaultConstructor_AllEmpty()
    {
        var qs = new QuickSlots();
        Assert.Equal(4, qs.Count);
        for (int i = 0; i < qs.Count; i++)
            Assert.Equal(-1, qs[i]);
    }

    [Fact]
    public void QuickSlots_Indexer_SetAndGet()
    {
        var qs = new QuickSlots();
        qs[0] = 5;
        qs[1] = 10;
        qs[2] = 15;
        qs[3] = 20;
        Assert.Equal(5, qs[0]);
        Assert.Equal(10, qs[1]);
        Assert.Equal(15, qs[2]);
        Assert.Equal(20, qs[3]);
    }

    [Fact]
    public void QuickSlots_Indexer_OutOfRange_ReturnsNegativeOne()
    {
        var qs = new QuickSlots();
        Assert.Equal(-1, qs[-1]);
        Assert.Equal(-1, qs[99]);
    }

    [Fact]
    public void QuickSlots_FirstEmptySlot_ReturnsFirstEmpty()
    {
        var qs = new QuickSlots();
        Assert.Equal(0, qs.FirstEmptySlot());
        qs[0] = 5;
        Assert.Equal(1, qs.FirstEmptySlot());
        qs[1] = 6;
        Assert.Equal(2, qs.FirstEmptySlot());
        qs[2] = 7;
        Assert.Equal(3, qs.FirstEmptySlot());
        qs[3] = 8;
        Assert.Equal(-1, qs.FirstEmptySlot());
    }

    [Fact]
    public void QuickSlots_ClearIndex_RemovesMatchingSlots()
    {
        var qs = new QuickSlots();
        qs[0] = 5;
        qs[1] = 10;
        qs[2] = 5;
        qs.ClearIndex(5);
        Assert.Equal(-1, qs[0]);
        Assert.Equal(10, qs[1]);
        Assert.Equal(-1, qs[2]);
    }

    [Fact]
    public void QuickSlots_OnItemRemoved_ClearsRemovedAndDecrementsHigher()
    {
        var qs = new QuickSlots();
        qs[0] = 2;
        qs[1] = 5;
        qs[2] = 3;
        qs[3] = 1;
        qs.OnItemRemoved(3);
        // slot 0: 2 < 3 → unchanged
        Assert.Equal(2, qs[0]);
        // slot 1: 5 > 3 → decremented to 4
        Assert.Equal(4, qs[1]);
        // slot 2: 3 == 3 → cleared
        Assert.Equal(-1, qs[2]);
        // slot 3: 1 < 3 → unchanged
        Assert.Equal(1, qs[3]);
    }

    // ── Inventory ──

    [Fact]
    public void Inventory_Constructor_SetsCapacity()
    {
        var inv = new Inventory(10);
        Assert.Equal(10, inv.Capacity);
        Assert.False(inv.IsFull);
        Assert.NotNull(inv.Items);
    }

    [Fact]
    public void Inventory_IsFull_TrueWhenAtCapacity()
    {
        var inv = new Inventory(2);
        inv.Items.Add(new ItemData { ItemTypeId = 1, StackCount = 1 });
        Assert.False(inv.IsFull);
        inv.Items.Add(new ItemData { ItemTypeId = 2, StackCount = 1 });
        Assert.True(inv.IsFull);
    }

    [Fact]
    public void Inventory_FindSlotWithItem_ReturnsCorrectIndex()
    {
        var inv = new Inventory(10);
        inv.Items.Add(new ItemData { ItemTypeId = 5, StackCount = 1 });
        inv.Items.Add(new ItemData { ItemTypeId = 10, StackCount = 2 });
        inv.Items.Add(new ItemData { ItemTypeId = 15, StackCount = 3 });

        Assert.True(inv.FindSlotWithItem(10, out int slot));
        Assert.Equal(1, slot);
    }

    [Fact]
    public void Inventory_FindSlotWithItem_ReturnsFalseWhenNotFound()
    {
        var inv = new Inventory(10);
        inv.Items.Add(new ItemData { ItemTypeId = 5, StackCount = 1 });

        Assert.False(inv.FindSlotWithItem(999, out int slot));
        Assert.Equal(-1, slot);
    }

    // ── ItemData ──

    [Fact]
    public void ItemData_None_IsNone()
    {
        Assert.True(ItemData.None.IsNone);
        Assert.Equal(0, ItemData.None.ItemTypeId);
    }

    [Fact]
    public void ItemData_WithTypeId_IsNotNone()
    {
        var item = new ItemData { ItemTypeId = 42, StackCount = 5 };
        Assert.False(item.IsNone);
        Assert.Equal(42, item.ItemTypeId);
        Assert.Equal(5, item.StackCount);
    }

    // ── Position: Zero constant ──

    [Fact]
    public void Position_Zero_IsOrigin()
    {
        Assert.Equal(0, Position.Zero.X);
        Assert.Equal(0, Position.Zero.Y);
        Assert.Equal(0, Position.Zero.Z);
    }

    // ── Position: PackCoord/UnpackCoord roundtrip with negatives ──

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(100, 200, 127)]
    [InlineData(-100, -200, 0)]
    [InlineData(-1, 1, 255)]
    public void Position_PackUnpack_RoundTrips(int x, int y, int z)
    {
        long packed = Position.PackCoord(x, y, z);
        var result = Position.UnpackCoord(packed);
        Assert.Equal(x, result.X);
        Assert.Equal(y, result.Y);
        Assert.Equal(z, result.Z);
    }

    // ── PlayerEntity: IsDead ──

    [Fact]
    public void PlayerEntity_IsDead_WhenHealthZero()
    {
        var player = new PlayerEntity(1);
        player.Health = new Health { Current = 0, Max = 100 };
        Assert.True(player.IsDead);
    }

    [Fact]
    public void PlayerEntity_IsNotDead_WhenHealthPositive()
    {
        var player = new PlayerEntity(1);
        player.Health = new Health { Current = 50, Max = 100 };
        Assert.False(player.IsDead);
    }

    // ── Survival: boundary values ──

    [Fact]
    public void Survival_IsStarving_BoundaryValues()
    {
        var s = Survival.Default();
        s.Hunger = 19;
        Assert.True(s.IsStarving);
        s.Hunger = 20;
        Assert.False(s.IsStarving);
    }

    [Fact]
    public void Survival_IsHungry_BoundaryValues()
    {
        var s = Survival.Default();
        s.Hunger = 49;
        Assert.True(s.IsHungry);
        s.Hunger = 50;
        Assert.False(s.IsHungry);
    }

    [Fact]
    public void Survival_IsDehydrated_BoundaryValues()
    {
        var s = Survival.Default();
        s.Thirst = 19;
        Assert.True(s.IsDehydrated);
        s.Thirst = 20;
        Assert.False(s.IsDehydrated);
    }

    [Fact]
    public void Survival_IsThirsty_BoundaryValues()
    {
        var s = Survival.Default();
        s.Thirst = 49;
        Assert.True(s.IsThirsty);
        s.Thirst = 50;
        Assert.False(s.IsThirsty);
    }

    [Fact]
    public void Survival_Constructor_SetsAllFields()
    {
        var s = new Survival(50, 100, 60, 200);
        Assert.Equal(50, s.Hunger);
        Assert.Equal(100, s.HungerDecayRate);
        Assert.Equal(60, s.Thirst);
        Assert.Equal(200, s.ThirstDecayRate);
        Assert.Equal(Survival.DefaultMaxHunger, s.MaxHunger);
        Assert.Equal(Survival.DefaultMaxThirst, s.MaxThirst);
    }
}
