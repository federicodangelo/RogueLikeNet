using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Tests;

public class QuickSlotsTests
{
    private static readonly BspDungeonGenerator _gen = new(42);

    private static Data.ItemDefinition Item(string id) => GameData.Instance.Items.Get(id)!;
    private static Data.ItemDefinition[] AllItems() => GameData.Instance.Items.All.ToArray();

    private GameEngine CreateEngine()
    {
        var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
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
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Spawn item at player's position
        engine.SpawnItemOnGround(Item("short_sword"), Position.FromCoords(sx, sy, Position.DefaultZ));

        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Item added at index 0, should auto-assign to quick slot 0
        Assert.Equal(0, player.QuickSlots[0]);
        Assert.Equal(-1, player.QuickSlots[1]);
    }

    [Fact]
    public void PickUp_SecondItem_AutoAssignsToNextSlot()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Pick up two items
        engine.SpawnItemOnGround(Item("short_sword"), Position.FromCoords(sx, sy, Position.DefaultZ));
        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        engine.SpawnItemOnGround(Item("long_sword"), Position.FromCoords(sx, sy, Position.DefaultZ));
        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        Assert.Equal(0, player.QuickSlots[0]);
        Assert.Equal(1, player.QuickSlots[1]);
    }

    [Fact]
    public void PickUp_AllSlotsFull_NoAutoAssign()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Fill all 4 quick slots with distinct weapons
        string[] weapons = ["short_sword", "long_sword", "dagger", "battle_axe"];
        for (int i = 0; i < 4; i++)
        {
            engine.SpawnItemOnGround(Item(weapons[i]), Position.FromCoords(sx, sy, Position.DefaultZ));
            player.Input.ActionType = ActionTypes.PickUp;
            engine.Tick();
        }

        // Pick up a 5th item
        engine.SpawnItemOnGround(Item("spear"), Position.FromCoords(sx, sy, Position.DefaultZ));
        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Quick slots should still be 0-3
        Assert.Equal(0, player.QuickSlots[0]);
        Assert.Equal(1, player.QuickSlots[1]);
        Assert.Equal(2, player.QuickSlots[2]);
        Assert.Equal(3, player.QuickSlots[3]);
    }

    [Fact]
    public void SetQuickSlot_AssignsItemToSlot()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Pick up an item
        engine.SpawnItemOnGround(Item("short_sword"), Position.FromCoords(sx, sy, Position.DefaultZ));
        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Pick up another item
        engine.SpawnItemOnGround(Item("long_sword"), Position.FromCoords(sx, sy, Position.DefaultZ));
        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Manually assign inventory index 1 to quick slot 0
        player.Input.ActionType = ActionTypes.SetQuickSlot;
        player.Input.ItemSlot = 0;    // quick slot number
        player.Input.TargetSlot = 1;  // inventory index
        engine.Tick();

        Assert.Equal(1, player.QuickSlots[0]); // Should now point to inventory index 1
    }

    [Fact]
    public void SetQuickSlot_ToggleClear()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Pick up an item (auto-assigned to slot 0)
        engine.SpawnItemOnGround(Item("short_sword"), Position.FromCoords(sx, sy, Position.DefaultZ));
        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        Assert.Equal(0, player.QuickSlots[0]); // Auto-assigned

        // Now set quick slot 0 to the same item (toggle off)
        player.Input.ActionType = ActionTypes.SetQuickSlot;
        player.Input.ItemSlot = 0;    // quick slot number
        player.Input.TargetSlot = 0;  // same inventory index
        engine.Tick();

        Assert.Equal(-1, player.QuickSlots[0]); // Should be cleared
    }

    [Fact]
    public void Drop_AdjustsQuickSlots()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Pick up 3 items (auto-assigned to slots 0, 1, 2)
        string[] weapons = ["short_sword", "long_sword", "dagger"];
        for (int i = 0; i < 3; i++)
        {
            engine.SpawnItemOnGround(Item(weapons[i]), Position.FromCoords(sx, sy, Position.DefaultZ));
            player.Input.ActionType = ActionTypes.PickUp;
            engine.Tick();
        }

        // Drop item at index 0 — should clear player.QuickSlots[0] and shift player.QuickSlots[1], player.QuickSlots[2] down
        player.Input.ActionType = ActionTypes.Drop;
        player.Input.ItemSlot = 0;
        engine.Tick();

        Assert.Equal(-1, player.QuickSlots[0]); // Was pointing to dropped item
        Assert.Equal(0, player.QuickSlots[1]);  // Was 1, shifted down
        Assert.Equal(1, player.QuickSlots[2]);  // Was 2, shifted down
    }

    [Fact]
    public void UseQuickSlot_UsesCorrectItem()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Damage player
        player.Health.Current = 50;

        // Pick up a player.Health potion (auto-assigned to quick slot 0)
        var potionTemplate = Item("health_potion_small");
        engine.SpawnItemOnGround(potionTemplate, Position.FromCoords(sx, sy, Position.DefaultZ));
        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        // Use quick slot 0
        player.Input.ActionType = ActionTypes.UseQuickSlot;
        player.Input.ItemSlot = 0;
        engine.Tick();

        Assert.True(player.Health.Current > 50);
    }

    [Fact]
    public void UseQuickSlot_EmptySlot_DoesNothing()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        player.Input.ActionType = ActionTypes.UseQuickSlot;
        player.Input.ItemSlot = 0;
        engine.Tick(); // Should not crash
    }

    [Fact]
    public void GetPlayerStateData_IncludesQuickSlotIndices()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Pick up 2 items (auto-assigned)
        engine.SpawnItemOnGround(Item("short_sword"), Position.FromCoords(sx, sy, Position.DefaultZ));
        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        engine.SpawnItemOnGround(Item("long_sword"), Position.FromCoords(sx, sy, Position.DefaultZ));
        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        var stateData = engine.GetPlayerStateData(player);
        Assert.NotNull(stateData);
        Assert.Equal(4, stateData!.QuickSlotIndices.Length);
        Assert.Equal(0, stateData.QuickSlotIndices[0]);
        Assert.Equal(1, stateData.QuickSlotIndices[1]);
        Assert.Equal(-1, stateData.QuickSlotIndices[2]);
        Assert.Equal(-1, stateData.QuickSlotIndices[3]);
    }

    [Fact]
    public void PickUp_BuildableItem_AutoAssignsToQuickSlot()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Spawn a buildable item (Wooden Door) at player's position
        var template = Item("wooden_door");
        engine.SpawnItemOnGround(template, Position.FromCoords(sx, sy, Position.DefaultZ));

        player.Input.ActionType = ActionTypes.PickUp;
        engine.Tick();

        Assert.Equal(0, player.QuickSlots[0]); // Buildable should auto-assign to quick slot 0
    }
}
