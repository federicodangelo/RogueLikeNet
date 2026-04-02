using Microsoft.Data.Sqlite;
using RogueLikeNet.Server.Persistence;

namespace RogueLikeNet.Server.Tests;

/// <summary>
/// Tests for the record/POCO types and that SqliteSaveGameProvider creates a valid schema.
/// Raw SQLite queries verify the schema was created correctly.
/// </summary>
public class GameDbContextTests : IDisposable
{
    private readonly string _dbPath;

    public GameDbContextTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
    }

    [Fact]
    public void ProviderCreation_CreatesDatabase()
    {
        using var saveGameProvider = new SqliteSaveGameProvider(_dbPath);
        Assert.True(File.Exists(_dbPath));
    }

    [Fact]
    public void Schema_HasAllTables()
    {
        using var saveGameProvider = new SqliteSaveGameProvider(_dbPath);
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        var tables = QueryScalar(conn, "SELECT group_concat(name) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'");
        Assert.Contains("SaveSlots", tables);
        Assert.Contains("WorldMetas", tables);
        Assert.Contains("Chunks", tables);
        Assert.Contains("Players", tables);
    }

    [Fact]
    public void Schema_ChunkCoordinates_UniquePerSlot()
    {
        using var saveGameProvider = new SqliteSaveGameProvider(_dbPath);
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        Execute(conn, "INSERT INTO Chunks (SlotId, ChunkX, ChunkY, ChunkZ, TileData) VALUES ('s1', 1, 1, 0, X'01')");
        Assert.Throws<SqliteException>(() =>
            Execute(conn, "INSERT INTO Chunks (SlotId, ChunkX, ChunkY, ChunkZ, TileData) VALUES ('s1', 1, 1, 0, X'02')"));
    }

    [Fact]
    public void Schema_PlayerName_UniquePerSlot()
    {
        using var saveGameProvider = new SqliteSaveGameProvider(_dbPath);
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        Execute(conn, "INSERT INTO Players (SlotId, PlayerName) VALUES ('s1', 'dup')");
        Assert.Throws<SqliteException>(() =>
            Execute(conn, "INSERT INTO Players (SlotId, PlayerName) VALUES ('s1', 'dup')"));
    }

    [Fact]
    public void SaveSlotRecord_DefaultValues()
    {
        var slot = new SaveSlotRecord();
        Assert.Equal("", slot.SlotId);
        Assert.Equal("", slot.Name);
        Assert.Equal(0, slot.Seed);
        Assert.Equal("", slot.GeneratorId);
    }

    [Fact]
    public void ChunkRecord_DefaultValues()
    {
        var chunk = new ChunkRecord();
        Assert.Equal(0, chunk.Id);
        Assert.Equal("", chunk.SlotId);
        Assert.Equal(0, chunk.ChunkX);
        Assert.Equal(0, chunk.ChunkY);
        Assert.Equal(0, chunk.ChunkZ);
        Assert.Empty(chunk.TileData);
        Assert.Equal("[]", chunk.EntityData);
    }

    [Fact]
    public void PlayerRecord_DefaultValues()
    {
        var p = new PlayerRecord();
        Assert.Equal(0, p.Id);
        Assert.Equal("", p.SlotId);
        Assert.Equal("", p.PlayerName);
        Assert.Equal(0, p.ClassId);
        Assert.Equal(0, p.Level);
        Assert.Equal("[]", p.InventoryJson);
        Assert.Equal("{}", p.EquipmentJson);
        Assert.Equal("{}", p.SkillsJson);
        Assert.Equal("{}", p.QuickSlotsJson);
    }

    [Fact]
    public void WorldMetaRecord_DefaultValues()
    {
        var meta = new WorldMetaRecord();
        Assert.Equal("", meta.SlotId);
        Assert.Equal(0, meta.Seed);
        Assert.Equal("", meta.GeneratorId);
        Assert.Equal(0, meta.CurrentTick);
    }

    private static void Execute(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static string QueryScalar(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar()?.ToString() ?? "";
    }
}
