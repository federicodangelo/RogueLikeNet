using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.World;
using RogueLikeNet.Server.Persistence;

namespace RogueLikeNet.Server.Tests;

public class InMemorySaveGameProviderTests
{
    private InMemorySaveGameProvider CreateProvider() => new();

    [Fact]
    public void ListSaveSlots_Empty_ReturnsEmpty()
    {
        using var provider = CreateProvider();
        Assert.Empty(provider.ListSaveSlots());
    }

    [Fact]
    public void CreateSaveSlot_ReturnsSaveSlotInfo()
    {
        using var provider = CreateProvider();
        var slot = provider.CreateSaveSlot("Test", 42, "bsp");
        Assert.NotEmpty(slot.SlotId);
        Assert.Equal("Test", slot.Name);
        Assert.Equal(42, slot.Seed);
        Assert.Equal("bsp", slot.GeneratorId);
    }

    [Fact]
    public void ListSaveSlots_ReturnsCreatedSlots()
    {
        using var provider = CreateProvider();
        provider.CreateSaveSlot("Slot1", 1, "bsp");
        provider.CreateSaveSlot("Slot2", 2, "bsp");
        var slots = provider.ListSaveSlots();
        Assert.Equal(2, slots.Count);
    }

    [Fact]
    public void GetSaveSlot_ReturnsSlot()
    {
        using var provider = CreateProvider();
        var created = provider.CreateSaveSlot("Test", 42, "bsp");
        var got = provider.GetSaveSlot(created.SlotId);
        Assert.NotNull(got);
        Assert.Equal(created.SlotId, got.SlotId);
    }

    [Fact]
    public void GetSaveSlot_ReturnsNull_WhenNotFound()
    {
        using var provider = CreateProvider();
        Assert.Null(provider.GetSaveSlot("nonexistent"));
    }

    [Fact]
    public void DeleteSaveSlot_RemovesSlot()
    {
        using var provider = CreateProvider();
        var slot = provider.CreateSaveSlot("Test", 42, "bsp");
        provider.DeleteSaveSlot(slot.SlotId);
        Assert.Null(provider.GetSaveSlot(slot.SlotId));
        Assert.Empty(provider.ListSaveSlots());
    }

    [Fact]
    public void DeleteSaveSlot_RemovesAssociatedData()
    {
        using var provider = CreateProvider();
        var slot = provider.CreateSaveSlot("Test", 42, "bsp");

        provider.SaveWorldMeta(slot.SlotId, new WorldSaveData { Seed = 42, GeneratorId = "bsp" });
        provider.SaveChunks(slot.SlotId, [new ChunkSaveEntry { ChunkX = 0, ChunkY = 0, ChunkZ = Position.DefaultZ }]);
        provider.SavePlayers(slot.SlotId, [new PlayerSaveData { PlayerName = "Hero" }]);

        provider.DeleteSaveSlot(slot.SlotId);

        Assert.Null(provider.LoadWorldMeta(slot.SlotId));
        Assert.Null(provider.LoadChunk(slot.SlotId, ChunkPosition.FromCoords(0, 0, Position.DefaultZ)));
        Assert.Null(provider.LoadPlayer(slot.SlotId, "Hero"));
    }

    // ── WorldMeta ──

    [Fact]
    public void SaveAndLoadWorldMeta()
    {
        using var provider = CreateProvider();
        var slot = provider.CreateSaveSlot("Test", 42, "bsp");
        var meta = new WorldSaveData { Seed = 42, GeneratorId = "bsp", CurrentTick = 100 };
        provider.SaveWorldMeta(slot.SlotId, meta);

        var loaded = provider.LoadWorldMeta(slot.SlotId);
        Assert.NotNull(loaded);
        Assert.Equal(42, loaded.Seed);
        Assert.Equal(100, loaded.CurrentTick);
    }

    [Fact]
    public void LoadWorldMeta_ReturnsNull_WhenMissing()
    {
        using var provider = CreateProvider();
        Assert.Null(provider.LoadWorldMeta("nonexistent"));
    }

    [Fact]
    public void SaveWorldMeta_UpdatesLastSavedAt()
    {
        using var provider = CreateProvider();
        var slot = provider.CreateSaveSlot("Test", 42, "bsp");
        var beforeSave = slot.LastSavedAt;
        provider.SaveWorldMeta(slot.SlotId, new WorldSaveData { Seed = 42 });
        Assert.True(slot.LastSavedAt >= beforeSave);
    }

    // ── Chunks ──

