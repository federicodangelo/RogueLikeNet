using Microsoft.EntityFrameworkCore;

namespace RogueLikeNet.Server.Persistence;

public class GameDbContext : DbContext
{
    public DbSet<PlayerAccount> Players { get; set; } = null!;
    public DbSet<CharacterRecord> Characters { get; set; } = null!;
    public DbSet<WorldChunkRecord> WorldChunks { get; set; } = null!;
    public DbSet<WorldMetadata> WorldMeta { get; set; } = null!;

    private readonly string _dbPath;

    public GameDbContext(string dbPath = "game.db")
    {
        _dbPath = dbPath;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlite($"Data Source={_dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlayerAccount>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.Username).IsUnique();
        });

        modelBuilder.Entity<CharacterRecord>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.PlayerId);
        });

        modelBuilder.Entity<WorldChunkRecord>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => new { c.ChunkX, c.ChunkY, c.ChunkZ }).IsUnique();
        });

        modelBuilder.Entity<WorldMetadata>(e =>
        {
            e.HasKey(m => m.Id);
        });
    }
}

public class PlayerAccount
{
    public long Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public long CreatedTick { get; set; }
    public long LastLoginTick { get; set; }
}

public class CharacterRecord
{
    public long Id { get; set; }
    public long PlayerId { get; set; }
    public string Name { get; set; } = "";
    public int ClassId { get; set; }
    public int Level { get; set; }
    public int Experience { get; set; }
    public int HealthCurrent { get; set; }
    public int HealthMax { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int Speed { get; set; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public int PositionZ { get; set; }
    /// <summary>Serialized inventory data (MessagePack binary)</summary>
    public byte[] InventoryData { get; set; } = [];
}

public class WorldChunkRecord
{
    public long Id { get; set; }
    public int ChunkX { get; set; }
    public int ChunkY { get; set; }
    public int ChunkZ { get; set; }
    /// <summary>Serialized chunk tile data (MessagePack binary)</summary>
    public byte[] TileData { get; set; } = [];
}

public class WorldMetadata
{
    public int Id { get; set; }
    public long Seed { get; set; }
    public long CurrentTick { get; set; }
}
