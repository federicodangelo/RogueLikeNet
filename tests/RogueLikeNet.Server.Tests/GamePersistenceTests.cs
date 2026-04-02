using Microsoft.Data.Sqlite;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Server.Persistence;

namespace RogueLikeNet.Server.Tests;

public class SqliteSaveGameProviderTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteSaveGameProvider _provider;

    public SqliteSaveGameProviderTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        _provider = new SqliteSaveGameProvider(_dbPath);
    }

    public void Dispose()
    {
        _provider.Dispose();
        SqliteConnection.ClearAllPools();
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
    }

    [Fact]
    public void Constructor_CreatesDatabase()
    {
        Assert.True(File.Exists(_dbPath));
    }

    [Fact]
    public void ListSaveSlots_Empty()
    {
        var slots = _provider.ListSaveSlots();
        Assert.Empty(slots);
    }

    [Fact]
    public void CreateSaveSlot_ReturnsSlotInfo()
    {
        var slot = _provider.CreateSaveSlot("My Save", 42, "overworld");
        Assert.NotEmpty(slot.SlotId);
        Assert.Equal("My Save", slot.Name);
        Assert.Equal(42, slot.Seed);
        Assert.Equal("overworld", slot.GeneratorId);
    }

    [Fact]
    public void ListSaveSlots_AfterCreate()
    {
        _provider.CreateSaveSlot("Save1", 1, "overworld");
        _provider.CreateSaveSlot("Save2", 2, "bsp-dungeon");
        var slots = _provider.ListSaveSlots();
        Assert.Equal(2, slots.Count);
    }

    [Fact]
    public void GetSaveSlot_Found()
    {
        var created = _provider.CreateSaveSlot("Test", 42, "overworld");
        var found = _provider.GetSaveSlot(created.SlotId);
        Assert.NotNull(found);
        Assert.Equal("Test", found.Name);
    }

    [Fact]
    public void GetSaveSlot_NotFound()
    {
        Assert.Null(_provider.GetSaveSlot("nonexistent"));
    }

    [Fact]
    public void DeleteSaveSlot_RemovesSlotAndData()
    {
        var slot = _provider.CreateSaveSlot("ToDelete", 1, "overworld");
        _provider.SaveWorldMeta(slot.SlotId, new WorldSaveData { Seed = 1, GeneratorId = "overworld", CurrentTick = 100 });
        _provider.SaveChunks(slot.SlotId, [new ChunkSaveEntry { ChunkX = 0, ChunkY = 0, ChunkZ = 127, TileData = [1, 2, 3] }]);
        _provider.SavePlayers(slot.SlotId, [new PlayerSaveData { PlayerName = "Hero" }]);

        _provider.DeleteSaveSlot(slot.SlotId);

        Assert.Null(_provider.GetSaveSlot(slot.SlotId));
        Assert.Null(_provider.LoadWorldMeta(slot.SlotId));
        Assert.Null(_provider.LoadChunk(slot.SlotId, 0, 0, 127));
        Assert.Null(_provider.LoadPlayer(slot.SlotId, "Hero"));
    }

    [Fact]
    public void SaveAndLoadWorldMeta()
    {
        var slot = _provider.CreateSaveSlot("Test", 42, "overworld");
        _provider.SaveWorldMeta(slot.SlotId, new WorldSaveData { Seed = 42, GeneratorId = "overworld", CurrentTick = 500 });
        var meta = _provider.LoadWorldMeta(slot.SlotId);
        Assert.NotNull(meta);
        Assert.Equal(42, meta.Seed);
        Assert.Equal("overworld", meta.GeneratorId);
        Assert.Equal(500, meta.CurrentTick);
    }

    [Fact]
    public void SaveWorldMeta_UpdatesExisting()
    {
        var slot = _provider.CreateSaveSlot("Test", 42, "overworld");
        _provider.SaveWorldMeta(slot.SlotId, new WorldSaveData { Seed = 42, GeneratorId = "overworld", CurrentTick = 100 });
        _provider.SaveWorldMeta(slot.SlotId, new WorldSaveData { Seed = 42, GeneratorId = "overworld", CurrentTick = 200 });
        var meta = _provider.LoadWorldMeta(slot.SlotId);
        Assert.Equal(200, meta!.CurrentTick);
    }

    [Fact]
    public void LoadWorldMeta_ReturnsNull_WhenMissing()
    {
        Assert.Null(_provider.LoadWorldMeta("nonexistent"));
    }

    [Fact]
    public void SaveAndLoadChunk()
    {
        var slot = _provider.CreateSaveSlot("Test", 42, "overworld");
        _provider.SaveChunks(slot.SlotId, [new ChunkSaveEntry
        {
            ChunkX = 1, ChunkY = 2, ChunkZ = Position.DefaultZ,
            TileData = [10, 20, 30],
            EntityData = "[{\"Type\":\"Monster\"}]",
        }]);
        var loaded = _provider.LoadChunk(slot.SlotId, 1, 2, Position.DefaultZ);
        Assert.NotNull(loaded);
        Assert.Equal([10, 20, 30], loaded.TileData);
        Assert.Contains("Monster", loaded.EntityData);
    }

    [Fact]
    public void SaveChunk_UpdatesExisting()
    {
        var slot = _provider.CreateSaveSlot("Test", 42, "overworld");
        _provider.SaveChunks(slot.SlotId, [new ChunkSaveEntry { ChunkX = 0, ChunkY = 0, ChunkZ = 0, TileData = [1] }]);
        _provider.SaveChunks(slot.SlotId, [new ChunkSaveEntry { ChunkX = 0, ChunkY = 0, ChunkZ = 0, TileData = [2] }]);
        var loaded = _provider.LoadChunk(slot.SlotId, 0, 0, 0);
        Assert.Equal([2], loaded!.TileData);
    }

    [Fact]
    public void LoadChunk_ReturnsNull_WhenMissing()
    {
        Assert.Null(_provider.LoadChunk("nonexistent", 0, 0, 0));
    }

    [Fact]
    public void SaveAndLoadPlayer()
    {
        var slot = _provider.CreateSaveSlot("Test", 42, "overworld");
        _provider.SavePlayers(slot.SlotId, [new PlayerSaveData
        {
            PlayerName = "Hero",
            ClassId = 1,
            Level = 5,
            Experience = 500,
            PositionX = 32,
            PositionY = 16,
            PositionZ = Position.DefaultZ,
            HealthCurrent = 80,
            HealthMax = 100,
            Attack = 15,
            Defense = 10,
            Speed = 8,
        }]);
        var loaded = _provider.LoadPlayer(slot.SlotId, "Hero");
        Assert.NotNull(loaded);
        Assert.Equal("Hero", loaded.PlayerName);
        Assert.Equal(5, loaded.Level);
        Assert.Equal(80, loaded.HealthCurrent);
    }

    [Fact]
    public void SavePlayer_UpdatesExisting()
    {
        var slot = _provider.CreateSaveSlot("Test", 42, "overworld");
        _provider.SavePlayers(slot.SlotId, [new PlayerSaveData { PlayerName = "Hero", Level = 1 }]);
        _provider.SavePlayers(slot.SlotId, [new PlayerSaveData { PlayerName = "Hero", Level = 10 }]);
        var loaded = _provider.LoadPlayer(slot.SlotId, "Hero");
        Assert.Equal(10, loaded!.Level);
    }

    [Fact]
    public void LoadPlayer_ReturnsNull_WhenMissing()
    {
        Assert.Null(_provider.LoadPlayer("nonexistent", "nobody"));
    }

    [Fact]
    public void LoadAllPlayers()
    {
        var slot = _provider.CreateSaveSlot("Test", 42, "overworld");
        _provider.SavePlayers(slot.SlotId, [
            new PlayerSaveData { PlayerName = "Hero1" },
            new PlayerSaveData { PlayerName = "Hero2" },
        ]);
        var all = _provider.LoadAllPlayers(slot.SlotId);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void DefaultPath_UsesGameDb()
    {
        var provider = new SqliteSaveGameProvider();
        Assert.NotNull(provider);
        // Clean up
        SqliteConnection.ClearAllPools();
        try { File.Delete("game.db"); } catch { }
    }
}
