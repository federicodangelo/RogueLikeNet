namespace RogueLikeNet.Server.Persistence;

public class SaveSlotInfo
{
    public string SlotId { get; set; } = "";
    public string Name { get; set; } = "";
    public long Seed { get; set; }
    public string GeneratorId { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime LastSavedAt { get; set; }
}

public class WorldSaveData
{
    public long Seed { get; set; }
    public string GeneratorId { get; set; } = "";
    public long CurrentTick { get; set; }
}

public class ChunkSaveEntry
{
    public int ChunkX { get; set; }
    public int ChunkY { get; set; }
    public int ChunkZ { get; set; }
    public byte[] TileData { get; set; } = [];
    public string EntityData { get; set; } = "[]";
}

public class PlayerSaveData
{
    public string PlayerName { get; set; } = "";
    public int ClassId { get; set; }
    public int Level { get; set; }
    public int Experience { get; set; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public int PositionZ { get; set; }
    public int HealthCurrent { get; set; }
    public int HealthMax { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int Speed { get; set; }
    public string InventoryJson { get; set; } = "[]";
    public string EquipmentJson { get; set; } = "{}";
    public string QuickSlotsJson { get; set; } = "{}";
    public int Hunger { get; set; } = 100;
    public int MaxHunger { get; set; } = 100;
    public int Thirst { get; set; } = 100;
    public int MaxThirst { get; set; } = 100;
}
