using Microsoft.Data.Sqlite;
using RogueLikeNet.Server.Persistence;

namespace RogueLikeNet.Server.Tests;

public class GamePersistenceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly GamePersistence _persistence;

    public GamePersistenceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        _persistence = new GamePersistence(_dbPath);
        _persistence.EnsureCreated();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
    }

    [Fact]
    public void EnsureCreated_Succeeds()
    {
        // Already called in constructor; verify db file exists
        Assert.True(File.Exists(_dbPath));
    }

    [Fact]
    public void LoadWorldMeta_ReturnsNull_WhenEmpty()
    {
        var meta = _persistence.LoadWorldMeta();
        Assert.Null(meta);
    }

    [Fact]
    public void SaveWorldMeta_CreatesNew()
    {
        _persistence.SaveWorldMeta(42, 100);
        var meta = _persistence.LoadWorldMeta();
        Assert.NotNull(meta);
        Assert.Equal(42, meta.Seed);
        Assert.Equal(100, meta.CurrentTick);
    }

    [Fact]
    public void SaveWorldMeta_UpdatesExisting()
    {
        _persistence.SaveWorldMeta(42, 100);
        _persistence.SaveWorldMeta(42, 200);
        var meta = _persistence.LoadWorldMeta();
        Assert.NotNull(meta);
        Assert.Equal(200, meta.CurrentTick);
    }

    [Fact]
    public void SaveCharacter_CreatesNew()
    {
        var record = new CharacterRecord
        {
            Id = 1,
            PlayerId = 1,
            Name = "Hero",
            Level = 5,
            HealthCurrent = 100,
            HealthMax = 100,
            Attack = 15,
            Defense = 10,
            Speed = 8,
            PositionX = 32,
            PositionY = 16,
            InventoryData = [1, 2, 3]
        };
        _persistence.SaveCharacter(record);
        var loaded = _persistence.LoadCharacter(1);
        Assert.NotNull(loaded);
        Assert.Equal("Hero", loaded.Name);
        Assert.Equal(5, loaded.Level);
    }

    [Fact]
    public void SaveCharacter_UpdatesExisting()
    {
        _persistence.SaveCharacter(new CharacterRecord { Id = 1, PlayerId = 1, Name = "Hero", Level = 5 });
        _persistence.SaveCharacter(new CharacterRecord { Id = 1, PlayerId = 1, Name = "Hero", Level = 10 });
        var loaded = _persistence.LoadCharacter(1);
        Assert.NotNull(loaded);
        Assert.Equal(10, loaded.Level);
    }

    [Fact]
    public void LoadCharacter_ReturnsNull_WhenNotFound()
    {
        var loaded = _persistence.LoadCharacter(999);
        Assert.Null(loaded);
    }

    [Fact]
    public void SaveChunk_CreatesNew()
    {
        _persistence.SaveChunk(1, 2, [1, 2, 3]);
        var loaded = _persistence.LoadChunk(1, 2);
        Assert.NotNull(loaded);
        Assert.Equal([1, 2, 3], loaded);
    }

    [Fact]
    public void SaveChunk_UpdatesExisting()
    {
        _persistence.SaveChunk(1, 2, [1, 2, 3]);
        _persistence.SaveChunk(1, 2, [4, 5, 6]);
        var loaded = _persistence.LoadChunk(1, 2);
        Assert.NotNull(loaded);
        Assert.Equal([4, 5, 6], loaded);
    }

    [Fact]
    public void LoadChunk_ReturnsNull_WhenNotFound()
    {
        var loaded = _persistence.LoadChunk(999, 999);
        Assert.Null(loaded);
    }

    [Fact]
    public void DefaultPath_UsesGameDb()
    {
        var persistence = new GamePersistence();
        Assert.NotNull(persistence);
    }
}
