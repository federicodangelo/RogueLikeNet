using System.Text.Json;
using System.Text.Json.Serialization;
using RogueLikeNet.Server.Persistence;

namespace RogueLikeNet.Client.Web.Persistence;

/// <summary>
/// Source-generated JSON context for AOT-compatible serialization of save data
/// passed between C# and the IndexedDB JavaScript layer.
/// </summary>
[JsonSerializable(typeof(SaveSlotInfo))]
[JsonSerializable(typeof(List<SaveSlotInfo>))]
[JsonSerializable(typeof(WorldSaveData))]
[JsonSerializable(typeof(Dictionary<string, WorldSaveData>))]
[JsonSerializable(typeof(ChunkSaveEntry))]
[JsonSerializable(typeof(List<ChunkSaveEntry>))]
[JsonSerializable(typeof(Dictionary<string, List<ChunkSaveEntry>>))]
[JsonSerializable(typeof(PlayerSaveData))]
[JsonSerializable(typeof(List<PlayerSaveData>))]
[JsonSerializable(typeof(Dictionary<string, List<PlayerSaveData>>))]
[JsonSourceGenerationOptions(WriteIndented = false)]
internal partial class IndexedDbJsonContext : JsonSerializerContext;

/// <summary>
/// IndexedDB-backed implementation of <see cref="ISaveGameProvider"/> for the web build.
/// All reads are served from an in-memory cache (synchronous), while writes are
/// propagated to IndexedDB asynchronously (fire-and-forget) for cross-session persistence.
/// Call <see cref="InitializeAsync"/> once at startup to open the database and populate the cache.
/// </summary>
public class IndexedDbSaveGameProvider : ISaveGameProvider
{
    private readonly Dictionary<string, SaveSlotInfo> _slots = new();
    private readonly Dictionary<string, WorldSaveData> _worldMeta = new();
    private readonly Dictionary<string, Dictionary<long, ChunkSaveEntry>> _chunks = new();
    private readonly Dictionary<string, Dictionary<string, PlayerSaveData>> _players = new();

    /// <summary>
    /// Opens the IndexedDB database and loads all persisted data into the in-memory cache.
    /// Must be called (and awaited) before any <see cref="ISaveGameProvider"/> methods.
    /// </summary>
    public async Task InitializeAsync()
    {
        await JsIndexedDb.Open();

        // Load slots
        var slotsJson = await JsIndexedDb.LoadAllSlots();
        var slots = JsonSerializer.Deserialize(slotsJson, IndexedDbJsonContext.Default.ListSaveSlotInfo) ?? [];
        foreach (var slot in slots)
        {
            _slots[slot.SlotId] = slot;
            _chunks.TryAdd(slot.SlotId, new());
            _players.TryAdd(slot.SlotId, new());
        }

        // Load world metadata
        var worldMetaJson = await JsIndexedDb.LoadAllWorldMeta();
        var worldMetas = JsonSerializer.Deserialize(worldMetaJson, IndexedDbJsonContext.Default.DictionaryStringWorldSaveData) ?? new();
        foreach (var (slotId, data) in worldMetas)
            _worldMeta[slotId] = data;

        // Load chunks (grouped by slot)
        var chunksJson = await JsIndexedDb.LoadAllChunks();
        var chunksBySlot = JsonSerializer.Deserialize(chunksJson, IndexedDbJsonContext.Default.DictionaryStringListChunkSaveEntry) ?? new();
        foreach (var (slotId, chunkList) in chunksBySlot)
        {
            if (!_chunks.TryGetValue(slotId, out var dict))
            {
                dict = new();
                _chunks[slotId] = dict;
            }
            foreach (var chunk in chunkList)
            {
                long key = PackChunkKey(chunk.ChunkX, chunk.ChunkY, chunk.ChunkZ);
                dict[key] = chunk;
            }
        }

        // Load players (grouped by slot)
        var playersJson = await JsIndexedDb.LoadAllPlayers();
        var playersBySlot = JsonSerializer.Deserialize(playersJson, IndexedDbJsonContext.Default.DictionaryStringListPlayerSaveData) ?? new();
        foreach (var (slotId, playerList) in playersBySlot)
        {
            if (!_players.TryGetValue(slotId, out var dict))
            {
                dict = new();
                _players[slotId] = dict;
            }
            foreach (var player in playerList)
                dict[player.PlayerName] = player;
        }
    }

    // ── ISaveGameProvider implementation ──────────────────────────

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

        PersistSlot(slot);
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

        FireAndForget(JsIndexedDb.DeleteSlot(slotId));
    }

    public void SaveWorldMeta(string slotId, WorldSaveData data)
    {
        _worldMeta[slotId] = data;
        if (_slots.TryGetValue(slotId, out var slot))
            slot.LastSavedAt = DateTime.UtcNow;

        var json = JsonSerializer.Serialize(data, IndexedDbJsonContext.Default.WorldSaveData);
        FireAndForget(JsIndexedDb.SaveWorldMeta(slotId, json));

        if (slot != null)
            PersistSlot(slot);
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

        var json = JsonSerializer.Serialize(chunks, IndexedDbJsonContext.Default.ListChunkSaveEntry);
        FireAndForget(JsIndexedDb.SaveChunks(slotId, json));
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

        var json = JsonSerializer.Serialize(players, IndexedDbJsonContext.Default.ListPlayerSaveData);
        FireAndForget(JsIndexedDb.SavePlayers(slotId, json));
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

    // ── Helpers ───────────────────────────────────────────────────

    private static long PackChunkKey(int x, int y, int z)
    {
        return ((long)(x & 0xFFFFFF) << 32) | ((long)(y & 0xFFFFFF) << 8) | (long)(z & 0xFF);
    }

    private static void PersistSlot(SaveSlotInfo slot)
    {
        var json = JsonSerializer.Serialize(slot, IndexedDbJsonContext.Default.SaveSlotInfo);
        FireAndForget(JsIndexedDb.SaveSlot(json));
    }

    private static async void FireAndForget(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[IndexedDB] Write error: {ex.Message}");
        }
    }
}
