using Arch.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Tests;

public class QuickSlotsTests
{
    private static readonly BspDungeonGenerator _gen = new();

    private GameEngine CreateEngine()
    {
        var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0);
        return engine;
    }

    [Fact]
    public void QuickSlots_DefaultAllEmpty()
    {
        var qs = new QuickSlots();
        Assert.Equal(-1, qs[0]);
        Assert.Equal(-1, qs[1]);
        Assert.Equal(-1, qs[2]);
        Assert.Equal(-1, qs[3]);
    }

    [Fact]
    public void QuickSlots_Indexer_SetAndGet()
    {
        var qs = new QuickSlots();
        qs[0] = 5;
        qs[2] = 3;
        Assert.Equal(5, qs[0]);
        Assert.Equal(-1, qs[1]);
        Assert.Equal(3, qs[2]);
        Assert.Equal(-1, qs[3]);
    }

    [Fact]
    public void QuickSlots_FirstEmptySlot_ReturnsCorrectIndex()
    {
        var qs = new QuickSlots();
        Assert.Equal(0, qs.FirstEmptySlot());
        qs[0] = 1;
        Assert.Equal(1, qs.FirstEmptySlot());
        qs[1] = 2;
        qs[2] = 3;
        Assert.Equal(3, qs.FirstEmptySlot());
        qs[3] = 4;
        Assert.Equal(-1, qs.FirstEmptySlot());
    }

    [Fact]
    public void QuickSlots_ClearIndex_RemovesMatchingSlots()
    {
        var qs = new QuickSlots();
        qs[0] = 5;
        qs[1] = 5;
        qs[2] = 3;
        qs.ClearIndex(5);
        Assert.Equal(-1, qs[0]);
        Assert.Equal(-1, qs[1]);
        Assert.Equal(3, qs[2]);
    }

    [Fact]
    public void QuickSlots_OnItemRemoved_ClearsAndShifts()
    {
        var qs = new QuickSlots();
        qs[0] = 0;
        qs[1] = 2;
        qs[2] = 5;
        qs[3] = 1;

        // Remove item at index 2
        qs.OnItemRemoved(2);

        Assert.Equal(0, qs[0]);   // unchanged (below removed)
        Assert.Equal(-1, qs[1]);  // was pointing to removed index
        Assert.Equal(4, qs[2]);   // was 5, shifted down
        Assert.Equal(1, qs[3]);   // unchanged (below removed)
    }

    [Fact]
    public void PickUp_AutoAssignsToQuickSlot()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy);

        // Spawn item at player's position
        var template = ItemDefinitions.All[0]; // Short Sword
        engine.SpawnItemOnGround(template, 0, sx, sy);

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        ref var qs = ref engine.EcsWorld.Get<QuickSlots>(player);
        // Item added at index 0, should auto-assign to quick slot 0
        Assert.Equal(0, qs[0]);
        Assert.Equal(-1, qs[1]);
    }

    [Fact]
    public void PickUp_SecondItem_AutoAssignsToNextSlot()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy);

        // Pick up two items
        engine.SpawnItemOnGround(ItemDefinitions.All[0], 0, sx, sy);
        ref var input1 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input1.ActionType = ActionTypes.PickUp;
        engine.Tick();

        engine.SpawnItemOnGround(ItemDefinitions.All[1], 0, sx, sy);
        ref var input2 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input2.ActionType = ActionTypes.PickUp;
        engine.Tick();

        ref var qs = ref engine.EcsWorld.Get<QuickSlots>(player);
        Assert.Equal(0, qs[0]);
        Assert.Equal(1, qs[1]);
    }

    [Fact]
    public void PickUp_AllSlotsFull_NoAutoAssign()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy);

        // Fill all 4 quick slots
        for (int i = 0; i < 4; i++)
        {
            engine.SpawnItemOnGround(ItemDefinitions.All[i % ItemDefinitions.All.Length], 0, sx, sy);
            ref var inp = ref engine.EcsWorld.Get<PlayerInput>(player);
            inp.ActionType = ActionTypes.PickUp;
            engine.Tick();
        }

        // Pick up a 5th item
        engine.SpawnItemOnGround(ItemDefinitions.All[0], 0, sx, sy);
        ref var inp5 = ref engine.EcsWorld.Get<PlayerInput>(player);
        inp5.ActionType = ActionTypes.PickUp;
        engine.Tick();

        ref var qs = ref engine.EcsWorld.Get<QuickSlots>(player);
        // Quick slots should still be 0-3
        Assert.Equal(0, qs[0]);
        Assert.Equal(1, qs[1]);
        Assert.Equal(2, qs[2]);
        Assert.Equal(3, qs[3]);
    }

    [Fact]
    public void SetQuickSlot_AssignsItemToSlot()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy);

        // Pick up an item
        engine.SpawnItemOnGround(ItemDefinitions.All[0], 0, sx, sy);
        ref var inp1 = ref engine.EcsWorld.Get<PlayerInput>(player);
        inp1.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Pick up another item
        engine.SpawnItemOnGround(ItemDefinitions.All[1], 0, sx, sy);
        ref var inp2 = ref engine.EcsWorld.Get<PlayerInput>(player);
        inp2.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Manually assign inventory index 1 to quick slot 0
        ref var inp3 = ref engine.EcsWorld.Get<PlayerInput>(player);
        inp3.ActionType = ActionTypes.SetQuickSlot;
        inp3.ItemSlot = 0;    // quick slot number
        inp3.TargetSlot = 1;  // inventory index
        engine.Tick();

        ref var qs = ref engine.EcsWorld.Get<QuickSlots>(player);
        Assert.Equal(1, qs[0]); // Should now point to inventory index 1
    }

    [Fact]
    public void SetQuickSlot_ToggleClear()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy);

        // Pick up an item (auto-assigned to slot 0)
        engine.SpawnItemOnGround(ItemDefinitions.All[0], 0, sx, sy);
        ref var inp1 = ref engine.EcsWorld.Get<PlayerInput>(player);
        inp1.ActionType = ActionTypes.PickUp;
        engine.Tick();

        ref var qs1 = ref engine.EcsWorld.Get<QuickSlots>(player);
        Assert.Equal(0, qs1[0]); // Auto-assigned

        // Now set quick slot 0 to the same item (toggle off)
        ref var inp2 = ref engine.EcsWorld.Get<PlayerInput>(player);
        inp2.ActionType = ActionTypes.SetQuickSlot;
        inp2.ItemSlot = 0;    // quick slot number
        inp2.TargetSlot = 0;  // same inventory index
        engine.Tick();

        ref var qs2 = ref engine.EcsWorld.Get<QuickSlots>(player);
        Assert.Equal(-1, qs2[0]); // Should be cleared
    }

    [Fact]
    public void Drop_AdjustsQuickSlots()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy);

        // Pick up 3 items (auto-assigned to slots 0, 1, 2)
        for (int i = 0; i < 3; i++)
        {
            engine.SpawnItemOnGround(ItemDefinitions.All[i], 0, sx, sy);
            ref var inp = ref engine.EcsWorld.Get<PlayerInput>(player);
            inp.ActionType = ActionTypes.PickUp;
            engine.Tick();
        }

        // Drop item at index 0 — should clear qs[0] and shift qs[1], qs[2] down
        ref var dropInput = ref engine.EcsWorld.Get<PlayerInput>(player);
        dropInput.ActionType = ActionTypes.Drop;
        dropInput.ItemSlot = 0;
        engine.Tick();

        ref var qs = ref engine.EcsWorld.Get<QuickSlots>(player);
        Assert.Equal(-1, qs[0]); // Was pointing to dropped item
        Assert.Equal(0, qs[1]);  // Was 1, shifted down
        Assert.Equal(1, qs[2]);  // Was 2, shifted down
    }

    [Fact]
    public void UseQuickSlot_UsesCorrectItem()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy);

        // Damage player
        ref var health = ref engine.EcsWorld.Get<Health>(player);
        health.Current = 50;

        // Pick up a health potion (auto-assigned to quick slot 0)
        var potionTemplate = Array.Find(ItemDefinitions.All, t => t.TypeId == ItemDefinitions.HealthPotion);
        engine.SpawnItemOnGround(potionTemplate, 0, sx, sy);
        ref var inp1 = ref engine.EcsWorld.Get<PlayerInput>(player);
        inp1.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Use quick slot 0
        ref var inp2 = ref engine.EcsWorld.Get<PlayerInput>(player);
        inp2.ActionType = ActionTypes.UseQuickSlot;
        inp2.ItemSlot = 0;
        engine.Tick();

        ref var healthAfter = ref engine.EcsWorld.Get<Health>(player);
        Assert.True(healthAfter.Current > 50);
    }

    [Fact]
    public void UseQuickSlot_EmptySlot_DoesNothing()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy);

        ref var inp = ref engine.EcsWorld.Get<PlayerInput>(player);
        inp.ActionType = ActionTypes.UseQuickSlot;
        inp.ItemSlot = 0;
        engine.Tick(); // Should not crash
    }

    [Fact]
    public void GetPlayerStateData_IncludesQuickSlotIndices()
    {
        using var engine = CreateEngine();
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy);

        // Pick up 2 items (auto-assigned)
        engine.SpawnItemOnGround(ItemDefinitions.All[0], 0, sx, sy);
        ref var inp1 = ref engine.EcsWorld.Get<PlayerInput>(player);
        inp1.ActionType = ActionTypes.PickUp;
        engine.Tick();

        engine.SpawnItemOnGround(ItemDefinitions.All[1], 0, sx, sy);
        ref var inp2 = ref engine.EcsWorld.Get<PlayerInput>(player);
        inp2.ActionType = ActionTypes.PickUp;
        engine.Tick();

        var stateData = engine.GetPlayerStateData(player);
        Assert.NotNull(stateData);
        Assert.Equal(4, stateData!.QuickSlotIndices.Length);
        Assert.Equal(0, stateData.QuickSlotIndices[0]);
        Assert.Equal(1, stateData.QuickSlotIndices[1]);
        Assert.Equal(-1, stateData.QuickSlotIndices[2]);
        Assert.Equal(-1, stateData.QuickSlotIndices[3]);
    }
}