    [Fact]
    public void SaveAndLoadChunk()
    {
        using var provider = CreateProvider();
        var slot = provider.CreateSaveSlot("Test", 42, "bsp");
        var entry = new ChunkSaveEntry { ChunkX = 1, ChunkY = 2, ChunkZ = Position.DefaultZ, TileData = [1, 2, 3] };
        provider.SaveChunks(slot.SlotId, [entry]);

        var loaded = provider.LoadChunk(slot.SlotId, ChunkPosition.FromCoords(1, 2, Position.DefaultZ));
        Assert.NotNull(loaded);
        Assert.Equal(new byte[] { 1, 2, 3 }, loaded.TileData);
    }

    [Fact]
    public void LoadChunk_ReturnsNull_MissingSlot()
    {
        using var provider = CreateProvider();
        Assert.Null(provider.LoadChunk("nonexistent", ChunkPosition.FromCoords(0, 0, Position.DefaultZ)));
    }

    [Fact]
    public void LoadChunk_ReturnsNull_MissingChunk()
    {
        using var provider = CreateProvider();
        var slot = provider.CreateSaveSlot("Test", 42, "bsp");
        Assert.Null(provider.LoadChunk(slot.SlotId, ChunkPosition.FromCoords(99, 99, Position.DefaultZ)));
    }

    [Fact]
    public void SaveChunks_CreatesNewDict_WhenSlotNotInChunks()
    {
        using var provider = CreateProvider();
        // Save directly without CreateSaveSlot (which pre-creates the dict)
        provider.SaveChunks("newslot", [new ChunkSaveEntry { ChunkX = 0, ChunkY = 0, ChunkZ = Position.DefaultZ }]);
        var loaded = provider.LoadChunk("newslot", ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        Assert.NotNull(loaded);
    }

    // ── Players ──

    [Fact]
    public void SaveAndLoadPlayer()
    {
        using var provider = CreateProvider();
        var slot = provider.CreateSaveSlot("Test", 42, "bsp");
        var playerData = new PlayerSaveData { PlayerName = "Hero", Level = 5, HealthCurrent = 80, HealthMax = 100 };
        provider.SavePlayers(slot.SlotId, [playerData]);

        var loaded = provider.LoadPlayer(slot.SlotId, "Hero");
        Assert.NotNull(loaded);
        Assert.Equal("Hero", loaded.PlayerName);
        Assert.Equal(5, loaded.Level);
    }

    [Fact]
    public void LoadPlayer_ReturnsNull_MissingSlot()
    {
        using var provider = CreateProvider();
        Assert.Null(provider.LoadPlayer("nonexistent", "Hero"));
    }

    [Fact]
    public void LoadPlayer_ReturnsNull_MissingPlayer()
    {
        using var provider = CreateProvider();
        var slot = provider.CreateSaveSlot("Test", 42, "bsp");
        Assert.Null(provider.LoadPlayer(slot.SlotId, "NonExistent"));
    }

    [Fact]
    public void LoadAllPlayers_ReturnsAll()
    {
        using var provider = CreateProvider();
        var slot = provider.CreateSaveSlot("Test", 42, "bsp");
        provider.SavePlayers(slot.SlotId, [
            new PlayerSaveData { PlayerName = "Hero1" },
            new PlayerSaveData { PlayerName = "Hero2" },
        ]);

        var all = provider.LoadAllPlayers(slot.SlotId);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void LoadAllPlayers_ReturnsEmpty_MissingSlot()
    {
        using var provider = CreateProvider();
        Assert.Empty(provider.LoadAllPlayers("nonexistent"));
    }

    [Fact]
    public void SavePlayers_CreatesNewDict_WhenSlotNotInPlayers()
    {
        using var provider = CreateProvider();
        provider.SavePlayers("newslot", [new PlayerSaveData { PlayerName = "Hero" }]);
        var loaded = provider.LoadPlayer("newslot", "Hero");
        Assert.NotNull(loaded);
    }

    [Fact]
    public void SavePlayers_UpsertsExisting()
    {
        using var provider = CreateProvider();
        var slot = provider.CreateSaveSlot("Test", 42, "bsp");
        provider.SavePlayers(slot.SlotId, [new PlayerSaveData { PlayerName = "Hero", Level = 1 }]);
        provider.SavePlayers(slot.SlotId, [new PlayerSaveData { PlayerName = "Hero", Level = 5 }]);

        var loaded = provider.LoadPlayer(slot.SlotId, "Hero");
        Assert.Equal(5, loaded!.Level);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var provider = CreateProvider();
        provider.Dispose();
        provider.Dispose(); // Double dispose should be fine
    }
}
