namespace RogueLikeNet.Server.Persistence;

/// <summary>
/// Handles saving and loading game state to/from SQLite.
/// </summary>
public class GamePersistence
{
    private readonly string _dbPath;

    public GamePersistence(string dbPath = "game.db")
    {
        _dbPath = dbPath;
    }

    public void EnsureCreated()
    {
        using var db = new GameDbContext(_dbPath);
        db.Database.EnsureCreated();
    }

    public WorldMetadata? LoadWorldMeta()
    {
        using var db = new GameDbContext(_dbPath);
        return db.WorldMeta.FirstOrDefault();
    }

    public void SaveWorldMeta(long seed, long currentTick)
    {
        using var db = new GameDbContext(_dbPath);
        var meta = db.WorldMeta.FirstOrDefault();
        if (meta == null)
        {
            meta = new WorldMetadata { Id = 1, Seed = seed, CurrentTick = currentTick };
            db.WorldMeta.Add(meta);
        }
        else
        {
            meta.Seed = seed;
            meta.CurrentTick = currentTick;
        }
        db.SaveChanges();
    }

    public void SaveCharacter(CharacterRecord record)
    {
        using var db = new GameDbContext(_dbPath);
        var existing = db.Characters.Find(record.Id);
        if (existing == null)
        {
            db.Characters.Add(record);
        }
        else
        {
            db.Entry(existing).CurrentValues.SetValues(record);
        }
        db.SaveChanges();
    }

    public CharacterRecord? LoadCharacter(long playerId)
    {
        using var db = new GameDbContext(_dbPath);
        return db.Characters.FirstOrDefault(c => c.PlayerId == playerId);
    }

    public void SaveChunk(int chunkX, int chunkY, int chunkZ, byte[] tileData)
    {
        using var db = new GameDbContext(_dbPath);
        var existing = db.WorldChunks.FirstOrDefault(c => c.ChunkX == chunkX && c.ChunkY == chunkY && c.ChunkZ == chunkZ);
        if (existing == null)
        {
            db.WorldChunks.Add(new WorldChunkRecord
            {
                ChunkX = chunkX,
                ChunkY = chunkY,
                ChunkZ = chunkZ,
                TileData = tileData
            });
        }
        else
        {
            existing.TileData = tileData;
        }
        db.SaveChanges();
    }

    public byte[]? LoadChunk(int chunkX, int chunkY, int chunkZ)
    {
        using var db = new GameDbContext(_dbPath);
        return db.WorldChunks.FirstOrDefault(c => c.ChunkX == chunkX && c.ChunkY == chunkY && c.ChunkZ == chunkZ)?.TileData;
    }
}
