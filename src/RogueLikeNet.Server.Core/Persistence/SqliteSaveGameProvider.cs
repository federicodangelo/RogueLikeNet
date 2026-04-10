using Microsoft.Data.Sqlite;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Server.Persistence;

/// <summary>
/// SQLite-backed implementation of ISaveGameProvider using raw ADO.NET (Microsoft.Data.Sqlite).
/// Fully NativeAOT compatible — no EF Core, no reflection.
/// </summary>
public class SqliteSaveGameProvider : ISaveGameProvider
{
    private readonly SqliteConnection _conn;

    private readonly TextWriter _logWriter;

    public SqliteSaveGameProvider(string dbPath = "game.db", TextWriter? logWriter = null)
    {
        _logWriter = logWriter ?? TextWriter.Null;
        _conn = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString());
        _conn.Open();
        EnsureCreated();
        _logWriter.WriteLine($"[SqliteSaveGameProvider] Initialized SQLite SaveGameProvider with DB at '{dbPath}'");
    }

    public void Dispose()
    {
        _conn.Dispose();
        _logWriter.WriteLine("[SqliteSaveGameProvider] Disposed SQLite SaveGameProvider and closed DB connection");
    }

    private void EnsureCreated()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS SaveSlots (
                SlotId TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Seed INTEGER NOT NULL,
                GeneratorId TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                LastSavedAt TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS WorldMetas (
                SlotId TEXT PRIMARY KEY,
                Seed INTEGER NOT NULL,
                GeneratorId TEXT NOT NULL,
                CurrentTick INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Chunks (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SlotId TEXT NOT NULL,
                ChunkX INTEGER NOT NULL,
                ChunkY INTEGER NOT NULL,
                ChunkZ INTEGER NOT NULL,
                TileData BLOB NOT NULL,
                EntityData TEXT NOT NULL DEFAULT '[]'
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Chunks_Slot_XYZ ON Chunks(SlotId, ChunkX, ChunkY, ChunkZ);
            CREATE TABLE IF NOT EXISTS Players (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SlotId TEXT NOT NULL,
                PlayerName TEXT NOT NULL,
                ClassId INTEGER NOT NULL DEFAULT 0,
                Level INTEGER NOT NULL DEFAULT 1,
                Experience INTEGER NOT NULL DEFAULT 0,
                PositionX INTEGER NOT NULL DEFAULT 0,
                PositionY INTEGER NOT NULL DEFAULT 0,
                PositionZ INTEGER NOT NULL DEFAULT 0,
                HealthCurrent INTEGER NOT NULL DEFAULT 100,
                HealthMax INTEGER NOT NULL DEFAULT 100,
                Attack INTEGER NOT NULL DEFAULT 0,
                Defense INTEGER NOT NULL DEFAULT 0,
                Speed INTEGER NOT NULL DEFAULT 0,
                InventoryJson TEXT NOT NULL DEFAULT '[]',
                EquipmentJson TEXT NOT NULL DEFAULT '{}',
                QuickSlotsJson TEXT NOT NULL DEFAULT '{}',
                Hunger INTEGER NOT NULL DEFAULT 100,
                MaxHunger INTEGER NOT NULL DEFAULT 100,
                Thirst INTEGER NOT NULL DEFAULT 100,
                MaxThirst INTEGER NOT NULL DEFAULT 100
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Players_Slot_Name ON Players(SlotId, PlayerName);
            """;
        cmd.ExecuteNonQuery();

        // Migrate: add Hunger column to older databases
        MigrateAddColumn("Players", "Hunger", "INTEGER NOT NULL DEFAULT 100");
        MigrateAddColumn("Players", "MaxHunger", "INTEGER NOT NULL DEFAULT 100");
        MigrateAddColumn("Players", "Thirst", "INTEGER NOT NULL DEFAULT 100");
        MigrateAddColumn("Players", "MaxThirst", "INTEGER NOT NULL DEFAULT 100");
        MigrateRemoveColumn("Players", "SkillsJson");
    }

    private void MigrateRemoveColumn(string table, string column)
    {
        try
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = $"ALTER TABLE {table} DROP COLUMN {column}";
            cmd.ExecuteNonQuery();
        }
        catch (SqliteException) { /* Column doesn't exist */ }
    }

    private void MigrateAddColumn(string table, string column, string columnDef)
    {
        try
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {columnDef}";
            cmd.ExecuteNonQuery();
        }
        catch (SqliteException) { /* Column already exists */ }
    }

    public List<SaveSlotInfo> ListSaveSlots()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT SlotId, Name, Seed, GeneratorId, CreatedAt, LastSavedAt FROM SaveSlots";
        using var reader = cmd.ExecuteReader();
        var list = new List<SaveSlotInfo>();
        while (reader.Read())
        {
            list.Add(new SaveSlotInfo
            {
                SlotId = reader.GetString(0),
                Name = reader.GetString(1),
                Seed = reader.GetInt64(2),
                GeneratorId = reader.GetString(3),
                CreatedAt = DateTime.Parse(reader.GetString(4)),
                LastSavedAt = DateTime.Parse(reader.GetString(5)),
            });
        }
        return list;
    }

    public SaveSlotInfo CreateSaveSlot(string name, long seed, string generatorId)
    {
        var slotId = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT INTO SaveSlots (SlotId, Name, Seed, GeneratorId, CreatedAt, LastSavedAt) VALUES ($slotId, $name, $seed, $genId, $created, $saved)";
        cmd.Parameters.AddWithValue("$slotId", slotId);
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$seed", seed);
        cmd.Parameters.AddWithValue("$genId", generatorId);
        cmd.Parameters.AddWithValue("$created", now.ToString("O"));
        cmd.Parameters.AddWithValue("$saved", now.ToString("O"));
        cmd.ExecuteNonQuery();
        return new SaveSlotInfo
        {
            SlotId = slotId,
            Name = name,
            Seed = seed,
            GeneratorId = generatorId,
            CreatedAt = now,
            LastSavedAt = now,
        };
    }

    public SaveSlotInfo? GetSaveSlot(string slotId)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT SlotId, Name, Seed, GeneratorId, CreatedAt, LastSavedAt FROM SaveSlots WHERE SlotId = $slotId";
        cmd.Parameters.AddWithValue("$slotId", slotId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new SaveSlotInfo
        {
            SlotId = reader.GetString(0),
            Name = reader.GetString(1),
            Seed = reader.GetInt64(2),
            GeneratorId = reader.GetString(3),
            CreatedAt = DateTime.Parse(reader.GetString(4)),
            LastSavedAt = DateTime.Parse(reader.GetString(5)),
        };
    }

    public void DeleteSaveSlot(string slotId)
    {
        using var transaction = _conn.BeginTransaction();
        Execute("DELETE FROM Chunks WHERE SlotId = $slotId", ("$slotId", slotId));
        Execute("DELETE FROM Players WHERE SlotId = $slotId", ("$slotId", slotId));
        Execute("DELETE FROM WorldMetas WHERE SlotId = $slotId", ("$slotId", slotId));
        Execute("DELETE FROM SaveSlots WHERE SlotId = $slotId", ("$slotId", slotId));
        transaction.Commit();
    }

    public void SaveWorldMeta(string slotId, WorldSaveData data)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO WorldMetas (SlotId, Seed, GeneratorId, CurrentTick)
            VALUES ($slotId, $seed, $genId, $tick)
            ON CONFLICT(SlotId) DO UPDATE SET Seed=$seed, GeneratorId=$genId, CurrentTick=$tick
            """;
        cmd.Parameters.AddWithValue("$slotId", slotId);
        cmd.Parameters.AddWithValue("$seed", data.Seed);
        cmd.Parameters.AddWithValue("$genId", data.GeneratorId);
        cmd.Parameters.AddWithValue("$tick", data.CurrentTick);
        cmd.ExecuteNonQuery();

        Execute("UPDATE SaveSlots SET LastSavedAt = $now WHERE SlotId = $slotId",
            ("$now", DateTime.UtcNow.ToString("O")), ("$slotId", slotId));
    }

    public WorldSaveData? LoadWorldMeta(string slotId)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT Seed, GeneratorId, CurrentTick FROM WorldMetas WHERE SlotId = $slotId";
        cmd.Parameters.AddWithValue("$slotId", slotId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new WorldSaveData
        {
            Seed = reader.GetInt64(0),
            GeneratorId = reader.GetString(1),
            CurrentTick = reader.GetInt64(2),
        };
    }

    public void SaveChunks(string slotId, List<ChunkSaveEntry> chunks)
    {
        using var transaction = _conn.BeginTransaction();
        foreach (var chunk in chunks)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO Chunks (SlotId, ChunkX, ChunkY, ChunkZ, TileData, EntityData)
                VALUES ($slotId, $cx, $cy, $cz, $tile, $entity)
                ON CONFLICT(SlotId, ChunkX, ChunkY, ChunkZ) DO UPDATE SET TileData=$tile, EntityData=$entity
                """;
            cmd.Parameters.AddWithValue("$slotId", slotId);
            cmd.Parameters.AddWithValue("$cx", chunk.ChunkX);
            cmd.Parameters.AddWithValue("$cy", chunk.ChunkY);
            cmd.Parameters.AddWithValue("$cz", chunk.ChunkZ);
            cmd.Parameters.AddWithValue("$tile", chunk.TileData);
            cmd.Parameters.AddWithValue("$entity", chunk.EntityData ?? "[]");
            cmd.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public ChunkSaveEntry? LoadChunk(string slotId, ChunkPosition chunkPos)
    {
        var (chunkX, chunkY, chunkZ) = chunkPos;
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT ChunkX, ChunkY, ChunkZ, TileData, EntityData FROM Chunks WHERE SlotId=$slotId AND ChunkX=$cx AND ChunkY=$cy AND ChunkZ=$cz";
        cmd.Parameters.AddWithValue("$slotId", slotId);
        cmd.Parameters.AddWithValue("$cx", chunkX);
        cmd.Parameters.AddWithValue("$cy", chunkY);
        cmd.Parameters.AddWithValue("$cz", chunkZ);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new ChunkSaveEntry
        {
            ChunkX = reader.GetInt32(0),
            ChunkY = reader.GetInt32(1),
            ChunkZ = reader.GetInt32(2),
            TileData = (byte[])reader[3],
            EntityData = reader.GetString(4),
        };
    }

    public void SavePlayers(string slotId, List<PlayerSaveData> players)
    {
        using var transaction = _conn.BeginTransaction();
        foreach (var p in players)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO Players (SlotId, PlayerName, ClassId, Level, Experience, PositionX, PositionY, PositionZ, HealthCurrent, HealthMax, Attack, Defense, Speed, InventoryJson, EquipmentJson, QuickSlotsJson, Hunger, MaxHunger, Thirst, MaxThirst)
                VALUES ($slotId, $name, $classId, $level, $exp, $px, $py, $pz, $hpCur, $hpMax, $atk, $def, $spd, $inv, $equip, $quickSlots, $hunger, $maxHunger, $thirst, $maxThirst)
                ON CONFLICT(SlotId, PlayerName) DO UPDATE SET
                    ClassId=$classId, Level=$level, Experience=$exp,
                    PositionX=$px, PositionY=$py, PositionZ=$pz,
                    HealthCurrent=$hpCur, HealthMax=$hpMax,
                    Attack=$atk, Defense=$def, Speed=$spd,
                    InventoryJson=$inv, EquipmentJson=$equip, QuickSlotsJson=$quickSlots,
                    Hunger=$hunger, MaxHunger=$maxHunger,
                    Thirst=$thirst, MaxThirst=$maxThirst
                """;
            cmd.Parameters.AddWithValue("$slotId", slotId);
            cmd.Parameters.AddWithValue("$name", p.PlayerName);
            cmd.Parameters.AddWithValue("$classId", p.ClassId);
            cmd.Parameters.AddWithValue("$level", p.Level);
            cmd.Parameters.AddWithValue("$exp", p.Experience);
            cmd.Parameters.AddWithValue("$px", p.PositionX);
            cmd.Parameters.AddWithValue("$py", p.PositionY);
            cmd.Parameters.AddWithValue("$pz", p.PositionZ);
            cmd.Parameters.AddWithValue("$hpCur", p.HealthCurrent);
            cmd.Parameters.AddWithValue("$hpMax", p.HealthMax);
            cmd.Parameters.AddWithValue("$atk", p.Attack);
            cmd.Parameters.AddWithValue("$def", p.Defense);
            cmd.Parameters.AddWithValue("$spd", p.Speed);
            cmd.Parameters.AddWithValue("$inv", p.InventoryJson);
            cmd.Parameters.AddWithValue("$equip", p.EquipmentJson);
            cmd.Parameters.AddWithValue("$quickSlots", p.QuickSlotsJson);
            cmd.Parameters.AddWithValue("$hunger", p.Hunger);
            cmd.Parameters.AddWithValue("$maxHunger", p.MaxHunger);
            cmd.Parameters.AddWithValue("$thirst", p.Thirst);
            cmd.Parameters.AddWithValue("$maxThirst", p.MaxThirst);
            cmd.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public PlayerSaveData? LoadPlayer(string slotId, string playerName)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Players WHERE SlotId=$slotId AND PlayerName=$name";
        cmd.Parameters.AddWithValue("$slotId", slotId);
        cmd.Parameters.AddWithValue("$name", playerName);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return ReadPlayer(reader);
    }

    public List<PlayerSaveData> LoadAllPlayers(string slotId)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Players WHERE SlotId=$slotId";
        cmd.Parameters.AddWithValue("$slotId", slotId);
        using var reader = cmd.ExecuteReader();
        var list = new List<PlayerSaveData>();
        while (reader.Read())
            list.Add(ReadPlayer(reader));
        return list;
    }

    private static PlayerSaveData ReadPlayer(SqliteDataReader reader)
    {
        var data = new PlayerSaveData
        {
            PlayerName = reader.GetString(reader.GetOrdinal("PlayerName")),
            ClassId = reader.GetInt32(reader.GetOrdinal("ClassId")),
            Level = reader.GetInt32(reader.GetOrdinal("Level")),
            Experience = reader.GetInt32(reader.GetOrdinal("Experience")),
            PositionX = reader.GetInt32(reader.GetOrdinal("PositionX")),
            PositionY = reader.GetInt32(reader.GetOrdinal("PositionY")),
            PositionZ = reader.GetInt32(reader.GetOrdinal("PositionZ")),
            HealthCurrent = reader.GetInt32(reader.GetOrdinal("HealthCurrent")),
            HealthMax = reader.GetInt32(reader.GetOrdinal("HealthMax")),
            Attack = reader.GetInt32(reader.GetOrdinal("Attack")),
            Defense = reader.GetInt32(reader.GetOrdinal("Defense")),
            Speed = reader.GetInt32(reader.GetOrdinal("Speed")),
            InventoryJson = reader.GetString(reader.GetOrdinal("InventoryJson")),
            EquipmentJson = reader.GetString(reader.GetOrdinal("EquipmentJson")),
            QuickSlotsJson = reader.GetString(reader.GetOrdinal("QuickSlotsJson")),
            Hunger = reader.GetInt32(reader.GetOrdinal("Hunger")),
            MaxHunger = reader.GetInt32(reader.GetOrdinal("MaxHunger")),
            Thirst = reader.GetInt32(reader.GetOrdinal("Thirst")),
            MaxThirst = reader.GetInt32(reader.GetOrdinal("MaxThirst")),

        };
        return data;
    }

    private void Execute(string sql, params (string name, object value)[] parameters)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value);
        cmd.ExecuteNonQuery();
    }
}
