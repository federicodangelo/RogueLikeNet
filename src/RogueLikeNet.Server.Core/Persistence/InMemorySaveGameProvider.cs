namespace RogueLikeNet.Server.Persistence;

/// <summary>
/// In-memory implementation of ISaveGameProvider.
/// Data persists during the process lifetime only (suitable for web offline mode).
/// </summary>
public class InMemorySaveGameProvider : ISaveGameProvider
{
    private readonly Dictionary<string, SaveSlotInfo> _slots = new();
    private readonly Dictionary<string, WorldSaveData> _worldMeta = new();
    private readonly Dictionary<string, Dictionary<long, ChunkSaveEntry>> _chunks = new();
    private readonly Dictionary<string, Dictionary<string, PlayerSaveData>> _players = new();

    public List<SaveSlotInfo> ListSaveSlots()
    {
        return _slots.Values.OrderByDescending(s => s.LastSavedAt).ToList();
    }

    public SaveSlotInfo CreateSaveSlot(string name, long seed, string generatorId)
    {
        var slot = new SaveSlotInfo
        {
            SlotId = Guid.NewGuid().ToString("N"),
            Name = name,
            Seed = seed,
            GeneratorId = generatorId,
            CreatedAt = DateTime.UtcNow,
            LastSavedAt = DateTime.UtcNow,
        };
        _slots[slot.SlotId] = slot;
        _chunks[slot.SlotId] = new();
        _players[slot.SlotId] = new();
        return slot;
    }

    public SaveSlotInfo? GetSaveSlot(string slotId)
    {
        return _slots.GetValueOrDefault(slotId);
    }

    public void DeleteSaveSlot(string slotId)
    {
        _slots.Remove(slotId);
        _worldMeta.Remove(slotId);
        _chunks.Remove(slotId);
        _players.Remove(slotId);
    }

    public void SaveWorldMeta(string slotId, WorldSaveData data)
    {
        _worldMeta[slotId] = data;
        if (_slots.TryGetValue(slotId, out var slot))
            slot.LastSavedAt = DateTime.UtcNow;
    }

    public WorldSaveData? LoadWorldMeta(string slotId)
    {
        return _worldMeta.GetValueOrDefault(slotId);
    }

    public void SaveChunks(string slotId, List<ChunkSaveEntry> chunks)
    {
        if (!_chunks.TryGetValue(slotId, out var dict))
        {
            dict = new();
            _chunks[slotId] = dict;
        }

        foreach (var chunk in chunks)
        {
            long key = PackChunkKey(chunk.ChunkX, chunk.ChunkY, chunk.ChunkZ);
            dict[key] = chunk;
        }
    }

    public ChunkSaveEntry? LoadChunk(string slotId, int chunkX, int chunkY, int chunkZ)
    {
        if (!_chunks.TryGetValue(slotId, out var dict))
            return null;
        long key = PackChunkKey(chunkX, chunkY, chunkZ);
        return dict.GetValueOrDefault(key);
    }

    public void SavePlayers(string slotId, List<PlayerSaveData> players)
    {
        if (!_players.TryGetValue(slotId, out var dict))
        {
            dict = new();
            _players[slotId] = dict;
        }

        foreach (var player in players)
            dict[player.PlayerName] = player;
    }

    public PlayerSaveData? LoadPlayer(string slotId, string playerName)
    {
        if (!_players.TryGetValue(slotId, out var dict))
            return null;
        return dict.GetValueOrDefault(playerName);
    }

    public List<PlayerSaveData> LoadAllPlayers(string slotId)
    {
        if (!_players.TryGetValue(slotId, out var dict))
            return [];
        return dict.Values.ToList();
    }

    private static long PackChunkKey(int x, int y, int z)
    {
        return ((long)(x & 0xFFFFFF) << 32) | ((long)(y & 0xFFFFFF) << 8) | (long)(z & 0xFF);
    }
}
