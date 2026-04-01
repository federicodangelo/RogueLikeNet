using System.Runtime.InteropServices.JavaScript;

namespace RogueLikeNet.Client.Web.Persistence;

/// <summary>
/// [JSImport] bindings for the IndexedDB save game storage implemented in indexeddb-save.js.
/// All async methods map to JavaScript Promises and are awaited on the C# side.
/// </summary>
internal static partial class JsIndexedDb
{
    [JSImport("indexedDb.open", "game.js")]
    internal static partial Task Open();

    [JSImport("indexedDb.loadAllSlots", "game.js")]
    internal static partial Task<string> LoadAllSlots();

    [JSImport("indexedDb.loadAllWorldMeta", "game.js")]
    internal static partial Task<string> LoadAllWorldMeta();

    [JSImport("indexedDb.loadAllChunks", "game.js")]
    internal static partial Task<string> LoadAllChunks();

    [JSImport("indexedDb.loadAllPlayers", "game.js")]
    internal static partial Task<string> LoadAllPlayers();

    [JSImport("indexedDb.saveSlot", "game.js")]
    internal static partial Task SaveSlot(string json);

    [JSImport("indexedDb.deleteSlot", "game.js")]
    internal static partial Task DeleteSlot(string slotId);

    [JSImport("indexedDb.saveWorldMeta", "game.js")]
    internal static partial Task SaveWorldMeta(string slotId, string json);

    [JSImport("indexedDb.saveChunks", "game.js")]
    internal static partial Task SaveChunks(string slotId, string chunksJson);

    [JSImport("indexedDb.savePlayers", "game.js")]
    internal static partial Task SavePlayers(string slotId, string playersJson);
}
