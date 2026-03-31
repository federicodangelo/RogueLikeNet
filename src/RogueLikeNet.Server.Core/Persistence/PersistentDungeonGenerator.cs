using RogueLikeNet.Core;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Server.Persistence;

/// <summary>
/// Wraps a base IDungeonGenerator to load previously-saved chunks from persistence.
/// If a chunk was saved, tile data is restored from the provider and entities are
/// deserialized after the chunk is loaded. If not saved, delegates to the base generator.
/// </summary>
public class PersistentDungeonGenerator : IDungeonGenerator
{
    private readonly IDungeonGenerator _baseGenerator;
    private readonly ISaveGameProvider _provider;
    private readonly string _slotId;
    private GameEngine? _engine;

    public PersistentDungeonGenerator(IDungeonGenerator baseGenerator, ISaveGameProvider provider, string slotId)
    {
        _baseGenerator = baseGenerator;
        _provider = provider;
        _slotId = slotId;
    }

    /// <summary>
    /// Must be called after GameEngine is created so entity deserialization can spawn into the ECS world.
    /// </summary>
    public void SetEngine(GameEngine engine) => _engine = engine;

    public bool Exists(int chunkX, int chunkY, int chunkZ)
    {
        // If we have a saved chunk, it definitely exists
        var saved = _provider.LoadChunk(_slotId, chunkX, chunkY, chunkZ);
        if (saved != null)
            return true;

        return _baseGenerator.Exists(chunkX, chunkY, chunkZ);
    }

    public GenerationResult Generate(int chunkX, int chunkY, int chunkZ)
    {
        var saved = _provider.LoadChunk(_slotId, chunkX, chunkY, chunkZ);
        if (saved != null)
        {
            // Restore tiles from saved data
            var tiles = ChunkSerializer.DeserializeTiles(saved.TileData);
            var chunk = new Chunk(chunkX, chunkY, chunkZ);
            Array.Copy(tiles, chunk.Tiles, tiles.Length);
            var result = new GenerationResult(chunk);

            // Restore entities — they'll be spawned by the engine when it processes the result
            // We deserialize them directly here since GenerationResult entity lists
            // use different types than our JSON format
            if (_engine != null && !string.IsNullOrEmpty(saved.EntityData) && saved.EntityData != "[]")
            {
                EntitySerializer.DeserializeEntities(saved.EntityData, _engine);
            }

            return result;
        }

        // Not saved — generate fresh from base
        return _baseGenerator.Generate(chunkX, chunkY, chunkZ);
    }
}
