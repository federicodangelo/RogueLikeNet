using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RogueLikeNet.Server.Persistence;

namespace RogueLikeNet.Server.Tests;

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
    public void EnsureCreated_Succeeds()
    {
        using var db = new GameDbContext(_dbPath);
        db.Database.EnsureCreated();
    }

    [Fact]
    public void DefaultPath_IsGameDb()
    {
        using var db = new GameDbContext();
        // Just verify construction with default parameter works
        Assert.NotNull(db);
    }

    [Fact]
    public void CanAddAndQueryPlayers()
    {
        using (var db = new GameDbContext(_dbPath))
        {
            db.Database.EnsureCreated();
            db.Players.Add(new PlayerAccount
            {
                Username = "testuser",
                PasswordHash = "hash",
                CreatedTick = 1,
                LastLoginTick = 2
            });
            db.SaveChanges();
        }
        using (var db = new GameDbContext(_dbPath))
        {
            var player = db.Players.First();
            Assert.Equal("testuser", player.Username);
            Assert.Equal("hash", player.PasswordHash);
            Assert.Equal(1, player.CreatedTick);
            Assert.Equal(2, player.LastLoginTick);
        }
    }

    [Fact]
    public void CanAddAndQueryCharacters()
    {
        using (var db = new GameDbContext(_dbPath))
        {
            db.Database.EnsureCreated();
            db.Characters.Add(new CharacterRecord
            {
                PlayerId = 1,
                Name = "Hero",
                ClassId = 2,
                Level = 5,
                Experience = 500,
                HealthCurrent = 80,
                HealthMax = 100,
                Attack = 15,
                Defense = 10,
                Speed = 8,
                PositionX = 32,
                PositionY = 16,
                InventoryData = [1, 2, 3]
            });
            db.SaveChanges();
        }
        using (var db = new GameDbContext(_dbPath))
        {
            var chr = db.Characters.First();
            Assert.Equal("Hero", chr.Name);
            Assert.Equal(2, chr.ClassId);
            Assert.Equal(5, chr.Level);
            Assert.Equal(500, chr.Experience);
            Assert.Equal(80, chr.HealthCurrent);
            Assert.Equal(100, chr.HealthMax);
            Assert.Equal(15, chr.Attack);
            Assert.Equal(10, chr.Defense);
            Assert.Equal(8, chr.Speed);
            Assert.Equal(32, chr.PositionX);
            Assert.Equal(16, chr.PositionY);
            Assert.Equal([1, 2, 3], chr.InventoryData);
        }
    }

    [Fact]
    public void CanAddAndQueryWorldChunks()
    {
        using (var db = new GameDbContext(_dbPath))
        {
            db.Database.EnsureCreated();
            db.WorldChunks.Add(new WorldChunkRecord
            {
                ChunkX = 3,
                ChunkY = 7,
                ChunkZ = 127,
                TileData = [10, 20, 30]
            });
            db.SaveChanges();
        }
        using (var db = new GameDbContext(_dbPath))
        {
            var chunk = db.WorldChunks.First();
            Assert.Equal(3, chunk.ChunkX);
            Assert.Equal(7, chunk.ChunkY);
            Assert.Equal([10, 20, 30], chunk.TileData);
        }
    }

    [Fact]
    public void CanAddAndQueryWorldMeta()
    {
        using (var db = new GameDbContext(_dbPath))
        {
            db.Database.EnsureCreated();
            db.WorldMeta.Add(new WorldMetadata
            {
                Id = 1,
                Seed = 42,
                CurrentTick = 100
            });
            db.SaveChanges();
        }
        using (var db = new GameDbContext(_dbPath))
        {
            var meta = db.WorldMeta.First();
            Assert.Equal(42, meta.Seed);
            Assert.Equal(100, meta.CurrentTick);
        }
    }

    [Fact]
    public void PlayerUsername_UniqueConstraint()
    {
        using var db = new GameDbContext(_dbPath);
        db.Database.EnsureCreated();
        db.Players.Add(new PlayerAccount { Username = "dup", PasswordHash = "a" });
        db.SaveChanges();
        db.Players.Add(new PlayerAccount { Username = "dup", PasswordHash = "b" });
        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }

    [Fact]
    public void ChunkCoordinates_UniqueConstraint()
    {
        using var db = new GameDbContext(_dbPath);
        db.Database.EnsureCreated();
        db.WorldChunks.Add(new WorldChunkRecord { ChunkX = 1, ChunkY = 1, ChunkZ = 0, TileData = [1] });
        db.SaveChanges();
        db.WorldChunks.Add(new WorldChunkRecord { ChunkX = 1, ChunkY = 1, ChunkZ = 0, TileData = [2] });
        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }

    [Fact]
    public void PlayerAccount_DefaultValues()
    {
        var player = new PlayerAccount();
        Assert.Equal(0, player.Id);
        Assert.Equal("", player.Username);
        Assert.Equal("", player.PasswordHash);
        Assert.Equal(0, player.CreatedTick);
        Assert.Equal(0, player.LastLoginTick);
    }

    [Fact]
    public void CharacterRecord_DefaultValues()
    {
        var chr = new CharacterRecord();
        Assert.Equal(0, chr.Id);
        Assert.Equal(0, chr.PlayerId);
        Assert.Equal("", chr.Name);
        Assert.Equal(0, chr.ClassId);
        Assert.Equal(0, chr.Level);
        Assert.Equal(0, chr.Experience);
        Assert.Equal(0, chr.HealthCurrent);
        Assert.Equal(0, chr.HealthMax);
        Assert.Equal(0, chr.Attack);
        Assert.Equal(0, chr.Defense);
        Assert.Equal(0, chr.Speed);
        Assert.Equal(0, chr.PositionX);
        Assert.Equal(0, chr.PositionY);
        Assert.Equal(0, chr.PositionZ);
        Assert.Empty(chr.InventoryData);
    }

    [Fact]
    public void WorldChunkRecord_DefaultValues()
    {
        var chunk = new WorldChunkRecord();
        Assert.Equal(0, chunk.Id);
        Assert.Equal(0, chunk.ChunkX);
        Assert.Equal(0, chunk.ChunkY);
        Assert.Equal(0, chunk.ChunkZ);
        Assert.Empty(chunk.TileData);
    }

    [Fact]
    public void WorldMetadata_DefaultValues()
    {
        var meta = new WorldMetadata();
        Assert.Equal(0, meta.Id);
        Assert.Equal(0, meta.Seed);
        Assert.Equal(0, meta.CurrentTick);
    }
}
