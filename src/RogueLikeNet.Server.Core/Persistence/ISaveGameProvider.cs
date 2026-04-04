using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Server.Persistence;

/// <summary>
/// Abstraction for save game storage. Implementations: SQLite (desktop/server), in-memory (web).
/// All methods are synchronous — they run on the server tick thread.
/// </summary>
public interface ISaveGameProvider : IDisposable
{
    List<SaveSlotInfo> ListSaveSlots();
    SaveSlotInfo CreateSaveSlot(string name, long seed, string generatorId);
    SaveSlotInfo? GetSaveSlot(string slotId);
    void DeleteSaveSlot(string slotId);

    void SaveWorldMeta(string slotId, WorldSaveData data);
    WorldSaveData? LoadWorldMeta(string slotId);

    void SaveChunks(string slotId, List<ChunkSaveEntry> chunks);
    ChunkSaveEntry? LoadChunk(string slotId, Position chunkPos);

    void SavePlayers(string slotId, List<PlayerSaveData> players);
    PlayerSaveData? LoadPlayer(string slotId, string playerName);
    List<PlayerSaveData> LoadAllPlayers(string slotId);
}
